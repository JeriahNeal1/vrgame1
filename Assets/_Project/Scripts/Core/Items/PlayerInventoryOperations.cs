namespace VRGame.Items
{
    public static class PlayerInventoryOperations
    {
        public static InventoryOperationResult AddStack(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemDefId itemDefId,
            StackQuantity quantity)
        {
            InventoryOperationResult validation = ValidateStackOperation(
                InventoryOperationType.AddStack,
                inventoryState,
                itemDefinitionDatabase,
                itemDefId,
                quantity,
                out ItemDefinition itemDefinition);

            if (!validation.Success)
            {
                return validation;
            }

            if (!itemDefinition.ResolvedStackPolicy.IsStackable)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.AddStack,
                    InventoryFailureReason.ItemMustBeStackable,
                    $"Item definition '{itemDefId}' is unstackable and should be created as an item instance.",
                    inventoryState.Revision);
            }

            int stackIndex = inventoryState.FindStackIndex(itemDefId);
            if (stackIndex < 0)
            {
                inventoryState.AddStackRecord(itemDefId, quantity);
            }
            else
            {
                InventoryStackRecord stackRecord = inventoryState.StackLedger[stackIndex];
                if (!stackRecord.Quantity.TryAdd(quantity, out StackQuantity newQuantity))
                {
                    return InventoryOperationResult.Failed(
                        InventoryOperationType.AddStack,
                        InventoryFailureReason.StackOverflow,
                        $"Adding {quantity} to '{itemDefId}' would exceed the practical stack quantity limit.",
                        inventoryState.Revision);
                }

                stackRecord.SetQuantity(newQuantity);
            }

            inventoryState.IncrementRevision();
            return InventoryOperationResult
                .Succeeded(InventoryOperationType.AddStack, inventoryState.Revision, "Stack quantity added.")
                .WithChangedItemDefinition(itemDefId);
        }

        public static InventoryOperationResult RemoveStack(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemDefId itemDefId,
            StackQuantity quantity)
        {
            InventoryOperationResult validation = ValidateStackOperation(
                InventoryOperationType.RemoveStack,
                inventoryState,
                itemDefinitionDatabase,
                itemDefId,
                quantity,
                out ItemDefinition itemDefinition);

            if (!validation.Success)
            {
                return validation;
            }

            if (!itemDefinition.ResolvedStackPolicy.IsStackable)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.RemoveStack,
                    InventoryFailureReason.ItemMustBeStackable,
                    $"Item definition '{itemDefId}' is unstackable and is not stored in the stack ledger.",
                    inventoryState.Revision);
            }

            int stackIndex = inventoryState.FindStackIndex(itemDefId);
            if (stackIndex < 0)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.RemoveStack,
                    InventoryFailureReason.InsufficientStack,
                    $"No stack exists for item definition '{itemDefId}'.",
                    inventoryState.Revision);
            }

            InventoryStackRecord stackRecord = inventoryState.StackLedger[stackIndex];
            if (!stackRecord.Quantity.TrySubtract(quantity, out StackQuantity newQuantity))
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.RemoveStack,
                    InventoryFailureReason.InsufficientStack,
                    $"Stack '{itemDefId}' has {stackRecord.Quantity}, cannot remove {quantity}.",
                    inventoryState.Revision);
            }

            if (newQuantity.IsZero)
            {
                inventoryState.RemoveStackAt(stackIndex);
            }
            else
            {
                stackRecord.SetQuantity(newQuantity);
            }

            inventoryState.IncrementRevision();
            return InventoryOperationResult
                .Succeeded(InventoryOperationType.RemoveStack, inventoryState.Revision, "Stack quantity removed.")
                .WithChangedItemDefinition(itemDefId);
        }

        public static bool HasStack(PlayerInventoryState inventoryState, ItemDefId itemDefId, StackQuantity requiredQuantity)
        {
            if (inventoryState == null || itemDefId.IsEmpty || !requiredQuantity.IsPositive)
            {
                return false;
            }

            return inventoryState.TryGetStack(itemDefId, out InventoryStackRecord stackRecord) &&
                   stackRecord.Quantity >= requiredQuantity;
        }

        public static InventoryOperationResult CreateItemInstance(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemDefId itemDefId,
            out ItemInstanceId itemInstanceId)
        {
            return CreateItemInstance(inventoryState, itemDefinitionDatabase, itemDefId, ItemInstanceId.NewId(), out itemInstanceId);
        }

        public static InventoryOperationResult CreateItemInstance(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemDefId itemDefId,
            ItemInstanceId requestedInstanceId,
            out ItemInstanceId itemInstanceId)
        {
            itemInstanceId = default;

            InventoryOperationResult validation = ValidateDefinition(
                InventoryOperationType.CreateItemInstance,
                inventoryState,
                itemDefinitionDatabase,
                itemDefId,
                out ItemDefinition itemDefinition);

            if (!validation.Success)
            {
                return validation;
            }

            if (itemDefinition.ResolvedStackPolicy.IsStackable)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.CreateItemInstance,
                    InventoryFailureReason.ItemMustBeUnstackable,
                    $"Item definition '{itemDefId}' is stackable and should be stored in the stack ledger.",
                    inventoryState.Revision);
            }

            if (requestedInstanceId.IsEmpty)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.CreateItemInstance,
                    InventoryFailureReason.InvalidItemInstanceId,
                    "Requested item instance ID is empty.",
                    inventoryState.Revision);
            }

            if (inventoryState.FindInstanceIndex(requestedInstanceId) >= 0)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.CreateItemInstance,
                    InventoryFailureReason.DuplicateItemInstanceId,
                    $"Item instance ID '{requestedInstanceId}' already exists.",
                    inventoryState.Revision);
            }

            ItemInstanceState itemInstance = new ItemInstanceState(requestedInstanceId, itemDefId, ItemLifecycleState.InInventory);
            inventoryState.AddItemInstance(itemInstance);
            inventoryState.IncrementRevision();
            itemInstanceId = requestedInstanceId;

            return InventoryOperationResult
                .Succeeded(InventoryOperationType.CreateItemInstance, inventoryState.Revision, "Item instance created.")
                .WithChangedItemDefinition(itemDefId)
                .WithChangedItemInstance(itemInstanceId);
        }

        public static InventoryOperationResult DestroyItemInstance(
            PlayerInventoryState inventoryState,
            ItemInstanceId itemInstanceId)
        {
            InventoryOperationResult validation = ValidateInstance(
                InventoryOperationType.DestroyItemInstance,
                inventoryState,
                itemInstanceId,
                out ItemInstanceState itemInstance);

            if (!validation.Success)
            {
                return validation;
            }

            if (itemInstance.LifecycleState == ItemLifecycleState.Destroyed)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.DestroyItemInstance,
                    InventoryFailureReason.InstanceAlreadyDestroyed,
                    $"Item instance '{itemInstanceId}' is already destroyed.",
                    inventoryState.Revision);
            }

            itemInstance.SetLifecycleState(ItemLifecycleState.Destroyed);
            inventoryState.IncrementRevision();

            return InventoryOperationResult
                .Succeeded(InventoryOperationType.DestroyItemInstance, inventoryState.Revision, "Item instance marked destroyed.")
                .WithChangedItemDefinition(itemInstance.ItemDefId)
                .WithChangedItemInstance(itemInstanceId);
        }

        public static InventoryOperationResult MoveInstanceToState(
            PlayerInventoryState inventoryState,
            ItemInstanceId itemInstanceId,
            ItemLifecycleState newState)
        {
            InventoryOperationResult validation = ValidateInstance(
                InventoryOperationType.MoveInstanceToState,
                inventoryState,
                itemInstanceId,
                out ItemInstanceState itemInstance);

            if (!validation.Success)
            {
                return validation;
            }

            if (itemInstance.LifecycleState == ItemLifecycleState.Destroyed)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.MoveInstanceToState,
                    InventoryFailureReason.InstanceAlreadyDestroyed,
                    $"Item instance '{itemInstanceId}' is destroyed and cannot move to '{newState}'.",
                    inventoryState.Revision);
            }

            itemInstance.SetLifecycleState(newState);
            inventoryState.IncrementRevision();

            return InventoryOperationResult
                .Succeeded(InventoryOperationType.MoveInstanceToState, inventoryState.Revision, $"Item instance moved to '{newState}'.")
                .WithChangedItemDefinition(itemInstance.ItemDefId)
                .WithChangedItemInstance(itemInstanceId);
        }

        public static InventoryOperationResult CanApplyModifier(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemInstanceId itemInstanceId)
        {
            InventoryOperationResult validation = ValidateInstanceAndDefinition(
                InventoryOperationType.CanApplyModifier,
                inventoryState,
                itemDefinitionDatabase,
                itemInstanceId,
                out ItemInstanceState itemInstance,
                out ItemDefinition itemDefinition);

            if (!validation.Success)
            {
                return validation;
            }

            return CanApplyEquipmentOnlyOperation(InventoryOperationType.CanApplyModifier, inventoryState.Revision, itemInstance.ItemDefId, itemInstanceId, itemDefinition, "modifier");
        }

        public static InventoryOperationResult CanApplyModifier(
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemDefId itemDefId)
        {
            InventoryOperationResult validation = ValidateDefinition(
                InventoryOperationType.CanApplyModifier,
                null,
                itemDefinitionDatabase,
                itemDefId,
                out ItemDefinition itemDefinition);

            if (!validation.Success)
            {
                return validation;
            }

            return CanApplyEquipmentOnlyOperation(InventoryOperationType.CanApplyModifier, 0, itemDefId, default, itemDefinition, "modifier");
        }

        public static InventoryOperationResult CanApplyEnchantment(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemInstanceId itemInstanceId)
        {
            InventoryOperationResult validation = ValidateInstanceAndDefinition(
                InventoryOperationType.CanApplyEnchantment,
                inventoryState,
                itemDefinitionDatabase,
                itemInstanceId,
                out ItemInstanceState itemInstance,
                out ItemDefinition itemDefinition);

            if (!validation.Success)
            {
                return validation;
            }

            return CanApplyEquipmentOnlyOperation(InventoryOperationType.CanApplyEnchantment, inventoryState.Revision, itemInstance.ItemDefId, itemInstanceId, itemDefinition, "enchantment");
        }

        public static InventoryOperationResult CanApplyEnchantment(
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemDefId itemDefId)
        {
            InventoryOperationResult validation = ValidateDefinition(
                InventoryOperationType.CanApplyEnchantment,
                null,
                itemDefinitionDatabase,
                itemDefId,
                out ItemDefinition itemDefinition);

            if (!validation.Success)
            {
                return validation;
            }

            return CanApplyEquipmentOnlyOperation(InventoryOperationType.CanApplyEnchantment, 0, itemDefId, default, itemDefinition, "enchantment");
        }

        private static InventoryOperationResult CanApplyEquipmentOnlyOperation(
            InventoryOperationType operationType,
            long revision,
            ItemDefId itemDefId,
            ItemInstanceId itemInstanceId,
            ItemDefinition itemDefinition,
            string operationName)
        {
            if (!itemDefinition.IsEquipment)
            {
                return InventoryOperationResult.Failed(
                    operationType,
                    InventoryFailureReason.ItemMustBeEquipment,
                    $"Only equipment can receive {operationName}s. Item definition '{itemDefId}' is not equipment.",
                    revision);
            }

            InventoryOperationResult result = InventoryOperationResult
                .Succeeded(operationType, revision, $"Item definition '{itemDefId}' can receive {operationName}s.")
                .WithChangedItemDefinition(itemDefId);

            if (!itemInstanceId.IsEmpty)
            {
                result.WithChangedItemInstance(itemInstanceId);
            }

            return result;
        }

        private static InventoryOperationResult ValidateStackOperation(
            InventoryOperationType operationType,
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemDefId itemDefId,
            StackQuantity quantity,
            out ItemDefinition itemDefinition)
        {
            InventoryOperationResult validation = ValidateDefinition(operationType, inventoryState, itemDefinitionDatabase, itemDefId, out itemDefinition);
            if (!validation.Success)
            {
                return validation;
            }

            if (!quantity.IsPositive)
            {
                return InventoryOperationResult.Failed(
                    operationType,
                    InventoryFailureReason.InvalidQuantity,
                    "Stack quantity must be positive.",
                    inventoryState != null ? inventoryState.Revision : 0);
            }

            return InventoryOperationResult.Succeeded(operationType, inventoryState != null ? inventoryState.Revision : 0);
        }

        private static InventoryOperationResult ValidateDefinition(
            InventoryOperationType operationType,
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemDefId itemDefId,
            out ItemDefinition itemDefinition)
        {
            itemDefinition = null;

            if (inventoryState == null && OperationRequiresInventory(operationType))
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidInventoryState, "Player inventory state is null.");
            }

            if (itemDefinitionDatabase == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidItemDefinitionDatabase, "Item definition database is null.", inventoryState != null ? inventoryState.Revision : 0);
            }

            if (itemDefId.IsEmpty)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidItemDefinitionId, "Item definition ID is empty.", inventoryState != null ? inventoryState.Revision : 0);
            }

            if (!itemDefinitionDatabase.TryGet(itemDefId, out itemDefinition) || itemDefinition == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.UnknownItemDefinition, $"Unknown item definition '{itemDefId}'.", inventoryState != null ? inventoryState.Revision : 0);
            }

            return InventoryOperationResult.Succeeded(operationType, inventoryState != null ? inventoryState.Revision : 0);
        }

        private static InventoryOperationResult ValidateInstance(
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

        private static InventoryOperationResult ValidateInstanceAndDefinition(
            InventoryOperationType operationType,
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemInstanceId itemInstanceId,
            out ItemInstanceState itemInstance,
            out ItemDefinition itemDefinition)
        {
            itemDefinition = null;
            InventoryOperationResult instanceValidation = ValidateInstance(operationType, inventoryState, itemInstanceId, out itemInstance);
            if (!instanceValidation.Success)
            {
                return instanceValidation;
            }

            InventoryOperationResult definitionValidation = ValidateDefinition(operationType, inventoryState, itemDefinitionDatabase, itemInstance.ItemDefId, out itemDefinition);
            if (!definitionValidation.Success)
            {
                return definitionValidation;
            }

            return InventoryOperationResult.Succeeded(operationType, inventoryState.Revision);
        }

        private static bool OperationRequiresInventory(InventoryOperationType operationType)
        {
            return operationType != InventoryOperationType.CanApplyModifier &&
                   operationType != InventoryOperationType.CanApplyEnchantment &&
                   operationType != InventoryOperationType.Validate;
        }
    }
}
