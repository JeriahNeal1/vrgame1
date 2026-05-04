using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VRGame.Items.Editor
{
    public static class ItemDefinitionValidationMenu
    {
        [MenuItem("Tools/VRGame/Items/Validate Item Definitions")]
        public static void ValidateItemDefinitions()
        {
            int issueCount = 0;

            string[] databaseGuids = AssetDatabase.FindAssets("t:ItemDefinitionDatabase");
            for (int i = 0; i < databaseGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(databaseGuids[i]);
                ItemDefinitionDatabase database = AssetDatabase.LoadAssetAtPath<ItemDefinitionDatabase>(path);
                if (database == null)
                {
                    continue;
                }

                issueCount += LogIssues(database.ValidateDefinitions());
                IReadOnlyList<ItemDefinition> definitions = database.ItemDefinitions;
                for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
                {
                    issueCount += ItemDefinitionEditorUtility.LogAssetReferenceIssues(definitions[definitionIndex]);
                }
            }

            if (databaseGuids.Length == 0)
            {
                string[] itemGuids = AssetDatabase.FindAssets("t:ItemDefinition");
                for (int i = 0; i < itemGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(itemGuids[i]);
                    ItemDefinition itemDefinition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                    if (itemDefinition == null)
                    {
                        continue;
                    }

                    issueCount += LogIssues(ItemDefinitionValidator.Validate(itemDefinition));
                    issueCount += ItemDefinitionEditorUtility.LogAssetReferenceIssues(itemDefinition);
                }
            }

            if (issueCount == 0)
            {
                Debug.Log("Item definition validation completed with no issues.");
            }
            else
            {
                Debug.Log($"Item definition validation completed with {issueCount} issue(s).");
            }
        }

        private static int LogIssues(IReadOnlyList<ItemDefinitionValidationIssue> issues)
        {
            if (issues == null)
            {
                return 0;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                ItemDefinitionValidationIssue issue = issues[i];
                Object context = issue.ItemDefinition;
                LogType logType = ItemDefinitionEditorUtility.ToLogType(issue.Severity);
                Debug.unityLogger.Log(logType, "ItemDefinition", issue.Message, context);
            }

            return issues.Count;
        }
    }
}
