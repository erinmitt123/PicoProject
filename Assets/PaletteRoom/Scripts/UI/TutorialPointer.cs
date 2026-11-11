using System;
using UnityEngine;
using UnityEngine.UI;

namespace PaletteRoom
{
    /// <summary>
    /// Shows the user exactly what the tutorial means: a glowing outline round the button to press with a
    /// bobbing arrow above it, or a big arrow over the piece of furniture to use. When the target is
    /// outside the user's view, an arrow at the edge of their sight points the way to it. Drawn over
    /// everything (it's on overlay canvases) and never catches a press.
    /// </summary>
    public class TutorialPointer : MonoBehaviour
    {
        static readonly Color Glow = new Color(1f, 0.56f, 0.22f, 1f);

        const float Pad = 5f;                  // mm around a highlighted button
        const float ArrowW = 26f, ArrowH = 32f; // mm, over a button
        const float ObjectArrowScale = 3.2f;    // the arrow over furniture is bigger: it's metres away
        const float OffViewDegrees = 30f;

        Transform m_Head;
        Func<Component> m_Resolve;
        RectTransform m_UiRoot, m_ObjectRoot, m_EdgeRoot;
        Image m_Outline, m_Arrow, m_ObjectArrow, m_EdgeArrow;
        Vector3 m_EdgePos;
        bool m_EdgeShown;

        public void Build(Camera eventCamera)
        {
            m_Head = eventCamera.transform;

            var ui = UIKit.CreateWorldCanvas("Pointer (Button)", new Vector2(100f, 100f), transform, eventCamera);
            m_UiRoot = (RectTransform)ui.transform;
            m_Outline = NewImage(m_UiRoot, "Outline", UIKit.OutlineSprite);
            m_Outline.type = Image.Type.Sliced;
            m_Outline.pixelsPerUnitMultiplier = 2f;
            UIKit.Stretch(m_Outline.rectTransform);
            m_Arrow = NewImage(m_UiRoot, "Arrow", UIKit.ArrowSprite);
            var art = m_Arrow.rectTransform;
            art.anchorMin = art.anchorMax = new Vector2(0.5f, 1f);
            art.pivot = new Vector2(0.5f, 0f);
            art.sizeDelta = new Vector2(ArrowW, ArrowH);

            var obj = UIKit.CreateWorldCanvas("Pointer (Object)", new Vector2(ArrowW, ArrowH) * ObjectArrowScale, transform, eventCamera);
            m_ObjectRoot = (RectTransform)obj.transform;
            m_ObjectRoot.pivot = new Vector2(0.5f, 0f); // the tip, at the bottom
            m_ObjectArrow = NewImage(m_ObjectRoot, "Arrow", UIKit.ArrowSprite);
            UIKit.Stretch(m_ObjectArrow.rectTransform);

            var edge = UIKit.CreateWorldCanvas("Pointer (Edge)", new Vector2(ArrowW, ArrowH) * 1.3f, transform, eventCamera);
            m_EdgeRoot = (RectTransform)edge.transform;
            m_EdgeArrow = NewImage(m_EdgeRoot, "Arrow", UIKit.ArrowSprite);
            UIKit.Stretch(m_EdgeArrow.rectTransform);

            Hide();
        }

        static Image NewImage(Transform parent, string name, Sprite sprite)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.color = Glow;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Points at whatever <paramref name="resolve"/> returns each frame (a UI RectTransform or a piece); null hides it.</summary>
        public void Point(Func<Component> resolve)
        {
            m_Resolve = resolve;
            m_EdgeShown = false;
            if (resolve == null)
                Hide();
        }

        public void Hide()
        {
            m_Resolve = null;
            Show(m_UiRoot, false);
            Show(m_ObjectRoot, false);
            Show(m_EdgeRoot, false);
        }

        static void Show(Component c, bool on)
        {
            if (c != null && c.gameObject.activeSelf != on)
                c.gameObject.SetActive(on);
        }

        void LateUpdate()
        {
            if (m_Resolve == null || m_Head == null)
                return;
            Component target = null;
            try
            {
                target = m_Resolve();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PaletteRoom] Tutorial pointer: {e.Message}");
            }
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                Show(m_UiRoot, false);
                Show(m_ObjectRoot, false);
                Show(m_EdgeRoot, false);
                return;
            }

            float t = Time.unscaledTime;
            float bob = Mathf.Sin(t * 5f) * 0.5f + 0.5f;
            float pulse = 0.65f + 0.35f * (Mathf.Sin(t * 6.5f) * 0.5f + 0.5f);
            Vector3 aim;
            if (target is RectTransform rt)
            {
                Show(m_ObjectRoot, false);
                Show(m_UiRoot, true);
                aim = PointAtUi(rt, bob, pulse);
            }
            else
            {
                Show(m_UiRoot, false);
                Show(m_ObjectRoot, true);
                aim = PointAtObject(target.transform, bob, pulse);
            }
            UpdateEdge(aim);
        }

        static readonly Vector3[] s_Corners = new Vector3[4];

        Vector3 PointAtUi(RectTransform target, float bob, float pulse)
        {
            target.GetWorldCorners(s_Corners); // bottom-left, top-left, top-right, bottom-right
            var center = (s_Corners[0] + s_Corners[2]) / 2f;
            var right = s_Corners[3] - s_Corners[0];
            var up = s_Corners[1] - s_Corners[0];
            float mm = UIKit.MillimetresToMetres;
            var size = new Vector2(right.magnitude / mm, up.magnitude / mm);
            if (size.x < 0.01f || size.y < 0.01f)
                return center;
            var forward = Vector3.Cross(right, up).normalized; // canvases face away from their viewer
            if (Vector3.Dot(forward, center - m_Head.position) < 0f)
                forward = -forward;
            m_UiRoot.SetPositionAndRotation(center - forward * 0.003f, Quaternion.LookRotation(forward, up.normalized));
            float grow = 1f + 0.04f * pulse;
            m_UiRoot.sizeDelta = (size + Vector2.one * 2f * Pad) * grow;
            m_Arrow.rectTransform.anchoredPosition = new Vector2(0f, 4f + bob * 7f);
            var c = Glow;
            c.a = pulse;
            m_Outline.color = c;
            return center;
        }

        Vector3 PointAtObject(Transform target, float bob, float pulse)
        {
            var bounds = new Bounds(target.position, Vector3.zero);
            foreach (var r in target.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(r.bounds);
            var tip = new Vector3(bounds.center.x, bounds.max.y + 0.06f + bob * 0.06f, bounds.center.z);
            var toTip = tip - m_Head.position;
            var flat = Vector3.ProjectOnPlane(toTip, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f)
                flat = m_Head.forward;
            m_ObjectRoot.SetPositionAndRotation(tip, Quaternion.LookRotation(flat.normalized, Vector3.up));
            var c = Glow;
            c.a = 0.75f + 0.25f * pulse;
            m_ObjectArrow.color = c;
            return bounds.center;
        }

        /// <summary>An arrow at the edge of view, pointing round to a target the user isn't looking at.</summary>
        void UpdateEdge(Vector3 aim)
        {
            var toTarget = aim - m_Head.position;
            float angle = Vector3.Angle(m_Head.forward, toTarget);
            // A little hysteresis so it doesn't flicker at the boundary.
            bool show = m_EdgeShown ? angle > OffViewDegrees - 4f : angle > OffViewDegrees;
            m_EdgeShown = show;
            Show(m_EdgeRoot, show);
            if (!show)
                return;
            var dir2 = new Vector2(Vector3.Dot(toTarget, m_Head.right), Vector3.Dot(toTarget, m_Head.up));
            if (Vector3.Dot(toTarget, m_Head.forward) < 0f && dir2.sqrMagnitude < 0.0001f)
                dir2 = Vector2.right; // straight behind: turn right
            dir2.Normalize();
            var want = m_Head.position + m_Head.forward * 0.7f + (m_Head.right * dir2.x + m_Head.up * dir2.y) * 0.17f;
            m_EdgePos = m_EdgeRoot.gameObject.activeSelf && m_EdgePos != Vector3.zero
                ? Vector3.Lerp(m_EdgePos, want, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime))
                : want;
            float spin = Mathf.Atan2(dir2.y, dir2.x) * Mathf.Rad2Deg + 90f; // the sprite points down
            float nudge = Mathf.Sin(Time.unscaledTime * 6f) * 0.012f;
            m_EdgeRoot.SetPositionAndRotation(m_EdgePos + (m_Head.right * dir2.x + m_Head.up * dir2.y) * nudge,
                Quaternion.LookRotation(m_Head.forward, m_Head.up) * Quaternion.Euler(0f, 0f, spin));
        }

        /// <summary>The first active object called <paramref name="name"/> under <paramref name="root"/>.</summary>
        public static Transform FindActive(Transform root, string name)
        {
            if (root == null || !root.gameObject.activeInHierarchy)
                return null;
            if (root.name == name)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindActive(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
