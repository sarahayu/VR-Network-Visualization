using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class HairballLayout : NetworkLayout
    {
        NetworkDataStructure _network;

        public override void Initialize()
        {
            _network = GetComponentInParent<NetworkDataStructure>();
        }

        public override void ApplyLayout()
        {
            // TODO calculate at runtime
            var fileLoader = GetComponentInParent<NetworkFilesLoader>();

            foreach (var node in fileLoader.HairballLayout.nodes)
            {
                _network.Nodes[node.idx].Position3D = node._position3D;
            }
        }

        public override LayoutInterpolator GetInterpolator()
        {
            var fileLoader = GetComponentInParent<NetworkFilesLoader>();

            return new HairballInterpolator(_network, fileLoader);
        }
    }

    public class HairballInterpolator : LayoutInterpolator
    {
        List<Node> _nodes;
        List<Vector3> _startPositions;
        List<Vector3> _endPositions;

        public HairballInterpolator(NetworkDataStructure networkData, NetworkFilesLoader fileLoader)
        {
            // get actual array instead of the node collection so we can use list indices rather than 
            // their ids specified in data
            _nodes = networkData.Nodes.NodeArray;
            var nodeCount = _nodes.Count;
            var nodeIdToIndex = networkData.Nodes.IdToIndex;

            _startPositions = new List<Vector3>(nodeCount);
            _endPositions = new List<Vector3>(nodeCount);

            for (int i = 0; i < nodeCount; i++)
            {
                var node = _nodes[i];

                _startPositions.Add(node.Position3D);
                // TODO calculate at runtime
                _endPositions.Add(fileLoader.HairballLayout.nodes[nodeIdToIndex[node.id]]._position3D);
            }
        }

        public override void Interpolate(float t)
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i].Position3D = Vector3.Lerp(_startPositions[i], _endPositions[i], Mathf.SmoothStep(0f, 1f, t));
            }
        }
    }

}