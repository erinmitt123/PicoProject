using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>
    /// The finish moment. "Reveal" hides the menus, dims the room's light, and sweeps a warm spotlight
    /// across the design from one side to the other — each piece pops with a chime as the light finds it
    /// — then brings the light back up, takes a snapshot, and saves the room to the lookbook.
    /// </summary>
    public class RevealSequence : MonoBehaviour
    {
        const float DimSeconds = 0.6f, SweepSeconds = 2.8f, RestoreSeconds = 0.6f;
        const float DimTo = 0.25f;

        RoomFurniture m_Room;
        PaletteManager m_Palettes;
        Transform m_Head;
        Light m_KeyLight;
        Func<IEnumerable<GameObject>> m_Panels;
        Transform m_Toast;
        TextMeshProUGUI m_ToastTitle, m_ToastBody;
        UnityEngine.UI.Image[] m_ToastStars;
        Coroutine m_HideToast;

        public bool IsRunning { get; private set; }

        public event Action<Lookbook.Entry> Saved;

        public void Configure(RoomFurniture room, PaletteManager palettes, Camera eventCamera, Func<IEnumerable<GameObject>> panels)
        {
            m_Room = room;
            m_Palettes = palettes;
            m_Head = eventCamera.transform;
            m_Panels = panels;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                    m_KeyLight = light;
            BuildToast(eventCamera);
        }

        /// <summary>Runs the reveal and saves the room. <paramref name="brief"/> may be null for a free design.</summary>
        public void Play(DesignBrief brief, int stars)
        {
            if (IsRunning || m_Room == null)
                return;
            // Nothing placed: an empty room would be saved to the lookbook as a blank page.
            bool any = false;
            foreach (var item in m_Room.Items)
                any |= item != null;
            if (!any)
                return;
            StartCoroutine(Run(brief, stars));
        }

        IEnumerator Run(DesignBrief brief, int stars)
        {
            IsRunning = true;
            var items = new List<FurnitureItem>();
            foreach (var item in m_Room.Items)
                if (item != null)
                    items.Add(item);

            // Clear the stage.
            var hidden = new List<GameObject>();
            foreach (var panel in m_Panels())
            {
                if (panel != null && panel.activeSelf)
                {
                    hidden.Add(panel);
                    panel.SetActive(false);
                }
            }

            float baseIntensity = m_KeyLight != null ? m_KeyLight.intensity : 1f;
            yield return Fade(baseIntensity, baseIntensity * DimTo, DimSeconds);

            // Sweep left to right as the user sees it.
            var head = m_Head.position;
            var right = Vector3.ProjectOnPlane(m_Head.right, Vector3.up).normalized;
            items.Sort((a, b) => Vector3.Dot(a.transform.position - head, right).CompareTo(Vector3.Dot(b.transform.position - head, right)));
            if (items.Count > 0)
                yield return Sweep(items);

            yield return Fade(baseIntensity * DimTo, baseIntensity, RestoreSeconds);

            var snapshot = Snapshot(items);
            foreach (var panel in hidden)
                if (panel != null)
                    panel.SetActive(true);

            var palette = m_Palettes.Current;
            var entry = new Lookbook.Entry
            {
                title = brief != null ? brief.title : "My room",
                briefId = brief != null ? brief.id : null,
                stars = brief != null ? stars : 0,
                paletteName = palette.name,
                paletteHex = new[] { Hex(palette.dominant), Hex(palette.secondary), Hex(palette.accent), Hex(palette.neutral), Hex(palette.highlight) },
                pieces = Lookbook.Capture(items, head, out _, out _),
            };
            Lookbook.Save(entry, snapshot);
            if (snapshot != null)
                Destroy(snapshot);
            ShowToast(entry);
            Saved?.Invoke(entry);
            IsRunning = false;
        }

        IEnumerator Fade(float from, float to, float seconds)
        {
            if (m_KeyLight == null)
                yield break;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                m_KeyLight.intensity = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            m_KeyLight.intensity = to;
        }

        IEnumerator Sweep(List<FurnitureItem> items)
        {
            var go = new GameObject("Reveal Spotlight");
            go.transform.SetParent(transform, false);
            var spot = go.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(1f, 0.9f, 0.75f);
            spot.intensity = 9f;
            spot.range = 10f;
            spot.spotAngle = 38f;
            spot.innerSpotAngle = 20f;
            spot.shadows = LightShadows.None;

            // From above and behind the user, aimed along a path across the pieces.
            var from = m_Head.position + Vector3.up * 0.6f - Vector3.ProjectOnPlane(m_Head.forward, Vector3.up).normalized * 0.3f;
            go.transform.position = from;
            var start = Centre(items[0]);
            var end = Centre(items[items.Count - 1]);
            int next = 0;
            for (float t = 0f; t < SweepSeconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / SweepSeconds);
                var target = Vector3.Lerp(start, end, k);
                go.transform.rotation = Quaternion.LookRotation(target - from, Vector3.up);
                // Each piece flourishes as the light reaches it.
                while (next < items.Count && k >= next / (float)Mathf.Max(1, items.Count - 1) - 0.001f)
                {
                    if (PaletteWave.Instance != null)
                        PaletteWave.Instance.Accent(items[next], next, items.Count);
                    next++;
                }
                yield return null;
            }
            Destroy(go);
        }

        static Vector3 Centre(FurnitureItem item)
        {
            var box = item.GetComponent<BoxCollider>();
            return box != null ? item.transform.TransformPoint(box.center) : item.transform.position;
        }

        /// <summary>A small picture of the design from where the user stands (furniture only; passthrough isn't capturable).</summary>
        Texture2D Snapshot(List<FurnitureItem> items)
        {
            if (items.Count == 0)
                return null;
            var bounds = new Bounds(Centre(items[0]), Vector3.zero);
            foreach (var item in items)
                foreach (var r in item.GetComponentsInChildren<Renderer>())
                    bounds.Encapsulate(r.bounds);

            var go = new GameObject("Lookbook Camera");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.93f, 0.91f, 0.88f);
            // No menus, and no room-scan occluder (it's drawn as transparent black holes for passthrough).
            cam.cullingMask = ~((1 << LayerMask.NameToLayer("UI")) | (1 << LayerMask.NameToLayer("Ignore Raycast")));
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 40f;
            // From the user's side of the design, a little above it, framing everything.
            var toViewer = Vector3.ProjectOnPlane(m_Head.position - bounds.center, Vector3.up);
            if (toViewer.sqrMagnitude < 0.01f)
                toViewer = Vector3.back;
            var dir = (toViewer.normalized + Vector3.up * 0.55f).normalized;
            float distance = bounds.extents.magnitude / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.95f;
            go.transform.position = bounds.center + dir * distance;
            go.transform.LookAt(bounds.center);

            const int w = 768, h = 480;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            // Controller rays and the wave ring aren't part of the design; keep them out of the picture.
            var lines = new List<LineRenderer>();
            foreach (var line in FindObjectsByType<LineRenderer>(FindObjectsSortMode.None))
                if (line.enabled)
                {
                    line.enabled = false;
                    lines.Add(line);
                }
            cam.Render();
            foreach (var line in lines)
                line.enabled = true;
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(go);
            return tex;
        }

        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        // ---- "Saved" message ----------------------------------------------------------------

        void BuildToast(Camera eventCamera)
        {
            const float W = 420f, H = 92f, Pad = 16f;
            var root = new GameObject("Reveal Toast");
            root.transform.SetParent(transform, false);
            m_Toast = root.transform;
            var canvas = UIKit.CreateWorldCanvas("Reveal Toast Canvas", new Vector2(W, H), m_Toast, eventCamera);
            var panel = UIKit.Window(canvas.transform, 16f);
            UIKit.Stretch(panel.rectTransform);
            m_ToastTitle = UIKit.Label(panel.transform, "Title", string.Empty, 17f, UIKit.TextColor, style: FontStyles.Bold);
            UIKit.Place(m_ToastTitle.rectTransform, Pad, Pad, W - 2 * Pad - 80f, 24f);
            m_ToastStars = UIKit.StarRow(panel.transform, W - Pad - 70f, Pad, 22f);
            m_ToastBody = UIKit.Label(panel.transform, "Body", string.Empty, 11f, UIKit.MutedTextColor);
            UIKit.Place(m_ToastBody.rectTransform, Pad, Pad + 28f, W - 2 * Pad, 40f);
            root.SetActive(false);
        }

        void ShowToast(Lookbook.Entry entry)
        {
            m_ToastTitle.Localize(entry.briefId != null ? entry.title : "Saved to your lookbook");
            foreach (var star in m_ToastStars)
                star.gameObject.SetActive(entry.briefId != null);
            UIKit.SetStars(m_ToastStars, entry.stars);
            if (entry.briefId != null)
                m_ToastBody.Localize("Saved to your lookbook with the {0} palette. Open the lookbook from the dock to see your rooms.", Loc.Arg(entry.paletteName));
            else
                m_ToastBody.Localize("{0} pieces in the {1} palette. Open the lookbook from the dock to see your rooms.", entry.pieces.Count, Loc.Arg(entry.paletteName));
            m_Toast.gameObject.SetActive(true);
            UIKit.PlaceInFront(m_Toast, m_Head, 1.1f, -0.25f);
            if (m_HideToast != null)
                StopCoroutine(m_HideToast);
            m_HideToast = StartCoroutine(HideToast());
        }

        IEnumerator HideToast()
        {
            yield return new WaitForSeconds(7f);
            m_Toast.gameObject.SetActive(false);
        }
    }
}
