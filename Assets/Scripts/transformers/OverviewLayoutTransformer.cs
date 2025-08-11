using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public class OverviewLayoutTransformer : NetworkContextTransformer
    {
        const int MAX_NODE_SELECT_AGE = 10;

        NetworkGlobal _networkGlobal;
        MinimapContext _networkContext;
        SurfaceManager _surfaceManager;
        NetworkManager _networkManager;
        MultiLayoutNetwork _mlNetwork;
        IEnumerable<BasicSubnetwork> _subnetworks;

        Dictionary<int, Dictionary<int, float>> _nodeSelectionTimes = new();

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkGlobal = networkGlobal;
            _networkContext = (MinimapContext)networkContext;
            _surfaceManager = GameObject.Find("/Surface Manager").GetComponent<SurfaceManager>();
            _networkManager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
        }

        public override void ApplyTransformation()
        {
            _networkContext.NodeRenderables.Clear();

            _networkContext.NodesDirty = true;

            var allRenderContexts = new HashSet<MultiLayoutContext>() { _mlNetwork.Context }.Union(_subnetworks.Select(subn => subn.Context));

            foreach (var context in allRenderContexts)
            {
                foreach (var (nodeID, node) in context.Nodes)
                {
                    if (_networkGlobal.Nodes[nodeID].IsVirtualNode) continue;
                    _networkContext.NodeRenderables[node.GUID] = new MinimapContext.Node()
                    {
                        ID = nodeID,
                        Position = node.Position,
                        Color = GetNodeColor(nodeID, subnetworkID: context.SubnetworkID),
                        Size = GetNodeSize(nodeID, subnetworkID: context.SubnetworkID)
                    };
                }
            }

            _networkContext.Surfaces.Clear();

            foreach (var (surfID, surf) in _surfaceManager.Surfaces)
            {
                _networkContext.Surfaces[surfID] = new MinimapContext.Surface()
                {
                    Position = surf.transform.position,
                    Rotation = surf.transform.rotation,
                };
            }
        }

        public void UpdateData(MultiLayoutNetwork mlNetwork, IEnumerable<BasicSubnetwork> subnetworks)
        {
            _mlNetwork = mlNetwork;
            _subnetworks = subnetworks;
        }

        public void PushSelectionEvent(IEnumerable<int> nodes, int subnetworkID = -1)
        {
            if (!_nodeSelectionTimes.ContainsKey(subnetworkID)) _nodeSelectionTimes[subnetworkID] = new();
            foreach (var nodeID in _nodeSelectionTimes[subnetworkID].Keys.ToList()) _nodeSelectionTimes[subnetworkID][nodeID] += 1;
            foreach (var nodeID in nodes) _nodeSelectionTimes[subnetworkID][nodeID] = 0;
        }

        Color GetNodeColor(int nodeID, int subnetworkID)
        {
            if (!_nodeSelectionTimes.ContainsKey(subnetworkID))
                return Color.gray;

            var subNodeTimes = _nodeSelectionTimes[subnetworkID];

            if (!subNodeTimes.ContainsKey(nodeID))
                return Color.gray;

            var lerped = Mathf.Min(1, subNodeTimes[nodeID] / MAX_NODE_SELECT_AGE);
            return Color.Lerp(Color.blue, Color.white, lerped);
        }

        int GetNodeSize(int nodeID, int subnetworkID)
        {
            if (!_nodeSelectionTimes.ContainsKey(subnetworkID))
                return 1;

            var subNodeTimes = _nodeSelectionTimes[subnetworkID];

            if (!subNodeTimes.ContainsKey(nodeID))
                return 1;

            return 3;
        }
    }
}