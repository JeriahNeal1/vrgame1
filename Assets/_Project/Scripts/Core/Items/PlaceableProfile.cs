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
        ElectricalDevice,
        Wire,
        Furniture,
        Custom
    }

    public enum PlacementMode
    {
        FrameworkSnap,
        FreeFurniture,
        ElectricalDevice,
        Wire,
        Machine,
        Decoration
    }

    public enum FrameworkPieceKind
    {
        None,
        Foundation,
        Wall,
        Ceiling,
        Pillar,
        Ramp,
        Doorway,
        Window,
        Custom
    }

    public enum PlacementSurfaceSnapMode
    {
        None,
        Optional,
        Required
    }

    public enum PlacementRotationMode
    {
        Fixed,
        FreeYaw,
        SnapYaw,
        AlignToSurface,
        MatchSnapPoint
    }

    [Serializable]
    public sealed class FrameworkSnapRule
    {
        [SerializeField]
        private FrameworkPieceKind placedPieceKind = FrameworkPieceKind.None;

        [SerializeField]
        private List<FrameworkPieceKind> compatibleSupportKinds = new List<FrameworkPieceKind>();

        [Tooltip("Optional stable socket tags/ids accepted by this rule. Empty means any compatible support socket.")]
        [SerializeField]
        private List<string> compatibleSocketTags = new List<string>();

        [SerializeField]
        private bool requiresOpenSocket = true;

        public FrameworkPieceKind PlacedPieceKind
        {
            get { return placedPieceKind; }
        }

        public IReadOnlyList<FrameworkPieceKind> CompatibleSupportKinds
        {
            get { return compatibleSupportKinds ?? (IReadOnlyList<FrameworkPieceKind>)Array.Empty<FrameworkPieceKind>(); }
        }

        public IReadOnlyList<string> CompatibleSocketTags
        {
            get { return compatibleSocketTags ?? (IReadOnlyList<string>)Array.Empty<string>(); }
        }

        public bool RequiresOpenSocket
        {
            get { return requiresOpenSocket; }
        }

        public bool AllowsSupport(FrameworkPieceKind supportKind)
        {
            IReadOnlyList<FrameworkPieceKind> supportKinds = CompatibleSupportKinds;
            if (supportKinds.Count == 0)
            {
                return DefaultAllowsSupport(placedPieceKind, supportKind);
            }

            for (int i = 0; i < supportKinds.Count; i++)
            {
                if (supportKinds[i] == supportKind)
                {
                    return true;
                }
            }

            return false;
        }

        public bool AllowsSocketTag(string socketTag)
        {
            IReadOnlyList<string> socketTags = CompatibleSocketTags;
            if (socketTags.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < socketTags.Count; i++)
            {
                if (StableIdUtility.EqualsNormalized(socketTags[i], socketTag))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool DefaultAllowsSupport(FrameworkPieceKind placedPieceKind, FrameworkPieceKind supportKind)
        {
            switch (placedPieceKind)
            {
                case FrameworkPieceKind.Foundation:
                    return supportKind == FrameworkPieceKind.None;
                case FrameworkPieceKind.Wall:
                    return supportKind == FrameworkPieceKind.Foundation ||
                           supportKind == FrameworkPieceKind.Wall ||
                           supportKind == FrameworkPieceKind.Ceiling;
                case FrameworkPieceKind.Ceiling:
                    return supportKind == FrameworkPieceKind.Foundation ||
                           supportKind == FrameworkPieceKind.Wall ||
                           supportKind == FrameworkPieceKind.Pillar;
                case FrameworkPieceKind.Pillar:
                case FrameworkPieceKind.Ramp:
                case FrameworkPieceKind.Doorway:
                case FrameworkPieceKind.Window:
                case FrameworkPieceKind.Custom:
                    return supportKind != FrameworkPieceKind.None;
                case FrameworkPieceKind.None:
                default:
                    return false;
            }
        }
    }

    [Serializable]
    public sealed class PlacementCollisionRules
    {
        [SerializeField]
        private bool requireNoBlockingOverlap = true;

        [SerializeField]
        private LayerMask blockingLayers = ~0;

        [SerializeField]
        private bool ignoreTriggerColliders = true;

        [SerializeField]
        private Vector3 boundsCenter = Vector3.zero;

        [SerializeField]
        private Vector3 boundsExtents = new Vector3(0.5f, 0.5f, 0.5f);

        [Min(0f)]
        [SerializeField]
        private float padding = 0.02f;

        public bool RequireNoBlockingOverlap
        {
            get { return requireNoBlockingOverlap; }
        }

        public LayerMask BlockingLayers
        {
            get { return blockingLayers; }
        }

        public bool IgnoreTriggerColliders
        {
            get { return ignoreTriggerColliders; }
        }

        public Vector3 BoundsCenter
        {
            get { return boundsCenter; }
        }

        public Vector3 BoundsExtents
        {
            get { return new Vector3(Mathf.Max(0f, boundsExtents.x), Mathf.Max(0f, boundsExtents.y), Mathf.Max(0f, boundsExtents.z)); }
        }

        public float Padding
        {
            get { return Mathf.Max(0f, padding); }
        }
    }

    [Serializable]
    public sealed class PlacementRotationRules
    {
        [SerializeField]
        private PlacementRotationMode rotationMode = PlacementRotationMode.SnapYaw;

        [Min(0f)]
        [SerializeField]
        private float yawSnapDegrees = 90f;

        [SerializeField]
        private bool allowManualYawOffset = true;

        public PlacementRotationMode RotationMode
        {
            get { return rotationMode; }
        }

        public float YawSnapDegrees
        {
            get { return yawSnapDegrees <= 0f ? 0f : yawSnapDegrees; }
        }

        public bool AllowManualYawOffset
        {
            get { return allowManualYawOffset; }
        }
    }

    [Serializable]
    public sealed class PlacementToolRequirement
    {
        [SerializeField]
        private bool requiresTool = false;

        [SerializeField]
        private HarvestingDomain requiredHarvestingType = HarvestingDomain.None;

        [SerializeField]
        private HarvestingSubtype requiredToolSubtype = HarvestingSubtype.None;

        [Min(0)]
        [SerializeField]
        private int requiredToolTier = 0;

        public bool RequiresTool
        {
            get { return requiresTool; }
        }

        public HarvestingDomain RequiredHarvestingType
        {
            get { return requiredHarvestingType; }
        }

        public HarvestingSubtype RequiredToolSubtype
        {
            get { return requiredToolSubtype; }
        }

        public int RequiredToolTier
        {
            get { return Mathf.Max(0, requiredToolTier); }
        }
    }

    [Serializable]
    public sealed class PlaceableProfile
    {
        [Header("Placement")]
        [SerializeField]
        private PlacementMode placementMode = PlacementMode.FreeFurniture;

        [SerializeField]
        private PlaceableKind kind = PlaceableKind.None;

        [SerializeField]
        private GameObject placedPrefab = null;

        [SerializeField]
        private GameObject previewPrefab = null;

        [SerializeField]
        private StackQuantity consumedItemQuantity = StackQuantity.One;

        [Header("Footprint")]
        [SerializeField]
        private Vector3Int footprint = Vector3Int.one;

        [SerializeField]
        private bool snapsToGrid = true;

        [SerializeField]
        private PlacementSurfaceSnapMode surfaceSnapMode = PlacementSurfaceSnapMode.Optional;

        [SerializeField]
        private bool requiresFoundation = false;

        [SerializeField]
        private string placementLayerId = string.Empty;

        [SerializeField]
        private List<string> placementTags = new List<string>();

        [Header("Framework Snapping")]
        [SerializeField]
        private FrameworkPieceKind frameworkPieceKind = FrameworkPieceKind.None;

        [SerializeField]
        private List<FrameworkSnapRule> snapRules = new List<FrameworkSnapRule>();

        [SerializeField]
        private LayerMask validGroundLayers = ~0;

        [Header("Rules")]
        [SerializeField]
        private PlacementCollisionRules collisionRules = new PlacementCollisionRules();

        [SerializeField]
        private PlacementRotationRules rotationRules = new PlacementRotationRules();

        [SerializeField]
        private PlacementToolRequirement requiredTool = new PlacementToolRequirement();

        public PlacementMode PlacementMode
        {
            get { return placementMode; }
        }

        public PlaceableKind Kind
        {
            get { return kind; }
        }

        public GameObject PlacedPrefab
        {
            get { return placedPrefab; }
        }

        public GameObject PreviewPrefab
        {
            get { return previewPrefab != null ? previewPrefab : placedPrefab; }
        }

        public StackQuantity ConsumedItemQuantity
        {
            get { return consumedItemQuantity.IsPositive ? consumedItemQuantity : StackQuantity.One; }
        }

        public Vector3Int Footprint
        {
            get { return new Vector3Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y), Mathf.Max(1, footprint.z)); }
        }

        public bool SnapsToGrid
        {
            get { return snapsToGrid; }
        }

        public PlacementSurfaceSnapMode SurfaceSnapMode
        {
            get { return surfaceSnapMode; }
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

        public FrameworkPieceKind FrameworkPieceKind
        {
            get { return frameworkPieceKind; }
        }

        public IReadOnlyList<FrameworkSnapRule> SnapRules
        {
            get { return snapRules ?? (IReadOnlyList<FrameworkSnapRule>)Array.Empty<FrameworkSnapRule>(); }
        }

        public LayerMask ValidGroundLayers
        {
            get { return validGroundLayers; }
        }

        public PlacementCollisionRules CollisionRules
        {
            get
            {
                collisionRules ??= new PlacementCollisionRules();
                return collisionRules;
            }
        }

        public PlacementRotationRules RotationRules
        {
            get
            {
                rotationRules ??= new PlacementRotationRules();
                return rotationRules;
            }
        }

        public PlacementToolRequirement RequiredTool
        {
            get
            {
                requiredTool ??= new PlacementToolRequirement();
                return requiredTool;
            }
        }

        public bool CanAttachTo(FrameworkPieceKind supportKind, string socketTag = "")
        {
            IReadOnlyList<FrameworkSnapRule> rules = SnapRules;
            if (rules.Count == 0)
            {
                return FrameworkSnapRule.DefaultAllowsSupport(frameworkPieceKind, supportKind);
            }

            bool hasMatchingPieceRule = false;
            for (int i = 0; i < rules.Count; i++)
            {
                FrameworkSnapRule rule = rules[i];
                if (rule == null)
                {
                    continue;
                }

                if (rule.PlacedPieceKind != FrameworkPieceKind.None && rule.PlacedPieceKind != frameworkPieceKind)
                {
                    continue;
                }

                hasMatchingPieceRule = true;
                if (rule.AllowsSupport(supportKind) && rule.AllowsSocketTag(socketTag))
                {
                    return true;
                }
            }

            return !hasMatchingPieceRule && FrameworkSnapRule.DefaultAllowsSupport(frameworkPieceKind, supportKind);
        }
    }
}
