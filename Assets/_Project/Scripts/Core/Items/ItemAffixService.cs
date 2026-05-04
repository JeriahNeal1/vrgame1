using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    public static class ItemAffixService
    {
        public static InventoryOperationResult CanApplyModifier(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            ItemInstanceId itemInstanceId,
            ModifierId modifierId)
        {
            return ValidateModifierRequest(
                InventoryOperationType.CanApplyModifier,
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                itemInstanceId,
                modifierId,
                out _,
                out _,
                out _);
        }

        public static InventoryOperationResult ApplyModifier(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            ItemInstanceId itemInstanceId,
            ModifierId modifierId,
            int rollSeed)
        {
            InventoryOperationResult validation = ValidateModifierRequest(
                InventoryOperationType.ApplyModifier,
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                itemInstanceId,
                modifierId,
                out ItemInstanceState itemInstance,
                out ItemDefinition itemDefinition,
                out ModifierDefinition modifierDefinition);

            if (!validation.Success)
            {
                return validation;
            }

            itemInstance.ApplyModifier(modifierDefinition.CreateRecord(ResolveSeed(rollSeed, inventoryState, itemInstanceId)), modifierDefinition.ExclusiveGroup, affixDefinitionDatabase);
            MarkAffixMutation(inventoryState);

            return InventoryOperationResult
                .Succeeded(InventoryOperationType.ApplyModifier, inventoryState.Revision, $"Applied modifier '{modifierDefinition.ModifierId}' to item instance '{itemInstanceId}'.")
                .WithChangedItemDefinition(itemDefinition.ItemDefId)
                .WithChangedItemInstance(itemInstanceId);
        }

        public static InventoryOperationResult RerollModifier(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            ItemInstanceId itemInstanceId,
            ReforgeContext reforgeContext,
            out ModifierId appliedModifierId)
        {
            appliedModifierId = default;

            InventoryOperationResult targetValidation = ValidateAffixTarget(
                InventoryOperationType.RerollModifier,
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                itemInstanceId,
                out ItemInstanceState itemInstance,
                out ItemDefinition itemDefinition);

            if (!targetValidation.Success)
            {
                return targetValidation;
            }

            List<ModifierDefinition> candidates = CollectModifierCandidates(affixDefinitionDatabase, itemDefinition, reforgeContext);
            RemoveInvalidModifierCandidates(candidates, itemDefinition);

            if (candidates.Count == 0)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.RerollModifier,
                    InventoryFailureReason.NoValidModifierCandidates,
                    $"No valid modifier candidates were found for item definition '{itemDefinition.ItemDefId}'.",
                    inventoryState.Revision);
            }

            int seed = ResolveSeed(reforgeContext != null ? reforgeContext.RandomSeed : 0, inventoryState, itemInstanceId);
            ModifierDefinition selectedModifier = PickWeightedModifier(candidates, seed, reforgeContext);
            if (selectedModifier == null)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.RerollModifier,
                    InventoryFailureReason.NoValidModifierCandidates,
                    $"No weighted modifier candidate could be selected for item definition '{itemDefinition.ItemDefId}'.",
                    inventoryState.Revision);
            }

            itemInstance.ApplyModifier(selectedModifier.CreateRecord(seed), selectedModifier.ExclusiveGroup, affixDefinitionDatabase);
            appliedModifierId = selectedModifier.ModifierId;
            MarkAffixMutation(inventoryState);

            return InventoryOperationResult
                .Succeeded(InventoryOperationType.RerollModifier, inventoryState.Revision, $"Rerolled modifier '{selectedModifier.ModifierId}' for item instance '{itemInstanceId}'.")
                .WithChangedItemDefinition(itemDefinition.ItemDefId)
                .WithChangedItemInstance(itemInstanceId);
        }

        public static InventoryOperationResult ClearModifier(PlayerInventoryState inventoryState, ItemInstanceId itemInstanceId)
        {
            return ClearModifier(inventoryState, itemInstanceId, default);
        }

        public static InventoryOperationResult ClearModifier(PlayerInventoryState inventoryState, ItemInstanceId itemInstanceId, ModifierId modifierId)
        {
            InventoryOperationResult validation = ValidateInstanceOnly(
                InventoryOperationType.ClearModifier,
                inventoryState,
                itemInstanceId,
                out ItemInstanceState itemInstance);

            if (!validation.Success)
            {
                return validation;
            }

            if (!itemInstance.ClearModifier(modifierId))
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.ClearModifier,
                    InventoryFailureReason.ModifierNotApplied,
                    modifierId.IsEmpty
                        ? $"Item instance '{itemInstanceId}' has no modifiers to clear."
                        : $"Item instance '{itemInstanceId}' does not have modifier '{modifierId}'.",
                    inventoryState.Revision);
            }

            MarkAffixMutation(inventoryState);

            return InventoryOperationResult
                .Succeeded(InventoryOperationType.ClearModifier, inventoryState.Revision, $"Cleared modifier data on item instance '{itemInstanceId}'.")
                .WithChangedItemDefinition(itemInstance.ItemDefId)
                .WithChangedItemInstance(itemInstanceId);
        }

        public static InventoryOperationResult CanApplyEnchantment(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            ItemInstanceId itemInstanceId,
            EnchantmentId enchantmentId)
        {
            return ValidateEnchantmentRequest(
                InventoryOperationType.CanApplyEnchantment,
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                itemInstanceId,
                enchantmentId,
                false,
                out _,
                out _,
                out _);
        }

        public static InventoryOperationResult ApplyEnchantment(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            ItemInstanceId itemInstanceId,
            EnchantmentId enchantmentId,
            int level,
            int rollSeed)
        {
            InventoryOperationResult validation = ValidateEnchantmentRequest(
                InventoryOperationType.ApplyEnchantment,
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                itemInstanceId,
                enchantmentId,
                false,
                out ItemInstanceState itemInstance,
                out ItemDefinition itemDefinition,
                out EnchantmentDefinition enchantmentDefinition);

            if (!validation.Success)
            {
                return validation;
            }

            itemInstance.ApplyEnchantment(enchantmentDefinition.CreateRecord(level, ResolveSeed(rollSeed, inventoryState, itemInstanceId)));
            MarkAffixMutation(inventoryState);

            return InventoryOperationResult
                .Succeeded(InventoryOperationType.ApplyEnchantment, inventoryState.Revision, $"Applied enchantment '{enchantmentDefinition.EnchantmentId}' to item instance '{itemInstanceId}'.")
                .WithChangedItemDefinition(itemDefinition.ItemDefId)
                .WithChangedItemInstance(itemInstanceId);
        }

        public static InventoryOperationResult UpgradeEnchantment(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            ItemInstanceId itemInstanceId,
            EnchantmentId enchantmentId,
            int levelsToAdd,
            int rollSeed)
        {
            InventoryOperationResult validation = ValidateEnchantmentRequest(
                InventoryOperationType.UpgradeEnchantment,
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                itemInstanceId,
                enchantmentId,
                false,
                out ItemInstanceState itemInstance,
                out ItemDefinition itemDefinition,
                out EnchantmentDefinition enchantmentDefinition);

            if (!validation.Success)
            {
                return validation;
            }

            if (!itemInstance.TryGetEnchantment(enchantmentId, out EnchantmentInstanceRecord existingRecord) || existingRecord == null)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.UpgradeEnchantment,
                    InventoryFailureReason.EnchantmentNotApplied,
                    $"Item instance '{itemInstanceId}' does not have enchantment '{enchantmentId}' to upgrade.",
                    inventoryState.Revision);
            }

            if (existingRecord.Level >= enchantmentDefinition.MaxLevel)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.UpgradeEnchantment,
                    InventoryFailureReason.EnchantmentAlreadyAtMaxLevel,
                    $"Enchantment '{enchantmentId}' is already at max level {enchantmentDefinition.MaxLevel}.",
                    inventoryState.Revision);
            }

            int newLevel = Mathf.Clamp(existingRecord.Level + Mathf.Max(1, levelsToAdd), 1, enchantmentDefinition.MaxLevel);
            itemInstance.ApplyEnchantment(enchantmentDefinition.CreateRecord(newLevel, ResolveSeed(rollSeed, inventoryState, itemInstanceId)));
            MarkAffixMutation(inventoryState);

            return InventoryOperationResult
                .Succeeded(InventoryOperationType.UpgradeEnchantment, inventoryState.Revision, $"Upgraded enchantment '{enchantmentDefinition.EnchantmentId}' to level {newLevel}.")
                .WithChangedItemDefinition(itemDefinition.ItemDefId)
                .WithChangedItemInstance(itemInstanceId);
        }

        public static InventoryOperationResult RemoveEnchantment(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            ItemInstanceId itemInstanceId,
            EnchantmentId enchantmentId)
        {
            InventoryOperationResult validation = ValidateEnchantmentRequest(
                InventoryOperationType.RemoveEnchantment,
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                itemInstanceId,
                enchantmentId,
                true,
                out ItemInstanceState itemInstance,
                out ItemDefinition itemDefinition,
                out EnchantmentDefinition enchantmentDefinition);

            if (!validation.Success)
            {
                return validation;
            }

            if (!enchantmentDefinition.CanBeRemoved)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.RemoveEnchantment,
                    InventoryFailureReason.EnchantmentRemovalNotAllowed,
                    $"Enchantment '{enchantmentId}' cannot be removed by generic operations.",
                    inventoryState.Revision);
            }

            if (!itemInstance.RemoveEnchantment(enchantmentId))
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.RemoveEnchantment,
                    InventoryFailureReason.EnchantmentNotApplied,
                    $"Item instance '{itemInstanceId}' does not have enchantment '{enchantmentId}'.",
                    inventoryState.Revision);
            }

            MarkAffixMutation(inventoryState);

            return InventoryOperationResult
                .Succeeded(InventoryOperationType.RemoveEnchantment, inventoryState.Revision, $"Removed enchantment '{enchantmentDefinition.EnchantmentId}' from item instance '{itemInstanceId}'.")
                .WithChangedItemDefinition(itemDefinition.ItemDefId)
                .WithChangedItemInstance(itemInstanceId);
        }

        public static InventoryOperationResult ApplyGemEnchantment(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            GemEnchantmentContext gemContext,
            out EnchantmentId appliedEnchantmentId)
        {
            appliedEnchantmentId = default;
            if (gemContext == null)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.ApplyGemEnchantment,
                    InventoryFailureReason.EnchantmentNotApplied,
                    "Gem enchantment context is null.");
            }

            InventoryOperationResult targetValidation = ValidateAffixTarget(
                InventoryOperationType.ApplyGemEnchantment,
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                gemContext.TargetItemInstanceId,
                out ItemInstanceState itemInstance,
                out ItemDefinition itemDefinition);

            if (!targetValidation.Success)
            {
                return targetValidation;
            }

            List<EnchantmentDefinition> candidates = CollectEnchantmentCandidates(affixDefinitionDatabase, itemDefinition, gemContext);
            RemoveInvalidEnchantmentCandidates(candidates, itemDefinition, itemInstance, affixDefinitionDatabase, gemContext.Behavior);

            if (candidates.Count == 0)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.ApplyGemEnchantment,
                    InventoryFailureReason.NoValidEnchantmentCandidates,
                    $"No valid enchantment candidates were found for gem '{gemContext.GemItemId}' and item '{itemDefinition.ItemDefId}'.",
                    inventoryState.Revision);
            }

            int seed = ResolveSeed(gemContext.RandomSeed, inventoryState, gemContext.TargetItemInstanceId);
            EnchantmentDefinition selected = PickEnchantment(candidates, seed);
            if (selected == null)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.ApplyGemEnchantment,
                    InventoryFailureReason.NoValidEnchantmentCandidates,
                    $"No enchantment candidate could be selected for item definition '{itemDefinition.ItemDefId}'.",
                    inventoryState.Revision);
            }

            appliedEnchantmentId = selected.EnchantmentId;

            if (itemInstance.TryGetEnchantment(selected.EnchantmentId, out EnchantmentInstanceRecord existing) &&
                existing != null &&
                (gemContext.Behavior == GemEnchantmentApplyBehavior.ApplyOrUpgrade || gemContext.Behavior == GemEnchantmentApplyBehavior.UpgradeOnly))
            {
                return UpgradeEnchantment(
                    inventoryState,
                    itemDefinitionDatabase,
                    affixDefinitionDatabase,
                    gemContext.TargetItemInstanceId,
                    selected.EnchantmentId,
                    1,
                    seed);
            }

            if (gemContext.Behavior == GemEnchantmentApplyBehavior.UpgradeOnly)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.ApplyGemEnchantment,
                    InventoryFailureReason.EnchantmentNotApplied,
                    $"No upgradeable enchantment from the gem pool exists on item instance '{gemContext.TargetItemInstanceId}'.",
                    inventoryState.Revision);
            }

            if (gemContext.Behavior == GemEnchantmentApplyBehavior.ReplaceExisting)
            {
                RemoveConflictingEnchantments(itemInstance, selected, affixDefinitionDatabase);
            }

            return ApplyEnchantment(
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                gemContext.TargetItemInstanceId,
                selected.EnchantmentId,
                1,
                seed);
        }

        private static InventoryOperationResult ValidateModifierRequest(
            InventoryOperationType operationType,
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            ItemInstanceId itemInstanceId,
            ModifierId modifierId,
            out ItemInstanceState itemInstance,
            out ItemDefinition itemDefinition,
            out ModifierDefinition modifierDefinition)
        {
            modifierDefinition = null;
            InventoryOperationResult targetValidation = ValidateAffixTarget(
                operationType,
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                itemInstanceId,
                out itemInstance,
                out itemDefinition);

            if (!targetValidation.Success)
            {
                return targetValidation;
            }

            if (modifierId.IsEmpty)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidModifierDefinitionId, "Modifier ID is empty.", inventoryState.Revision);
            }

            if (!affixDefinitionDatabase.TryGetModifier(modifierId, out modifierDefinition) || modifierDefinition == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.UnknownModifierDefinition, $"Unknown modifier definition '{modifierId}'.", inventoryState.Revision);
            }

            if (!modifierDefinition.CanApplyTo(itemDefinition))
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.ModifierNotAllowedForItem, $"Modifier '{modifierId}' cannot be applied to item definition '{itemDefinition.ItemDefId}'.", inventoryState.Revision);
            }

            return InventoryOperationResult
                .Succeeded(operationType, inventoryState.Revision, $"Modifier '{modifierId}' can apply to item instance '{itemInstanceId}'.")
                .WithChangedItemDefinition(itemDefinition.ItemDefId)
                .WithChangedItemInstance(itemInstanceId);
        }

        private static InventoryOperationResult ValidateEnchantmentRequest(
            InventoryOperationType operationType,
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            ItemInstanceId itemInstanceId,
            EnchantmentId enchantmentId,
            bool skipConflictCheck,
            out ItemInstanceState itemInstance,
            out ItemDefinition itemDefinition,
            out EnchantmentDefinition enchantmentDefinition)
        {
            enchantmentDefinition = null;
            InventoryOperationResult targetValidation = ValidateAffixTarget(
                operationType,
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                itemInstanceId,
                out itemInstance,
                out itemDefinition);

            if (!targetValidation.Success)
            {
                return targetValidation;
            }

            if (enchantmentId.IsEmpty)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidEnchantmentDefinitionId, "Enchantment ID is empty.", inventoryState.Revision);
            }

            if (!affixDefinitionDatabase.TryGetEnchantment(enchantmentId, out enchantmentDefinition) || enchantmentDefinition == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.UnknownEnchantmentDefinition, $"Unknown enchantment definition '{enchantmentId}'.", inventoryState.Revision);
            }

            if (!enchantmentDefinition.CanApplyTo(itemDefinition))
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.EnchantmentNotAllowedForItem, $"Enchantment '{enchantmentId}' cannot be applied to item definition '{itemDefinition.ItemDefId}'.", inventoryState.Revision);
            }

            if (!skipConflictCheck && HasEnchantmentConflict(itemInstance, enchantmentDefinition, affixDefinitionDatabase, out EnchantmentDefinition conflictingDefinition))
            {
                return InventoryOperationResult.Failed(
                    operationType,
                    InventoryFailureReason.EnchantmentConflict,
                    $"Enchantment '{enchantmentId}' conflicts with existing enchantment '{conflictingDefinition.EnchantmentId}'.",
                    inventoryState.Revision);
            }

            return InventoryOperationResult
                .Succeeded(operationType, inventoryState.Revision, $"Enchantment '{enchantmentId}' can apply to item instance '{itemInstanceId}'.")
                .WithChangedItemDefinition(itemDefinition.ItemDefId)
                .WithChangedItemInstance(itemInstanceId);
        }

        private static InventoryOperationResult ValidateAffixTarget(
            InventoryOperationType operationType,
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            ItemInstanceId itemInstanceId,
            out ItemInstanceState itemInstance,
            out ItemDefinition itemDefinition)
        {
            itemDefinition = null;
            InventoryOperationResult instanceValidation = ValidateInstanceOnly(operationType, inventoryState, itemInstanceId, out itemInstance);
            if (!instanceValidation.Success)
            {
                return instanceValidation;
            }

            if (itemDefinitionDatabase == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidItemDefinitionDatabase, "Item definition database is null.", inventoryState.Revision);
            }

            if (affixDefinitionDatabase == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidAffixDefinitionDatabase, "Item affix definition database is null.", inventoryState.Revision);
            }

            if (!itemDefinitionDatabase.TryGet(itemInstance.ItemDefId, out itemDefinition) || itemDefinition == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.UnknownItemDefinition, $"Unknown item definition '{itemInstance.ItemDefId}'.", inventoryState.Revision);
            }

            if (!itemDefinition.IsEquipment || itemDefinition.ResolvedStackPolicy.IsStackable)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.ItemMustBeEquipment, $"Only unstackable equipment can receive modifiers or enchantments. Item definition '{itemInstance.ItemDefId}' is not eligible.", inventoryState.Revision);
            }

            if (itemInstance.LifecycleState == ItemLifecycleState.Destroyed || itemInstance.LifecycleState == ItemLifecycleState.Consumed)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InstanceAlreadyDestroyed, $"Item instance '{itemInstanceId}' is in terminal state '{itemInstance.LifecycleState}'.", inventoryState.Revision);
            }

            return InventoryOperationResult.Succeeded(operationType, inventoryState.Revision);
        }

        private static InventoryOperationResult ValidateInstanceOnly(
            InventoryOperationType operationType,
            PlayerInventoryState inventoryState,
            ItemInstanceId itemInstanceId,
            out ItemInstanceState itemInstance)
        {
            itemInstance = null;
            if (inventoryState == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidInventoryState, "Player inventory state is null.");
            }

            if (itemInstanceId.IsEmpty)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidItemInstanceId, "Item instance ID is empty.", inventoryState.Revision);
            }

            if (!inventoryState.TryGetInstance(itemInstanceId, out itemInstance) || itemInstance == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.UnknownItemInstance, $"Unknown item instance '{itemInstanceId}'.", inventoryState.Revision);
            }

            return InventoryOperationResult.Succeeded(operationType, inventoryState.Revision);
        }

        private static List<ModifierDefinition> CollectModifierCandidates(ItemAffixDefinitionDatabase affixDefinitionDatabase, ItemDefinition itemDefinition, ReforgeContext reforgeContext)
        {
            List<ModifierDefinition> candidates = new List<ModifierDefinition>();
            if (affixDefinitionDatabase == null)
            {
                return candidates;
            }

            if (reforgeContext != null && reforgeContext.HasModifierPoolOverride)
            {
                affixDefinitionDatabase.AddModifierCandidatesFromReferences(reforgeContext.AllowedModifierPoolOverride, candidates);
                return candidates;
            }

            if (itemDefinition != null && CountValidReferences(itemDefinition.AllowedModifierPoolReferences) > 0)
            {
                affixDefinitionDatabase.AddModifierCandidatesFromReferences(itemDefinition.AllowedModifierPoolReferences, candidates);
                return candidates;
            }

            IReadOnlyList<ModifierDefinition> allModifiers = affixDefinitionDatabase.ModifierDefinitions;
            for (int i = 0; i < allModifiers.Count; i++)
            {
                ModifierDefinition modifier = allModifiers[i];
                if (modifier != null)
                {
                    candidates.Add(modifier);
                }
            }

            return candidates;
        }

        private static List<EnchantmentDefinition> CollectEnchantmentCandidates(ItemAffixDefinitionDatabase affixDefinitionDatabase, ItemDefinition itemDefinition, GemEnchantmentContext gemContext)
        {
            List<EnchantmentDefinition> candidates = new List<EnchantmentDefinition>();
            if (affixDefinitionDatabase == null)
            {
                return candidates;
            }

            if (gemContext != null && gemContext.HasEnchantmentPool)
            {
                affixDefinitionDatabase.AddEnchantmentCandidatesFromReferences(gemContext.EnchantmentPool, candidates);
                return candidates;
            }

            if (itemDefinition != null && CountValidReferences(itemDefinition.AllowedEnchantmentPoolReferences) > 0)
            {
                affixDefinitionDatabase.AddEnchantmentCandidatesFromReferences(itemDefinition.AllowedEnchantmentPoolReferences, candidates);
                return candidates;
            }

            IReadOnlyList<EnchantmentDefinition> allEnchantments = affixDefinitionDatabase.EnchantmentDefinitions;
            for (int i = 0; i < allEnchantments.Count; i++)
            {
                EnchantmentDefinition enchantment = allEnchantments[i];
                if (enchantment != null)
                {
                    candidates.Add(enchantment);
                }
            }

            return candidates;
        }

        private static void RemoveInvalidModifierCandidates(List<ModifierDefinition> candidates, ItemDefinition itemDefinition)
        {
            if (candidates == null)
            {
                return;
            }

            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                ModifierDefinition modifier = candidates[i];
                if (modifier == null || modifier.ModifierId.IsEmpty || modifier.Weight <= 0f || !modifier.CanApplyTo(itemDefinition))
                {
                    candidates.RemoveAt(i);
                }
            }
        }

        private static void RemoveInvalidEnchantmentCandidates(
            List<EnchantmentDefinition> candidates,
            ItemDefinition itemDefinition,
            ItemInstanceState itemInstance,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            GemEnchantmentApplyBehavior behavior)
        {
            if (candidates == null)
            {
                return;
            }

            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                EnchantmentDefinition enchantment = candidates[i];
                if (enchantment == null || enchantment.EnchantmentId.IsEmpty || !enchantment.CanApplyTo(itemDefinition))
                {
                    candidates.RemoveAt(i);
                    continue;
                }

                EnchantmentInstanceRecord existingRecord = null;
                bool hasExisting = itemInstance != null &&
                                   itemInstance.TryGetEnchantment(enchantment.EnchantmentId, out existingRecord) &&
                                   existingRecord != null;
                if (behavior == GemEnchantmentApplyBehavior.ApplyOnly && hasExisting)
                {
                    candidates.RemoveAt(i);
                    continue;
                }

                if (behavior == GemEnchantmentApplyBehavior.UpgradeOnly && !hasExisting)
                {
                    candidates.RemoveAt(i);
                    continue;
                }

                if (hasExisting && existingRecord.Level >= enchantment.MaxLevel)
                {
                    candidates.RemoveAt(i);
                    continue;
                }

                if (behavior != GemEnchantmentApplyBehavior.ReplaceExisting &&
                    itemInstance != null &&
                    HasEnchantmentConflict(itemInstance, enchantment, affixDefinitionDatabase, out _))
                {
                    candidates.RemoveAt(i);
                }
            }
        }

        private static ModifierDefinition PickWeightedModifier(List<ModifierDefinition> candidates, int seed, ReforgeContext reforgeContext)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            float qualityBonus = reforgeContext != null ? Mathf.Max(0f, reforgeContext.QualityBonus) : 0f;
            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                ModifierDefinition modifier = candidates[i];
                totalWeight += GetAdjustedModifierWeight(modifier, qualityBonus);
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            System.Random random = new System.Random(seed);
            float roll = (float)(random.NextDouble() * totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                ModifierDefinition modifier = candidates[i];
                cumulative += GetAdjustedModifierWeight(modifier, qualityBonus);
                if (roll <= cumulative)
                {
                    return modifier;
                }
            }

            return candidates[candidates.Count - 1];
        }

        private static EnchantmentDefinition PickEnchantment(List<EnchantmentDefinition> candidates, int seed)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            System.Random random = new System.Random(seed);
            return candidates[random.Next(0, candidates.Count)];
        }

        private static float GetAdjustedModifierWeight(ModifierDefinition modifier, float qualityBonus)
        {
            if (modifier == null)
            {
                return 0f;
            }

            return Mathf.Max(0f, modifier.Weight) * (1f + (qualityBonus * Mathf.Max(0, modifier.Rarity)));
        }

        private static bool HasEnchantmentConflict(
            ItemInstanceState itemInstance,
            EnchantmentDefinition enchantmentDefinition,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            out EnchantmentDefinition conflictingDefinition)
        {
            conflictingDefinition = null;
            if (itemInstance == null || enchantmentDefinition == null || affixDefinitionDatabase == null)
            {
                return false;
            }

            IReadOnlyList<EnchantmentInstanceRecord> enchantments = itemInstance.Enchantments;
            for (int i = 0; i < enchantments.Count; i++)
            {
                EnchantmentInstanceRecord record = enchantments[i];
                if (record == null || record.EnchantmentId == enchantmentDefinition.EnchantmentId)
                {
                    continue;
                }

                if (affixDefinitionDatabase.TryGetEnchantment(record.EnchantmentId, out EnchantmentDefinition existingDefinition) &&
                    existingDefinition != null &&
                    enchantmentDefinition.SharesConflictGroupWith(existingDefinition))
                {
                    conflictingDefinition = existingDefinition;
                    return true;
                }
            }

            return false;
        }

        private static void RemoveConflictingEnchantments(ItemInstanceState itemInstance, EnchantmentDefinition enchantmentDefinition, ItemAffixDefinitionDatabase affixDefinitionDatabase)
        {
            if (itemInstance == null || enchantmentDefinition == null || affixDefinitionDatabase == null)
            {
                return;
            }

            List<EnchantmentId> conflictingIds = new List<EnchantmentId>();
            IReadOnlyList<EnchantmentInstanceRecord> enchantments = itemInstance.Enchantments;
            for (int i = 0; i < enchantments.Count; i++)
            {
                EnchantmentInstanceRecord record = enchantments[i];
                if (record == null || record.EnchantmentId == enchantmentDefinition.EnchantmentId)
                {
                    continue;
                }

                if (affixDefinitionDatabase.TryGetEnchantment(record.EnchantmentId, out EnchantmentDefinition existingDefinition) &&
                    existingDefinition != null &&
                    enchantmentDefinition.SharesConflictGroupWith(existingDefinition))
                {
                    conflictingIds.Add(record.EnchantmentId);
                }
            }

            for (int i = 0; i < conflictingIds.Count; i++)
            {
                itemInstance.RemoveEnchantment(conflictingIds[i]);
            }
        }

        private static int ResolveSeed(int requestedSeed, PlayerInventoryState inventoryState, ItemInstanceId itemInstanceId)
        {
            if (requestedSeed != 0)
            {
                return requestedSeed;
            }

            unchecked
            {
                int revisionHash = inventoryState != null ? inventoryState.Revision.GetHashCode() : 0;
                return (revisionHash * 397) ^ itemInstanceId.GetHashCode();
            }
        }

        private static void MarkAffixMutation(PlayerInventoryState inventoryState)
        {
            if (inventoryState == null)
            {
                return;
            }

            inventoryState.EquipmentLoadout.MarkStatsDirty();
            inventoryState.IncrementRevision();
        }

        private static int CountValidReferences(IReadOnlyList<DefinitionIdReference> references)
        {
            if (references == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < references.Count; i++)
            {
                if (references[i].IsValid)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
