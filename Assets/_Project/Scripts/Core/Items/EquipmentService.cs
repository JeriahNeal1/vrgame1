using System.Collections.Generic;

namespace VRGame.Items
{
    public static class EquipmentService
    {
        public static InventoryOperationResult CanEquip(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            EquipmentLoadoutConfig loadoutConfig,
            ItemInstanceId itemInstanceId,
            string slotId)
        {
            return ValidateEquipRequest(
                InventoryOperationType.CanEquip,
                inventoryState,
                itemDefinitionDatabase,
                loadoutConfig,
                itemInstanceId,
                slotId,
                true,
                false,
                out _,
                out _,
                out _);
        }

        public static InventoryOperationResult Equip(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            EquipmentLoadoutConfig loadoutConfig,
            ItemInstanceId itemInstanceId,
            string slotId)
        {
            InventoryOperationResult validation = ValidateEquipRequest(
                InventoryOperationType.Equip,
                inventoryState,
                itemDefinitionDatabase,
                loadoutConfig,
                itemInstanceId,
                slotId,
                true,
                false,
                out ItemInstanceState itemInstance,
                out _,
                out EquipmentRuntimeSlot slot);

            if (!validation.Success)
            {
                return validation;
            }

            inventoryState.EquipmentLoadout.SetEquippedItem(slot.SlotId, itemInstanceId);
            itemInstance.SetLifecycleState(ItemLifecycleState.Equipped);
            inventoryState.IncrementRevision();

            return InventoryOperationResult
                .Succeeded(InventoryOperationType.Equip, inventoryState.Revision, $"Equipped item instance '{itemInstanceId}' to slot '{slot.SlotId}'.")
                .WithChangedItemDefinition(itemInstance.ItemDefId)
                .WithChangedItemInstance(itemInstanceId);
        }

        public static InventoryOperationResult Unequip(
            PlayerInventoryState inventoryState,
            EquipmentLoadoutConfig loadoutConfig,
            string slotId,
            out ItemInstanceId unequippedItemInstanceId)
        {
            unequippedItemInstanceId = default;

            InventoryOperationResult slotValidation = ValidateSlot(
                InventoryOperationType.Unequip,
                inventoryState,
                loadoutConfig,
                slotId,
                out EquipmentRuntimeSlot slot);

            if (!slotValidation.Success)
            {
                return slotValidation;
            }

            if (!inventoryState.EquipmentLoadout.TryGetEquippedItem(slot.SlotId, out ItemInstanceId equippedItemInstanceId))
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.Unequip,
                    InventoryFailureReason.EquipmentSlotEmpty,
                    $"Equipment slot '{slot.SlotId}' is empty.",
                    inventoryState.Revision);
            }

            inventoryState.EquipmentLoadout.ClearSlot(slot.SlotId, out unequippedItemInstanceId);
            if (inventoryState.TryGetInstance(equippedItemInstanceId, out ItemInstanceState itemInstance) && itemInstance != null)
            {
                itemInstance.SetLifecycleState(ItemLifecycleState.InInventory);
            }

            inventoryState.IncrementRevision();

            return InventoryOperationResult
                .Succeeded(InventoryOperationType.Unequip, inventoryState.Revision, $"Unequipped item instance '{equippedItemInstanceId}' from slot '{slot.SlotId}'.")
                .WithChangedItemInstance(equippedItemInstanceId);
        }

        public static InventoryOperationResult Swap(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            EquipmentLoadoutConfig loadoutConfig,
            string leftSlotId,
            string rightSlotId)
        {
            InventoryOperationResult leftSlotValidation = ValidateSlot(
                InventoryOperationType.SwapEquipment,
                inventoryState,
                loadoutConfig,
                leftSlotId,
                out EquipmentRuntimeSlot leftSlot);

            if (!leftSlotValidation.Success)
            {
                return leftSlotValidation;
            }

            InventoryOperationResult rightSlotValidation = ValidateSlot(
                InventoryOperationType.SwapEquipment,
                inventoryState,
                loadoutConfig,
                rightSlotId,
                out EquipmentRuntimeSlot rightSlot);

            if (!rightSlotValidation.Success)
            {
                return rightSlotValidation;
            }

            if (StableIdUtility.EqualsNormalized(leftSlot.SlotId, rightSlot.SlotId))
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.SwapEquipment,
                    InventoryFailureReason.InvalidEquipmentSwap,
                    "Cannot swap an equipment slot with itself.",
                    inventoryState.Revision);
            }

            bool hasLeft = inventoryState.EquipmentLoadout.TryGetEquippedItem(leftSlot.SlotId, out ItemInstanceId leftItemInstanceId);
            bool hasRight = inventoryState.EquipmentLoadout.TryGetEquippedItem(rightSlot.SlotId, out ItemInstanceId rightItemInstanceId);

            if (!hasLeft && !hasRight)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.SwapEquipment,
                    InventoryFailureReason.InvalidEquipmentSwap,
                    "Cannot swap two empty equipment slots.",
                    inventoryState.Revision);
            }

            if (hasLeft)
            {
                InventoryOperationResult leftFitsRight = ValidateEquipRequest(
                    InventoryOperationType.SwapEquipment,
                    inventoryState,
                    itemDefinitionDatabase,
                    loadoutConfig,
                    leftItemInstanceId,
                    rightSlot.SlotId,
                    false,
                    true,
                    out _,
                    out _,
                    out _);

                if (!leftFitsRight.Success)
                {
                    return leftFitsRight;
                }
            }

            if (hasRight)
            {
                InventoryOperationResult rightFitsLeft = ValidateEquipRequest(
                    InventoryOperationType.SwapEquipment,
                    inventoryState,
                    itemDefinitionDatabase,
                    loadoutConfig,
                    rightItemInstanceId,
                    leftSlot.SlotId,
                    false,
                    true,
                    out _,
                    out _,
                    out _);

                if (!rightFitsLeft.Success)
                {
                    return rightFitsLeft;
                }
            }

            if (hasRight)
            {
                inventoryState.EquipmentLoadout.SetEquippedItem(leftSlot.SlotId, rightItemInstanceId);
            }
            else
            {
                inventoryState.EquipmentLoadout.ClearSlot(leftSlot.SlotId, out _);
            }

            if (hasLeft)
            {
                inventoryState.EquipmentLoadout.SetEquippedItem(rightSlot.SlotId, leftItemInstanceId);
            }
            else
            {
                inventoryState.EquipmentLoadout.ClearSlot(rightSlot.SlotId, out _);
            }

            inventoryState.IncrementRevision();

            InventoryOperationResult result = InventoryOperationResult.Succeeded(
                InventoryOperationType.SwapEquipment,
                inventoryState.Revision,
                $"Swapped equipment slots '{leftSlot.SlotId}' and '{rightSlot.SlotId}'.");

            if (hasLeft)
            {
                result.WithChangedItemInstance(leftItemInstanceId);
            }

            if (hasRight)
            {
                result.WithChangedItemInstance(rightItemInstanceId);
            }

            return result;
        }

        public static List<ItemInstanceState> GetEquippedItems(PlayerInventoryState inventoryState)
        {
            List<ItemInstanceState> equippedItems = new List<ItemInstanceState>();
            if (inventoryState == null)
            {
                return equippedItems;
            }

            IReadOnlyList<EquipmentSlotAssignment> assignments = inventoryState.EquipmentLoadout.EquippedSlots;
            for (int i = 0; i < assignments.Count; i++)
            {
                EquipmentSlotAssignment assignment = assignments[i];
                if (assignment == null || assignment.ItemInstanceId.IsEmpty)
                {
                    continue;
                }

                if (inventoryState.TryGetInstance(assignment.ItemInstanceId, out ItemInstanceState itemInstance) && itemInstance != null)
                {
                    equippedItems.Add(itemInstance);
                }
            }

            return equippedItems;
        }

        private static InventoryOperationResult ValidateEquipRequest(
            InventoryOperationType operationType,
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            EquipmentLoadoutConfig loadoutConfig,
            ItemInstanceId itemInstanceId,
            string slotId,
            bool requireSlotEmpty,
            bool allowAlreadyEquippedInAnotherSlot,
            out ItemInstanceState itemInstance,
            out ItemDefinition itemDefinition,
            out EquipmentRuntimeSlot slot)
        {
            itemInstance = null;
            itemDefinition = null;

            InventoryOperationResult slotValidation = ValidateSlot(operationType, inventoryState, loadoutConfig, slotId, out slot);
            if (!slotValidation.Success)
            {
                return slotValidation;
            }

            if (itemDefinitionDatabase == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidItemDefinitionDatabase, "Item definition database is null.", inventoryState.Revision);
            }

            if (itemInstanceId.IsEmpty)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidItemInstanceId, "Item instance ID is empty.", inventoryState.Revision);
            }

            if (!inventoryState.TryGetInstance(itemInstanceId, out itemInstance) || itemInstance == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.UnknownItemInstance, $"Unknown item instance '{itemInstanceId}'.", inventoryState.Revision);
            }

            if (!itemDefinitionDatabase.TryGet(itemInstance.ItemDefId, out itemDefinition) || itemDefinition == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.UnknownItemDefinition, $"Unknown item definition '{itemInstance.ItemDefId}'.", inventoryState.Revision);
            }

            if (!itemDefinition.IsEquipment || itemDefinition.ResolvedStackPolicy.IsStackable)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.ItemMustBeEquipment, $"Item definition '{itemInstance.ItemDefId}' is not unstackable equipment.", inventoryState.Revision);
            }

            if (!itemDefinition.HasFlag(ItemFlags.CanBeEquipped) &&
                (!itemDefinition.HasEquipmentProfile || !itemDefinition.EquipmentProfile.CanEquipToLoadout))
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.ItemNotEquippable, $"Item definition '{itemInstance.ItemDefId}' is not marked as loadout-equippable.", inventoryState.Revision);
            }

            if (itemInstance.LifecycleState == ItemLifecycleState.Destroyed ||
                itemInstance.LifecycleState == ItemLifecycleState.Consumed ||
                itemInstance.LifecycleState == ItemLifecycleState.Placed ||
                itemInstance.LifecycleState == ItemLifecycleState.Socketed)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.ItemNotEquippable, $"Item instance '{itemInstanceId}' is in state '{itemInstance.LifecycleState}' and cannot be equipped.", inventoryState.Revision);
            }

            if (FindSlotContainingInstance(inventoryState.EquipmentLoadout, itemInstanceId, out string existingSlotId) &&
                !StableIdUtility.EqualsNormalized(existingSlotId, slot.SlotId) &&
                !allowAlreadyEquippedInAnotherSlot)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.ItemAlreadyEquipped, $"Item instance '{itemInstanceId}' is already equipped in slot '{existingSlotId}'.", inventoryState.Revision);
            }

            if (requireSlotEmpty && inventoryState.EquipmentLoadout.TryGetEquippedItem(slot.SlotId, out ItemInstanceId occupiedItemInstanceId) &&
                occupiedItemInstanceId != itemInstanceId)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.EquipmentSlotOccupied, $"Equipment slot '{slot.SlotId}' is occupied by '{occupiedItemInstanceId}'.", inventoryState.Revision);
            }

            if (!slot.Allows(itemDefinition) || !EquipmentProfileAllowsSlot(itemDefinition, slot))
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.ItemDoesNotMatchEquipmentSlot, $"Item definition '{itemInstance.ItemDefId}' cannot be equipped in slot '{slot.SlotId}'.", inventoryState.Revision);
            }

            return InventoryOperationResult
                .Succeeded(operationType, inventoryState.Revision, $"Item instance '{itemInstanceId}' can equip to slot '{slot.SlotId}'.")
                .WithChangedItemDefinition(itemInstance.ItemDefId)
                .WithChangedItemInstance(itemInstanceId);
        }

        private static InventoryOperationResult ValidateSlot(
            InventoryOperationType operationType,
            PlayerInventoryState inventoryState,
            EquipmentLoadoutConfig loadoutConfig,
            string slotId,
            out EquipmentRuntimeSlot slot)
        {
            slot = null;

            if (inventoryState == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidInventoryState, "Player inventory state is null.");
            }

            if (loadoutConfig == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidEquipmentLoadoutConfig, "Equipment loadout config is null.", inventoryState.Revision);
            }

            if (!StableIdUtility.IsValid(slotId))
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidEquipmentSlotId, "Equipment slot ID is empty.", inventoryState.Revision);
            }

            if (!loadoutConfig.TryGetSlot(slotId, out slot) || slot == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.UnknownEquipmentSlot, $"Unknown equipment slot '{slotId}'.", inventoryState.Revision);
            }

            return InventoryOperationResult.Succeeded(operationType, inventoryState.Revision);
        }

        private static bool EquipmentProfileAllowsSlot(ItemDefinition itemDefinition, EquipmentRuntimeSlot slot)
        {
            if (itemDefinition == null || slot == null || !itemDefinition.HasEquipmentProfile)
            {
                return true;
            }

            IReadOnlyList<EquipmentSlotReference> compatibleSlots = itemDefinition.EquipmentProfile.CompatibleSlots;
            if (compatibleSlots == null || compatibleSlots.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < compatibleSlots.Count; i++)
            {
                EquipmentSlotReference compatibleSlot = compatibleSlots[i];
                if (compatibleSlot == null || !compatibleSlot.IsValid)
                {
                    continue;
                }

                if (StableIdUtility.EqualsNormalized(compatibleSlot.SlotId, slot.SlotId))
                {
                    return true;
                }

                if (compatibleSlot.SlotKind == EquipmentSlotKind.Ring && slot.SlotKind == EquipmentSlotKind.Ring)
                {
                    int ringIndex = ParseGeneratedRingIndex(slot.SlotId);
                    if (ringIndex >= compatibleSlot.MinIndex && ringIndex <= compatibleSlot.MaxIndex)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool FindSlotContainingInstance(EquipmentLoadoutState loadoutState, ItemInstanceId itemInstanceId, out string slotId)
        {
            slotId = string.Empty;
            if (loadoutState == null || itemInstanceId.IsEmpty)
            {
                return false;
            }

            IReadOnlyList<EquipmentSlotAssignment> assignments = loadoutState.EquippedSlots;
            for (int i = 0; i < assignments.Count; i++)
            {
                EquipmentSlotAssignment assignment = assignments[i];
                if (assignment != null && assignment.ItemInstanceId == itemInstanceId)
                {
                    slotId = assignment.SlotId;
                    return true;
                }
            }

            return false;
        }

        private static int ParseGeneratedRingIndex(string slotId)
        {
            string normalizedSlotId = StableIdUtility.Normalize(slotId);
            int separator = normalizedSlotId.LastIndexOf('_');
            if (separator < 0 || separator == normalizedSlotId.Length - 1)
            {
                return -1;
            }

            string suffix = normalizedSlotId.Substring(separator + 1);
            return int.TryParse(suffix, out int oneBasedIndex) ? oneBasedIndex - 1 : -1;
        }
    }
}
