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
            ItemDefinitionEditorUtility.DrawAssetReferenceIssues(itemDefinition);

            EditorGUILayout.Space();
            if (GUILayout.Button("Open Icon Generator"))
            {
                ItemIconGeneratorWindow.OpenForItem(itemDefinition);
            }
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

        public static void DrawAssetReferenceIssues(ItemDefinition itemDefinition)
        {
            if (itemDefinition == null)
            {
                return;
            }

            bool drewHeader = false;
            DrawGameObjectAssetIssue(itemDefinition.WorldPrefab, "World Prefab", ref drewHeader);

            PlaceableProfile placeableProfile = itemDefinition.PlaceableProfile;
            if (placeableProfile != null)
            {
                DrawGameObjectAssetIssue(placeableProfile.PlacedPrefab, "Placed Prefab", ref drewHeader);
                if (placeableProfile.PreviewPrefab != placeableProfile.PlacedPrefab)
                {
                    DrawGameObjectAssetIssue(placeableProfile.PreviewPrefab, "Preview Prefab", ref drewHeader);
                }
            }
        }

        public static int LogAssetReferenceIssues(ItemDefinition itemDefinition)
        {
            if (itemDefinition == null)
            {
                return 0;
            }

            int issueCount = 0;
            issueCount += LogGameObjectAssetIssue(itemDefinition, itemDefinition.WorldPrefab, "World Prefab");

            PlaceableProfile placeableProfile = itemDefinition.PlaceableProfile;
            if (placeableProfile != null)
            {
                issueCount += LogGameObjectAssetIssue(itemDefinition, placeableProfile.PlacedPrefab, "Placed Prefab");
                if (placeableProfile.PreviewPrefab != placeableProfile.PlacedPrefab)
                {
                    issueCount += LogGameObjectAssetIssue(itemDefinition, placeableProfile.PreviewPrefab, "Preview Prefab");
                }
            }

            return issueCount;
        }

        private static void DrawGameObjectAssetIssue(GameObject gameObjectReference, string label, ref bool drewHeader)
        {
            if (gameObjectReference == null || EditorUtility.IsPersistent(gameObjectReference))
            {
                return;
            }

            if (!drewHeader)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Asset Reference Validation", EditorStyles.boldLabel);
                drewHeader = true;
            }

            EditorGUILayout.HelpBox($"{label} references a scene object. Item definition assets should reference prefab/model assets, not scene instances.", MessageType.Warning);
        }

        private static int LogGameObjectAssetIssue(ItemDefinition itemDefinition, GameObject gameObjectReference, string label)
        {
            if (gameObjectReference == null || EditorUtility.IsPersistent(gameObjectReference))
            {
                return 0;
            }

            Debug.LogWarning($"{itemDefinition.name}: {label} references a scene object. Item definition assets should reference prefab/model assets, not scene instances.", itemDefinition);
            return 1;
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
