using System.Collections.Generic;
using NUnit.Framework;

using UnityEngine.XR.ARSubsystems;

namespace UnityEngine.XR.ARFoundation
{
    [TestFixture]
    class RaycastSamples
    {
        #pragma warning disable CS0649

        #region raycasthit_trackable
        ARPlaneManager m_PlaneManager;

        void HandleRaycast(ARRaycastHit hit)
        {
            // Determine if it is a plane
            if ((hit.hitType & TrackableType.Planes) != 0)
            {
                // Look up the plane by id
                var plane = m_PlaneManager.GetPlane(hit.trackableId);

                // Do something with 'plane':
                Debug.Log($"Hit a plane with alignment {plane.alignment}");
            }
            else
            {
                // What type of thing did we hit?
                Debug.Log($"Raycast hit a {hit.hitType}");
            }
        }
        #endregion

        class UsingTouch : MonoBehaviour
        {
            #region raycast_using_touch
            [SerializeField]
            ARRaycastManager m_RaycastManager;

            List<ARRaycastHit> m_Hits = new List<ARRaycastHit>();

            void Update()
            {
                if (Input.touchCount == 0)
                    return;

                if (m_RaycastManager.Raycast(Input.GetTouch(0).position, m_Hits))
                {
                    // Only returns true if there is at least one hit
                }
            }
            #endregion
        }

        #pragma warning restore CS0649
    }
}
