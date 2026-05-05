using System;
using System.Collections.Generic;
using UnityEngine;
using VRGame.Items;

namespace VRGame.Runtime
{
    [DisallowMultipleComponent]
    public sealed class Quest3DemoRuntimeBootstrap : MonoBehaviour
    {
        [Header("Definitions")]
        [SerializeField]
        private ItemDefinitionDatabase itemDefinitionDatabase = null;

        [SerializeField]
        private ItemAffixDefinitionDatabase affixDefinitionDatabase = null;

        [SerializeField]
        private EquipmentLoadoutConfig equipmentLoadoutConfig = null;

        [Header("Runtime Services")]
        [SerializeField]
        private DebugInventoryStateProvider inventoryStateProvider = null;

        [SerializeField]
        private ItemManifestationService manifestationService = null;

        [SerializeField]
        private ManifestationPortal manifestationPortal = null;

        [SerializeField]
        private VRInventoryUIController inventoryUiController = null;

        [SerializeField]
        private ItemPlacementService placementService = null;

        [SerializeField]
        private List<Harvestable> harvestables = new List<Harvestable>();

        [SerializeField]
        private bool bindSpawnedWorldItems = true;

        [Min(0.1f)]
        [SerializeField]
        private float worldItemBindIntervalSeconds = 0.5f;

        [Header("Seed Inventory")]
        [SerializeField]
        private bool seedOnAwake = true;

        [SerializeField]
        private bool equipSeedItems = true;

        [SerializeField]
        private List<Quest3DemoStackSeed> stackSeeds = new List<Quest3DemoStackSeed>();

        [SerializeField]
        private List<Quest3DemoInstanceSeed> instanceSeeds = new List<Quest3DemoInstanceSeed>();

        [Header("Debug")]
        [SerializeField]
        private bool logResults = true;

        private bool seeded;
        private float nextWorldItemBindTime;

        private void Awake()
        {
            BindRuntimeServices();

            if (seedOnAwake)
            {
                SeedOnce();
            }
        }

        private void Start()
        {
            BindRuntimeServices();
            inventoryUiController?.RefreshAll();
        }

        private void Update()
        {
            if (!bindSpawnedWorldItems || Time.time < nextWorldItemBindTime)
            {
                return;
            }

            nextWorldItemBindTime = Time.time + Mathf.Max(0.1f, worldItemBindIntervalSeconds);
            BindSpawnedWorldItems();
        }

        public void BindRuntimeServices()
        {
            IPlayerInventoryStateProvider provider = inventoryStateProvider;

            manifestationPortal?.BindRuntime(manifestationService, itemDefinitionDatabase, provider);
            inventoryUiController?.BindRuntime(provider, itemDefinitionDatabase, equipmentLoadoutConfig, manifestationService, manifestationPortal);
            placementService?.BindRuntime(itemDefinitionDatabase, provider);

            for (int i = 0; i < harvestables.Count; i++)
            {
                if (harvestables[i] != null)
                {
                    harvestables[i].BindRuntime(itemDefinitionDatabase, affixDefinitionDatabase, provider);
                }
            }

            BindRuntimeSmithingStations(provider);
        }

        public void SeedOnce()
        {
            if (seeded)
            {
                return;
            }

            seeded = true;
            if (inventoryStateProvider == null || itemDefinitionDatabase == null)
            {
                Log("Quest 3 demo bootstrap skipped inventory seeding because provider or item database is missing.");
                return;
            }

            PlayerInventoryState inventoryState = inventoryStateProvider.InventoryState;
            if (inventoryState == null)
            {
                Log("Quest 3 demo bootstrap skipped inventory seeding because inventory state is missing.");
                return;
            }

            for (int i = 0; i < stackSeeds.Count; i++)
            {
                Quest3DemoStackSeed seed = stackSeeds[i];
                if (seed == null || seed.ItemDefId.IsEmpty || !seed.Quantity.IsPositive)
                {
                    continue;
                }

                if (!PlayerInventoryOperations.HasStack(inventoryState, seed.ItemDefId, seed.Quantity))
                {
                    InventoryOperationResult result = PlayerInventoryOperations.AddStack(inventoryState, itemDefinitionDatabase, seed.ItemDefId, seed.Quantity);
                    LogResult($"Seed stack {seed.ItemDefId}", result);
                }
            }

            for (int i = 0; i < instanceSeeds.Count; i++)
            {
                Quest3DemoInstanceSeed seed = instanceSeeds[i];
                if (seed == null || seed.ItemDefId.IsEmpty || seed.ItemInstanceId.IsEmpty)
                {
                    continue;
                }

                if (!inventoryState.TryGetInstance(seed.ItemInstanceId, out _))
                {
                    InventoryOperationResult createResult = PlayerInventoryOperations.CreateItemInstance(
                        inventoryState,
                        itemDefinitionDatabase,
                        seed.ItemDefId,
                        seed.ItemInstanceId,
                        out _);
                    LogResult($"Create instance {seed.ItemInstanceId}", createResult);
                    if (!createResult.Success)
                    {
                        continue;
                    }
                }

                ApplySeedAffixes(inventoryState, seed);
                if (equipSeedItems && seed.EquipOnStart && !string.IsNullOrWhiteSpace(seed.EquipmentSlotId))
                {
                    InventoryOperationResult equipResult = EquipmentService.Equip(
                        inventoryState,
                        itemDefinitionDatabase,
                        equipmentLoadoutConfig,
                        seed.ItemInstanceId,
                        seed.EquipmentSlotId);
                    LogResult($"Equip {seed.ItemInstanceId} to {seed.EquipmentSlotId}", equipResult);
                }
            }

            inventoryUiController?.RefreshAll();
        }

        public void BindSpawnedWorldItems()
        {
            IPlayerInventoryStateProvider provider = inventoryStateProvider;
            WorldItemView[] worldItemViews = FindObjectsByType<WorldItemView>(FindObjectsInactive.Include);
            for (int i = 0; i < worldItemViews.Length; i++)
            {
                WorldItemView worldItemView = worldItemViews[i];
                if (worldItemView == null)
                {
                    continue;
                }

                MeleeDamageZoneComponent[] meleeZones = worldItemView.GetComponentsInChildren<MeleeDamageZoneComponent>(true);
                for (int zoneIndex = 0; zoneIndex < meleeZones.Length; zoneIndex++)
                {
                    meleeZones[zoneIndex]?.BindRuntime(worldItemView, itemDefinitionDatabase, affixDefinitionDatabase, provider);
                }

                HarvestToolHitZoneComponent[] harvestZones = worldItemView.GetComponentsInChildren<HarvestToolHitZoneComponent>(true);
                for (int zoneIndex = 0; zoneIndex < harvestZones.Length; zoneIndex++)
                {
                    harvestZones[zoneIndex]?.BindRuntime(worldItemView, itemDefinitionDatabase, affixDefinitionDatabase, provider);
                }
            }

            BindRuntimeSmithingStations(provider);
        }

        public void SelectDemoStack(string itemDefId)
        {
            inventoryUiController?.SelectStack(itemDefId);
        }

        public void ManifestSelectedRightHand()
        {
            inventoryUiController?.RequestManifestSelectedItem("right");
        }

        private void ApplySeedAffixes(PlayerInventoryState inventoryState, Quest3DemoInstanceSeed seed)
        {
            if (affixDefinitionDatabase == null)
            {
                return;
            }

            if (!seed.ModifierId.IsEmpty &&
                inventoryState.TryGetInstance(seed.ItemInstanceId, out ItemInstanceState modifierTarget) &&
                modifierTarget != null &&
                !modifierTarget.HasModifier(seed.ModifierId))
            {
                InventoryOperationResult modifierResult = ItemAffixService.ApplyModifier(
                    inventoryState,
                    itemDefinitionDatabase,
                    affixDefinitionDatabase,
                    seed.ItemInstanceId,
                    seed.ModifierId,
                    seed.RollSeed);
                LogResult($"Apply modifier {seed.ModifierId} to {seed.ItemInstanceId}", modifierResult);
            }

            if (!seed.EnchantmentId.IsEmpty &&
                inventoryState.TryGetInstance(seed.ItemInstanceId, out ItemInstanceState enchantmentTarget) &&
                enchantmentTarget != null &&
                !HasEnchantment(enchantmentTarget, seed.EnchantmentId))
            {
                InventoryOperationResult enchantmentResult = ItemAffixService.ApplyEnchantment(
                    inventoryState,
                    itemDefinitionDatabase,
                    affixDefinitionDatabase,
                    seed.ItemInstanceId,
                    seed.EnchantmentId,
                    seed.EnchantmentLevel,
                    seed.RollSeed);
                LogResult($"Apply enchantment {seed.EnchantmentId} to {seed.ItemInstanceId}", enchantmentResult);
            }
        }

        private void BindRuntimeSmithingStations(IPlayerInventoryStateProvider provider)
        {
            SmithingStation[] smithingStations = FindObjectsByType<SmithingStation>(FindObjectsInactive.Include);
            for (int i = 0; i < smithingStations.Length; i++)
            {
                smithingStations[i]?.BindRuntime(itemDefinitionDatabase, affixDefinitionDatabase, provider);
            }
        }

        private static bool HasEnchantment(ItemInstanceState itemInstance, EnchantmentId enchantmentId)
        {
            if (itemInstance == null || enchantmentId.IsEmpty)
            {
                return false;
            }

            IReadOnlyList<EnchantmentInstanceRecord> enchantments = itemInstance.Enchantments;
            for (int i = 0; i < enchantments.Count; i++)
            {
                if (enchantments[i] != null && enchantments[i].EnchantmentId == enchantmentId)
                {
                    return true;
                }
            }

            return false;
        }

        private void LogResult(string label, InventoryOperationResult result)
        {
            if (!logResults || result == null)
            {
                return;
            }

            if (result.Success)
            {
                Debug.Log($"Quest3Demo PASS: {label} - {result.Message}", this);
            }
            else
            {
                Debug.LogWarning($"Quest3Demo WARN: {label} - {result.FailureReason}: {result.Message}", this);
            }
        }

        private void Log(string message)
        {
            if (logResults)
            {
                Debug.Log(message, this);
            }
        }
    }

    [Serializable]
    public sealed class Quest3DemoStackSeed
    {
        [SerializeField]
        private ItemDefId itemDefId = default;

        [SerializeField]
        private StackQuantity quantity = StackQuantity.One;

        public Quest3DemoStackSeed()
        {
        }

        public Quest3DemoStackSeed(ItemDefId itemDefId, StackQuantity quantity)
        {
            this.itemDefId = itemDefId;
            this.quantity = quantity.IsPositive ? quantity : StackQuantity.One;
        }

        public ItemDefId ItemDefId
        {
            get { return itemDefId; }
        }

        public StackQuantity Quantity
        {
            get { return quantity.IsPositive ? quantity : StackQuantity.One; }
        }
    }

    [Serializable]
    public sealed class Quest3DemoInstanceSeed
    {
        [SerializeField]
        private ItemDefId itemDefId = default;

        [SerializeField]
        private ItemInstanceId itemInstanceId = default;

        [SerializeField]
        private ModifierId modifierId = default;

        [SerializeField]
        private EnchantmentId enchantmentId = default;

        [Min(1)]
        [SerializeField]
        private int enchantmentLevel = 1;

        [SerializeField]
        private int rollSeed = 0;

        [SerializeField]
        private bool equipOnStart = false;

        [SerializeField]
        private string equipmentSlotId = string.Empty;

        public Quest3DemoInstanceSeed()
        {
        }

        public Quest3DemoInstanceSeed(
            ItemDefId itemDefId,
            ItemInstanceId itemInstanceId,
            ModifierId modifierId,
            EnchantmentId enchantmentId,
            int enchantmentLevel,
            int rollSeed,
            bool equipOnStart,
            string equipmentSlotId)
        {
            this.itemDefId = itemDefId;
            this.itemInstanceId = itemInstanceId;
            this.modifierId = modifierId;
            this.enchantmentId = enchantmentId;
            this.enchantmentLevel = Mathf.Max(1, enchantmentLevel);
            this.rollSeed = rollSeed;
            this.equipOnStart = equipOnStart;
            this.equipmentSlotId = StableIdUtility.Normalize(equipmentSlotId);
        }

        public ItemDefId ItemDefId
        {
            get { return itemDefId; }
        }

        public ItemInstanceId ItemInstanceId
        {
            get { return itemInstanceId; }
        }

        public ModifierId ModifierId
        {
            get { return modifierId; }
        }

        public EnchantmentId EnchantmentId
        {
            get { return enchantmentId; }
        }

        public int EnchantmentLevel
        {
            get { return Mathf.Max(1, enchantmentLevel); }
        }

        public int RollSeed
        {
            get { return rollSeed; }
        }

        public bool EquipOnStart
        {
            get { return equipOnStart; }
        }

        public string EquipmentSlotId
        {
            get { return StableIdUtility.Normalize(equipmentSlotId); }
        }
    }
}
