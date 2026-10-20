using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace PaletteRoom
{
    /// <summary>
    /// Runs after XRI's general grab transformer and enforces real-world placement: floor pieces stay
    /// on the floor (the room root's height), only turn around the vertical axis, and can't be pushed
    /// into other floor-standing pieces — a blocked move slides along the obstacle or holds at the last
    /// valid pose. Wall pieces stay upright. Two-handed grabs rotate furniture in place.
    /// </summary>
    public class FurnitureGrabTransformer : XRBaseGrabTransformer
    {
        FurnitureItem m_Item;
        XRGrabInteractable m_Grab;
        RoomFurniture m_Room;
        bool m_Registered;
        Pose m_LastValid;
        bool m_StartedOverlapping;

        // Registered manually on first grab so it is guaranteed to run after the general transformer.
        protected override RegistrationMode registrationMode => RegistrationMode.None;

        void Awake()
        {
            m_Item = GetComponent<FurnitureItem>();
            m_Grab = GetComponent<XRGrabInteractable>();
        }

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
            if (!m_Registered)
            {
                m_Grab.AddSingleGrabTransformer(this);
                m_Grab.AddMultipleGrabTransformer(this);
                m_Registered = true;
            }
            if (m_Room == null)
                m_Room = GetComponentInParent<RoomFurniture>();
            m_LastValid = new Pose(transform.position, transform.rotation);
            // A piece that already overlaps (e.g. from an older layout) may move freely until it's clear.
            m_StartedOverlapping = m_Room != null && !m_Room.IsFree(m_Item, m_LastValid.position, m_LastValid.rotation);
        }

        public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase,
            ref Pose targetPose, ref Vector3 localScale)
        {
            if (m_Item == null || m_Item.Mount == FurnitureMount.Free)
                return;

            float yaw = targetPose.rotation.eulerAngles.y;
            targetPose.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (m_Item.Mount == FurnitureMount.Floor)
            {
                float floorY = transform.parent != null ? transform.parent.position.y : 0f;
                targetPose.position = new Vector3(targetPose.position.x, floorY, targetPose.position.z);
            }

            if (m_Room == null || !m_Item.BlocksFloor)
                return;

            if (m_StartedOverlapping)
            {
                if (m_Room.IsFree(m_Item, targetPose.position, targetPose.rotation))
                {
                    m_StartedOverlapping = false;
                    m_LastValid = targetPose;
                }
                return;
            }

            targetPose = Resolve(targetPose);
            m_LastValid = targetPose;
        }

        /// <summary>The wanted pose if it's free; otherwise slide along one axis; otherwise stay put.</summary>
        Pose Resolve(Pose wanted)
        {
            if (m_Room.IsFree(m_Item, wanted.position, wanted.rotation))
                return wanted;

            // Keep the rotation that was last valid while sliding, so a blocked turn doesn't jitter.
            var rot = m_LastValid.rotation;
            var p = m_LastValid.position;
            var slideX = new Vector3(wanted.position.x, p.y, p.z);
            var slideZ = new Vector3(p.x, p.y, wanted.position.z);
            bool xFree = m_Room.IsFree(m_Item, slideX, rot);
            bool zFree = m_Room.IsFree(m_Item, slideZ, rot);
            if (xFree && zFree)
                return Mathf.Abs(wanted.position.x - p.x) >= Mathf.Abs(wanted.position.z - p.z) ? new Pose(slideX, rot) : new Pose(slideZ, rot);
            if (xFree)
                return new Pose(slideX, rot);
            if (zFree)
                return new Pose(slideZ, rot);
            return m_LastValid;
        }
    }
}
