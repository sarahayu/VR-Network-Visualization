using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Assertions;

namespace VidiGraph
{
    public class BringNodeTransformer : NetworkContextTransformer
    {
        [SerializeField] float _targetSpread = 2f;
        [SerializeField] float _offset = 0.2f;

        NetworkGlobal _networkGlobal;
        NodeLinkContext _networkContext;

        // TODO remove this when we are able to calc at runtime
        NetworkFilesLoader _fileLoader;
        HashSet<int> _nodesToUpdate = new HashSet<int>();

        Transform _camera;

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkContext = (NodeLinkContext)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = manager.NetworkGlobal;
            _fileLoader = manager.FileLoader;

            _camera = GameObject.FindWithTag("MainCamera").transform;
        }

        public override void ApplyTransformation()
        {
            foreach (var nodeID in _nodesToUpdate)
            {
                if (_networkGlobal.Nodes[nodeID].IsVirtualNode) continue;

                var nodeContext = _networkContext.Nodes[nodeID];
                var nodePos = nodeContext.Position;
                nodeContext.Position = BringNodeUtils.GetDestinationPoint(nodePos, _camera, _targetSpread, _offset);
                nodeContext.Dirty = true;
                _networkContext.Communities[nodeContext.CommunityID].Dirty = true;
            }

            _nodesToUpdate.Clear();
        }

        public override TransformInterpolator GetInterpolator()
        {
            return new BringNodeInterpolator(_networkGlobal, _networkContext, _fileLoader, _nodesToUpdate, _camera, _targetSpread, _offset);
        }

        public void UpdateOnNextApply(int nodeID)
        {
            _nodesToUpdate.Add(nodeID);
        }

        public void UpdateOnNextApply(IEnumerable<int> nodeIDs)
        {
            _nodesToUpdate.UnionWith(nodeIDs);
        }
    }

    public class BringNodeInterpolator : TransformInterpolator
    {
        NodeLinkContext _networkContext;
        Dictionary<int, Vector3> _startPositions = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> _endPositions = new Dictionary<int, Vector3>();

        public BringNodeInterpolator(NetworkGlobal networkGlobal, NodeLinkContext networkContext,
            NetworkFilesLoader fileLoader, HashSet<int> nodesToUpdate, Transform camera, float targetSpread, float offset)
        {
            _networkContext = networkContext;

            foreach (var nodeID in nodesToUpdate)
            {
                if (networkGlobal.Nodes[nodeID].IsVirtualNode) continue;

                var nodePos = _networkContext.Nodes[nodeID].Position;
                var bringNodePos = BringNodeUtils.GetDestinationPoint(nodePos, camera, targetSpread, offset);
                _startPositions[nodeID] = nodePos;
                _endPositions[nodeID] = bringNodePos;
            }

            nodesToUpdate.Clear();
        }

        public override void Interpolate(float t)
        {
            foreach (var nodeID in _startPositions.Keys)
            {
                _networkContext.Nodes[nodeID].Position
                    = Vector3.Lerp(_startPositions[nodeID], _endPositions[nodeID], Mathf.SmoothStep(0f, 1f, t));
                _networkContext.Nodes[nodeID].Dirty = true;
                _networkContext.Communities[_networkContext.Nodes[nodeID].CommunityID].Dirty = true;
            }
        }
    }

}