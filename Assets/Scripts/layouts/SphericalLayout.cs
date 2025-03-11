using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class SphericalLayout : NetworkLayout
    {
        public Transform SphericalPosition;

        NetworkGlobal _networkGlobal;
        NetworkContext3D _networkContext;
        TransformInfo _sphericalTransform;

        // TODO remove this when we are able to calc at runtime
        NetworkFilesLoader _fileLoader;

        public override void Initialize(NetworkContext networkContext)
        {
            _networkContext = (NetworkContext3D)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = manager.NetworkGlobal;
            _fileLoader = manager.FileLoader;
            _sphericalTransform = new TransformInfo(SphericalPosition);
        }

        public override void ApplyLayout()
        {
            // TODO calculate at runtime
            foreach (var node in _fileLoader.SphericalLayout.nodes)
            {
                _networkContext.Nodes[node.idx].Position = node._position3D;
            }

            _networkContext.CurrentTransform.SetFromTransform(_sphericalTransform);
        }

        public override LayoutInterpolator GetInterpolator()
        {
            return new SphericalInterpolator(_sphericalTransform, _networkGlobal, _networkContext, _fileLoader);
        }
    }

    public class SphericalInterpolator : LayoutInterpolator
    {
        NetworkContext3D _networkContext;
        Dictionary<int, Vector3> _startPositions = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> _endPositions = new Dictionary<int, Vector3>();

        TransformInfo _startingContextTransform;
        TransformInfo _endingContextTransform;

        public SphericalInterpolator(TransformInfo endingContextTransform, NetworkGlobal networkGlobal, NetworkContext3D networkContext, NetworkFilesLoader fileLoader)
        {
            _networkContext = networkContext;
            // get actual array instead of the node collection so we can use list indices rather than 
            // their ids specified in data
            var nodes = networkGlobal.Nodes.NodeArray;
            var nodeCount = nodes.Count;

            var sphericalNodes = fileLoader.SphericalLayout.nodes;
            var idToIdx = fileLoader.SphericalLayout.idToIdx;

            for (int i = 0; i < nodeCount; i++)
            {
                var node = nodes[i];

                _startPositions[node.ID] = networkContext.Nodes[node.ID].Position;
                // TODO calculate at runtime
                _endPositions[node.ID] = sphericalNodes[idToIdx[node.ID]]._position3D;
            }

            _startingContextTransform = networkContext.CurrentTransform.Copy();
            _endingContextTransform = endingContextTransform;
        }

        public override void Interpolate(float t)
        {
            foreach (var nodeID in _startPositions.Keys)
            {
                _networkContext.Nodes[nodeID].Position
                    = Vector3.Lerp(_startPositions[nodeID], _endPositions[nodeID], Mathf.SmoothStep(0f, 1f, t));
            }

            GameObjectUtils.LerpTransform(_networkContext.CurrentTransform, _startingContextTransform, _endingContextTransform, t);
        }
    }

}