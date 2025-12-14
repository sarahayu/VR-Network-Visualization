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

        Dictionary<string, float> _nodeSelectionTimes = new();

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkGlobal = networkGlobal;
            _networkContext = (MinimapContext)networkContext;
            _surfaceManager = GameObject.Find("/Surface Manager")?.GetComponent<SurfaceManager>();
            _networkManager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
        }

        public override void ApplyTransformation()
        {
            if (_surfaceManager == null) return;

            _networkContext.NodeRenderables.Clear();

            _networkContext.NodesDirty = true;

            var allRenderContexts = new HashSet<NodeLinkContext>() { _mlNetwork.Context }.Union(_subnetworks.Select(subn => subn.Context));

            foreach (var context in allRenderContexts)
            {
                foreach (var (nodeID, node) in context.Nodes)
                {
                    if (_networkGlobal.Nodes[nodeID].IsVirtualNode) continue;
                    _networkContext.NodeRenderables[node.GUID] = new MinimapContext.Node()
                    {
                        ID = nodeID,
                        Position = node.Position,
                        Color = GetNodeColor(node.GUID),
                        Size = GetNodeSize(node.GUID)
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

        public void PushSelectionEvent(IEnumerable<string> nodes)
        {
            if (_surfaceManager == null) return;
            foreach (var nodeGUID in _nodeSelectionTimes.Keys.ToList()) _nodeSelectionTimes[nodeGUID] += 1;
            foreach (var nodeGUID in nodes) _nodeSelectionTimes[nodeGUID] = 0;
        }

        Color GetNodeColor(string nodeGUID)
        {
            if (!_nodeSelectionTimes.ContainsKey(nodeGUID))
                return Color.gray;

            var lerped = Mathf.Min(1, _nodeSelectionTimes[nodeGUID] / MAX_NODE_SELECT_AGE);
            return Color.Lerp(Color.blue, Color.white, lerped);
        }

        int GetNodeSize(string nodeGUID)
        {
            if (!_nodeSelectionTimes.ContainsKey(nodeGUID))
                return 1;

            return 3;
        }
    }
}