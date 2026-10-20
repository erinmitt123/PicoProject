using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace PaletteRoom
{
    /// <summary>
    /// Runs after XRI's general grab transformer while a panel is being moved: keeps it upright,
    /// turns it to face the user (so it is always readable wherever it's put down), and keeps it within
    /// a comfortable reading distance of the head.
    /// </summary>
    public class PanelGrabTransformer : XRBaseGrabTransformer
    {
        [SerializeField] float minDistance = 0.45f;
        [SerializeField] float maxDistance = 3f;

        Transform m_Head;
        XRGrabInteractable m_Grab;
        bool m_Registered;

        // Registered manually on first grab so it is guaranteed to run after the general transformer.
        protected override RegistrationMode registrationMode => RegistrationMode.None;

        public void Configure(Transform head) => m_Head = head;

        void Awake() => m_Grab = GetComponent<XRGrabInteractable>();

        void OnEnable()
        {
            if (m_Grab != null)
                m_Grab.selectEntered.AddListener(OnSelectEntered);
        }

        void OnDisable()
        {
            if (m_Grab != null)
                m_Grab.selectEntered.RemoveListener(OnSelectEntered);
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (m_Registered)
                return;
            m_Grab.AddSingleGrabTransformer(this);
            m_Grab.AddMultipleGrabTransformer(this);
            m_Registered = true;
        }

        public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase,
            ref Pose targetPose, ref Vector3 localScale)
        {
            if (m_Head == null)
                return;

            var offset = targetPose.position - m_Head.position;
            float distance = offset.magnitude;
            if (distance < 0.0001f)
                return;
            float clamped = Mathf.Clamp(distance, minDistance, maxDistance);
            if (!Mathf.Approximately(clamped, distance))
                targetPose.position = m_Head.position + offset / distance * clamped;

            var facing = Vector3.ProjectOnPlane(targetPose.position - m_Head.position, Vector3.up);
            if (facing.sqrMagnitude > 0.0001f)
                targetPose.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
        }
    }
}
