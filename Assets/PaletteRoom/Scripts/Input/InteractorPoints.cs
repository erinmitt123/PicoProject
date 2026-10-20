using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PaletteRoom
{
    /// <summary>
    /// Finds the active near/far interactors and where they really are. Hand interactor groups don't
    /// move themselves (their sibling "Aim Pose" is tracked), so position comes from the caster origin,
    /// which points at the tracked pose for both hands and controllers.
    /// </summary>
    public class InteractorPoints
    {
        readonly List<NearFarInteractor> m_Interactors = new List<NearFarInteractor>();
        float m_NextRefresh;

        /// <summary>Active interactors; the list is refreshed at most once a second.</summary>
        public IReadOnlyList<NearFarInteractor> Active
        {
            get
            {
                if (Time.unscaledTime >= m_NextRefresh)
                {
                    m_NextRefresh = Time.unscaledTime + 1f;
                    m_Interactors.Clear();
                    m_Interactors.AddRange(Object.FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None));
                }
                // The modality manager toggles hand / controller rigs as tracking changes.
                m_Interactors.RemoveAll(i => i == null);
                return m_Interactors;
            }
        }

        public NearFarInteractor Find(InteractorHandedness handedness)
        {
            foreach (var interactor in Active)
            {
                if (interactor.isActiveAndEnabled && interactor.handedness == handedness)
                    return interactor;
            }
            return null;
        }

        public static Vector3 Position(NearFarInteractor interactor)
        {
            var origin = interactor.nearInteractionCaster != null ? interactor.nearInteractionCaster.castOrigin : null;
            return origin != null ? origin.position : interactor.transform.position;
        }
    }
}
