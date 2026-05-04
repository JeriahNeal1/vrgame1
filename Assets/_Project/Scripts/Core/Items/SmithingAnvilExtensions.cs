using System;
using UnityEngine;

namespace VRGame.Items
{
    [Serializable]
    public sealed class SmithingStrikeRecord
    {
        [SerializeField]
        private ItemInstanceId targetItemInstanceId = default;

        [SerializeField]
        private ItemInstanceId hammerItemInstanceId = default;

        [SerializeField]
        private ItemDefId hammerItemDefId = default;

        [SerializeField]
        private string stationId = string.Empty;

        [Range(0f, 1f)]
        [SerializeField]
        private float normalizedForce = 0f;

        [Range(0f, 1f)]
        [SerializeField]
        private float timingQuality = 0f;

        [Range(0f, 1f)]
        [SerializeField]
        private float contactQuality = 0f;

        [Range(0f, 1f)]
        [SerializeField]
        private float overallQuality = 0f;

        [SerializeField]
        private int sequenceIndex = 0;

        [SerializeField]
        private int randomSeed = 0;

        public ItemInstanceId TargetItemInstanceId
        {
            get { return targetItemInstanceId; }
        }

        public ItemInstanceId HammerItemInstanceId
        {
            get { return hammerItemInstanceId; }
        }

        public ItemDefId HammerItemDefId
        {
            get { return hammerItemDefId; }
        }

        public string StationId
        {
            get { return StableIdUtility.Normalize(stationId); }
        }

        public float NormalizedForce
        {
            get { return Mathf.Clamp01(normalizedForce); }
        }

        public float TimingQuality
        {
            get { return Mathf.Clamp01(timingQuality); }
        }

        public float ContactQuality
        {
            get { return Mathf.Clamp01(contactQuality); }
        }

        public float OverallQuality
        {
            get { return Mathf.Clamp01(overallQuality); }
        }

        public int SequenceIndex
        {
            get { return Mathf.Max(0, sequenceIndex); }
        }

        public int RandomSeed
        {
            get { return randomSeed; }
        }
    }

    public interface IAnvilItemPlacementSink
    {
        void OnItemPlacedOnAnvil(ItemInstanceId itemInstanceId, string stationId);

        void OnItemRemovedFromAnvil(ItemInstanceId itemInstanceId, string stationId);
    }

    public interface ISmithingStrikeSource
    {
        event Action<SmithingStrikeRecord> StrikeRecorded;
    }

    public interface ISmithingReforgeContextFactory
    {
        bool TryCreateReforgeContext(SmithingStrikeRecord strikeRecord, out ReforgeContext reforgeContext);
    }

    public interface IManualSmithingAffixResolver
    {
        InventoryOperationResult ApplySmithingStrike(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            SmithingStrikeRecord strikeRecord,
            ReforgeContext reforgeContext,
            out ModifierId appliedModifierId);
    }
}
