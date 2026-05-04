using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    public enum ItemLifecycleState
    {
        InInventory,
        ManifestingFromPortal,
        HeldInWorld,
        DroppedInWorld,
        Equipped,
        Socketed,
        Placed,
        Consumed,
        Destroyed
    }

    [Serializable]
    public sealed class InventoryStackRecord
    {
        [SerializeField]
        private ItemDefId itemDefId;

        [SerializeField]
        private StackQuantity quantity;

        public InventoryStackRecord(ItemDefId itemDefId, StackQuantity quantity)
        {
            this.itemDefId = itemDefId;
            this.quantity = quantity;
        }

        public ItemDefId ItemDefId
        {
            get { return itemDefId; }
        }

        public StackQuantity Quantity
        {
            get { return quantity; }
        }

        internal void SetQuantity(StackQuantity newQuantity)
        {
            quantity = newQuantity;
        }
    }

    [Serializable]
    public sealed class ItemInstanceState
    {
        [SerializeField]
        private ItemInstanceId itemInstanceId = default;

        [SerializeField]
        private ItemDefId itemDefId;

        [SerializeField]
        private ItemLifecycleState lifecycleState = ItemLifecycleState.InInventory;

        [SerializeField]
        private List<ModifierInstanceRecord> modifiers = new List<ModifierInstanceRecord>();

        [SerializeField]
        private List<EnchantmentInstanceRecord> enchantments = new List<EnchantmentInstanceRecord>();

        [SerializeField]
        private List<ItemInstanceVariableRecord> customVariables = new List<ItemInstanceVariableRecord>();

        public ItemInstanceState(ItemInstanceId itemInstanceId, ItemDefId itemDefId, ItemLifecycleState lifecycleState)
        {
            this.itemInstanceId = itemInstanceId;
            this.itemDefId = itemDefId;
            this.lifecycleState = lifecycleState;
        }

        public ItemInstanceId ItemInstanceId
        {
            get { return itemInstanceId; }
        }

        public ItemDefId ItemDefId
        {
            get { return itemDefId; }
        }

        public ItemLifecycleState LifecycleState
        {
            get { return lifecycleState; }
        }

        public IReadOnlyList<ModifierInstanceRecord> Modifiers
        {
            get { return modifiers ?? (IReadOnlyList<ModifierInstanceRecord>)Array.Empty<ModifierInstanceRecord>(); }
        }

        public IReadOnlyList<EnchantmentInstanceRecord> Enchantments
        {
            get { return enchantments ?? (IReadOnlyList<EnchantmentInstanceRecord>)Array.Empty<EnchantmentInstanceRecord>(); }
        }

        public IReadOnlyList<ItemInstanceVariableRecord> CustomVariables
        {
            get { return customVariables ?? (IReadOnlyList<ItemInstanceVariableRecord>)Array.Empty<ItemInstanceVariableRecord>(); }
        }

        internal void SetLifecycleState(ItemLifecycleState newState)
        {
            lifecycleState = newState;
        }

        internal void EnsureLists()
        {
            modifiers ??= new List<ModifierInstanceRecord>();
            enchantments ??= new List<EnchantmentInstanceRecord>();
            customVariables ??= new List<ItemInstanceVariableRecord>();
        }

        internal void ApplyModifier(ModifierInstanceRecord modifierRecord, string exclusiveGroup, ItemAffixDefinitionDatabase affixDatabase)
        {
            EnsureLists();
            if (modifierRecord == null || modifierRecord.ModifierId.IsEmpty)
            {
                return;
            }

            string normalizedGroup = StableIdUtility.Normalize(exclusiveGroup);
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                ModifierInstanceRecord existing = modifiers[i];
                if (existing == null)
                {
                    modifiers.RemoveAt(i);
                    continue;
                }

                if (existing.ModifierId == modifierRecord.ModifierId)
                {
                    modifiers.RemoveAt(i);
                    continue;
                }

                if (!string.IsNullOrEmpty(normalizedGroup) &&
                    affixDatabase != null &&
                    affixDatabase.TryGetModifier(existing.ModifierId, out ModifierDefinition existingDefinition) &&
                    existingDefinition != null &&
                    StableIdUtility.EqualsNormalized(existingDefinition.ExclusiveGroup, normalizedGroup))
                {
                    modifiers.RemoveAt(i);
                }
            }

            modifiers.Add(modifierRecord);
        }

        internal bool ClearModifier(ModifierId modifierId)
        {
            EnsureLists();
            bool changed = false;
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                ModifierInstanceRecord modifier = modifiers[i];
                if (modifier == null || modifierId.IsEmpty || modifier.ModifierId == modifierId)
                {
                    modifiers.RemoveAt(i);
                    changed = true;
                }
            }

            return changed;
        }

        public bool HasModifier(ModifierId modifierId)
        {
            EnsureLists();
            if (modifierId.IsEmpty)
            {
                return false;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                ModifierInstanceRecord modifier = modifiers[i];
                if (modifier != null && modifier.ModifierId == modifierId)
                {
                    return true;
                }
            }

            return false;
        }

        internal void ApplyEnchantment(EnchantmentInstanceRecord enchantmentRecord)
        {
            EnsureLists();
            if (enchantmentRecord == null || enchantmentRecord.EnchantmentId.IsEmpty)
            {
                return;
            }

            for (int i = 0; i < enchantments.Count; i++)
            {
                EnchantmentInstanceRecord existing = enchantments[i];
                if (existing != null && existing.EnchantmentId == enchantmentRecord.EnchantmentId)
                {
                    enchantments[i] = enchantmentRecord;
                    return;
                }
            }

            enchantments.Add(enchantmentRecord);
        }

        internal bool RemoveEnchantment(EnchantmentId enchantmentId)
        {
            EnsureLists();
            if (enchantmentId.IsEmpty)
            {
                return false;
            }

            for (int i = enchantments.Count - 1; i >= 0; i--)
            {
                EnchantmentInstanceRecord enchantment = enchantments[i];
                if (enchantment != null && enchantment.EnchantmentId == enchantmentId)
                {
                    enchantments.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        internal bool TryGetEnchantment(EnchantmentId enchantmentId, out EnchantmentInstanceRecord enchantmentRecord)
        {
            EnsureLists();
            if (enchantmentId.IsEmpty)
            {
                enchantmentRecord = null;
                return false;
            }

            for (int i = 0; i < enchantments.Count; i++)
            {
                EnchantmentInstanceRecord enchantment = enchantments[i];
                if (enchantment != null && enchantment.EnchantmentId == enchantmentId)
                {
                    enchantmentRecord = enchantment;
                    return true;
                }
            }

            enchantmentRecord = null;
            return false;
        }
    }

    [Serializable]
    public sealed class ModifierInstanceRecord
    {
        [SerializeField]
        private ModifierId modifierId = default;

        [SerializeField]
        private int rollSeed = 0;

        [SerializeField]
        private List<ItemInstanceVariableRecord> rolledValues = new List<ItemInstanceVariableRecord>();

        public ModifierInstanceRecord()
        {
        }

        public ModifierInstanceRecord(ModifierId modifierId, int rollSeed, IReadOnlyList<ItemInstanceVariableRecord> rolledValues = null)
        {
            this.modifierId = modifierId;
            this.rollSeed = rollSeed;
            this.rolledValues = rolledValues != null
                ? new List<ItemInstanceVariableRecord>(rolledValues)
                : new List<ItemInstanceVariableRecord>();
        }

        public ModifierId ModifierId
        {
            get { return modifierId; }
        }

        public string ModifierIdValue
        {
            get { return modifierId.Value; }
        }

        public int RollSeed
        {
            get { return rollSeed; }
        }

        public IReadOnlyList<ItemInstanceVariableRecord> RolledValues
        {
            get { return rolledValues ?? (IReadOnlyList<ItemInstanceVariableRecord>)Array.Empty<ItemInstanceVariableRecord>(); }
        }
    }

    [Serializable]
    public sealed class EnchantmentInstanceRecord
    {
        [SerializeField]
        private EnchantmentId enchantmentId = default;

        [Min(1)]
        [SerializeField]
        private int level = 1;

        [SerializeField]
        private int rollSeed = 0;

        [SerializeField]
        private List<ItemInstanceVariableRecord> rolledValues = new List<ItemInstanceVariableRecord>();

        public EnchantmentInstanceRecord()
        {
        }

        public EnchantmentInstanceRecord(EnchantmentId enchantmentId, int level, int rollSeed, IReadOnlyList<ItemInstanceVariableRecord> rolledValues = null)
        {
            this.enchantmentId = enchantmentId;
            this.level = Mathf.Max(1, level);
            this.rollSeed = rollSeed;
            this.rolledValues = rolledValues != null
                ? new List<ItemInstanceVariableRecord>(rolledValues)
                : new List<ItemInstanceVariableRecord>();
        }

        public EnchantmentId EnchantmentId
        {
            get { return enchantmentId; }
        }

        public string EnchantmentIdValue
        {
            get { return enchantmentId.Value; }
        }

        public int Level
        {
            get { return Mathf.Max(1, level); }
        }

        public int RollSeed
        {
            get { return rollSeed; }
        }

        public IReadOnlyList<ItemInstanceVariableRecord> RolledValues
        {
            get { return rolledValues ?? (IReadOnlyList<ItemInstanceVariableRecord>)Array.Empty<ItemInstanceVariableRecord>(); }
        }
    }

    [Serializable]
    public sealed class ItemInstanceVariableRecord
    {
        [SerializeField]
        private string key = string.Empty;

        [SerializeField]
        private string value = string.Empty;

        public ItemInstanceVariableRecord()
        {
        }

        public ItemInstanceVariableRecord(string key, string value)
        {
            this.key = StableIdUtility.Normalize(key);
            this.value = value ?? string.Empty;
        }

        public string Key
        {
            get { return StableIdUtility.Normalize(key); }
        }

        public string Value
        {
            get { return value ?? string.Empty; }
        }
    }

    [Serializable]
    public sealed class HeldWorldItemReference
    {
        [SerializeField]
        private string manifestationId = string.Empty;

        [SerializeField]
        private string handId = string.Empty;

        [SerializeField]
        private ItemDefId itemDefId = default;

        [SerializeField]
        private ItemInstanceId itemInstanceId = default;

        [SerializeField]
        private StackQuantity quantity = default;

        [SerializeField]
        private ItemLifecycleState lifecycleState = ItemLifecycleState.HeldInWorld;

        public string ManifestationId
        {
            get { return StableIdUtility.Normalize(manifestationId); }
        }

        public string HandId
        {
            get { return StableIdUtility.Normalize(handId); }
        }

        public ItemDefId ItemDefId
        {
            get { return itemDefId; }
        }

        public ItemInstanceId ItemInstanceId
        {
            get { return itemInstanceId; }
        }

        public StackQuantity Quantity
        {
            get { return quantity; }
        }

        public ItemLifecycleState LifecycleState
        {
            get { return lifecycleState; }
        }
    }
}
