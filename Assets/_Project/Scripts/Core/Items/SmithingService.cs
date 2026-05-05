using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    public static class SmithingService
    {
        public static ReforgeContext CreateManualSmithingContext(
            string stationId,
            int skillLevel,
            ItemDefId toolUsedId,
            IReadOnlyList<ItemDefId> consumedMaterialIds,
            int randomSeed,
            float qualityBonus,
            IReadOnlyList<DefinitionIdReference> allowedModifierPoolOverride)
        {
            return new ReforgeContext(
                ReforgeSourceType.ManualSmithing,
                skillLevel,
                stationId,
                toolUsedId,
                consumedMaterialIds,
                randomSeed,
                Mathf.Max(0f, qualityBonus),
                allowedModifierPoolOverride);
        }

        public static InventoryOperationResult ApplySmithingStrike(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            SmithingStrikeRecord strikeRecord,
            ReforgeContext reforgeContext,
            out ModifierId appliedModifierId)
        {
            appliedModifierId = default;
            if (strikeRecord == null)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.RerollModifier,
                    InventoryFailureReason.InvalidManifestationRequest,
                    "Smithing strike record is null.");
            }

            InventoryOperationResult hammerValidation = ValidateSmithingHammer(
                itemDefinitionDatabase,
                strikeRecord.HammerItemDefId,
                inventoryState != null ? inventoryState.Revision : 0);

            if (!hammerValidation.Success)
            {
                return hammerValidation;
            }

            return ApplySmithingReforge(
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                strikeRecord.TargetItemInstanceId,
                reforgeContext,
                out appliedModifierId);
        }

        public static InventoryOperationResult ApplySmithingReforge(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            ItemInstanceId targetItemInstanceId,
            ReforgeContext reforgeContext,
            out ModifierId appliedModifierId)
        {
            appliedModifierId = default;
            if (reforgeContext == null)
            {
                reforgeContext = new ReforgeContext(ReforgeSourceType.ManualSmithing, 0);
            }

            return ItemAffixService.RerollModifier(
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                targetItemInstanceId,
                reforgeContext,
                out appliedModifierId);
        }

        public static InventoryOperationResult ApplyGemProfileFromInventoryStack(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            GemEnchantmentProfileDefinition gemProfile,
            ItemInstanceId targetItemInstanceId,
            int randomSeed,
            out EnchantmentId appliedEnchantmentId)
        {
            appliedEnchantmentId = default;
            InventoryOperationResult validation = ValidateGemProfileRequest(
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                gemProfile,
                targetItemInstanceId);

            if (!validation.Success)
            {
                return validation;
            }

            if (!PlayerInventoryOperations.HasStack(inventoryState, gemProfile.GemItemDefId, gemProfile.ConsumedQuantity))
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.ApplyGemEnchantment,
                    InventoryFailureReason.InsufficientStack,
                    $"Inventory does not contain {gemProfile.ConsumedQuantity} of gem '{gemProfile.GemItemDefId}'.",
                    inventoryState.Revision);
            }

            GemEnchantmentContext context = gemProfile.CreateContext(targetItemInstanceId, randomSeed);
            InventoryOperationResult preflight = ItemAffixService.CanApplyGemEnchantment(
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                context);

            if (!preflight.Success)
            {
                return preflight;
            }

            InventoryOperationResult consumeResult = PlayerInventoryOperations.RemoveStack(
                inventoryState,
                itemDefinitionDatabase,
                gemProfile.GemItemDefId,
                gemProfile.ConsumedQuantity);

            if (!consumeResult.Success)
            {
                return consumeResult;
            }

            InventoryOperationResult applyResult = ItemAffixService.ApplyGemEnchantment(
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                context,
                out appliedEnchantmentId);

            if (!applyResult.Success)
            {
                InventoryOperationResult rollbackResult = PlayerInventoryOperations.AddStack(inventoryState, itemDefinitionDatabase, gemProfile.GemItemDefId, gemProfile.ConsumedQuantity);
                return InventoryOperationResult
                    .Failed(
                        InventoryOperationType.ApplyGemEnchantment,
                        applyResult.FailureReason,
                        rollbackResult.Success
                            ? applyResult.Message + " Gem stack consumption was rolled back."
                            : applyResult.Message + " Gem stack rollback also failed: " + rollbackResult.Message,
                        inventoryState.Revision)
                    .WithChangedItemDefinition(gemProfile.GemItemDefId)
                    .WithChangedItemInstance(targetItemInstanceId);
            }

            return applyResult.WithChangedItemDefinition(gemProfile.GemItemDefId);
        }

        public static InventoryOperationResult ApplyGemProfileFromWorldGem(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            GemEnchantmentProfileDefinition gemProfile,
            ItemInstanceId targetItemInstanceId,
            int randomSeed,
            float stationBonus,
            float skillBonus,
            out EnchantmentId appliedEnchantmentId)
        {
            appliedEnchantmentId = default;
            InventoryOperationResult validation = ValidateGemProfileRequest(
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                gemProfile,
                targetItemInstanceId);

            if (!validation.Success)
            {
                return validation;
            }

            GemEnchantmentContext context = gemProfile.CreateContext(targetItemInstanceId, randomSeed, stationBonus, skillBonus);
            InventoryOperationResult applyResult = ItemAffixService.ApplyGemEnchantment(
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                context,
                out appliedEnchantmentId);

            return applyResult.Success
                ? applyResult.WithChangedItemDefinition(gemProfile.GemItemDefId)
                : applyResult;
        }

        private static InventoryOperationResult ValidateGemProfileRequest(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            GemEnchantmentProfileDefinition gemProfile,
            ItemInstanceId targetItemInstanceId)
        {
            long revision = inventoryState != null ? inventoryState.Revision : 0;
            if (inventoryState == null)
            {
                return InventoryOperationResult.Failed(InventoryOperationType.ApplyGemEnchantment, InventoryFailureReason.InvalidInventoryState, "Player inventory state is null.");
            }

            if (itemDefinitionDatabase == null)
            {
                return InventoryOperationResult.Failed(InventoryOperationType.ApplyGemEnchantment, InventoryFailureReason.InvalidItemDefinitionDatabase, "Item definition database is null.", revision);
            }

            if (affixDefinitionDatabase == null)
            {
                return InventoryOperationResult.Failed(InventoryOperationType.ApplyGemEnchantment, InventoryFailureReason.InvalidAffixDefinitionDatabase, "Item affix definition database is null.", revision);
            }

            if (gemProfile == null || !gemProfile.IsValid)
            {
                return InventoryOperationResult.Failed(InventoryOperationType.ApplyGemEnchantment, InventoryFailureReason.InvalidItemDefinitionId, "Gem enchantment profile is missing or invalid.", revision);
            }

            if (targetItemInstanceId.IsEmpty)
            {
                return InventoryOperationResult.Failed(InventoryOperationType.ApplyGemEnchantment, InventoryFailureReason.InvalidItemInstanceId, "Target item instance ID is empty.", revision);
            }

            if (!itemDefinitionDatabase.TryGet(gemProfile.GemItemDefId, out ItemDefinition gemDefinition) || gemDefinition == null)
            {
                return InventoryOperationResult.Failed(InventoryOperationType.ApplyGemEnchantment, InventoryFailureReason.UnknownItemDefinition, $"Unknown gem item definition '{gemProfile.GemItemDefId}'.", revision);
            }

            if (!gemDefinition.ResolvedStackPolicy.IsStackable || gemDefinition.IsEquipment)
            {
                return InventoryOperationResult.Failed(InventoryOperationType.ApplyGemEnchantment, InventoryFailureReason.ItemMustBeStackable, $"Gem item definition '{gemProfile.GemItemDefId}' must be a stackable non-equipment item.", revision);
            }

            return InventoryOperationResult.Succeeded(InventoryOperationType.ApplyGemEnchantment, revision);
        }

        private static InventoryOperationResult ValidateSmithingHammer(ItemDefinitionDatabase itemDefinitionDatabase, ItemDefId hammerItemDefId, long revision)
        {
            if (hammerItemDefId.IsEmpty)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.RerollModifier,
                    InventoryFailureReason.InvalidItemDefinitionId,
                    "Smithing strike is missing a hammer item definition ID.",
                    revision);
            }

            if (itemDefinitionDatabase == null)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.RerollModifier,
                    InventoryFailureReason.InvalidItemDefinitionDatabase,
                    "Item definition database is null.",
                    revision);
            }

            if (!itemDefinitionDatabase.TryGet(hammerItemDefId, out ItemDefinition hammerDefinition) || hammerDefinition == null)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.RerollModifier,
                    InventoryFailureReason.UnknownItemDefinition,
                    $"Unknown smithing hammer item definition '{hammerItemDefId}'.",
                    revision);
            }

            if (!hammerDefinition.HasToolProfile ||
                hammerDefinition.ToolProfile == null ||
                hammerDefinition.ToolProfile.HarvestingType != HarvestingDomain.ConstructionArchitecture ||
                hammerDefinition.ToolProfile.ToolSubtype != HarvestingSubtype.Hammer)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.RerollModifier,
                    InventoryFailureReason.ItemDefinitionMismatch,
                    $"Item definition '{hammerItemDefId}' is not a smithing hammer tool.",
                    revision);
            }

            return InventoryOperationResult.Succeeded(InventoryOperationType.RerollModifier, revision);
        }
    }

    public sealed class ManualSmithingAffixResolver : IManualSmithingAffixResolver
    {
        public InventoryOperationResult ApplySmithingStrike(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            SmithingStrikeRecord strikeRecord,
            ReforgeContext reforgeContext,
            out ModifierId appliedModifierId)
        {
            return SmithingService.ApplySmithingStrike(
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                strikeRecord,
                reforgeContext,
                out appliedModifierId);
        }
    }
}
