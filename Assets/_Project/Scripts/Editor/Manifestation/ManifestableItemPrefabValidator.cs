using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRGame.Items;
using VRGame.Runtime;

namespace VRGame.Manifestation.Editor
{
    public enum ManifestablePrefabValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class ManifestablePrefabValidationIssue
    {
        public ManifestablePrefabValidationIssue(ManifestablePrefabValidationSeverity severity, ItemDefinition itemDefinition, string message)
        {
            Severity = severity;
            ItemDefinition = itemDefinition;
            Message = message ?? string.Empty;
        }

        public ManifestablePrefabValidationSeverity Severity { get; }

        public ItemDefinition ItemDefinition { get; }

        public string Message { get; }
    }

    public static class ManifestableItemPrefabValidator
    {
        private const string HvrGrabbableTypeName = "HurricaneVR.Framework.Core.HVRGrabbable, HurricaneVR.Framework";

        public static List<ManifestablePrefabValidationIssue> Validate(ItemDefinition itemDefinition)
        {
            List<ManifestablePrefabValidationIssue> issues = new List<ManifestablePrefabValidationIssue>();
            AppendIssues(itemDefinition, issues);
            return issues;
        }

        public static List<ManifestablePrefabValidationIssue> Validate(ItemDefinitionDatabase database)
        {
            List<ManifestablePrefabValidationIssue> issues = new List<ManifestablePrefabValidationIssue>();
            if (database == null)
            {
                issues.Add(new ManifestablePrefabValidationIssue(ManifestablePrefabValidationSeverity.Error, null, "Item definition database is null."));
                return issues;
            }

            IReadOnlyList<ItemDefinition> definitions = database.ItemDefinitions;
            for (int i = 0; i < definitions.Count; i++)
            {
                AppendIssues(definitions[i], issues);
            }

            return issues;
        }

        private static void AppendIssues(ItemDefinition itemDefinition, List<ManifestablePrefabValidationIssue> issues)
        {
            if (itemDefinition == null || !itemDefinition.IsManifestable)
            {
                return;
            }

            GameObject prefab = itemDefinition.WorldPrefab;
            if (prefab == null)
            {
                issues.Add(new ManifestablePrefabValidationIssue(ManifestablePrefabValidationSeverity.Error, itemDefinition, $"Manifestable item '{itemDefinition.ItemDefId}' is missing a world prefab."));
                return;
            }

            if (prefab.GetComponentInChildren<Rigidbody>(true) == null)
            {
                issues.Add(new ManifestablePrefabValidationIssue(ManifestablePrefabValidationSeverity.Warning, itemDefinition, $"World prefab '{prefab.name}' for '{itemDefinition.ItemDefId}' is missing a Rigidbody."));
            }

            if (prefab.GetComponentInChildren<Collider>(true) == null)
            {
                issues.Add(new ManifestablePrefabValidationIssue(ManifestablePrefabValidationSeverity.Warning, itemDefinition, $"World prefab '{prefab.name}' for '{itemDefinition.ItemDefId}' is missing a Collider."));
            }

            if (prefab.GetComponentInChildren<WorldItemIdentity>(true) == null)
            {
                issues.Add(new ManifestablePrefabValidationIssue(ManifestablePrefabValidationSeverity.Warning, itemDefinition, $"World prefab '{prefab.name}' for '{itemDefinition.ItemDefId}' is missing WorldItemIdentity. The spawner can add it at runtime, but prefab authoring should include it."));
            }

            if (prefab.GetComponentInChildren<WorldItemView>(true) == null)
            {
                issues.Add(new ManifestablePrefabValidationIssue(ManifestablePrefabValidationSeverity.Warning, itemDefinition, $"World prefab '{prefab.name}' for '{itemDefinition.ItemDefId}' is missing WorldItemView. The spawner can add it at runtime, but prefab authoring should include it."));
            }

            if (itemDefinition.HasFlag(ItemFlags.CanBeHeld) && !HasHvrGrabbable(prefab))
            {
                issues.Add(new ManifestablePrefabValidationIssue(ManifestablePrefabValidationSeverity.Warning, itemDefinition, $"Held prefab '{prefab.name}' for '{itemDefinition.ItemDefId}' is missing HVRGrabbable."));
            }
        }

        private static bool HasHvrGrabbable(GameObject prefab)
        {
            if (prefab == null)
            {
                return false;
            }

            Type hvrType = Type.GetType(HvrGrabbableTypeName);
            if (hvrType != null && prefab.GetComponentInChildren(hvrType, true) != null)
            {
                return true;
            }

            Component[] components = prefab.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component != null && component.GetType().Name == "HVRGrabbable")
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static class ManifestableItemPrefabValidationMenu
    {
        [MenuItem("Tools/VRGame/Items/Validate Manifestable Prefabs")]
        public static void ValidateSelectedManifestablePrefabs()
        {
            UnityEngine.Object selected = Selection.activeObject;
            List<ManifestablePrefabValidationIssue> issues;
            if (selected is ItemDefinitionDatabase database)
            {
                issues = ManifestableItemPrefabValidator.Validate(database);
            }
            else if (selected is ItemDefinition itemDefinition)
            {
                issues = ManifestableItemPrefabValidator.Validate(itemDefinition);
            }
            else
            {
                Debug.LogWarning("Select an ItemDefinition or ItemDefinitionDatabase before running manifestable prefab validation.");
                return;
            }

            if (issues.Count == 0)
            {
                Debug.Log("Manifestable prefab validation passed with no issues.");
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                ManifestablePrefabValidationIssue issue = issues[i];
                string message = issue.ItemDefinition != null ? $"{issue.ItemDefinition.name}: {issue.Message}" : issue.Message;
                switch (issue.Severity)
                {
                    case ManifestablePrefabValidationSeverity.Error:
                        Debug.LogError(message, issue.ItemDefinition);
                        break;
                    case ManifestablePrefabValidationSeverity.Warning:
                        Debug.LogWarning(message, issue.ItemDefinition);
                        break;
                    default:
                        Debug.Log(message, issue.ItemDefinition);
                        break;
                }
            }
        }
    }
}
