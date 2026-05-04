using System.Collections.Generic;
using UnityEngine;
using VRGame.Items;

namespace VRGame.Runtime
{
    [DisallowMultipleComponent]
    public sealed class FrameworkStructurePiece : MonoBehaviour
    {
        [SerializeField]
        private string pieceId = string.Empty;

        [SerializeField]
        private FrameworkPieceKind pieceKind = FrameworkPieceKind.None;

        [SerializeField]
        private bool supportsSnapping = true;

        [SerializeField]
        private bool supportsCeilings = false;

        [SerializeField]
        private List<FrameworkSnapPoint> snapPoints = new List<FrameworkSnapPoint>();

        public string PieceId
        {
            get { return StableIdUtility.Normalize(pieceId); }
        }

        public FrameworkPieceKind PieceKind
        {
            get { return pieceKind; }
        }

        public bool SupportsSnapping
        {
            get { return supportsSnapping; }
        }

        public bool SupportsCeilings
        {
            get { return supportsCeilings || pieceKind == FrameworkPieceKind.Foundation || pieceKind == FrameworkPieceKind.Wall || pieceKind == FrameworkPieceKind.Pillar; }
        }

        public IReadOnlyList<FrameworkSnapPoint> SnapPoints
        {
            get
            {
                EnsureSnapPoints();
                return snapPoints;
            }
        }

        public bool CanSupport(FrameworkPieceKind incomingPieceKind)
        {
            if (!supportsSnapping)
            {
                return false;
            }

            if (incomingPieceKind == FrameworkPieceKind.Ceiling)
            {
                return SupportsCeilings;
            }

            return FrameworkSnapRule.DefaultAllowsSupport(incomingPieceKind, pieceKind);
        }

        public void Bind(string newPieceId, FrameworkPieceKind newPieceKind)
        {
            pieceId = StableIdUtility.Normalize(newPieceId);
            pieceKind = newPieceKind;
            EnsureSnapPoints();
        }

        private void Awake()
        {
            EnsureSnapPoints();
        }

        private void OnValidate()
        {
            EnsureSnapPoints();
        }

        private void EnsureSnapPoints()
        {
            snapPoints ??= new List<FrameworkSnapPoint>();
            FrameworkSnapPoint[] childSnapPoints = GetComponentsInChildren<FrameworkSnapPoint>(true);
            for (int i = 0; i < childSnapPoints.Length; i++)
            {
                FrameworkSnapPoint snapPoint = childSnapPoints[i];
                if (snapPoint != null && !snapPoints.Contains(snapPoint))
                {
                    snapPoints.Add(snapPoint);
                }

                if (snapPoint != null && snapPoint.OwnerPiece == null)
                {
                    snapPoint.BindOwner(this);
                }
            }
        }
    }
}
