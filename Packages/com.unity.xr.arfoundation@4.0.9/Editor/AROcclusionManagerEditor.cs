using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace UnityEditor.XR.ARFoundation
{
    [CustomEditor(typeof(AROcclusionManager))]
    internal class AROcclusionManagerEditor : Editor
    {
        SerializedProperty m_HumanSegmentationStencilMode;
        SerializedProperty m_HumanSegmentationDepthMode;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_HumanSegmentationStencilMode);
            if (!((HumanSegmentationStencilMode)m_HumanSegmentationStencilMode.enumValueIndex).Enabled())
            {
                ++EditorGUI.indentLevel;
                EditorGUILayout.HelpBox("Automatic occlusion during runtime rendering will be disabled with "
                                        + $"{m_HumanSegmentationStencilMode.displayName} set to "
                                        + $"{m_HumanSegmentationStencilMode.enumDisplayNames[m_HumanSegmentationStencilMode.enumValueIndex]}.",
                                        MessageType.Warning);
                --EditorGUI.indentLevel;
            }

            EditorGUILayout.PropertyField(m_HumanSegmentationDepthMode);
            if (!((HumanSegmentationDepthMode)m_HumanSegmentationDepthMode.enumValueIndex).Enabled())
            {
                ++EditorGUI.indentLevel;
                EditorGUILayout.HelpBox("Automatic occlusion during runtime rendering will be disabled with "
                                        + $"{m_HumanSegmentationDepthMode.displayName} set to "
                                        + $"{m_HumanSegmentationDepthMode.enumDisplayNames[m_HumanSegmentationDepthMode.enumValueIndex]}.",
                                        MessageType.Warning);
                --EditorGUI.indentLevel;
            }

            serializedObject.ApplyModifiedProperties();
        }

        void OnEnable()
        {
            m_HumanSegmentationStencilMode = serializedObject.FindProperty("m_HumanSegmentationStencilMode");
            m_HumanSegmentationDepthMode = serializedObject.FindProperty("m_HumanSegmentationDepthMode");
        }
    }
}
