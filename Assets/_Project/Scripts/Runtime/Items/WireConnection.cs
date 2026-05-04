using System;
using System.Collections.Generic;
using UnityEngine;
using VRGame.Items;

namespace VRGame.Runtime
{
    [Serializable]
    public sealed class WireConnection
    {
        [SerializeField]
        private string connectionId = string.Empty;

        [SerializeField]
        private string ownerId = string.Empty;

        [SerializeField]
        private ItemDefId wireItemDefId = default;

        [SerializeField]
        private string fromNodeId = string.Empty;

        [SerializeField]
        private string toNodeId = string.Empty;

        [SerializeField]
        private Vector3 fromPosition = Vector3.zero;

        [SerializeField]
        private Vector3 toPosition = Vector3.zero;

        [SerializeField]
        private float length = 0f;

        public WireConnection(string ownerId, ItemDefId wireItemDefId, ElectricalNode fromNode, ElectricalNode toNode)
        {
            connectionId = CreateConnectionId();
            this.ownerId = StableIdUtility.Normalize(ownerId);
            this.wireItemDefId = wireItemDefId;
            fromNodeId = fromNode != null ? fromNode.NodeId : string.Empty;
            toNodeId = toNode != null ? toNode.NodeId : string.Empty;
            fromPosition = fromNode != null ? fromNode.WorldPosition : Vector3.zero;
            toPosition = toNode != null ? toNode.WorldPosition : Vector3.zero;
            length = Vector3.Distance(fromPosition, toPosition);
        }

        public string ConnectionId
        {
            get { return StableIdUtility.Normalize(connectionId); }
        }

        public string OwnerId
        {
            get { return StableIdUtility.Normalize(ownerId); }
        }

        public ItemDefId WireItemDefId
        {
            get { return wireItemDefId; }
        }

        public string FromNodeId
        {
            get { return StableIdUtility.Normalize(fromNodeId); }
        }

        public string ToNodeId
        {
            get { return StableIdUtility.Normalize(toNodeId); }
        }

        public Vector3 FromPosition
        {
            get { return fromPosition; }
        }

        public Vector3 ToPosition
        {
            get { return toPosition; }
        }

        public float Length
        {
            get { return Mathf.Max(0f, length); }
        }

        public static string CreateConnectionId()
        {
            return "wire_" + Guid.NewGuid().ToString("N");
        }
    }

    public sealed class WireConnectionResult
    {
        private WireConnectionResult(bool success, string message, WireConnection connection)
        {
            Success = success;
            Message = message ?? string.Empty;
            Connection = connection;
        }

        public bool Success { get; }

        public string Message { get; }

        public WireConnection Connection { get; }

        public static WireConnectionResult Succeeded(WireConnection connection, string message)
        {
            return new WireConnectionResult(true, message, connection);
        }

        public static WireConnectionResult Failed(string message)
        {
            return new WireConnectionResult(false, message, null);
        }
    }

    public sealed class ElectricalConnectionRegistry : MonoBehaviour
    {
        [SerializeField]
        private List<WireConnection> wireConnections = new List<WireConnection>();

        public IReadOnlyList<WireConnection> WireConnections
        {
            get { return wireConnections ?? (IReadOnlyList<WireConnection>)Array.Empty<WireConnection>(); }
        }

        public WireConnectionResult TryCreateConnection(ElectricalNode fromNode, ElectricalNode toNode, ItemDefId wireItemDefId, string ownerId)
        {
            if (fromNode == null || toNode == null)
            {
                return WireConnectionResult.Failed("Both electrical nodes are required.");
            }

            if (!fromNode.CanConnectTo(toNode, out string compatibilityMessage))
            {
                return WireConnectionResult.Failed(compatibilityMessage);
            }

            if (HasConnectionBetween(fromNode.NodeId, toNode.NodeId))
            {
                return WireConnectionResult.Failed($"A wire connection already exists between '{fromNode.NodeId}' and '{toNode.NodeId}'.");
            }

            WireConnection connection = new WireConnection(ownerId, wireItemDefId, fromNode, toNode);
            wireConnections ??= new List<WireConnection>();
            wireConnections.Add(connection);
            fromNode.AttachConnection(connection.ConnectionId);
            toNode.AttachConnection(connection.ConnectionId);
            return WireConnectionResult.Succeeded(connection, $"Created wire connection '{connection.ConnectionId}'.");
        }

        private bool HasConnectionBetween(string firstNodeId, string secondNodeId)
        {
            wireConnections ??= new List<WireConnection>();
            string first = StableIdUtility.Normalize(firstNodeId);
            string second = StableIdUtility.Normalize(secondNodeId);
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
            {
                return false;
            }

            for (int i = 0; i < wireConnections.Count; i++)
            {
                WireConnection connection = wireConnections[i];
                if (connection == null)
                {
                    continue;
                }

                bool sameDirection =
                    StableIdUtility.EqualsNormalized(connection.FromNodeId, first) &&
                    StableIdUtility.EqualsNormalized(connection.ToNodeId, second);
                bool reverseDirection =
                    StableIdUtility.EqualsNormalized(connection.FromNodeId, second) &&
                    StableIdUtility.EqualsNormalized(connection.ToNodeId, first);

                if (sameDirection || reverseDirection)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnValidate()
        {
            wireConnections ??= new List<WireConnection>();
        }
    }
}
