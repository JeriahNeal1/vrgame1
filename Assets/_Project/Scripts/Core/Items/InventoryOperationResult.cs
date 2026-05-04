using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    // Inventory mutations return explicit results so local-only calls can later become server-authoritative
    // commands with transaction IDs, expected revisions, and reconciliation events without changing callers.
    public enum InventoryOperationType
    {
        AddStack,
        RemoveStack,
        HasStack,
        CreateItemInstance,
        DestroyItemInstance,
        MoveInstanceToState,
        CanApplyModifier,
        CanApplyEnchantment,
        ApplyModifier,
        RerollModifier,
        ClearModifier,
        ApplyEnchantment,
        UpgradeEnchantment,
        RemoveEnchantment,
        ApplyGemEnchantment,
        CanEquip,
        Equip,
        Unequip,
        SwapEquipment,
        GetEquippedItems,
        ManifestStack,
        ManifestItemInstance,
        ReturnManifestedItem,
        DropManifestedItem,
        CancelManifestation,
        Validate
    }

    public enum InventoryFailureReason
    {
        None,
        InvalidInventoryState,
        InvalidItemDefinitionDatabase,
        InvalidItemDefinitionId,
        UnknownItemDefinition,
        InvalidQuantity,
        StackOverflow,
        InsufficientStack,
        ItemMustBeStackable,
        ItemMustBeUnstackable,
        InvalidItemInstanceId,
        UnknownItemInstance,
        DuplicateItemInstanceId,
        InstanceAlreadyDestroyed,
        ItemMustBeEquipment,
        ItemDefinitionMismatch,
        InvalidEquipmentLoadoutConfig,
        InvalidEquipmentSlotId,
        UnknownEquipmentSlot,
        EquipmentSlotOccupied,
        EquipmentSlotEmpty,
        ItemNotEquippable,
        ItemDoesNotMatchEquipmentSlot,
        ItemAlreadyEquipped,
        InvalidEquipmentSwap,
        InvalidAffixDefinitionDatabase,
        InvalidModifierDefinitionId,
        UnknownModifierDefinition,
        InvalidEnchantmentDefinitionId,
        UnknownEnchantmentDefinition,
        ModifierNotAllowedForItem,
        EnchantmentNotAllowedForItem,
        ModifierConflict,
        EnchantmentConflict,
        ModifierNotApplied,
        EnchantmentNotApplied,
        EnchantmentAlreadyAtMaxLevel,
        EnchantmentRemovalNotAllowed,
        NoValidModifierCandidates,
        NoValidEnchantmentCandidates,
        ItemNotManifestable,
        MissingWorldPrefab,
        InvalidManifestationRequest,
        DuplicateManifestationRequest,
        ManifestationAlreadyActive,
        UnknownManifestation,
        ManifestationAlreadyResolved,
        SpawnFailed,
        InvalidWorldItem,
        ItemNotInInventory
    }

    [Serializable]
    public sealed class InventoryOperationResult
    {
        [SerializeField]
        private InventoryOperationType operationType;

        [SerializeField]
        private bool success;

        [SerializeField]
        private InventoryFailureReason failureReason;

        [SerializeField]
        private string message = string.Empty;

        [SerializeField]
        private long resultingRevision;

        [SerializeField]
        private List<string> changedItemDefinitionIds = new List<string>();

        [SerializeField]
        private List<string> changedItemInstanceIds = new List<string>();

        private InventoryOperationResult(InventoryOperationType operationType, bool success, InventoryFailureReason failureReason, string message, long resultingRevision)
        {
            this.operationType = operationType;
            this.success = success;
            this.failureReason = failureReason;
            this.message = message ?? string.Empty;
            this.resultingRevision = Math.Max(0, resultingRevision);
        }

        public InventoryOperationType OperationType
        {
            get { return operationType; }
        }

        public bool Success
        {
            get { return success; }
        }

        public InventoryFailureReason FailureReason
        {
            get { return failureReason; }
        }

        public string Message
        {
            get { return message ?? string.Empty; }
        }

        public long ResultingRevision
        {
            get { return Math.Max(0, resultingRevision); }
        }

        public IReadOnlyList<string> ChangedItemDefinitionIds
        {
            get { return changedItemDefinitionIds ?? (IReadOnlyList<string>)Array.Empty<string>(); }
        }

        public IReadOnlyList<string> ChangedItemInstanceIds
        {
            get { return changedItemInstanceIds ?? (IReadOnlyList<string>)Array.Empty<string>(); }
        }

        public static InventoryOperationResult Succeeded(InventoryOperationType operationType, long revision, string message = "")
        {
            return new InventoryOperationResult(operationType, true, InventoryFailureReason.None, message, revision);
        }

        public static InventoryOperationResult Failed(InventoryOperationType operationType, InventoryFailureReason failureReason, string message, long revision = 0)
        {
            return new InventoryOperationResult(operationType, false, failureReason, message, revision);
        }

        public InventoryOperationResult WithChangedItemDefinition(ItemDefId itemDefId)
        {
            if (!itemDefId.IsEmpty)
            {
                changedItemDefinitionIds ??= new List<string>();
                changedItemDefinitionIds.Add(itemDefId.Value);
            }

            return this;
        }

        public InventoryOperationResult WithChangedItemInstance(ItemInstanceId itemInstanceId)
        {
            if (!itemInstanceId.IsEmpty)
            {
                changedItemInstanceIds ??= new List<string>();
                changedItemInstanceIds.Add(itemInstanceId.Value);
            }

            return this;
        }
    }
}
