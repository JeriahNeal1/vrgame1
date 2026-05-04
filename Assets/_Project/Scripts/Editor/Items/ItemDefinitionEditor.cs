using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VRGame.Items.Editor
{
    [CustomEditor(typeof(ItemDefinition))]
    public sealed class ItemDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ItemDefinition itemDefinition = (ItemDefinition)target;
            List<ItemDefinitionValidationIssue> issues = ItemDefinitionValidator.Validate(itemDefinition);
            ItemDefinitionEditorUtility.DrawValidationIssues(issues);
        }
    }

    internal static class ItemDefinitionEditorUtility
    {
        public static void DrawValidationIssues(IReadOnlyList<ItemDefinitionValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("No item definition validation issues found.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

            for (int i = 0; i < issues.Count; i++)
            {
                ItemDefinitionValidationIssue issue = issues[i];
                EditorGUILayout.HelpBox(issue.Message, ToMessageType(issue.Severity));
            }
        }

        public static MessageType ToMessageType(ItemDefinitionValidationSeverity severity)
        {
            switch (severity)
            {
                case ItemDefinitionValidationSeverity.Error:
                    return MessageType.Error;
                case ItemDefinitionValidationSeverity.Warning:
                    return MessageType.Warning;
                case ItemDefinitionValidationSeverity.Info:
                default:
                    return MessageType.Info;
            }
        }

        public static LogType ToLogType(ItemDefinitionValidationSeverity severity)
        {
            switch (severity)
            {
                case ItemDefinitionValidationSeverity.Error:
                    return LogType.Error;
                case ItemDefinitionValidationSeverity.Warning:
                    return LogType.Warning;
                case ItemDefinitionValidationSeverity.Info:
                default:
                    return LogType.Log;
            }
        }
    }
}
