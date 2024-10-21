using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace VidiGraph
{
    public class SpiderLayout : NetworkLayout
    {
        NetworkDataStructure _network;
        // use hashset to prevent duplicates
        HashSet<int> _focusCommunities = new HashSet<int>();

        public override void Initialize()
        {
            _network = GetComponentInParent<NetworkDataStructure>();
        }

        public override void ApplyLayout()
        {
            // TODO calculate at runtime
            var fileLoader = GetComponentInParent<NetworkFilesLoader>();

            var spiderNodes = fileLoader.SpiderData.nodes;
            var spiderIdToIdx = fileLoader.SpiderData.idToIdx;

            var sphericalNodes = fileLoader.SphericalLayout.nodes;
            var sphericalIdToIdx = fileLoader.SphericalLayout.idToIdx;

            foreach (var (communityIdx, community) in _network.Communities)
            {
                // reset to original position
                if (community.focus && !_focusCommunities.Contains(communityIdx))
                {
                    community.focus = false;

                    foreach (var node in community.communityNodes)
                    {
                        var sphericalPos = sphericalNodes[sphericalIdToIdx[node.id]]._position3D;
                        _network.Nodes[node.id].Position3D = sphericalPos;
                    }
                }
                // move to spider position
                else if (!community.focus && _focusCommunities.Contains(communityIdx))
                {
                    community.focus = true;

                    foreach (var node in community.communityNodes)
                    {
                        var spiderPos = spiderNodes[spiderIdToIdx[node.id]].spiderPos;
                        _network.Nodes[node.id].Position3D = new Vector3(spiderPos.x, spiderPos.y, spiderPos.z);
                    }
                }
            }

            if (_focusCommunities.Count == 0)
            {
                foreach (var link in _network.Links)
                {
                    link.state.SetLinkState(LinkState.Normal);
                }
            }
            else
            {
                foreach (var link in _network.Links)
                {
                    var a = _network.Communities[link.sourceNode.communityIdx];
                    var b = _network.Communities[link.targetNode.communityIdx];
                    if (a.focus && b.focus)
                    {
                        link.state.SetLinkState(LinkState.Focus);
                    }
                    else if (a.focus || b.focus)
                    {
                        link.state.SetLinkState(LinkState.Focus2Context);
                    }
                    else
                    {
                        link.state.SetLinkState(LinkState.Context);
                    }
                }
            }
        }

        public override LayoutInterpolator GetInterpolator()
        {
            var fileLoader = GetComponentInParent<NetworkFilesLoader>();

            return new SpiderInterpolator(_network, fileLoader, _focusCommunities);
        }

        public void SetFocusCommunity(int focusCommunity, bool isFocused)
        {
            if (isFocused)
            {
                _focusCommunities.Add(focusCommunity);
            }
            else
            {
                _focusCommunities.Remove(focusCommunity);
            }
        }
    }

    public class SpiderInterpolator : LayoutInterpolator
    {
        List<Node> _nodes;
        List<Vector3> _startPositions;
        List<Vector3> _endPositions;

        public SpiderInterpolator(NetworkDataStructure networkData, NetworkFilesLoader fileLoader, HashSet<int> focusCommunities)
        {
            _nodes = new List<Node>();

            _startPositions = new List<Vector3>();
            _endPositions = new List<Vector3>();

            var spiderNodes = fileLoader.SpiderData.nodes;
            var spiderIdToIdx = fileLoader.SpiderData.idToIdx;

            var sphericalNodes = fileLoader.SphericalLayout.nodes;
            var sphericalIdToIdx = fileLoader.SphericalLayout.idToIdx;

            foreach (var (communityIdx, community) in networkData.Communities)
            {
                // reset to original position
                if (community.focus && !focusCommunities.Contains(communityIdx))
                {
                    community.focus = false;

                    foreach (var node in community.communityNodes)
                    {
                        _nodes.Add(node);
                        _startPositions.Add(networkData.Nodes[node.id].Position3D);
                        _endPositions.Add(sphericalNodes[sphericalIdToIdx[node.id]]._position3D);
                    }
                }
                // move to spider position
                else if (!community.focus && focusCommunities.Contains(communityIdx))
                {
                    community.focus = true;

                    foreach (var node in community.communityNodes)
                    {
                        _nodes.Add(node);
                        _startPositions.Add(networkData.Nodes[node.id].Position3D);
                        var spiderPos = spiderNodes[spiderIdToIdx[node.id]].spiderPos;
                        _endPositions.Add(new Vector3(spiderPos.x, spiderPos.y, spiderPos.z));
                    }
                }
            }

            if (focusCommunities.Count == 0)
            {
                foreach (var link in networkData.Links)
                {
                    link.state.SetLinkState(LinkState.Normal);
                }
            }
            else
            {
                foreach (var link in networkData.Links)
                {
                    var a = networkData.Communities[link.sourceNode.communityIdx];
                    var b = networkData.Communities[link.targetNode.communityIdx];
                    if (a.focus && b.focus)
                    {
                        link.state.SetLinkState(LinkState.Focus);
                    }
                    else if (a.focus || b.focus)
                    {
                        link.state.SetLinkState(LinkState.Focus2Context);
                    }
                    else
                    {
                        link.state.SetLinkState(LinkState.Context);
                    }
                }
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