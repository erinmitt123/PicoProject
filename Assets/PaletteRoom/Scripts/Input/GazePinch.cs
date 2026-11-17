using System.Collections;
using System.Collections.Generic;
using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace PaletteRoom
{
    /// <summary>
    /// Look and pinch, the way PICO's spatial OS works: where the user looks is what a hand's pinch acts
    /// on. Each tracked hand's far ray is moved off the hand and onto the eye gaze. Whenever it rests on
    /// something pressable or grabbable (and while holding it), a ray is drawn from the hand to that spot,
    /// with a small dot at the end. While the pinch is held the ray is
    /// handed back to the hand's motion (moving the hand swings the gaze ray around the head, about 1.6×),
    /// so sliders, scrolling and carrying furniture follow the hand, not the eyes.
    /// Needs eye tracking on the headset; without it (PICO 4 Ultra, or when turned off in Settings) the
    /// hands keep their normal rays. Poke and near grab are untouched. Controllers always use their rays.
    /// </summary>
    [DefaultExecutionOrder(-150)] // before XRI's interaction manager (-105) reads the cast origins
    public class GazePinch : MonoBehaviour
    {
        const float SwingGain = 1.6f;
        const float RetrySeconds = 2f;

        public static GazePinch Instance { get; private set; }

        /// <summary>The headset tracks the eyes and the app has started eye tracking.</summary>
        public bool Supported { get; private set; }

        /// <summary>The user's choice in Settings (on by default when supported).</summary>
        public bool Enabled
        {
            get => SaveData.GazeInput;
            set
            {
                SaveData.GazeInput = value;
                if (!value)
                    RestoreAll();
                Debug.Log($"[PaletteRoom] Eye-gaze input {(value ? "on" : "off")}.");
            }
        }

        /// <summary>Gaze is steering the hands right now.</summary>
        public bool Active => Supported && Enabled && m_GazeValid;

        class Driven
        {
            public NearFarInteractor interactor;
            public InteractionCasterBase caster;
            public Transform handOrigin;
            public Transform gazeOrigin;
            public bool stabilization;
            public readonly List<CurveVisualController> visuals = new List<CurveVisualController>();
            public bool pressed;
            public LineRenderer ray;
            public Vector3 headAtPress, handAtPress, originAtPress;
            public Quaternion rotationAtPress;
        }

        readonly Dictionary<NearFarInteractor, Driven> m_Driven = new Dictionary<NearFarInteractor, Driven>();
        readonly InteractorPoints m_Points = new InteractorPoints();
        XRInputModalityManager m_Modality;
        Transform m_Head;
        Vector3 m_GazeOrigin, m_GazeDirection;
        bool m_GazeValid;
        float m_RetryAt;
        Transform m_Cursor;
        Renderer m_CursorRenderer;

        public void Setup(Transform head)
        {
            Instance = this;
            m_Head = head;
            BuildCursor();
            StartCoroutine(StartEyeTracking());
        }

        IEnumerator StartEyeTracking()
        {
            yield return null; // after PXR_Manager has started
            if (Application.isEditor)
            {
                Debug.Log("[PaletteRoom] Eye-gaze input is off in the Editor; hands use their rays.");
                yield break;
            }
            bool supported = false;
            int count = 0;
            var modes = new EyeTrackingMode[0];
            int result = -1;
            try
            {
                result = PXR_MotionTracking.GetEyeTrackingSupported(ref supported, ref count, ref modes);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PaletteRoom] Couldn't ask about eye tracking: {e.Message}");
            }
            if (result != 0 || !supported)
            {
                Debug.Log("[PaletteRoom] This headset has no eye tracking; hands keep their rays.");
                yield break;
            }
            if (PXR_Manager.Instance != null)
                PXR_Manager.Instance.eyeTracking = true;
            var start = new EyeTrackingStartInfo { needCalibration = 1, mode = EyeTrackingMode.PXR_ETM_BOTH };
            int started = PXR_MotionTracking.StartEyeTracking(ref start);
            Supported = started == 0;
            Debug.Log(Supported
                ? "[PaletteRoom] Eye tracking started: look at something and pinch to use it."
                : $"[PaletteRoom] Eye tracking didn't start ({started}); hands keep their rays.");
        }

        void OnDisable() => RestoreAll();

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (Supported && !Application.isEditor)
            {
                var stop = new EyeTrackingStopInfo();
                PXR_MotionTracking.StopEyeTracking(ref stop);
            }
        }

        void Update()
        {
            m_GazeValid = Supported && Enabled && ReadGaze();
            if (!m_GazeValid)
            {
                RestoreAll();
                m_Cursor.gameObject.SetActive(false);
                return;
            }

            if (m_Modality == null)
                m_Modality = FindFirstObjectByType<XRInputModalityManager>();
            foreach (var interactor in m_Points.Active)
            {
                if (!interactor.isActiveAndEnabled || !IsHand(interactor))
                {
                    if (m_Driven.TryGetValue(interactor, out var stale))
                        Restore(stale);
                    continue;
                }
                var d = Drive(interactor);
                if (d == null)
                    continue;
                Steer(d);
            }
        }

        void LateUpdate()
        {
            // After XRI has cast: a dot where the gaze rests on something it can press or grab.
            if (!m_GazeValid)
                return;
            bool shown = false;
            foreach (var d in m_Driven.Values)
                DrawRay(d);
            foreach (var d in m_Driven.Values)
            {
                if (d.interactor == null || !d.interactor.isActiveAndEnabled || d.pressed)
                    continue;
                var type = d.interactor.TryGetCurveEndPoint(out var end);
                if (shown)
                    continue;
                if (type == EndPointType.UI || type == EndPointType.ValidCastHit)
                {
                    var toEye = m_Head.position - end;
                    m_Cursor.position = end + toEye.normalized * 0.004f;
                    m_Cursor.rotation = Quaternion.LookRotation(-toEye.normalized, Vector3.up);
                    m_Cursor.localScale = Vector3.one * Mathf.Clamp(toEye.magnitude * 0.012f, 0.006f, 0.04f);
                    shown = true;
                    break;
                }
            }
            m_Cursor.gameObject.SetActive(shown);
        }

        /// <summary>
        /// The ray from the hand to whatever the gaze is on, so it's always clear what a pinch will act on;
        /// held while something is pressed or carried. Hidden when the gaze rests on nothing.
        /// </summary>
        void DrawRay(Driven d)
        {
            if (d.ray == null)
                return;
            bool on = false;
            if (m_GazeValid && d.interactor != null && d.interactor.isActiveAndEnabled && d.handOrigin != null
                && d.caster != null && d.caster.castOrigin == d.gazeOrigin)
            {
                var type = d.interactor.TryGetCurveEndPoint(out var end);
                bool holding = d.interactor.hasSelection;
                if (type == EndPointType.UI || type == EndPointType.ValidCastHit || type == EndPointType.AttachPoint || holding)
                {
                    if (holding && type != EndPointType.AttachPoint && type != EndPointType.ValidCastHit)
                        end = d.interactor.interactablesSelected[0].transform.position;
                    var start = d.handOrigin.position;
                    d.ray.SetPosition(0, start);
                    d.ray.SetPosition(1, Vector3.Lerp(start, end, 0.5f));
                    d.ray.SetPosition(2, end);
                    var c = d.pressed || holding ? new Color(0.55f, 0.85f, 1f) : Color.white;
                    d.ray.startColor = new Color(c.r, c.g, c.b, 0.15f);
                    d.ray.endColor = new Color(c.r, c.g, c.b, 0.9f);
                    on = true;
                }
            }
            d.ray.enabled = on;
        }

        LineRenderer NewRay(string name)
        {
            var go = new GameObject($"Gaze Ray ({name})");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 3;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.004f;
            line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1f));
            line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            var mat = new Material(Shader.Find("UI/Default")) { name = "Gaze Ray" };
            UIKit.MakeOverlay(mat);
            mat.renderQueue = 3990;
            line.sharedMaterial = mat;
            line.enabled = false;
            return line;
        }

        /// <summary>Reads the combined gaze into world space, smoothed so fixations hold still but saccades land.</summary>
        bool ReadGaze()
        {
            if (Time.unscaledTime < m_RetryAt || m_Head == null)
                return false;
            if (!PXR_EyeTracking.GetCombineEyeGazeVector(out var vector) || vector.sqrMagnitude < 1e-6f || float.IsNaN(vector.x))
            {
                m_RetryAt = Time.unscaledTime + RetrySeconds; // eyes closed, headset off, not calibrated…
                return false;
            }
            var origin = m_Head.position;
            if (PXR_EyeTracking.GetCombineEyeGazePoint(out var point) && point.sqrMagnitude < 0.04f)
                origin = m_Head.TransformPoint(point);
            var direction = m_Head.TransformDirection(vector.normalized);

            if (m_GazeDirection == Vector3.zero)
                m_GazeDirection = direction;
            float angle = Vector3.Angle(m_GazeDirection, direction);
            // Small drifts are tremor and get heavily smoothed; a jump of a few degrees is a new look.
            float rate = angle > 4f ? 45f : 12f;
            m_GazeDirection = Vector3.Slerp(m_GazeDirection, direction, 1f - Mathf.Exp(-rate * Time.unscaledDeltaTime)).normalized;
            m_GazeOrigin = origin;
            return true;
        }

        bool IsHand(NearFarInteractor interactor)
        {
            if (m_Modality == null)
                return false;
            var t = interactor.transform;
            return (m_Modality.leftHand != null && t.IsChildOf(m_Modality.leftHand.transform))
                || (m_Modality.rightHand != null && t.IsChildOf(m_Modality.rightHand.transform));
        }

        Driven Drive(NearFarInteractor interactor)
        {
            if (m_Driven.TryGetValue(interactor, out var d) && d.gazeOrigin != null)
            {
                if (d.caster.castOrigin != d.gazeOrigin)
                    Take(d);
                return d;
            }
            if (!(interactor.farInteractionCaster is InteractionCasterBase caster))
                return null;
            d = new Driven { interactor = interactor, caster = caster, handOrigin = caster.castOrigin };
            d.gazeOrigin = new GameObject($"Gaze Origin ({interactor.name})").transform;
            d.gazeOrigin.SetParent(transform, false);
            d.ray = NewRay(interactor.name);
            var group = interactor.transform.parent != null ? interactor.transform.parent : interactor.transform;
            foreach (var visual in group.GetComponentsInChildren<CurveVisualController>(true))
                if (ReferenceEquals(visual.curveInteractionDataProvider, interactor))
                    d.visuals.Add(visual);
            m_Driven[interactor] = d;
            Take(d);
            return d;
        }

        void Take(Driven d)
        {
            d.stabilization = d.caster.enableStabilization;
            d.caster.enableStabilization = false; // the gaze is smoothed here
            d.caster.castOrigin = d.gazeOrigin;
            d.pressed = false;
            foreach (var v in d.visuals)
            {
                v.enabled = false;
                if (v.lineRenderer != null)
                    v.lineRenderer.enabled = false;
            }
        }

        void Restore(Driven d)
        {
            if (d.caster == null || d.caster.castOrigin != d.gazeOrigin)
                return;
            d.caster.castOrigin = d.handOrigin;
            d.caster.enableStabilization = d.stabilization;
            d.pressed = false;
            foreach (var v in d.visuals)
                if (v != null)
                    v.enabled = true;
            if (d.ray != null)
                d.ray.enabled = false;
        }

        void RestoreAll()
        {
            foreach (var d in m_Driven.Values)
                Restore(d);
        }

        void Steer(Driven d)
        {
            var hand = d.handOrigin != null ? d.handOrigin.position : m_Head.position;
            bool pressed = d.interactor.selectInput.ReadIsPerformed() || d.interactor.uiPressInput.ReadIsPerformed();
            if (pressed && !d.pressed)
            {
                d.headAtPress = m_Head.position;
                d.handAtPress = hand;
                d.originAtPress = m_GazeOrigin;
                d.rotationAtPress = Quaternion.LookRotation(m_GazeDirection, Vector3.up);
            }
            d.pressed = pressed;

            if (!pressed)
            {
                d.gazeOrigin.SetPositionAndRotation(m_GazeOrigin, Quaternion.LookRotation(m_GazeDirection, Vector3.up));
                return;
            }
            // Held: the hand's swing around the head turns the ray from where the eyes picked.
            var from = d.handAtPress - d.headAtPress;
            var to = hand - m_Head.position;
            var swing = Quaternion.FromToRotation(from, to);
            swing.ToAngleAxis(out float angle, out var axis);
            if (angle > 180f)
                angle -= 360f;
            var scaled = float.IsNaN(axis.x) ? Quaternion.identity : Quaternion.AngleAxis(angle * SwingGain, axis);
            d.gazeOrigin.SetPositionAndRotation(d.originAtPress + (m_Head.position - d.headAtPress), scaled * d.rotationAtPress);
        }

        /// <summary>A soft white dot with a faint dark rim, readable on light and dark surfaces.</summary>
        static Texture2D DotTexture()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float fill = Mathf.Clamp01((0.62f - r) * 16f);
                float rim = Mathf.Clamp01((0.82f - r) * 16f) - fill;
                byte v = (byte)(255f * (fill + rim * 0.2f) / Mathf.Max(fill + rim, 0.001f));
                px[y * size + x] = new Color32(v, v, v, (byte)(255f * Mathf.Clamp01(fill + rim * 0.5f)));
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        void BuildCursor()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Gaze Cursor";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            m_Cursor = go.transform;
            m_CursorRenderer = go.GetComponent<Renderer>();
            var shader = Shader.Find("UI/Default");
            var mat = new Material(shader) { name = "Gaze Cursor" };
            mat.mainTexture = DotTexture();
            mat.color = new Color(1f, 1f, 1f, 0.75f);
            UIKit.MakeOverlay(mat);
            mat.renderQueue = 4000;
            m_CursorRenderer.sharedMaterial = mat;
            m_CursorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_CursorRenderer.receiveShadows = false;
            go.SetActive(false);
        }
    }
}
