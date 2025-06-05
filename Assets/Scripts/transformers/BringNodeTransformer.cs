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
        [SerializeField]
        float _targetSpread = 2f;
        [SerializeField]
        float _offset = 0.2f;

        NetworkGlobal _networkGlobal;
        MultiLayoutContext _networkContext;

        // TODO remove this when we are able to calc at runtime
        NetworkFilesLoader _fileLoader;
        HashSet<int> _nodesToUpdate = new HashSet<int>();

        Transform _camera;

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkContext = (MultiLayoutContext)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = manager.NetworkGlobal;
            _fileLoader = manager.FileLoader;

            _camera = GameObject.FindWithTag("MainCamera").transform;
        }

        public override void ApplyTransformation()
        {
            var focalPoint = BringNodeUtils.GetFocalPoint(_camera, _targetSpread);

            foreach (var nodeID in _nodesToUpdate)
            {
                var nodePos = _networkContext.Nodes[nodeID].Position;
                _networkContext.Nodes[nodeID].Position = BringNodeUtils.GetDestinationPoint(focalPoint, nodePos, _targetSpread, _offset);
                _networkContext.Nodes[nodeID].Dirty = true;
            }

            _nodesToUpdate.Clear();
        }

        public override TransformInterpolator GetInterpolator()
        {
            var playerPos = _camera.position;
            var focalPoint = playerPos - _camera.forward * _targetSpread;
            return new BringNodeInterpolator(_networkGlobal, _networkContext, _fileLoader, _nodesToUpdate, focalPoint, _targetSpread, _offset);
        }

        public void UpdateOnNextApply(int nodeID)
        {
            _nodesToUpdate.Add(nodeID);
        }

        public void UpdateOnNextApply(List<int> nodeIDs)
        {
            _nodesToUpdate.UnionWith(nodeIDs);
        }
    }

    public class BringNodeInterpolator : TransformInterpolator
    {
        MultiLayoutContext _networkContext;
        Dictionary<int, Vector3> _startPositions = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> _endPositions = new Dictionary<int, Vector3>();

        public BringNodeInterpolator(NetworkGlobal networkGlobal, MultiLayoutContext networkContext,
            NetworkFilesLoader fileLoader, HashSet<int> nodesToUpdate, Vector3 focalPoint, float targetSpread, float offset)
        {
            _networkContext = networkContext;

            foreach (var nodeID in nodesToUpdate)
            {
                var nodePos = _networkContext.Nodes[nodeID].Position;
                var bringNodePos = BringNodeUtils.GetDestinationPoint(focalPoint, nodePos, targetSpread, offset);
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
            }

            // GameObjectUtils.LerpTransform(_networkContext.CurrentTransform, _startingContextTransform, _endingContextTransform, t);
        }
    }

}