using System;
using UnityEditor;
using UnityEngine;
using VRGame.Items;

namespace VRGame.Editor
{
    public static class SmithingGemSelfCheckMenu
    {
        private const string ItemDatabasePath = "Assets/_Project/Data/Quest3Demo/Quest3Demo_ItemDefinitionDatabase.asset";
        private const string AffixDatabasePath = "Assets/_Project/Data/Quest3Demo/Quest3Demo_ItemAffixDefinitionDatabase.asset";
        private const string RubyProfilePath = "Assets/_Project/Data/Quest3Demo/Quest3Demo_RubyGemEnchant.asset";
        private const string SapphireProfilePath = "Assets/_Project/Data/Quest3Demo/Quest3Demo_SapphireGemEnchant.asset";
        private const string SmithingModifierSetId = "demo.modifier_set.smithing_basic";

        private static readonly ItemDefId CopperSwordId = ItemDefId.FromString("demo.weapon.copper_sword");
        private static readonly ItemDefId SmithingHammerId = ItemDefId.FromString("demo.tool.smithing_hammer");
        private static readonly ItemDefId RubyGemId = ItemDefId.FromString("demo.resource.ruby_gem");
        private static readonly ItemDefId SapphireGemId = ItemDefId.FromString("demo.resource.sapphire_gem");
        private static readonly ItemInstanceId SwordInstanceId = ItemInstanceId.FromString("selfcheck.instance.copper_sword");
        private static readonly ItemInstanceId HammerInstanceId = ItemInstanceId.FromString("selfcheck.instance.smithing_hammer");

        [MenuItem("Tools/VRGame/Items/Run Smithing Gem Self Check")]
        public static void Run()
        {
            ItemDefinitionDatabase itemDatabase = AssetDatabase.LoadAssetAtPath<ItemDefinitionDatabase>(ItemDatabasePath);
            ItemAffixDefinitionDatabase affixDatabase = AssetDatabase.LoadAssetAtPath<ItemAffixDefinitionDatabase>(AffixDatabasePath);
            GemEnchantmentProfileDefinition rubyProfile = AssetDatabase.LoadAssetAtPath<GemEnchantmentProfileDefinition>(RubyProfilePath);
            GemEnchantmentProfileDefinition sapphireProfile = AssetDatabase.LoadAssetAtPath<GemEnchantmentProfileDefinition>(SapphireProfilePath);

            int failures = 0;
            failures += ExpectTrue("Generated item database exists", itemDatabase != null);
            failures += ExpectTrue("Generated affix database exists", affixDatabase != null);
            failures += ExpectTrue("Ruby gem profile exists", rubyProfile != null);
            failures += ExpectTrue("Sapphire gem profile exists", sapphireProfile != null);
            if (failures > 0)
            {
                Debug.LogWarning("Smithing/gem self-check requires generated Quest3Demo assets. Run Tools/VRGame/Scenes/Build Quest 3 Demo Scene first.");
                return;
            }

            itemDatabase.RebuildLookup();
            affixDatabase.RebuildLookup();
            PlayerInventoryState inventoryState = new PlayerInventoryState("smithing_self_check");

            failures += ExpectOperation("Seed Ruby gems", PlayerInventoryOperations.AddStack(inventoryState, itemDatabase, RubyGemId, StackQuantity.FromLong(2)));
            failures += ExpectOperation("Seed Sapphire gem", PlayerInventoryOperations.AddStack(inventoryState, itemDatabase, SapphireGemId, StackQuantity.One));
            failures += ExpectOperation("Create sword instance", PlayerInventoryOperations.CreateItemInstance(inventoryState, itemDatabase, CopperSwordId, SwordInstanceId, out _));
            failures += ExpectOperation("Create hammer instance", PlayerInventoryOperations.CreateItemInstance(inventoryState, itemDatabase, SmithingHammerId, HammerInstanceId, out _));

            ReforgeContext reforgeContext = SmithingService.CreateManualSmithingContext(
                "selfcheck.station.anvil",
                2,
                SmithingHammerId,
                Array.Empty<ItemDefId>(),
                3101,
                0.4f,
                new[] { new DefinitionIdReference(SmithingModifierSetId, "Self-check smithing pool") });
            SmithingStrikeRecord strikeRecord = new SmithingStrikeRecord(
                SwordInstanceId,
                HammerInstanceId,
                SmithingHammerId,
                "selfcheck.station.anvil",
                0.9f,
                1f,
                1f,
                3,
                3101);
            failures += ExpectOperation("Manual smithing strike rerolls modifier", SmithingService.ApplySmithingStrike(
                inventoryState,
                itemDatabase,
                affixDatabase,
                strikeRecord,
                reforgeContext,
                out _));

            failures += ExpectOperation("Ruby gem applies enchantment", SmithingService.ApplyGemProfileFromInventoryStack(
                inventoryState,
                itemDatabase,
                affixDatabase,
                rubyProfile,
                SwordInstanceId,
                3201,
                out _));
            failures += ExpectOperation("Second Ruby gem upgrades enchantment", SmithingService.ApplyGemProfileFromInventoryStack(
                inventoryState,
                itemDatabase,
                affixDatabase,
                rubyProfile,
                SwordInstanceId,
                3202,
                out _));

            InventoryOperationResult sapphireResult = SmithingService.ApplyGemProfileFromInventoryStack(
                inventoryState,
                itemDatabase,
                affixDatabase,
                sapphireProfile,
                SwordInstanceId,
                3203,
                out _);
            failures += ExpectTrue("Conflicting Sapphire gem is rejected", sapphireResult != null && !sapphireResult.Success);

            if (inventoryState.TryGetInstance(SwordInstanceId, out ItemInstanceState swordState) && swordState != null)
            {
                failures += ExpectTrue("Sword has no durability field assumption", swordState.CustomVariables.Count == 0);
            }

            Debug.Log(failures == 0
                ? "Smithing/gem self-check passed."
                : $"Smithing/gem self-check completed with {failures} issue(s).");
        }

        private static int ExpectOperation(string label, InventoryOperationResult result)
        {
            if (result != null && result.Success)
            {
                Debug.Log($"PASS: {label}");
                return 0;
            }

            Debug.LogWarning($"WARN: {label} - {(result == null ? "null result" : result.FailureReason + ": " + result.Message)}");
            return 1;
        }

        private static int ExpectTrue(string label, bool condition)
        {
            if (condition)
            {
                Debug.Log($"PASS: {label}");
                return 0;
            }

            Debug.LogWarning($"WARN: {label}");
            return 1;
        }
    }
}
