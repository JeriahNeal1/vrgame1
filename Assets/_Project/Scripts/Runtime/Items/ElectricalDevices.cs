using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VRGame.Runtime
{
    [DisallowMultipleComponent]
    public sealed class ElectricalGenerator : MonoBehaviour
    {
        [SerializeField]
        private bool active = true;

        [Min(0f)]
        [SerializeField]
        private float outputWatts = 1000f;

        [SerializeField]
        private List<ElectricalNode> outputNodes = new List<ElectricalNode>();

        public bool Active
        {
            get { return active; }
        }

        public float OutputWatts
        {
            get { return Mathf.Max(0f, outputWatts); }
        }

        public IReadOnlyList<ElectricalNode> OutputNodes
        {
            get
            {
                EnsureNodes();
                return outputNodes;
            }
        }

        public void SetActive(bool isActive)
        {
            active = isActive;
        }

        private void Awake()
        {
            EnsureNodes();
        }

        private void OnValidate()
        {
            outputWatts = Mathf.Max(0f, outputWatts);
            EnsureNodes();
        }

        private void EnsureNodes()
        {
            outputNodes ??= new List<ElectricalNode>();
            ElectricalNode[] nodes = GetComponentsInChildren<ElectricalNode>(true);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] != null && !outputNodes.Contains(nodes[i]) && nodes[i].CanStartConnection)
                {
                    outputNodes.Add(nodes[i]);
                }
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class ElectricalSwitch : MonoBehaviour
    {
        [SerializeField]
        private bool closed = true;

        [SerializeField]
        private ElectricalNode inputNode = null;

        [SerializeField]
        private ElectricalNode outputNode = null;

        [SerializeField]
        private UnityEvent onSwitchChanged = new UnityEvent();

        public bool Closed
        {
            get { return closed; }
        }

        public ElectricalNode InputNode
        {
            get { return inputNode; }
        }

        public ElectricalNode OutputNode
        {
            get { return outputNode; }
        }

        public void SetClosed(bool isClosed)
        {
            if (closed == isClosed)
            {
                return;
            }

            closed = isClosed;
            onSwitchChanged.Invoke();
        }

        public void Toggle()
        {
            SetClosed(!closed);
        }
    }

    [DisallowMultipleComponent]
    public sealed class ElectricalDiode : MonoBehaviour
    {
        [SerializeField]
        private ElectricalNode inputNode = null;

        [SerializeField]
        private ElectricalNode outputNode = null;

        public ElectricalNode InputNode
        {
            get { return inputNode; }
        }

        public ElectricalNode OutputNode
        {
            get { return outputNode; }
        }

        public bool AllowsFlow(ElectricalNode fromNode, ElectricalNode toNode)
        {
            return fromNode != null &&
                   toNode != null &&
                   fromNode == inputNode &&
                   toNode == outputNode;
        }
    }
}
