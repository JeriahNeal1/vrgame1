using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    public enum ItemAffixDefinitionValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class ItemAffixDefinitionValidationIssue
    {
        public ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity severity, Object source, string message)
        {
            Severity = severity;
            Source = source;
            Message = message ?? string.Empty;
        }

        public ItemAffixDefinitionValidationSeverity Severity { get; }

        public Object Source { get; }

        public string Message { get; }
    }

    public static class ItemAffixDefinitionValidator
    {
        public static List<ItemAffixDefinitionValidationIssue> Validate(ModifierDefinition modifierDefinition)
        {
            List<ItemAffixDefinitionValidationIssue> issues = new List<ItemAffixDefinitionValidationIssue>();
            AppendModifierIssues(modifierDefinition, issues);
            return issues;
        }

        public static List<ItemAffixDefinitionValidationIssue> Validate(EnchantmentDefinition enchantmentDefinition)
        {
            List<ItemAffixDefinitionValidationIssue> issues = new List<ItemAffixDefinitionValidationIssue>();
            AppendEnchantmentIssues(enchantmentDefinition, issues);
            return issues;
        }

        public static List<ItemAffixDefinitionValidationIssue> ValidateDatabase(ItemAffixDefinitionDatabase database)
        {
            List<ItemAffixDefinitionValidationIssue> issues = new List<ItemAffixDefinitionValidationIssue>();
            if (database == null)
            {
                issues.Add(new ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity.Error, null, "Item affix definition database is null."));
                return issues;
            }

            Dictionary<string, ModifierDefinition> seenModifiers = new Dictionary<string, ModifierDefinition>(StableIdUtility.Comparer);
            IReadOnlyList<ModifierDefinition> modifiers = database.ModifierDefinitions;
            for (int i = 0; i < modifiers.Count; i++)
            {
                ModifierDefinition modifier = modifiers[i];
                AppendModifierIssues(modifier, issues);
                if (modifier == null || modifier.ModifierId.IsEmpty)
                {
                    continue;
                }

                string id = modifier.ModifierId.Value;
                if (seenModifiers.TryGetValue(id, out ModifierDefinition existing))
                {
                    string existingName = existing != null ? existing.name : "unknown modifier";
                    issues.Add(new ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity.Error, modifier, $"Duplicate modifier ID '{id}' also used by '{existingName}'."));
                }
                else
                {
                    seenModifiers.Add(id, modifier);
                }
            }

            Dictionary<string, EnchantmentDefinition> seenEnchantments = new Dictionary<string, EnchantmentDefinition>(StableIdUtility.Comparer);
            IReadOnlyList<EnchantmentDefinition> enchantments = database.EnchantmentDefinitions;
            for (int i = 0; i < enchantments.Count; i++)
            {
                EnchantmentDefinition enchantment = enchantments[i];
                AppendEnchantmentIssues(enchantment, issues);
                if (enchantment == null || enchantment.EnchantmentId.IsEmpty)
                {
                    continue;
                }

                string id = enchantment.EnchantmentId.Value;
                if (seenEnchantments.TryGetValue(id, out EnchantmentDefinition existing))
                {
                    string existingName = existing != null ? existing.name : "unknown enchantment";
                    issues.Add(new ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity.Error, enchantment, $"Duplicate enchantment ID '{id}' also used by '{existingName}'."));
                }
                else
                {
                    seenEnchantments.Add(id, enchantment);
                }
            }

            AppendSetIssues(database, issues);
            return issues;
        }

        public static bool HasErrors(IReadOnlyList<ItemAffixDefinitionValidationIssue> issues)
        {
            if (issues == null)
            {
                return false;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == ItemAffixDefinitionValidationSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AppendModifierIssues(ModifierDefinition modifierDefinition, List<ItemAffixDefinitionValidationIssue> issues)
        {
            if (modifierDefinition == null)
            {
                issues.Add(new ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity.Warning, null, "Null modifier definition reference."));
                return;
            }

            if (modifierDefinition.ModifierId.IsEmpty)
            {
                issues.Add(new ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity.Warning, modifierDefinition, "Missing stable modifier ID."));
            }

            if (modifierDefinition.Weight <= 0f)
            {
                issues.Add(new ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity.Warning, modifierDefinition, "Modifier has zero roll weight and will not be selected by weighted rerolls."));
            }

            if (string.IsNullOrEmpty(modifierDefinition.ExclusiveGroup))
            {
                issues.Add(new ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity.Info, modifierDefinition, "Modifier has no exclusive group and can coexist with other modifiers."));
            }

            if (modifierDefinition.StatModifiers.Count == 0 && modifierDefinition.ActionPresetReferences.Count == 0)
            {
                issues.Add(new ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity.Info, modifierDefinition, "Modifier has no stat modifiers or action preset hooks yet."));
            }
        }

        private static void AppendEnchantmentIssues(EnchantmentDefinition enchantmentDefinition, List<ItemAffixDefinitionValidationIssue> issues)
        {
            if (enchantmentDefinition == null)
            {
                issues.Add(new ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity.Warning, null, "Null enchantment definition reference."));
                return;
            }

            if (enchantmentDefinition.EnchantmentId.IsEmpty)
            {
                issues.Add(new ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity.Warning, enchantmentDefinition, "Missing stable enchantment ID."));
            }

            if (enchantmentDefinition.MaxLevel < 1)
            {
                issues.Add(new ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity.Error, enchantmentDefinition, "Enchantment max level must be at least 1."));
            }

            if (enchantmentDefinition.StatEffectsPerLevel.Count == 0 && enchantmentDefinition.ActionPresetReferences.Count == 0)
            {
                issues.Add(new ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity.Info, enchantmentDefinition, "Enchantment has no stat effects or action preset hooks yet."));
            }
        }

        private static void AppendSetIssues(ItemAffixDefinitionDatabase database, List<ItemAffixDefinitionValidationIssue> issues)
        {
            Dictionary<string, ModifierSetDefinition> seenModifierSets = new Dictionary<string, ModifierSetDefinition>(StableIdUtility.Comparer);
            IReadOnlyList<ModifierSetDefinition> modifierSets = database.ModifierSets;
            for (int i = 0; i < modifierSets.Count; i++)
            {
                ModifierSetDefinition set = modifierSets[i];
                if (set == null)
                {
                    issues.Add(new ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity.Warning, database, "Null modifier set reference."));
                    continue;
                }

                if (!StableIdUtility.IsValid(set.ModifierSetId))
                {
                    issues.Add(new ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity.Warning, set, "Missing stable modifier set ID."));
                }
                else if (seenModifierSets.ContainsKey(set.ModifierSetId))
                {
                    issues.Add(new ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity.Error, set, $"Duplicate modifier set ID '{set.ModifierSetId}'."));
                }
                else
                {
                    seenModifierSets.Add(set.ModifierSetId, set);
                }
            }

            Dictionary<string, EnchantmentSetDefinition> seenEnchantmentSets = new Dictionary<string, EnchantmentSetDefinition>(StableIdUtility.Comparer);
            IReadOnlyList<EnchantmentSetDefinition> enchantmentSets = database.EnchantmentSets;
            for (int i = 0; i < enchantmentSets.Count; i++)
            {
                EnchantmentSetDefinition set = enchantmentSets[i];
                if (set == null)
                {
                    issues.Add(new ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity.Warning, database, "Null enchantment set reference."));
                    continue;
                }

                if (!StableIdUtility.IsValid(set.EnchantmentSetId))
                {
                    issues.Add(new ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity.Warning, set, "Missing stable enchantment set ID."));
                }
                else if (seenEnchantmentSets.ContainsKey(set.EnchantmentSetId))
                {
                    issues.Add(new ItemAffixDefinitionValidationIssue(ItemAffixDefinitionValidationSeverity.Error, set, $"Duplicate enchantment set ID '{set.EnchantmentSetId}'."));
                }
                else
                {
                    seenEnchantmentSets.Add(set.EnchantmentSetId, set);
                }
            }
        }
    }
}
