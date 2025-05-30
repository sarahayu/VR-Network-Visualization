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
        public Transform BringNodePosition;

        NetworkGlobal _networkGlobal;
        MultiLayoutContext _networkContext;

        // TODO remove this when we are able to calc at runtime
        NetworkFilesLoader _fileLoader;
        HashSet<int> _nodesToUpdate = new HashSet<int>();
        TransformInfo _bringNodeTransform;

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkContext = (MultiLayoutContext)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = manager.NetworkGlobal;
            _fileLoader = manager.FileLoader;
            _bringNodeTransform = new TransformInfo(BringNodePosition);
        }

        public override void ApplyTransformation()
        {
            // TODO calculate at runtime
            var sphericalNodes = _fileLoader.SphericalLayout.nodes;
            var sphericalIdToIdx = _fileLoader.SphericalLayout.idToIdx;

            foreach (var nodeID in _nodesToUpdate)
            {
                var bringNodePos = sphericalNodes[sphericalIdToIdx[nodeID]]._position3D * 0.2f;
                _networkContext.Nodes[nodeID].Position = _bringNodeTransform.TransformPoint(new Vector3(bringNodePos.x, bringNodePos.y, bringNodePos.z));
                _networkContext.Nodes[nodeID].Dirty = true;
            }

            _nodesToUpdate.Clear();
        }

        public override TransformInterpolator GetInterpolator()
        {
            return new BringNodeInterpolator(_bringNodeTransform, _networkGlobal, _networkContext, _fileLoader, _nodesToUpdate);
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

        public BringNodeInterpolator(TransformInfo endingContextTransform, NetworkGlobal networkGlobal, MultiLayoutContext networkContext,
            NetworkFilesLoader fileLoader, HashSet<int> nodesToUpdate)
        {
            _networkContext = networkContext;
            var sphericalNodes = fileLoader.SphericalLayout.nodes;
            var sphericalIdToIdx = fileLoader.SphericalLayout.idToIdx;

            foreach (var nodeID in nodesToUpdate)
            {
                _startPositions[nodeID] = networkContext.Nodes[nodeID].Position;
                // TODO calculate at runtime
                var bringNodePos = sphericalNodes[sphericalIdToIdx[nodeID]]._position3D * 0.2f;
                _endPositions[nodeID] = endingContextTransform.TransformPoint(new Vector3(bringNodePos.x, bringNodePos.y, bringNodePos.z));
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