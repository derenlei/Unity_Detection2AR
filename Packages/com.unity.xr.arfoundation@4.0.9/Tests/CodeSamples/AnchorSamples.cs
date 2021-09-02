using NUnit.Framework;

namespace UnityEngine.XR.ARFoundation
{
    [TestFixture]
    class AnchorSamples
    {
        // Disable "field never assigned to"
        #pragma warning disable CS0649
        class ExistingContent
        {
            #region anchor_existing_content
            ARAnchorManager m_AnchorManager;

            void AnchorContent(Vector3 position, Transform content)
            {
                // Create a new anchor.
                var anchor = m_AnchorManager.AddAnchor(new Pose(position, Quaternion.identity));

                // Parent 'content' to it.
                content.parent = anchor.transform;
            }
            #endregion
        }

        class Prefab : MonoBehaviour
        {
            #region anchor_prefab_content
            ARAnchorManager m_AnchorManager;

            void AnchorContent(Vector3 position, GameObject prefab)
            {
                // Create a new anchor.
                var anchor = m_AnchorManager.AddAnchor(new Pose(position, Quaternion.identity));

                // Instantiate 'prefab' as a child of the new anchor.
                Instantiate(prefab, anchor.transform);
            }
            #endregion
        }
        #pragma warning restore CS0649
    }
}
