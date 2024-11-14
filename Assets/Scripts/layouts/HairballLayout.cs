using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class HairballLayout : NetworkLayout
    {
        NetworkDataStructure _network;
        NetworkContext3D _networkProperties;

        // TODO remove this when we are able to calc at runtime
        NetworkFilesLoader _fileLoader;

        public override void Initialize(NetworkContext networkContext)
        {
            _networkProperties = (NetworkContext3D)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _network = manager.Data;
            _fileLoader = manager.FileLoader;
        }

        public override void ApplyLayout()
        {
            // TODO calculate at runtime
            foreach (var node in _fileLoader.HairballLayout.nodes)
            {
                _networkProperties.Nodes[node.idx].Position = node._position3D;
            }
        }

        public override LayoutInterpolator GetInterpolator()
        {
            return new HairballInterpolator(_network, _networkProperties, _fileLoader);
        }
    }

    public class HairballInterpolator : LayoutInterpolator
    {
        NetworkContext3D _networkProperties;
        Dictionary<int, Vector3> _startPositions = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> _endPositions = new Dictionary<int, Vector3>();

        public HairballInterpolator(NetworkDataStructure networkData, NetworkContext3D networkProperties, NetworkFilesLoader fileLoader)
        {
            _networkProperties = networkProperties;
            // get actual array instead of the node collection so we can use list indices rather than 
            // their ids specified in data
            var nodes = networkData.Nodes.NodeArray;
            var nodeCount = nodes.Count;

            var hairballNodes = fileLoader.HairballLayout.nodes;
            var idToIdx = fileLoader.HairballLayout.idToIdx;

            for (int i = 0; i < nodeCount; i++)
            {
                var node = nodes[i];

                _startPositions[node.ID] = networkProperties.Nodes[node.ID].Position;
                // TODO calculate at runtime
                _endPositions[node.ID] = hairballNodes[idToIdx[node.ID]]._position3D;
            }
        }

        public override void Interpolate(float t)
        {
            foreach (var nodeID in _startPositions.Keys)
            {
                _networkProperties.Nodes[nodeID].Position
                    = Vector3.Lerp(_startPositions[nodeID], _endPositions[nodeID], Mathf.SmoothStep(0f, 1f, t));
            }
        }
    }

}