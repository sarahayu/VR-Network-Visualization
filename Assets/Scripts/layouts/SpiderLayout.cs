using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace VidiGraph
{
    public class SpiderLayout : NetworkLayout
    {
        NetworkDataStructure _network;
        int _prevFocusCommunity = -1;
        int _focusCommunity = -1;

        public override void Initialize()
        {
            _network = GetComponentInParent<NetworkDataStructure>();
        }

        public override void ApplyLayout()
        {
            // TODO calculate at runtime
            var fileLoader = GetComponentInParent<NetworkFilesLoader>();

            var spiderNodes = fileLoader.SpiderData.nodes;
            var idToIdx = fileLoader.SpiderData.idToIdx;

            foreach (var node in _network.Communities[_focusCommunity].communityNodes)
            {
                var spiderPos = spiderNodes[idToIdx[node.id]].spiderPos;
                _network.Nodes[node.id].Position3D = new Vector3(spiderPos.x, spiderPos.y, spiderPos.z);
            }
        }

        public override LayoutInterpolator GetInterpolator()
        {
            var fileLoader = GetComponentInParent<NetworkFilesLoader>();

            return new SpiderInterpolator(_network, fileLoader, _focusCommunity);
        }

        // TODO change focusCommunity to array to allow multiple focusCommunitys
        public void SetFocusCommunity(int focusCommunity)
        {
            _prevFocusCommunity = _focusCommunity;
            _focusCommunity = focusCommunity;
        }
    }

    public class SpiderInterpolator : LayoutInterpolator
    {
        List<Node> _nodes;
        List<Vector3> _startPositions;
        List<Vector3> _endPositions;

        public SpiderInterpolator(NetworkDataStructure networkData, NetworkFilesLoader fileLoader, int focusCommunity)
        {
            _nodes = networkData.Communities[focusCommunity].communityNodes;
            var nodeCount = _nodes.Count;

            _startPositions = new List<Vector3>(nodeCount);
            _endPositions = new List<Vector3>(nodeCount);

            var spiderNodes = fileLoader.SpiderData.nodes;
            var idToIdx = fileLoader.SpiderData.idToIdx;

            for (int i = 0; i < nodeCount; i++)
            {
                var node = _nodes[i];

                _startPositions.Add(node.Position3D);
                // TODO calculate at runtime
                var spiderPos = spiderNodes[idToIdx[node.id]].spiderPos;
                _endPositions.Add(new Vector3(spiderPos.x, spiderPos.y, spiderPos.z));
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