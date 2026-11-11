using System.Collections.Generic;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>
    /// Keeps floating panels from covering each other. Panels are world-anchored, so a panel opened in front
    /// of the user can land on top of one placed earlier (the tutorial card, a brief's checklist, a piece's
    /// colors). Overlap is judged as the user sees it: each panel's extent in yaw and pitch from the head.
    /// A clash is resolved by swinging a panel round the user, a step at a time, keeping its distance and
    /// height and still facing them.
    /// </summary>
    public static class PanelSpace
    {
        const float StepDegrees = 8f;
        const int MaxSteps = 5;           // up to 40° either way
        const float MarginDegrees = 1.5f; // a clear gap between neighbours

        struct Extent
        {
            public float yawMin, yawMax, pitchMin, pitchMax;

            public bool Overlaps(Extent o) =>
                yawMin < o.yawMax + MarginDegrees && o.yawMin < yawMax + MarginDegrees &&
                pitchMin < o.pitchMax + MarginDegrees && o.pitchMin < pitchMax + MarginDegrees;
        }

        static readonly Vector3[] s_Corners = new Vector3[4];

        // Panels swung aside to make room, with where they were, so they can go back when the panel that
        // pushed them closes (unless the user has moved them since).
        struct Pushed
        {
            public Transform panel, by;
            public Vector3 homePos, pushedPos;
            public Quaternion homeRot;
        }

        static readonly List<Pushed> s_Pushed = new List<Pushed>();
        static Transform s_Head;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Pushed.Clear();
            s_Head = null;
        }

        /// <summary>
        /// The panel was just opened where the user is looking and has priority: any other open panel it covers
        /// is swung out of the way, to whichever side it was already on.
        /// </summary>
        public static void MakeRoomFor(Transform panel, Transform head)
        {
            if (panel == null || head == null || !TryExtent(panel, head, out var mine))
                return;
            s_Head = head;
            float mid = (mine.yawMin + mine.yawMax) / 2f;
            foreach (var other in MovablePanel.All)
            {
                if (other == null || other.transform == panel || !other.gameObject.activeInHierarchy)
                    continue;
                if (!TryExtent(other.transform, head, out var theirs) || !mine.Overlaps(theirs))
                    continue;
                float side = (theirs.yawMin + theirs.yawMax) / 2f >= mid ? 1f : -1f;
                var t = other.transform;
                var homePos = t.position;
                var homeRot = t.rotation;
                KeepClear(t, head, side);
                if ((t.position - homePos).sqrMagnitude < 1e-6f)
                    continue;
                // Pushed twice: keep the first home, the spot the user actually chose.
                int existing = s_Pushed.FindIndex(p => p.panel == t);
                if (existing >= 0)
                {
                    var p = s_Pushed[existing];
                    p.pushedPos = t.position;
                    p.by = panel;
                    s_Pushed[existing] = p;
                }
                else
                {
                    s_Pushed.Add(new Pushed { panel = t, by = panel, homePos = homePos, homeRot = homeRot, pushedPos = t.position });
                }
            }
        }

        /// <summary>
        /// A panel closed: anything it swung aside goes back where it was, if the user hasn't moved it
        /// in the meantime and the spot is free again.
        /// </summary>
        public static void Released(Transform panel)
        {
            for (int i = s_Pushed.Count - 1; i >= 0; i--)
            {
                var p = s_Pushed[i];
                if (p.panel == null)
                {
                    s_Pushed.RemoveAt(i);
                    continue;
                }
                if (p.by != panel)
                    continue;
                s_Pushed.RemoveAt(i);
                if ((p.panel.position - p.pushedPos).sqrMagnitude > 0.0004f)
                    continue; // moved by the user (or recalled): leave it where it is now
                var nowPos = p.panel.position;
                var nowRot = p.panel.rotation;
                p.panel.SetPositionAndRotation(p.homePos, p.homeRot);
                if (s_Head != null && Clashes(p.panel, s_Head))
                    p.panel.SetPositionAndRotation(nowPos, nowRot);
            }
        }

        /// <summary>The user moved or recalled the panel: it no longer has a home to go back to.</summary>
        public static void Forget(Transform panel) => s_Pushed.RemoveAll(p => p.panel == panel);

        /// <summary>
        /// Moves <paramref name="panel"/> round the user until it covers no other open panel, trying the
        /// <paramref name="preferredSide"/> (+1 right, −1 left) first. Left where it is if nothing clears.
        /// </summary>
        public static void KeepClear(Transform panel, Transform head, float preferredSide)
        {
            if (panel == null || head == null || !Clashes(panel, head))
                return;
            var startPos = panel.position;
            var startRot = panel.rotation;
            float side = preferredSide >= 0f ? 1f : -1f;
            for (int pass = 0; pass < 2; pass++, side = -side)
            {
                for (int step = 1; step <= MaxSteps; step++)
                {
                    Swing(panel, head, startPos, startRot, side * step * StepDegrees);
                    if (!Clashes(panel, head))
                        return;
                }
            }
            panel.SetPositionAndRotation(startPos, startRot);
        }

        static void Swing(Transform panel, Transform head, Vector3 startPos, Quaternion startRot, float degrees)
        {
            var pivot = new Vector3(head.position.x, startPos.y, head.position.z);
            var turn = Quaternion.AngleAxis(degrees, Vector3.up);
            panel.SetPositionAndRotation(pivot + turn * (startPos - pivot), turn * startRot);
        }

        static bool Clashes(Transform panel, Transform head)
        {
            if (!TryExtent(panel, head, out var mine))
                return false;
            foreach (var other in MovablePanel.All)
            {
                if (other == null || other.transform == panel || !other.gameObject.activeInHierarchy)
                    continue;
                if (TryExtent(other.transform, head, out var theirs) && mine.Overlaps(theirs))
                    return true;
            }
            return false;
        }

        static readonly List<Canvas> s_Canvases = new List<Canvas>();

        /// <summary>
        /// The panel as seen from the head, in degrees: every visible canvas it has (a dock's side rails
        /// as well as its main board), so nothing it shows can end up under another panel.
        /// </summary>
        static bool TryExtent(Transform panel, Transform head, out Extent extent)
        {
            extent = default;
            panel.GetComponentsInChildren(false, s_Canvases);
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            var basis = Quaternion.Inverse(Quaternion.LookRotation(forward.normalized, Vector3.up));
            extent.yawMin = extent.pitchMin = float.MaxValue;
            extent.yawMax = extent.pitchMax = float.MinValue;
            bool any = false;
            foreach (var c in s_Canvases)
            {
                if (!c.isRootCanvas)
                    continue;
                ((RectTransform)c.transform).GetWorldCorners(s_Corners);
                foreach (var corner in s_Corners)
                {
                    var d = basis * (corner - head.position);
                    float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    float pitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                    extent.yawMin = Mathf.Min(extent.yawMin, yaw);
                    extent.yawMax = Mathf.Max(extent.yawMax, yaw);
                    extent.pitchMin = Mathf.Min(extent.pitchMin, pitch);
                    extent.pitchMax = Mathf.Max(extent.pitchMax, pitch);
                }
                any = true;
            }
            s_Canvases.Clear();
            return any;
        }
    }
}
