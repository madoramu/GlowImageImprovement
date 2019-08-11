using UnityEngine;
using UnityEngine.UI;

namespace UnityEditor.UI
{
    [CustomEditor(typeof(GlowImageImprovement), true)]
    [CanEditMultipleObjects]
    public class GlowImageImprovementEditor : ImageEditor
    {
        private SerializedProperty m_OutlineSize;
        private SerializedProperty m_OutlineColor;
        private SerializedProperty m_OutlineStrength;
        private SerializedProperty m_Quality;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_OutlineSize = serializedObject.FindProperty("m_OutlineSize");
            m_OutlineColor = serializedObject.FindProperty("m_OutlineColor");
            m_OutlineStrength = serializedObject.FindProperty("m_OutlineStrength");
            m_Quality = serializedObject.FindProperty("m_Quality");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            EditorGUILayout.PropertyField(m_OutlineSize, new GUIContent("アウトラインの大きさ"));
            EditorGUILayout.PropertyField(m_OutlineColor, new GUIContent("アウトラインの色"));
            EditorGUILayout.PropertyField(m_OutlineStrength, new GUIContent("アウトラインの強さ"));
            EditorGUILayout.PropertyField(m_Quality, new GUIContent("線のクオリティ"));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
