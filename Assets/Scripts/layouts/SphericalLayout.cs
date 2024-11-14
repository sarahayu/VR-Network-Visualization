using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class SphericalLayout : NetworkLayout
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
            foreach (var node in _fileLoader.SphericalLayout.nodes)
            {
                _networkProperties.Nodes[node.idx].Position = node._position3D;
            }

            foreach (var link in _networkProperties.Links.Values)
            {
                link.State = NetworkContext3D.Link.LinkState.Normal;
            }
        }

        public override LayoutInterpolator GetInterpolator()
        {
            return new SphericalInterpolator(_network, _networkProperties, _fileLoader);
        }
    }

    public class SphericalInterpolator : LayoutInterpolator
    {
        NetworkContext3D _networkProperties;
        Dictionary<int, Vector3> _startPositions = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> _endPositions = new Dictionary<int, Vector3>();

        public SphericalInterpolator(NetworkDataStructure networkData, NetworkContext3D networkProperties, NetworkFilesLoader fileLoader)
        {
            _networkProperties = networkProperties;
            // get actual array instead of the node collection so we can use list indices rather than 
            // their ids specified in data
            var nodes = networkData.Nodes.NodeArray;
            var nodeCount = nodes.Count;

            var sphericalNodes = fileLoader.SphericalLayout.nodes;
            var idToIdx = fileLoader.SphericalLayout.idToIdx;

            for (int i = 0; i < nodeCount; i++)
            {
                var node = nodes[i];

                _startPositions[node.id] = networkProperties.Nodes[node.id].Position;
                // TODO calculate at runtime
                _endPositions[node.id] = sphericalNodes[idToIdx[node.id]]._position3D;
            }

            foreach (var link in _networkProperties.Links.Values)
            {
                link.State = NetworkContext3D.Link.LinkState.Normal;
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