using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PaletteRoom
{
    /// <summary>
    /// Lazily keeps a panel pinned above the left hand or controller, facing the user. When neither is
    /// tracked it rests front-left of the user at chest height. Never comes closer than
    /// <see cref="minDistanceFromHead"/>.
    /// </summary>
    public class HandDockFollower : MonoBehaviour
    {
        [SerializeField] Transform head;
        [SerializeField] float aboveHand = 0.16f;
        [SerializeField] float minDistanceFromHead = 0.5f;
        [Tooltip("The panel only starts moving once its target drifts this far (m).")]
        [SerializeField] float moveThreshold = 0.1f;
        [SerializeField] float smoothTime = 0.25f;

        readonly InteractorPoints m_Points = new InteractorPoints();
        Vector3 m_Target;
        Vector3 m_Velocity;
        bool m_Moving;
        bool m_Initialized;

        public void Configure(Transform headTransform) => head = headTransform;

        /// <summary>Jumps straight to the target pose, e.g. when the dock first appears.</summary>
        public void Snap()
        {
            m_Target = ComputeTarget();
            transform.position = m_Target;
            FaceHead();
            m_Velocity = Vector3.zero;
            m_Moving = false;
            m_Initialized = true;
        }

        void OnEnable() => m_Initialized = false;

        void LateUpdate()
        {
            if (head == null)
                return;
            if (!m_Initialized)
            {
                Snap();
                return;
            }

            var target = ComputeTarget();
            if (!m_Moving && Vector3.Distance(target, transform.position) > moveThreshold)
                m_Moving = true;
            if (m_Moving)
            {
                m_Target = target;
                transform.position = Vector3.SmoothDamp(transform.position, m_Target, ref m_Velocity, smoothTime);
                if (Vector3.Distance(transform.position, m_Target) < 0.005f)
                    m_Moving = false;
            }
            FaceHead();
        }

        Vector3 ComputeTarget()
        {
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            forward.Normalize();

            // A hand at the head's position is an untracked pose, not a real hand.
            var hand = m_Points.Find(InteractorHandedness.Left);
            if (hand != null)
            {
                var handPos = InteractorPoints.Position(hand);
                if ((handPos - head.position).sqrMagnitude > 0.15f * 0.15f)
                    return KeepAway(handPos + Vector3.up * aboveHand, forward);
            }

            var restDir = Quaternion.Euler(0f, -35f, 0f) * forward;
            return KeepAway(head.position + restDir * 0.6f + Vector3.down * 0.3f, forward);
        }

        /// <summary>Pushes the target out horizontally so UI never sits closer than the minimum distance.</summary>
        Vector3 KeepAway(Vector3 target, Vector3 headForward)
        {
            var flat = Vector3.ProjectOnPlane(target - head.position, Vector3.up);
            float horizontalMin = Mathf.Sqrt(Mathf.Max(0f,
                minDistanceFromHead * minDistanceFromHead - (target.y - head.position.y) * (target.y - head.position.y)));
            if (flat.magnitude >= horizontalMin)
                return target;
            var dir = flat.sqrMagnitude > 0.0001f ? flat.normalized : headForward;
            var pushed = head.position + dir * horizontalMin;
            return new Vector3(pushed.x, target.y, pushed.z);
        }

        void FaceHead()
        {
            var away = transform.position - head.position;
            away.y *= 0.5f; // keep the panel mostly upright
            if (away.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }
    }
}
