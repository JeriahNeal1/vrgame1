using System.Collections.Generic;
using UnityEngine;
using VRGame.Items;

namespace VRGame.Runtime
{
    [DisallowMultipleComponent]
    public sealed class FrameworkSnapPoint : MonoBehaviour
    {
        [SerializeField]
        private string snapPointId = string.Empty;

        [SerializeField]
        private string socketTag = string.Empty;

        [SerializeField]
        private FrameworkStructurePiece ownerPiece = null;

        [SerializeField]
        private FrameworkPieceKind ownerPieceKindOverride = FrameworkPieceKind.None;

        [SerializeField]
        private List<FrameworkPieceKind> acceptedPieceKinds = new List<FrameworkPieceKind>();

        [SerializeField]
        private bool occupied = false;

        [Min(0f)]
        [SerializeField]
        private float maxSnapDistance = 0.35f;

        public string SnapPointId
        {
            get { return StableIdUtility.Normalize(snapPointId); }
        }

        public string SocketTag
        {
            get { return StableIdUtility.Normalize(socketTag); }
        }

        public FrameworkStructurePiece OwnerPiece
        {
            get { return ownerPiece; }
        }

        public FrameworkPieceKind OwnerPieceKind
        {
            get
            {
                if (ownerPiece != null && ownerPiece.PieceKind != FrameworkPieceKind.None)
                {
                    return ownerPiece.PieceKind;
                }

                return ownerPieceKindOverride;
            }
        }

        public bool Occupied
        {
            get { return occupied; }
        }

        public float MaxSnapDistance
        {
            get { return Mathf.Max(0f, maxSnapDistance); }
        }

        public void BindOwner(FrameworkStructurePiece newOwnerPiece)
        {
            ownerPiece = newOwnerPiece;
        }

        public bool CanAccept(PlaceableProfile profile, Vector3 requestedPosition, out string reason)
        {
            if (profile == null)
            {
                reason = "Placeable profile is missing.";
                return false;
            }

            if (occupied)
            {
                reason = "Snap point is already occupied.";
                return false;
            }

            FrameworkPieceKind incomingKind = profile.FrameworkPieceKind;
            if (incomingKind == FrameworkPieceKind.None)
            {
                reason = "Incoming framework piece kind is not set.";
                return false;
            }

            if (!AcceptsPieceKind(incomingKind))
            {
                reason = $"Snap point does not accept '{incomingKind}'.";
                return false;
            }

            FrameworkPieceKind supportKind = OwnerPieceKind;
            if (!profile.CanAttachTo(supportKind, SocketTag))
            {
                reason = $"Profile for '{incomingKind}' cannot attach to support '{supportKind}'.";
                return false;
            }

            if (Vector3.Distance(transform.position, requestedPosition) > MaxSnapDistance)
            {
                reason = "Requested placement is outside snap range.";
                return false;
            }

            if (incomingKind == FrameworkPieceKind.Ceiling && ownerPiece != null && !ownerPiece.CanSupport(FrameworkPieceKind.Ceiling))
            {
                reason = "Ceiling requires a support that can carry ceilings.";
                return false;
            }

            reason = "Snap point accepts placement.";
            return true;
        }

        public void MarkOccupied(bool isOccupied)
        {
            occupied = isOccupied;
        }

        private bool AcceptsPieceKind(FrameworkPieceKind incomingKind)
        {
            if (acceptedPieceKinds == null || acceptedPieceKinds.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < acceptedPieceKinds.Count; i++)
            {
                if (acceptedPieceKinds[i] == incomingKind)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnValidate()
        {
            if (ownerPiece == null)
            {
                ownerPiece = GetComponentInParent<FrameworkStructurePiece>();
            }
        }
    }
}
