using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    public enum HarvestValidationFailureReason
    {
        None,
        InvalidHarvestableProfile,
        MissingToolDefinition,
        InvalidHeldToolState,
        ItemIsNotTool,
        MissingToolProfile,
        HarvestingTypeMismatch,
        ToolSubtypeMismatch,
        MaterialTagMismatch,
        CategoryTagMismatch,
        ToolTierTooLow,
        ToolHardnessTooLow
    }

    public readonly struct HarvestToolValidationInput
    {
        public HarvestToolValidationInput(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            ItemDefinition heldToolDefinition,
            ItemInstanceState heldToolInstance,
            HarvestableProfile harvestableProfile)
        {
            InventoryState = inventoryState;
            ItemDefinitionDatabase = itemDefinitionDatabase;
            AffixDefinitionDatabase = affixDefinitionDatabase;
            HeldToolDefinition = heldToolDefinition;
            HeldToolInstance = heldToolInstance;
            HarvestableProfile = harvestableProfile;
        }

        public PlayerInventoryState InventoryState { get; }

        public ItemDefinitionDatabase ItemDefinitionDatabase { get; }

        public ItemAffixDefinitionDatabase AffixDefinitionDatabase { get; }

        public ItemDefinition HeldToolDefinition { get; }

        public ItemInstanceState HeldToolInstance { get; }

        public HarvestableProfile HarvestableProfile { get; }
    }

    public sealed class HarvestToolStats
    {
        public HarvestingDomain HarvestingType { get; set; }

        public HarvestingSubtype ToolSubtype { get; set; }

        public float BaseHardness { get; set; }

        public float EffectiveHardness { get; set; }

        public int ToolTier { get; set; }

        public float HarvestSpeed { get; set; }
    }

    public sealed class HarvestToolValidationResult
    {
        public HarvestToolValidationResult(
            bool success,
            HarvestValidationFailureReason failureReason,
            string message,
            HarvestToolStats toolStats)
        {
            Success = success;
            FailureReason = failureReason;
            Message = message ?? string.Empty;
            ToolStats = toolStats;
        }

        public bool Success { get; }

        public HarvestValidationFailureReason FailureReason { get; }

        public string Message { get; }

        public HarvestToolStats ToolStats { get; }

        public static HarvestToolValidationResult Succeeded(HarvestToolStats toolStats, string message = "")
        {
            return new HarvestToolValidationResult(true, HarvestValidationFailureReason.None, message, toolStats);
        }

        public static HarvestToolValidationResult Failed(HarvestValidationFailureReason reason, string message, HarvestToolStats toolStats = null)
        {
            return new HarvestToolValidationResult(false, reason, message, toolStats);
        }
    }

    public static class HarvestingToolValidationService
    {
        public static HarvestToolValidationResult ValidateToolForHarvest(HarvestToolValidationInput input)
        {
            HarvestableProfile harvestableProfile = input.HarvestableProfile;
            if (harvestableProfile == null)
            {
                return HarvestToolValidationResult.Failed(HarvestValidationFailureReason.InvalidHarvestableProfile, "Harvestable profile is missing.");
            }

            ItemDefinition toolDefinition = input.HeldToolDefinition;
            if (toolDefinition == null)
            {
                return HarvestToolValidationResult.Failed(HarvestValidationFailureReason.MissingToolDefinition, "Held item definition is missing.");
            }

            if (harvestableProfile.RequiresCorrectToolFlag && !toolDefinition.HasFlag(ItemFlags.Tool))
            {
                return HarvestToolValidationResult.Failed(HarvestValidationFailureReason.ItemIsNotTool, $"Item definition '{toolDefinition.ItemDefId}' is not flagged as a tool.");
            }

            ToolProfile toolProfile = toolDefinition.ToolProfile;
            if (toolProfile == null)
            {
                return HarvestToolValidationResult.Failed(HarvestValidationFailureReason.MissingToolProfile, $"Item definition '{toolDefinition.ItemDefId}' has no tool profile.");
            }

            HarvestToolStats toolStats = CalculateEffectiveToolStats(input, toolProfile);

            if (harvestableProfile.RequiredHarvestingType != HarvestingDomain.None &&
                toolProfile.HarvestingType != harvestableProfile.RequiredHarvestingType)
            {
                return HarvestToolValidationResult.Failed(
                    HarvestValidationFailureReason.HarvestingTypeMismatch,
                    $"Tool harvesting type '{toolProfile.HarvestingType}' cannot harvest target requiring '{harvestableProfile.RequiredHarvestingType}'.",
                    toolStats);
            }

            if (harvestableProfile.RequiredToolSubtype != HarvestingSubtype.None &&
                toolProfile.ToolSubtype != harvestableProfile.RequiredToolSubtype)
            {
                return HarvestToolValidationResult.Failed(
                    HarvestValidationFailureReason.ToolSubtypeMismatch,
                    $"Tool subtype '{toolProfile.ToolSubtype}' cannot harvest target requiring '{harvestableProfile.RequiredToolSubtype}'.",
                    toolStats);
            }

            if (!TagsAllow(toolProfile.EffectiveMaterialTags, harvestableProfile.MaterialTags))
            {
                return HarvestToolValidationResult.Failed(
                    HarvestValidationFailureReason.MaterialTagMismatch,
                    "Tool material tags do not match this harvestable target.",
                    toolStats);
            }

            if (!TagsAllow(toolProfile.EffectiveCategoryTags, harvestableProfile.CategoryTags))
            {
                return HarvestToolValidationResult.Failed(
                    HarvestValidationFailureReason.CategoryTagMismatch,
                    "Tool category tags do not match this harvestable target.",
                    toolStats);
            }

            if (toolStats.ToolTier < harvestableProfile.RequiredTier)
            {
                return HarvestToolValidationResult.Failed(
                    HarvestValidationFailureReason.ToolTierTooLow,
                    $"Tool tier {toolStats.ToolTier} is below required tier {harvestableProfile.RequiredTier}.",
                    toolStats);
            }

            if (toolStats.EffectiveHardness < harvestableProfile.RequiredMaterialHardnessScore)
            {
                return HarvestToolValidationResult.Failed(
                    HarvestValidationFailureReason.ToolHardnessTooLow,
                    $"Tool hardness {toolStats.EffectiveHardness:0.##} is below required hardness {harvestableProfile.RequiredMaterialHardnessScore:0.##}.",
                    toolStats);
            }

            return HarvestToolValidationResult.Succeeded(toolStats, $"Tool '{toolDefinition.ItemDefId}' can harvest target.");
        }

        public static HarvestToolStats CalculateEffectiveToolStats(HarvestToolValidationInput input, ToolProfile toolProfile)
        {
            if (toolProfile == null)
            {
                return new HarvestToolStats();
            }

            StatId powerStat = GetDomainPowerStat(toolProfile.HarvestingType);
            StatId speedStat = GetDomainSpeedStat(toolProfile.HarvestingType);

            StatBlock toolBaseStats = new StatBlock();
            toolBaseStats.SetValue(StatIds.ToolHardness, toolProfile.BaseMaterialHardnessScore);
            if (!powerStat.IsEmpty)
            {
                toolBaseStats.SetValue(powerStat, 0f);
            }

            if (!speedStat.IsEmpty)
            {
                toolBaseStats.SetValue(speedStat, toolProfile.HarvestSpeed);
            }

            StatBlock heldToolStats = new StatBlock();
            StatAggregator.RecalculateItemInstanceStats(
                toolBaseStats,
                input.HeldToolInstance,
                input.HeldToolDefinition,
                input.AffixDefinitionDatabase,
                heldToolStats);

            StatBlock equipmentStats = new StatBlock();
            if (input.InventoryState != null && input.ItemDefinitionDatabase != null)
            {
                StatAggregator.RecalculateEquipmentStats(
                    null,
                    input.InventoryState,
                    input.ItemDefinitionDatabase,
                    input.AffixDefinitionDatabase,
                    equipmentStats,
                    null,
                    false);
            }

            float effectiveHardness = heldToolStats.GetValue(StatIds.ToolHardness, toolProfile.BaseMaterialHardnessScore) +
                                      equipmentStats.GetValue(StatIds.ToolHardness, 0f);

            if (!powerStat.IsEmpty)
            {
                effectiveHardness += heldToolStats.GetValue(powerStat, 0f) + equipmentStats.GetValue(powerStat, 0f);
            }

            float harvestSpeed = !speedStat.IsEmpty
                ? heldToolStats.GetValue(speedStat, toolProfile.HarvestSpeed) + equipmentStats.GetValue(speedStat, 0f)
                : toolProfile.HarvestSpeed;

            return new HarvestToolStats
            {
                HarvestingType = toolProfile.HarvestingType,
                ToolSubtype = toolProfile.ToolSubtype,
                BaseHardness = toolProfile.BaseMaterialHardnessScore,
                EffectiveHardness = Mathf.Max(0f, effectiveHardness),
                ToolTier = toolProfile.ToolTier,
                HarvestSpeed = Mathf.Max(0.01f, harvestSpeed)
            };
        }

        public static StatId GetDomainPowerStat(HarvestingDomain harvestingDomain)
        {
            switch (harvestingDomain)
            {
                case HarvestingDomain.Mining:
                    return StatIds.MiningPower;
                case HarvestingDomain.Lumber:
                    return StatIds.LumberPower;
                case HarvestingDomain.ConstructionArchitecture:
                    return StatIds.ConstructionPower;
                case HarvestingDomain.Fishing:
                    return StatIds.FishingPower;
                default:
                    return default;
            }
        }

        public static StatId GetDomainSpeedStat(HarvestingDomain harvestingDomain)
        {
            switch (harvestingDomain)
            {
                case HarvestingDomain.Mining:
                    return StatIds.MiningSpeed;
                case HarvestingDomain.Lumber:
                    return StatIds.LumberSpeed;
                default:
                    return default;
            }
        }

        private static bool TagsAllow(IReadOnlyList<string> toolTags, IReadOnlyList<string> targetTags)
        {
            if (targetTags == null || targetTags.Count == 0 || toolTags == null || toolTags.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < targetTags.Count; i++)
            {
                string targetTag = StableIdUtility.Normalize(targetTags[i]);
                if (string.IsNullOrEmpty(targetTag))
                {
                    continue;
                }

                for (int j = 0; j < toolTags.Count; j++)
                {
                    if (StableIdUtility.EqualsNormalized(toolTags[j], targetTag))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
