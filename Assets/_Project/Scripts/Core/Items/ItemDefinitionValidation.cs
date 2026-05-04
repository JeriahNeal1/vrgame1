using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    public enum ItemDefinitionValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class ItemDefinitionValidationIssue
    {
        public ItemDefinitionValidationIssue(ItemDefinitionValidationSeverity severity, ItemDefinition itemDefinition, string message)
        {
            Severity = severity;
            ItemDefinition = itemDefinition;
            Message = message;
        }

        public ItemDefinitionValidationSeverity Severity { get; }

        public ItemDefinition ItemDefinition { get; }

        public string Message { get; }
    }

    public static class ItemDefinitionValidator
    {
        public static List<ItemDefinitionValidationIssue> Validate(ItemDefinition itemDefinition)
        {
            List<ItemDefinitionValidationIssue> issues = new List<ItemDefinitionValidationIssue>();
            AppendItemIssues(itemDefinition, issues);
            return issues;
        }

        public static List<ItemDefinitionValidationIssue> ValidateDatabase(ItemDefinitionDatabase database)
        {
            List<ItemDefinitionValidationIssue> issues = new List<ItemDefinitionValidationIssue>();
            if (database == null)
            {
                issues.Add(new ItemDefinitionValidationIssue(ItemDefinitionValidationSeverity.Error, null, "Item definition database is null."));
                return issues;
            }

            IReadOnlyList<ItemDefinition> itemDefinitions = database.ItemDefinitions;
            Dictionary<string, ItemDefinition> seenIds = new Dictionary<string, ItemDefinition>(StableIdUtility.Comparer);

            for (int i = 0; i < itemDefinitions.Count; i++)
            {
                ItemDefinition itemDefinition = itemDefinitions[i];
                AppendItemIssues(itemDefinition, issues);

                if (itemDefinition == null || itemDefinition.ItemDefId.IsEmpty)
                {
                    continue;
                }

                string id = itemDefinition.ItemDefId.Value;
                if (seenIds.TryGetValue(id, out ItemDefinition existingDefinition))
                {
                    string existingName = existingDefinition != null ? existingDefinition.name : "unknown item";
                    issues.Add(new ItemDefinitionValidationIssue(
                        ItemDefinitionValidationSeverity.Error,
                        itemDefinition,
                        $"Duplicate item definition ID '{id}' also used by '{existingName}'."));
                }
                else
                {
                    seenIds.Add(id, itemDefinition);
                }
            }

            return issues;
        }

        public static bool HasErrors(IReadOnlyList<ItemDefinitionValidationIssue> issues)
        {
            if (issues == null)
            {
                return false;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == ItemDefinitionValidationSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AppendItemIssues(ItemDefinition itemDefinition, List<ItemDefinitionValidationIssue> issues)
        {
            if (itemDefinition == null)
            {
                issues.Add(new ItemDefinitionValidationIssue(ItemDefinitionValidationSeverity.Warning, null, "Null item definition reference."));
                return;
            }

            if (itemDefinition.ItemDefId.IsEmpty)
            {
                issues.Add(new ItemDefinitionValidationIssue(ItemDefinitionValidationSeverity.Warning, itemDefinition, "Missing stable item definition ID."));
            }

            if (itemDefinition.CategoryPath == null || !itemDefinition.CategoryPath.IsValid)
            {
                issues.Add(new ItemDefinitionValidationIssue(ItemDefinitionValidationSeverity.Warning, itemDefinition, "Missing or invalid category path."));
            }

            if (itemDefinition.IsEquipment && itemDefinition.ResolvedStackPolicy.IsStackable)
            {
                issues.Add(new ItemDefinitionValidationIssue(ItemDefinitionValidationSeverity.Warning, itemDefinition, "Equipment should be unstackable. Use DefaultByItemFlags or AlwaysUnstackable."));
            }

            if (!itemDefinition.IsEquipment && itemDefinition.HasModifierOrEnchantmentPools)
            {
                issues.Add(new ItemDefinitionValidationIssue(ItemDefinitionValidationSeverity.Warning, itemDefinition, "Only equipment can have modifier or enchantment pools."));
            }

            if (itemDefinition.IsManifestable && itemDefinition.WorldPrefab == null)
            {
                issues.Add(new ItemDefinitionValidationIssue(ItemDefinitionValidationSeverity.Warning, itemDefinition, "Manifestable items should reference a world prefab."));
            }

            if (itemDefinition.WorldPrefab != null && itemDefinition.GeneratedIcon == null)
            {
                issues.Add(new ItemDefinitionValidationIssue(ItemDefinitionValidationSeverity.Warning, itemDefinition, "Item has a world prefab but no generated inventory icon."));
            }

            if (itemDefinition.HasFlag(ItemFlags.CanBeEquipped) && !itemDefinition.HasEquipmentProfile)
            {
                issues.Add(new ItemDefinitionValidationIssue(ItemDefinitionValidationSeverity.Warning, itemDefinition, "Equippable items should enable an equipment profile with slot compatibility."));
            }
        }
    }
}
