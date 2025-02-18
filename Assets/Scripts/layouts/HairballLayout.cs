using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class HairballLayout : NetworkLayout
    {
        public Transform HairballPosition;
        NetworkDataStructure _network;
        NetworkContext3D _networkContext;

        // TODO remove this when we are able to calc at runtime
        NetworkFilesLoader _fileLoader;

        public override void Initialize(NetworkContext networkContext)
        {
            _networkContext = (NetworkContext3D)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _network = manager.NetworkData;
            _fileLoader = manager.FileLoader;
        }

        public override void ApplyLayout()
        {
            // TODO calculate at runtime
            foreach (var node in _fileLoader.HairballLayout.nodes)
            {
                _networkContext.Nodes[node.idx].Position = node._position3D;
            }

            _networkContext.CurrentTransform.position = HairballPosition.position;
            _networkContext.CurrentTransform.localScale = HairballPosition.localScale;
        }

        public override LayoutInterpolator GetInterpolator()
        {
            return new HairballInterpolator(HairballPosition, _network, _networkContext, _fileLoader);
        }
    }

    public class HairballInterpolator : LayoutInterpolator
    {
        NetworkContext3D _networkContext;
        Dictionary<int, Vector3> _startPositions = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> _endPositions = new Dictionary<int, Vector3>();

        TransformInfo _startingContextTransform;
        Transform _endingContextTransform;

        public HairballInterpolator(Transform endingContextTransform, NetworkDataStructure networkData, NetworkContext3D networkContext, NetworkFilesLoader fileLoader)
        {
            _networkContext = networkContext;
            // get actual array instead of the node collection so we can use list indices rather than 
            // their ids specified in data
            var nodes = networkData.Nodes.NodeArray;
            var nodeCount = nodes.Count;

            var hairballNodes = fileLoader.HairballLayout.nodes;
            var idToIdx = fileLoader.HairballLayout.idToIdx;

            for (int i = 0; i < nodeCount; i++)
            {
                var node = nodes[i];

                _startPositions[node.ID] = networkContext.Nodes[node.ID].Position;
                // TODO calculate at runtime
                _endPositions[node.ID] = hairballNodes[idToIdx[node.ID]]._position3D;
            }


            _startingContextTransform = new TransformInfo(networkContext.CurrentTransform);
            _endingContextTransform = endingContextTransform;
            Console.Write(_startingContextTransform.Position);
            Console.Write(_startingContextTransform.Scale);
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