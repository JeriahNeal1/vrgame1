using System.Text;
using UnityEditor;
using UnityEngine;

namespace VRGame.Items.Editor
{
    [CustomPropertyDrawer(typeof(ItemDefId))]
    public sealed class ItemDefIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty valueProperty = property.FindPropertyRelative("value");
            EditorGUI.PropertyField(position, valueProperty, label);
        }
    }

    [CustomPropertyDrawer(typeof(StatId))]
    public sealed class StatIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty valueProperty = property.FindPropertyRelative("value");
            EditorGUI.PropertyField(position, valueProperty, label);
        }
    }

    [CustomPropertyDrawer(typeof(ModifierId))]
    public sealed class ModifierIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty valueProperty = property.FindPropertyRelative("value");
            EditorGUI.PropertyField(position, valueProperty, label);
        }
    }

    [CustomPropertyDrawer(typeof(EnchantmentId))]
    public sealed class EnchantmentIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty valueProperty = property.FindPropertyRelative("value");
            EditorGUI.PropertyField(position, valueProperty, label);
        }
    }

    [CustomPropertyDrawer(typeof(DefinitionIdReference))]
    public sealed class DefinitionIdReferenceDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorGUIUtility.singleLineHeight * 2f) + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty idProperty = property.FindPropertyRelative("id");
            SerializedProperty noteProperty = property.FindPropertyRelative("note");

            Rect idRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect noteRect = new Rect(
                position.x,
                position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight);

            EditorGUI.PropertyField(idRect, idProperty, label);
            EditorGUI.PropertyField(noteRect, noteProperty);
        }
    }

    [CustomPropertyDrawer(typeof(ItemCategoryPath))]
    public sealed class ItemCategoryPathDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty segmentsProperty = property.FindPropertyRelative("segments");
            return EditorGUI.GetPropertyHeight(segmentsProperty, label, true) +
                   EditorGUIUtility.singleLineHeight +
                   EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty segmentsProperty = property.FindPropertyRelative("segments");
            float segmentsHeight = EditorGUI.GetPropertyHeight(segmentsProperty, label, true);

            Rect segmentsRect = new Rect(position.x, position.y, position.width, segmentsHeight);
            Rect previewRect = new Rect(
                position.x,
                position.y + segmentsHeight + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight);

            EditorGUI.PropertyField(segmentsRect, segmentsProperty, label, true);
            EditorGUI.LabelField(previewRect, "Normalized Path", BuildPreview(segmentsProperty), EditorStyles.miniLabel);
        }

        private static string BuildPreview(SerializedProperty segmentsProperty)
        {
            if (segmentsProperty == null || !segmentsProperty.isArray || segmentsProperty.arraySize == 0)
            {
                return "(empty)";
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < segmentsProperty.arraySize; i++)
            {
                SerializedProperty element = segmentsProperty.GetArrayElementAtIndex(i);
                if (element == null || string.IsNullOrWhiteSpace(element.stringValue))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(" > ");
                }

                builder.Append(element.stringValue.Trim());
            }

            return builder.Length == 0 ? "(empty)" : builder.ToString();
        }
    }
}
