using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public class OverviewLayoutTransformer : NetworkContextTransformer
    {
        NetworkGlobal _networkGlobal;
        MinimapContext _networkContext;
        SurfaceManager _surfaceManager;
        NetworkManager _networkManager;
        MultiLayoutNetwork _mlNetwork;
        IEnumerable<BasicSubnetwork> _subnetworks;

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkGlobal = networkGlobal;
            _networkContext = (MinimapContext)networkContext;
            _surfaceManager = GameObject.Find("/Surface Manager").GetComponent<SurfaceManager>();
            _networkManager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
        }

        public override void ApplyTransformation()
        {
            _networkContext.Nodes.Clear();

            _networkContext.NodesDirty = true;

            foreach (var (nodeID, node) in _mlNetwork.Context.Nodes)
            {
                if (_networkGlobal.Nodes[nodeID].IsVirtualNode) continue;
                _networkContext.Nodes[nodeID] = new MinimapContext.Node()
                {
                    Position = node.Position,
                    Color = Color.white,
                };
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
    }
}