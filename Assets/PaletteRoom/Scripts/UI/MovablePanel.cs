using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace PaletteRoom
{
    /// <summary>
    /// Makes a world-space panel world-anchored and movable, following common MR guidance: panels stay
    /// where the user puts them rather than following the head, and a visible grab bar under the panel
    /// (like a window bar) moves it — up close with a pinch/grip or from afar with the ray. Optionally a
    /// controller's menu button (M in the Editor) recalls the panel in front of the user.
    /// </summary>
    public class MovablePanel : MonoBehaviour
    {
        static readonly Color BarIdle = new Color(1f, 1f, 1f, 0.55f);
        static readonly Color BarHover = new Color(1f, 1f, 1f, 0.95f);
        static readonly Color BarHeld = new Color(0.17f, 0.20f, 0.24f, 0.95f);

        Transform m_Head;
        Image m_Bar;
        InputAction m_Recall;
        bool m_Placed;
        float m_RecallDistance = 0.7f;
        float m_RecallYaw;
        float m_RecallBelowEyes = 0.2f;

        /// <summary>Raised when the user takes hold of the panel's grab bar.</summary>
        public event System.Action Grabbed;

        /// <summary>True once the panel has a world position (from first placement or the user).</summary>
        public bool IsPlaced => m_Placed;

        /// <summary>The grab bar under the panel (for the tutorial to point at).</summary>
        public RectTransform Bar => m_BarCanvas as RectTransform;

        static readonly System.Collections.Generic.List<MovablePanel> s_All = new System.Collections.Generic.List<MovablePanel>();

        /// <summary>Every movable panel, open or not (see <see cref="PanelSpace"/>).</summary>
        public static System.Collections.Generic.IReadOnlyList<MovablePanel> All => s_All;

        /// <summary>
        /// Builds the grab bar under the panel's canvas and makes the panel grabbable.
        /// <paramref name="recallable"/> binds the menu button to bring it back in front of the user.
        /// </summary>
        public void Setup(Transform head, Camera eventCamera, bool recallable)
        {
            m_Head = head;
            if (!s_All.Contains(this))
                s_All.Add(this);
            var canvas = GetComponentInChildren<Canvas>(true);
            m_PanelCanvas = canvas;
            var size = ((RectTransform)canvas.transform).sizeDelta * UIKit.MillimetresToMetres;

            BuildBar(eventCamera, size);
            MakeGrabbable();

            if (recallable)
            {
                m_Recall = new InputAction("Recall Panel", InputActionType.Button);
                m_Recall.AddBinding("<XRController>/{MenuButton}");
                m_Recall.AddBinding("<Keyboard>/m");
                m_Recall.performed += _ => PlaceInFront();
                if (isActiveAndEnabled)
                    m_Recall.Enable();
            }
        }

        /// <summary>Where <see cref="PlaceInFront"/> puts the panel, relative to where the user is looking.</summary>
        public void SetRecallPose(float distance, float yawDegrees, float belowEyes)
        {
            m_RecallDistance = distance;
            m_RecallYaw = yawDegrees;
            m_RecallBelowEyes = belowEyes;
        }

        /// <summary>Puts the panel in front of the user (at the recall pose), facing them.</summary>
        public void PlaceInFront()
        {
            if (m_Head == null)
                return;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            var forward = Vector3.ProjectOnPlane(m_Head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            forward = Quaternion.Euler(0f, m_RecallYaw, 0f) * forward.normalized;
            transform.position = m_Head.position + forward * m_RecallDistance + Vector3.down * m_RecallBelowEyes;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            m_Placed = true;
            PanelSpace.Forget(transform);
            // Don't land on another open panel: swing further out to this panel's own side first.
            PanelSpace.KeepClear(transform, m_Head, m_RecallYaw >= 0f ? 1f : -1f);
        }

        /// <summary>Places the panel only if it hasn't been placed yet, so it stays where the user left it.</summary>
        public void PlaceIfNeeded()
        {
            if (!m_Placed)
                PlaceInFront();
        }

        // Recall stays enabled while the panel is hidden (it's how the user gets it back), so it is
        // only disposed with the panel, never disabled.
        void OnEnable() => m_Recall?.Enable();

        // Closing gives back the space this panel took: panels it swung aside return.
        void OnDisable() => PanelSpace.Released(transform);

        void OnDestroy()
        {
            m_Recall?.Dispose();
            s_All.Remove(this);
        }

        const float BarW = 0.14f, BarH = 0.014f, BarGap = 0.018f;
        // The ray is a cone, and its nearest hit wins. Sitting behind the panel's plane keeps the hit box
        // from beating the buttons just above it, so a pinch on them presses instead of moving the panel.
        const float BarHitBehind = 0.02f;

        Transform m_BarCanvas, m_BarHit;
        Canvas m_PanelCanvas;

        /// <summary>Moves the grab bar under the panel's current bottom edge (call after resizing the panel).</summary>
        public void Relayout()
        {
            if (m_PanelCanvas == null || m_BarCanvas == null)
                return;
            var size = ((RectTransform)m_PanelCanvas.transform).sizeDelta * UIKit.MillimetresToMetres;
            var pos = new Vector3(0f, -(size.y / 2f + BarGap + BarH / 2f), 0f);
            m_BarCanvas.localPosition = pos;
            m_BarHit.localPosition = pos + Vector3.forward * BarHitBehind;
        }

        void BuildBar(Camera eventCamera, Vector2 panelSize)
        {
            const float barW = BarW, barH = BarH, gap = BarGap;
            var barCanvas = UIKit.CreateWorldCanvas("Grab Bar", new Vector2(barW, barH) / UIKit.MillimetresToMetres, transform, eventCamera);
            barCanvas.transform.localPosition = new Vector3(0f, -(panelSize.y / 2f + gap + barH / 2f), 0f);
            m_BarCanvas = barCanvas.transform;
            // The bar is grabbed through its collider, not through UI raycasts.
            Destroy(barCanvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>());
            var raycaster = barCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                Destroy(raycaster);

            m_Bar = UIKit.Panel(barCanvas.transform, "Bar", BarIdle, barH / UIKit.MillimetresToMetres / 2f);
            m_Bar.raycastTarget = false;
            UIKit.Stretch(m_Bar.rectTransform);

            // Generous hit box so the bar is easy to catch with a hand or a ray.
            var hit = new GameObject("Grab Bar Collider");
            hit.transform.SetParent(transform, false);
            hit.transform.localPosition = barCanvas.transform.localPosition + Vector3.forward * BarHitBehind;
            var box = hit.AddComponent<BoxCollider>();
            box.size = new Vector3(barW + 0.04f, barH + 0.03f, 0.03f);
            m_BarHit = hit.transform;
        }

        void MakeGrabbable()
        {
            var body = GetComponent<Rigidbody>();
            if (body == null)
                body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var grab = gameObject.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.trackScale = false;
            grab.useDynamicAttach = true;                      // hold it where you grabbed it
            grab.farAttachMode = InteractableFarAttachMode.Far; // a ray grab keeps the panel at a distance
            grab.hoverEntered.AddListener(_ => RefreshBar(grab));
            grab.hoverExited.AddListener(_ => RefreshBar(grab));
            grab.selectEntered.AddListener(_ => { m_Placed = true; PanelSpace.Forget(transform); RefreshBar(grab); Sfx.Play(SfxKind.PickUp, 0.6f, 1.2f); Grabbed?.Invoke(); });
            grab.selectExited.AddListener(_ => { RefreshBar(grab); Sfx.Play(SfxKind.Drop, 0.35f, 1.4f); });

            var general = gameObject.AddComponent<XRGeneralGrabTransformer>();
            general.allowOneHandedScaling = false;
            general.allowTwoHandedScaling = false;
            gameObject.AddComponent<PanelGrabTransformer>().Configure(m_Head);
        }

        void RefreshBar(XRGrabInteractable grab)
        {
            if (m_Bar == null)
                return;
            m_Bar.color = grab.isSelected ? BarHeld : grab.isHovered ? BarHover : BarIdle;
        }
    }
}
