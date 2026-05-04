using System;
using UnityEngine;
using VRGame.Items;

namespace VRGame.Runtime
{
    public enum WireToolFailureReason
    {
        None,
        MissingFirstNode,
        MissingSecondNode,
        IncompatibleNodes,
        OutOfRange,
        MissingRegistry,
        MissingInventoryState,
        MissingItemDatabase,
        MissingWireItem,
        InsufficientWireStack,
        InventoryConsumeFailed,
        ConnectionFailed
    }

    public sealed class WireToolResult
    {
        private WireToolResult(
            bool success,
            WireToolFailureReason failureReason,
            string message,
            WireConnection connection = null,
            InventoryOperationResult inventoryResult = null)
        {
            Success = success;
            FailureReason = failureReason;
            Message = message ?? string.Empty;
            Connection = connection;
            InventoryResult = inventoryResult;
        }

        public bool Success { get; }

        public WireToolFailureReason FailureReason { get; }

        public string Message { get; }

        public WireConnection Connection { get; }

        public InventoryOperationResult InventoryResult { get; }

        public static WireToolResult Succeeded(WireConnection connection, string message, InventoryOperationResult inventoryResult = null)
        {
            return new WireToolResult(true, WireToolFailureReason.None, message, connection, inventoryResult);
        }

        public static WireToolResult Failed(WireToolFailureReason failureReason, string message, InventoryOperationResult inventoryResult = null)
        {
            return new WireToolResult(false, failureReason, message, null, inventoryResult);
        }
    }

    public sealed class WireToolAction : MonoBehaviour
    {
        [Header("Services")]
        [SerializeField]
        private ElectricalConnectionRegistry connectionRegistry = null;

        [SerializeField]
        private ItemDefinitionDatabase itemDefinitionDatabase = null;

        [Tooltip("Optional MonoBehaviour that implements IPlayerInventoryStateProvider.")]
        [SerializeField]
        private MonoBehaviour inventoryStateProviderBehaviour = null;

        [Header("Wire Item")]
        [SerializeField]
        private ItemDefId wireItemDefId = default;

        [SerializeField]
        private bool consumeWireFromInventory = true;

        [SerializeField]
        private StackQuantity consumedWireQuantity = StackQuantity.One;

        [Header("Runtime Preview")]
        [SerializeField]
        private Vector3 previewEndPosition = Vector3.zero;

        private ElectricalNode firstNode;
        private IPlayerInventoryStateProvider inventoryStateProvider;
        private IPlayerInventoryStateProvider runtimeInventoryStateProvider;

        public ElectricalNode FirstNode
        {
            get { return firstNode; }
        }

        public Vector3 PreviewEndPosition
        {
            get { return previewEndPosition; }
        }

        public void BindRuntime(
            ElectricalConnectionRegistry registry,
            ItemDefinitionDatabase database,
            IPlayerInventoryStateProvider provider,
            ItemDefId newWireItemDefId)
        {
            connectionRegistry = registry;
            itemDefinitionDatabase = database;
            runtimeInventoryStateProvider = provider;
            inventoryStateProvider = provider;
            wireItemDefId = newWireItemDefId;
        }

        public WireToolResult SelectFirstNode(ElectricalNode node)
        {
            if (node == null)
            {
                firstNode = null;
                return WireToolResult.Failed(WireToolFailureReason.MissingFirstNode, "First electrical node is missing.");
            }

            if (!node.CanStartConnection)
            {
                firstNode = null;
                return WireToolResult.Failed(WireToolFailureReason.IncompatibleNodes, "First electrical node cannot start a wire connection.");
            }

            firstNode = node;
            previewEndPosition = node.WorldPosition;
            return WireToolResult.Succeeded(null, $"Selected first electrical node '{node.NodeId}'.");
        }

        public void UpdatePreview(Vector3 currentEndPosition)
        {
            previewEndPosition = currentEndPosition;
        }

        public WireToolResult ReleaseOnNode(ElectricalNode secondNode)
        {
            if (firstNode == null)
            {
                return WireToolResult.Failed(WireToolFailureReason.MissingFirstNode, "No first electrical node has been selected.");
            }

            if (secondNode == null)
            {
                return WireToolResult.Failed(WireToolFailureReason.MissingSecondNode, "Second electrical node is missing.");
            }

            if (connectionRegistry == null)
            {
                return WireToolResult.Failed(WireToolFailureReason.MissingRegistry, "Electrical connection registry is missing.");
            }

            if (!firstNode.CanConnectTo(secondNode, out string compatibilityMessage))
            {
                return WireToolResult.Failed(WireToolFailureReason.IncompatibleNodes, compatibilityMessage);
            }

            InventoryOperationResult consumeResult = null;
            PlayerInventoryState inventoryState = ResolveInventoryState();
            StackQuantity quantity = consumedWireQuantity.IsPositive ? consumedWireQuantity : StackQuantity.One;
            if (consumeWireFromInventory)
            {
                WireToolResult inventoryValidation = ValidateWireInventory(inventoryState, quantity);
                if (!inventoryValidation.Success)
                {
                    return inventoryValidation;
                }

                consumeResult = PlayerInventoryOperations.RemoveStack(inventoryState, itemDefinitionDatabase, wireItemDefId, quantity);
                if (!consumeResult.Success)
                {
                    return WireToolResult.Failed(WireToolFailureReason.InventoryConsumeFailed, consumeResult.Message, consumeResult);
                }
            }

            WireConnectionResult connectionResult;
            try
            {
                connectionResult = connectionRegistry.TryCreateConnection(firstNode, secondNode, wireItemDefId, inventoryState != null ? inventoryState.OwnerId : string.Empty);
            }
            catch (Exception exception)
            {
                RefundWire(inventoryState, quantity);
                return WireToolResult.Failed(WireToolFailureReason.ConnectionFailed, exception.Message, consumeResult);
            }

            if (!connectionResult.Success)
            {
                RefundWire(inventoryState, quantity);
                return WireToolResult.Failed(WireToolFailureReason.ConnectionFailed, connectionResult.Message, consumeResult);
            }

            firstNode = null;
            previewEndPosition = secondNode.WorldPosition;
            return WireToolResult.Succeeded(connectionResult.Connection, connectionResult.Message, consumeResult);
        }

        public void Cancel()
        {
            firstNode = null;
        }

        private WireToolResult ValidateWireInventory(PlayerInventoryState inventoryState, StackQuantity quantity)
        {
            if (inventoryState == null)
            {
                return WireToolResult.Failed(WireToolFailureReason.MissingInventoryState, "Player inventory state is missing.");
            }

            if (itemDefinitionDatabase == null)
            {
                return WireToolResult.Failed(WireToolFailureReason.MissingItemDatabase, "Item definition database is missing.");
            }

            if (wireItemDefId.IsEmpty || !itemDefinitionDatabase.TryGet(wireItemDefId, out ItemDefinition wireDefinition) || wireDefinition == null)
            {
                return WireToolResult.Failed(WireToolFailureReason.MissingWireItem, "Wire item definition is missing.");
            }

            if (!wireDefinition.HasFlag(ItemFlags.Placeable) || !wireDefinition.HasFlag(ItemFlags.Electrical))
            {
                return WireToolResult.Failed(WireToolFailureReason.MissingWireItem, $"Wire item '{wireItemDefId}' must be placeable and electrical.");
            }

            if (!PlayerInventoryOperations.HasStack(inventoryState, wireItemDefId, quantity))
            {
                return WireToolResult.Failed(WireToolFailureReason.InsufficientWireStack, $"Inventory does not contain {quantity} of wire item '{wireItemDefId}'.");
            }

            return WireToolResult.Succeeded(null, "Wire inventory is valid.");
        }

        private PlayerInventoryState ResolveInventoryState()
        {
            IPlayerInventoryStateProvider provider = ResolveInventoryProvider();
            return provider != null ? provider.InventoryState : null;
        }

        private void RefundWire(PlayerInventoryState inventoryState, StackQuantity quantity)
        {
            if (consumeWireFromInventory && inventoryState != null)
            {
                PlayerInventoryOperations.AddStack(inventoryState, itemDefinitionDatabase, wireItemDefId, quantity);
            }
        }

        private IPlayerInventoryStateProvider ResolveInventoryProvider()
        {
            if (runtimeInventoryStateProvider != null)
            {
                inventoryStateProvider = runtimeInventoryStateProvider;
                return inventoryStateProvider;
            }

            if (inventoryStateProvider != null)
            {
                return inventoryStateProvider;
            }

            inventoryStateProvider = inventoryStateProviderBehaviour as IPlayerInventoryStateProvider;
            return inventoryStateProvider;
        }
    }
}
