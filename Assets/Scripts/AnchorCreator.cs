using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class AnchorCreator : MonoBehaviour
{
    void Awake()
    {
        m_RaycastManager = GetComponent<ARRaycastManager>();
        m_AnchorManager = GetComponent<ARAnchorManager>();
        GameObject cameraImage = GameObject.Find("Camera Image");
        phoneARCamera = cameraImage.GetComponent<PhoneARCamera>();
    }

    ARAnchor CreateAnchor(in ARRaycastHit hit)
    {
        // TODO: create plane anchor

        // create a regular anchor at the hit pose
        Debug.Log($"DEBUG: Creating regular anchor. distance: {hit.distance}. session distance: {hit.sessionRelativeDistance} type: {hit.hitType}.");
        return m_AnchorManager.AddAnchor(hit.pose);
    }

    private bool Pos2Anchor(float x, float y, BoundingBox outline)
    {
        // GameObject anchorObj = m_RaycastManager.raycastPrefab;
        // TextMesh anchorObj_mesh = anchorObj.GetComponent<TextMesh>();
        anchorObj_mesh.text = $"{outline.Label}: {(int)(outline.Confidence * 100)}%";
        // Perform the raycast
        if (m_RaycastManager.Raycast(new Vector2(x, y), s_Hits, trackableTypes))
        {
            // Raycast hits are sorted by distance, so the first one will be the closest hit.
            var hit = s_Hits[0];
            //TextMesh anchorObj = GameObject.Find("New Text").GetComponent<TextMesh>();
            // Create a new anchor
            var anchor = CreateAnchor(hit);
            if (anchor)
            {
                Debug.Log($"DEBUG: creating anchor. {outline}");
                // Remember the anchor so we can remove it later.
                anchorDic.Add(anchor, outline);
                return true;
            }
            else
            {
                Debug.Log("DEBUG: Error creating anchor");
                return false;
            }
        }
        return false;
    }

    void Update()
    {
        // If bounding boxes are not stable, return directly without raycast
        if (!phoneARCamera.localization)
        {
            return;
        }

        boxSavedOutlines = phoneARCamera.boxSavedOutlines;
        shiftX = phoneARCamera.shiftX;
        shiftY = phoneARCamera.shiftY;
        scaleFactor = phoneARCamera.scaleFactor;
        // Remove outdated anchor that is not in boxSavedOutlines
        // Currently not using. Can be removed.
        if (anchorDic.Count != 0)
        {
            foreach (KeyValuePair<ARAnchor, BoundingBox> pair in anchorDic)
            {
                if (!boxSavedOutlines.Contains(pair.Value))
                {
                    Debug.Log( $"DEBUG: anchor removed. {pair.Value.Label}: {(int)(pair.Value.Confidence * 100)}%");

                    anchorDic.Remove(pair.Key);
                    m_AnchorManager.RemoveAnchor(pair.Key);
                    s_Hits.Clear();
                }
            }
        }

        // return if no bounding boxes
        if (boxSavedOutlines.Count == 0)
        {
            return;
        }
        // create anchor for new bounding boxes
        foreach (var outline in boxSavedOutlines)
        {
            if (outline.Used)
            {
                continue;
            }

            // Note: rect bounding box coordinates starts from top left corner.
            // AR camera starts from borrom left corner.
            // Need to flip Y axis coordinate of the anchor 2D position when raycast
            var xMin = outline.Dimensions.X * this.scaleFactor + this.shiftX;
            var width = outline.Dimensions.Width * this.scaleFactor;
            var yMin = outline.Dimensions.Y * this.scaleFactor + this.shiftY;
            yMin = Screen.height - yMin;
            var height = outline.Dimensions.Height * this.scaleFactor;

            float center_x = xMin + width / 2f;
            float center_y = yMin - height / 2f;


            if (Pos2Anchor(center_x, center_y, outline))
            {
                outline.Used = true;
            }
        }
        Debug.Log($"DEBUG: Current number of anchors {anchorDic.Count}.");
    }

    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

    IDictionary<ARAnchor, BoundingBox> anchorDic = new Dictionary<ARAnchor, BoundingBox>();

    // from PhoneARCamera
    private List<BoundingBox>  boxSavedOutlines;
    private float shiftX;
    private float shiftY;
    private float scaleFactor;

    public PhoneARCamera phoneARCamera;
    public ARRaycastManager m_RaycastManager;
    public TextMesh anchorObj_mesh;
    public ARAnchorManager m_AnchorManager;

    // Raycast against planes and feature points
    const TrackableType trackableTypes = TrackableType.Planes;//FeaturePoint;
}
