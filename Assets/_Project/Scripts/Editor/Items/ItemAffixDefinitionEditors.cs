using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VRGame.Items.Editor
{
    [CustomEditor(typeof(ModifierDefinition))]
    public sealed class ModifierDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ModifierDefinition modifierDefinition = (ModifierDefinition)target;
            ItemAffixEditorUtility.DrawValidationIssues(modifierDefinition.ValidateDefinition());
        }
    }

    [CustomEditor(typeof(EnchantmentDefinition))]
    public sealed class EnchantmentDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EnchantmentDefinition enchantmentDefinition = (EnchantmentDefinition)target;
            ItemAffixEditorUtility.DrawValidationIssues(enchantmentDefinition.ValidateDefinition());
        }
    }

    [CustomEditor(typeof(ItemAffixDefinitionDatabase))]
    public sealed class ItemAffixDefinitionDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ItemAffixDefinitionDatabase database = (ItemAffixDefinitionDatabase)target;

            EditorGUILayout.Space();
            if (GUILayout.Button("Rebuild Lookup Cache"))
            {
                database.RebuildLookup();
                EditorUtility.SetDirty(database);
            }

            ItemAffixEditorUtility.DrawValidationIssues(database.ValidateDefinitions());
        }
    }

    internal static class ItemAffixEditorUtility
    {
        public static void DrawValidationIssues(IReadOnlyList<ItemAffixDefinitionValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("No affix validation issues found.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

            for (int i = 0; i < issues.Count; i++)
            {
                ItemAffixDefinitionValidationIssue issue = issues[i];
                EditorGUILayout.HelpBox(issue.Message, ToMessageType(issue.Severity));
            }
        }

        public static MessageType ToMessageType(ItemAffixDefinitionValidationSeverity severity)
        {
            switch (severity)
            {
                case ItemAffixDefinitionValidationSeverity.Error:
                    return MessageType.Error;
                case ItemAffixDefinitionValidationSeverity.Warning:
                    return MessageType.Warning;
                case ItemAffixDefinitionValidationSeverity.Info:
                default:
                    return MessageType.Info;
            }
        }
    }
}
