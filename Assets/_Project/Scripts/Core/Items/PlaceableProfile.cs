using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    public enum PlaceableKind
    {
        None,
        Block,
        Wall,
        Machine,
        Conveyor,
        Pipe,
        PowerPole,
        Decoration,
        Custom
    }

    [Serializable]
    public sealed class PlaceableProfile
    {
        [SerializeField]
        private PlaceableKind kind = PlaceableKind.None;

        [SerializeField]
        private Vector3Int footprint = Vector3Int.one;

        [SerializeField]
        private bool snapsToGrid = true;

        [SerializeField]
        private bool requiresFoundation = false;

        [SerializeField]
        private string placementLayerId = string.Empty;

        [SerializeField]
        private List<string> placementTags = new List<string>();

        public PlaceableKind Kind
        {
            get { return kind; }
        }

        public Vector3Int Footprint
        {
            get { return new Vector3Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y), Mathf.Max(1, footprint.z)); }
        }

        public bool SnapsToGrid
        {
            get { return snapsToGrid; }
        }

        public bool RequiresFoundation
        {
            get { return requiresFoundation; }
        }

        public string PlacementLayerId
        {
            get { return StableIdUtility.Normalize(placementLayerId); }
        }

        public IReadOnlyList<string> PlacementTags
        {
            get { return placementTags ?? (IReadOnlyList<string>)Array.Empty<string>(); }
        }
    }
}
