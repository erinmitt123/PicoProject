using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace PaletteRoom
{
    /// <summary>
    /// Runs after XRI's general grab transformer and enforces real-world placement: nothing goes below
    /// the floor (the room root's height); floor pieces stay on it and only turn around the vertical
    /// axis; wall pieces stay upright. While a floor piece is over a spot it can't stand on (another
    /// piece, a wall, or real furniture such as a bed), it glows red and the controller knocks twice;
    /// dropping it there settles it at the nearest spot it can stand on (or back where it was picked
    /// up), with a firmer buzz.
    /// </summary>
    public class FurnitureGrabTransformer : XRBaseGrabTransformer
    {
        static readonly Color Blocked = new Color(0.95f, 0.18f, 0.15f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        FurnitureItem m_Item;
        XRGrabInteractable m_Grab;
        RoomFurniture m_Room;
        Pose m_PickedUp;
        bool m_Invalid;
        string m_Blocker;
        bool m_Tinted;
        MaterialPropertyBlock m_Block;
        readonly List<Renderer> m_Renderers = new List<Renderer>();

        // Added explicitly in Start (after XRI's own transformers), so it always runs last.
        protected override RegistrationMode registrationMode => RegistrationMode.None;

        Transform m_RoomRoot;

        void Awake()
        {
            m_Item = GetComponent<FurnitureItem>();
            m_Grab = GetComponent<XRGrabInteractable>();
            // Cached now, while the piece is still parented to the room: XRI unparents a piece for the
            // duration of a grab, so the parent can't be asked during one.
            m_RoomRoot = transform.parent;
            m_Room = GetComponentInParent<RoomFurniture>();
        }

        /// <summary>The floor's height: the room root sits on it.</summary>
        float FloorY => m_RoomRoot != null ? m_RoomRoot.position.y : 0f;

        protected override void Start()
        {
            base.Start(); // registration mode is None, so XRI leaves the registering to the lines below
            if (m_Grab == null)
                return;
            m_Grab.AddSingleGrabTransformer(this);
            m_Grab.AddMultipleGrabTransformer(this);
        }

        /// <summary>
        /// XRI's general transformer registers itself in its own Start, so whether it lands before or
        /// after this one varies between runs and devices. This one must run last or its floor and
        /// placement rules are overwritten, so it moves itself to the end whenever a grab begins.
        /// </summary>
        void EnsureLast()
        {
            int single = m_Grab.singleGrabTransformersCount;
            if (single > 0 && !ReferenceEquals(m_Grab.GetSingleGrabTransformerAt(single - 1), this))
                m_Grab.MoveSingleGrabTransformerTo(this, single - 1);
            int multiple = m_Grab.multipleGrabTransformersCount;
            if (multiple > 0 && !ReferenceEquals(m_Grab.GetMultipleGrabTransformerAt(multiple - 1), this))
                m_Grab.MoveMultipleGrabTransformerTo(this, multiple - 1);
        }

        // Pieces resting on this one (pillows on a sofa) move with it; grabbing a pillow moves only the pillow.
        readonly List<(Transform rider, Vector3 localPos, Quaternion localRot)> m_Riders = new List<(Transform, Vector3, Quaternion)>();

        void CollectRiders()
        {
            m_Riders.Clear();
            if (m_Room == null || m_Item.Mount != FurnitureMount.Floor)
                return;
            var mine = m_Item.CurrentFootprints;
            float floorY = FloorY;
            foreach (var other in m_Room.Items)
            {
                if (other == null || other == m_Item || other.Mount != FurnitureMount.Free)
                    continue;
                var p = other.transform.position;
                if (p.y < floorY + 0.1f)
                    continue; // on the floor beside it, not on it
                var point = new[] { new Footprint(new Vector2(p.x, p.z), Vector2.right, Vector2.up, Vector2.one * 0.01f) };
                // Offsets ignore scale, so a piece still scaling in from its drop-in animation carries correctly.
                var inverse = Quaternion.Inverse(transform.rotation);
                if (FurnitureItem.Overlap(mine, point))
                    m_Riders.Add((other.transform, inverse * (p - transform.position), inverse * other.transform.rotation));
            }
        }

        void MoveRiders()
        {
            foreach (var (rider, localPos, localRot) in m_Riders)
                if (rider != null)
                    rider.SetPositionAndRotation(transform.position + transform.rotation * localPos, transform.rotation * localRot);
        }

        void LateUpdate()
        {
            if (m_Riders.Count > 0 && m_Grab != null && m_Grab.isSelected)
                MoveRiders();
        }

        void OnEnable()
        {
            if (m_Grab == null)
                return;
            m_Grab.selectEntered.AddListener(OnSelectEntered);
            m_Grab.selectExited.AddListener(OnSelectExited);
        }

        void OnDisable()
        {
            if (m_Grab == null)
                return;
            m_Grab.selectEntered.RemoveListener(OnSelectEntered);
            m_Grab.selectExited.RemoveListener(OnSelectExited);
            SetTint(false);
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (m_Grab.interactorsSelecting.Count > 1)
                return; // a second hand joining keeps the original pick-up pose
            EnsureLast();
            if (m_Room != null)
                m_Room.MarkEdited();
            m_PickedUp = new Pose(transform.position, transform.rotation);
            m_Invalid = false;
            CollectRiders();
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (m_Grab.isSelected)
                return;
            if (m_Invalid)
            {
                // The releasing controller is no longer selecting, so it's addressed directly.
                if (args.interactorObject is XRBaseInputInteractor releasing)
                    releasing.SendHapticImpulse(0.8f, 0.18f);
                // Dropped on a spot it can't stand on: it settles at the nearest spot it can, else goes back.
                SettleLegally();
            }
            MoveRiders();
            float floorY = FloorY;
            Debug.Log($"[PaletteRoom] {m_Item.DisplayName} set down at y={transform.position.y:F3} (floor {floorY:F3}), carrying {m_Riders.Count}.");
            m_Riders.Clear();
            m_Invalid = false;
            SetTint(false);
        }

        /// <summary>
        /// Puts a piece dropped on a blocked spot somewhere it can stand: the nearest free spot within a
        /// short reach of where it was let go (so it lands about where the user meant), otherwise back
        /// where it was picked up if that spot is free, otherwise the nearest free spot to that.
        /// </summary>
        void SettleLegally()
        {
            const float nearDrop = 1f;
            var released = new Vector3(transform.position.x, FloorY, transform.position.z);
            var rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
            var user = Camera.main != null ? Camera.main.transform.position : m_PickedUp.position;
            if (m_Room.TryFindFreeSpot(m_Item, released, rotation, user, out var spot, nearDrop))
            {
                transform.SetPositionAndRotation(spot, rotation);
                Debug.Log($"[PaletteRoom] {m_Item.DisplayName} can't go there ({m_Blocker}); moved to the nearest free spot, {Vector3.Distance(spot, released):F2} m away.");
                return;
            }
            if (m_Room.IsFree(m_Item, m_PickedUp.position, m_PickedUp.rotation))
            {
                transform.SetPositionAndRotation(m_PickedUp.position, m_PickedUp.rotation);
                Debug.Log($"[PaletteRoom] {m_Item.DisplayName} can't go there ({m_Blocker}); returned to its last spot.");
                return;
            }
            // It was picked up somewhere it couldn't stand (e.g. lifted from the shopping list): find room.
            if (m_Room.TryFindFreeSpot(m_Item, m_PickedUp.position, m_PickedUp.rotation, user, out spot, 4.5f))
            {
                transform.SetPositionAndRotation(spot, m_PickedUp.rotation);
                Debug.Log($"[PaletteRoom] {m_Item.DisplayName} can't go there ({m_Blocker}); moved to the nearest free spot.");
                return;
            }
            transform.SetPositionAndRotation(m_PickedUp.position, m_PickedUp.rotation);
            Debug.LogWarning($"[PaletteRoom] No free floor space anywhere near for {m_Item.DisplayName}; returned to its last spot.");
        }

        public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase,
            ref Pose targetPose, ref Vector3 localScale)
        {
            if (m_Item == null)
                return;

            // Every root sits at the piece's bottom, so nothing may go below the floor (the room root's height).
            float floorY = FloorY;
            var p = targetPose.position;
            if (m_Item.Mount == FurnitureMount.Floor)
                p.y = floorY;
            else
                p.y = Mathf.Max(p.y, floorY);
            targetPose.position = p;

            if (m_Item.Mount == FurnitureMount.Free)
                return;

            float yaw = targetPose.rotation.eulerAngles.y;
            targetPose.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (m_Room == null || !m_Item.BlocksFloor)
                return;
            bool wasInvalid = m_Invalid;
            m_Blocker = m_Room.WhatBlocks(m_Item, targetPose.position, targetPose.rotation);
            m_Invalid = m_Blocker != null;
            SetTint(m_Invalid);
            if (m_Invalid && !wasInvalid)
                StartCoroutine(BlockedBuzz()); // feel it the moment the piece moves onto a spot it can't go
        }

        // ---- haptics ----------------------------------------------------------------------

        /// <summary>A "no" pattern: two short knocks on the holding controller.</summary>
        IEnumerator BlockedBuzz()
        {
            Buzz(0.55f, 0.06f);
            yield return new WaitForSecondsRealtime(0.11f);
            Buzz(0.55f, 0.06f);
        }

        void Buzz(float amplitude, float seconds)
        {
            if (m_Grab == null)
                return;
            foreach (var interactor in m_Grab.interactorsSelecting)
                if (interactor is XRBaseInputInteractor input)
                    input.SendHapticImpulse(amplitude, seconds);
        }

        /// <summary>Shows the piece red (without touching its palette materials) while it's over a bad spot.</summary>
        void SetTint(bool on)
        {
            if (on == m_Tinted)
                return;
            m_Tinted = on;
            if (m_Block == null)
                m_Block = new MaterialPropertyBlock();
            GetComponentsInChildren(m_Renderers);
            foreach (var r in m_Renderers)
            {
                if (!on)
                {
                    r.SetPropertyBlock(null);
                    continue;
                }
                var mat = r.sharedMaterial;
                var baseColor = mat != null && mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
                m_Block.Clear();
                m_Block.SetColor(BaseColorId, Color.Lerp(baseColor, Blocked, 0.7f));
                r.SetPropertyBlock(m_Block);
            }
        }
    }
}
