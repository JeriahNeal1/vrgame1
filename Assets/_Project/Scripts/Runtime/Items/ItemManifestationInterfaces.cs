using UnityEngine;
using VRGame.Items;

namespace VRGame.Runtime
{
    public interface IPlayerInventoryStateProvider
    {
        PlayerInventoryState InventoryState { get; }
    }

    public interface IVRHandItemSpawner
    {
        bool TrySpawnIntoHand(HandItemSpawnRequest request, out WorldItemView worldItemView, out string message);
    }

    public interface IHeldItemService
    {
        ItemManifestationResult ManifestStack(PlayerInventoryState inventoryState, ItemDefinitionDatabase itemDefinitionDatabase, ItemDefId itemDefId, string requestedHandId);

        ItemManifestationResult ManifestItemInstance(PlayerInventoryState inventoryState, ItemDefinitionDatabase itemDefinitionDatabase, ItemInstanceId itemInstanceId, string requestedHandId);

        InventoryOperationResult ReturnToInventory(PlayerInventoryState inventoryState, ItemDefinitionDatabase itemDefinitionDatabase, string manifestationRequestId);

        InventoryOperationResult DropManifestedItem(PlayerInventoryState inventoryState, string manifestationRequestId);

        InventoryOperationResult CommitManifestedItemAsEquipped(PlayerInventoryState inventoryState, string manifestationRequestId);
    }

    public sealed class HandItemSpawnRequest
    {
        public string RequestedHandId { get; set; }

        public Transform SpawnOrigin { get; set; }

        public Transform OptionalParent { get; set; }

        public ItemDefinition ItemDefinition { get; set; }

        public ItemInstanceState ItemInstance { get; set; }

        public ManifestationReservation Reservation { get; set; }

        public WorldItemBinding Binding { get; set; }

        public GameObject WorldPrefab
        {
            get { return ItemDefinition != null ? ItemDefinition.WorldPrefab : null; }
        }
    }
}
