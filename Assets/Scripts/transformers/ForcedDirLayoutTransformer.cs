using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public class ForcedDirLayoutTransformer : NetworkContextTransformer
    {
        public Transform ForcedDirPosition;

        NetworkGlobal _networkGlobal;
        MultiLayoutContext _networkContext;
        TransformInfo _ForcedDirTransform;

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkContext = (MultiLayoutContext)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = manager.NetworkGlobal;
            _ForcedDirTransform = new TransformInfo(ForcedDirPosition);
        }

        public override void ApplyTransformation()
        {
            foreach (var (nodeID, nodeContext) in _networkContext.Nodes)
            {
                var nodeGlobal = _networkGlobal.Nodes[nodeID];

                if (nodeGlobal.IsVirtualNode) continue;

                nodeContext.Position = _ForcedDirTransform.TransformPoint(UnityEngine.Random.insideUnitSphere);
                nodeContext.Dirty = true;

                _networkContext.Communities[nodeContext.CommunityID].Dirty = true;
            }
        }

        public override TransformInterpolator GetInterpolator()
        {
            return new ForcedDirLayoutInterpolator(_ForcedDirTransform, _networkGlobal, _networkContext);
        }
    }

    public class ForcedDirLayoutInterpolator : TransformInterpolator
    {
        MultiLayoutContext _networkContext;
        Dictionary<int, Vector3> _startPositions = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> _endPositions = new Dictionary<int, Vector3>();

        public ForcedDirLayoutInterpolator(TransformInfo endingContextTransform, NetworkGlobal networkGlobal,
            MultiLayoutContext networkContext)
        {
            _networkContext = networkContext;

            foreach (var (nodeID, nodeContext) in _networkContext.Nodes)
            {
                if (networkGlobal.Nodes[nodeID].IsVirtualNode) continue;

                _startPositions[nodeID] = nodeContext.Position;
                _endPositions[nodeID] = endingContextTransform.TransformPoint(UnityEngine.Random.insideUnitSphere);
            }
        }

        public override void Interpolate(float t)
        {
            foreach (var nodeID in _startPositions.Keys)
            {
                _networkContext.Nodes[nodeID].Position
                    = Vector3.Lerp(_startPositions[nodeID], _endPositions[nodeID], Mathf.SmoothStep(0f, 1f, t));
                _networkContext.Nodes[nodeID].Dirty = true;

                if (_networkContext.Nodes[nodeID].CommunityID != -1)
                    _networkContext.Communities[_networkContext.Nodes[nodeID].CommunityID].Dirty = true;
            }
        }
    }

}