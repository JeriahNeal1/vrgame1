using System;
using System.Collections.Generic;
using UnityEngine;
using VRGame.Items;

namespace VRGame.Runtime
{
    [Flags]
    public enum ElectricalNodeRole
    {
        None = 0,
        Input = 1 << 0,
        Output = 1 << 1
    }

    public enum ElectricalNodeKind
    {
        Generic,
        GeneratorOutput,
        SwitchInput,
        SwitchOutput,
        DiodeInput,
        DiodeOutput,
        ConsumerInput,
        WireJunction
    }

    [DisallowMultipleComponent]
    public sealed class ElectricalNode : MonoBehaviour
    {
        [SerializeField]
        private string nodeId = string.Empty;

        [SerializeField]
        private ElectricalNodeKind nodeKind = ElectricalNodeKind.Generic;

        [SerializeField]
        private ElectricalNodeRole roles = ElectricalNodeRole.Input | ElectricalNodeRole.Output;

        [Min(0f)]
        [SerializeField]
        private float maxWireDistance = 15f;

        [Min(1)]
        [SerializeField]
        private int maxConnections = 4;

        [SerializeField]
        private List<string> compatibleTags = new List<string>();

        [SerializeField]
        private List<string> blockedTags = new List<string>();

        [SerializeField]
        private List<string> connectionIds = new List<string>();

        public string NodeId
        {
            get
            {
                EnsureNodeId();
                return StableIdUtility.Normalize(nodeId);
            }
        }

        public ElectricalNodeKind NodeKind
        {
            get { return nodeKind; }
        }

        public ElectricalNodeRole Roles
        {
            get { return roles; }
        }

        public float MaxWireDistance
        {
            get { return Mathf.Max(0f, maxWireDistance); }
        }

        public int MaxConnections
        {
            get { return Mathf.Max(1, maxConnections); }
        }

        public Vector3 WorldPosition
        {
            get { return transform.position; }
        }

        public IReadOnlyList<string> ConnectionIds
        {
            get { return connectionIds ?? (IReadOnlyList<string>)Array.Empty<string>(); }
        }

        public bool CanStartConnection
        {
            get { return (roles & ElectricalNodeRole.Output) == ElectricalNodeRole.Output; }
        }

        public bool CanReceiveConnection
        {
            get { return (roles & ElectricalNodeRole.Input) == ElectricalNodeRole.Input; }
        }

        public bool CanConnectTo(ElectricalNode other, out string message)
        {
            if (other == null)
            {
                message = "Target electrical node is missing.";
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                message = "Cannot connect an electrical node to itself.";
                return false;
            }

            if (!CanStartConnection)
            {
                message = "Source node cannot start a wire connection.";
                return false;
            }

            if (!other.CanReceiveConnection)
            {
                message = "Target node cannot receive a wire connection.";
                return false;
            }

            if (ConnectionIds.Count >= MaxConnections)
            {
                message = "Source node has reached its connection limit.";
                return false;
            }

            if (other.ConnectionIds.Count >= other.MaxConnections)
            {
                message = "Target node has reached its connection limit.";
                return false;
            }

            float distance = Vector3.Distance(WorldPosition, other.WorldPosition);
            float maxDistance = Mathf.Min(MaxWireDistance, other.MaxWireDistance);
            if (distance > maxDistance)
            {
                message = $"Wire distance {distance:0.##} exceeds max range {maxDistance:0.##}.";
                return false;
            }

            if (!TagsCompatibleWith(other))
            {
                message = "Electrical node compatibility tags do not match.";
                return false;
            }

            message = "Electrical nodes are compatible.";
            return true;
        }

        public void AttachConnection(string connectionId)
        {
            EnsureList();
            string normalized = StableIdUtility.Normalize(connectionId);
            if (!string.IsNullOrEmpty(normalized) && !connectionIds.Contains(normalized))
            {
                connectionIds.Add(normalized);
            }
        }

        public void DetachConnection(string connectionId)
        {
            EnsureList();
            string normalized = StableIdUtility.Normalize(connectionId);
            for (int i = connectionIds.Count - 1; i >= 0; i--)
            {
                if (StableIdUtility.EqualsNormalized(connectionIds[i], normalized))
                {
                    connectionIds.RemoveAt(i);
                }
            }
        }

        private bool TagsCompatibleWith(ElectricalNode other)
        {
            if (HasAnyBlockedTag(other.compatibleTags) || other.HasAnyBlockedTag(compatibleTags))
            {
                return false;
            }

            if (!HasAnyCompatibleTags() || !other.HasAnyCompatibleTags())
            {
                return true;
            }

            for (int i = 0; i < compatibleTags.Count; i++)
            {
                for (int j = 0; j < other.compatibleTags.Count; j++)
                {
                    if (StableIdUtility.EqualsNormalized(compatibleTags[i], other.compatibleTags[j]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasAnyBlockedTag(IReadOnlyList<string> tags)
        {
            if (blockedTags == null || blockedTags.Count == 0 || tags == null)
            {
                return false;
            }

            for (int i = 0; i < blockedTags.Count; i++)
            {
                for (int j = 0; j < tags.Count; j++)
                {
                    if (StableIdUtility.EqualsNormalized(blockedTags[i], tags[j]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasAnyCompatibleTags()
        {
            return compatibleTags != null && compatibleTags.Count > 0;
        }

        private void Awake()
        {
            EnsureNodeId();
            EnsureList();
        }

        private void OnValidate()
        {
            maxWireDistance = Mathf.Max(0f, maxWireDistance);
            maxConnections = Mathf.Max(1, maxConnections);
            EnsureList();
        }

        private void EnsureNodeId()
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                nodeId = "node_" + Guid.NewGuid().ToString("N");
            }
        }

        private void EnsureList()
        {
            compatibleTags ??= new List<string>();
            blockedTags ??= new List<string>();
            connectionIds ??= new List<string>();
        }
    }
}
