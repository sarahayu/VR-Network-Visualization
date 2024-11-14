using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace VidiGraph
{
    public class SpiderLayout : NetworkLayout
    {
        NetworkDataStructure _network;
        NetworkContext3D _networkProperties;

        // TODO remove this when we are able to calc at runtime
        NetworkFilesLoader _fileLoader;
        // use hashset to prevent duplicates
        HashSet<int> _focusCommunities = new HashSet<int>();

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
            var spiderNodes = _fileLoader.SpiderData.nodes;
            var spiderIdToIdx = _fileLoader.SpiderData.idToIdx;

            var sphericalNodes = _fileLoader.SphericalLayout.nodes;
            var sphericalIdToIdx = _fileLoader.SphericalLayout.idToIdx;

            foreach (var (communityIdx, community) in _network.Communities)
            {
                // reset to original position
                if (community.focus && !_focusCommunities.Contains(communityIdx))
                {
                    community.focus = false;

                    foreach (var node in community.communityNodes)
                    {
                        var sphericalPos = sphericalNodes[sphericalIdToIdx[node.id]]._position3D;
                        _networkProperties.Nodes[node.id].Position = sphericalPos;
                    }
                }
                // move to spider position
                else if (!community.focus && _focusCommunities.Contains(communityIdx))
                {
                    community.focus = true;

                    foreach (var node in community.communityNodes)
                    {
                        var spiderPos = spiderNodes[spiderIdToIdx[node.id]].spiderPos;
                        _networkProperties.Nodes[node.id].Position = new Vector3(spiderPos.x, spiderPos.y, spiderPos.z);
                    }
                }
            }

            // change all links to normal if no communities selected
            if (_focusCommunities.Count == 0)
            {
                foreach (var link in _networkProperties.Links.Values)
                {
                    link.State = NetworkContext3D.Link.LinkState.Normal;
                }
            }
            // otherwise apply different link states depending on relation to selected communities
            else
            {
                foreach (var link in _network.Links)
                {
                    var a = _network.Communities[link.sourceNode.communityIdx];
                    var b = _network.Communities[link.targetNode.communityIdx];
                    if (a.focus && b.focus)
                    {
                        _networkProperties.Links[link.linkIdx].State = NetworkContext3D.Link.LinkState.Normal;
                    }
                    else if (a.focus || b.focus)
                    {
                        _networkProperties.Links[link.linkIdx].State = NetworkContext3D.Link.LinkState.Focus2Context;
                    }
                    else
                    {
                        _networkProperties.Links[link.linkIdx].State = NetworkContext3D.Link.LinkState.Context;
                    }
                }
            }
        }

        public override LayoutInterpolator GetInterpolator()
        {
            return new SpiderInterpolator(_network, _networkProperties, _fileLoader, _focusCommunities);
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
        NetworkContext3D _networkProperties;
        Dictionary<int, Vector3> _startPositions = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> _endPositions = new Dictionary<int, Vector3>();

        public SpiderInterpolator(NetworkDataStructure networkData, NetworkContext3D networkProperties,
            NetworkFilesLoader fileLoader, HashSet<int> focusCommunities)
        {
            _networkProperties = networkProperties;

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
                        _startPositions[node.id] = networkProperties.Nodes[node.id].Position;
                        // TODO calculate at runtime
                        _endPositions[node.id] = sphericalNodes[sphericalIdToIdx[node.id]]._position3D;
                    }
                }
                // move to spider position
                else if (!community.focus && focusCommunities.Contains(communityIdx))
                {
                    community.focus = true;

                    foreach (var node in community.communityNodes)
                    {
                        _startPositions[node.id] = networkProperties.Nodes[node.id].Position;
                        // TODO calculate at runtime
                        var spiderPos = spiderNodes[spiderIdToIdx[node.id]].spiderPos;
                        _endPositions[node.id] = new Vector3(spiderPos.x, spiderPos.y, spiderPos.z);
                    }
                }
            }

            // change all links to normal if no communities selected
            if (focusCommunities.Count == 0)
            {
                foreach (var link in _networkProperties.Links.Values)
                {
                    link.State = NetworkContext3D.Link.LinkState.Normal;
                }
            }
            // otherwise apply different link states depending on relation to selected communities
            else
            {
                foreach (var link in networkData.Links)
                {
                    var a = networkData.Communities[link.sourceNode.communityIdx];
                    var b = networkData.Communities[link.targetNode.communityIdx];
                    if (a.focus && b.focus)
                    {
                        _networkProperties.Links[link.linkIdx].State = NetworkContext3D.Link.LinkState.Normal;
                    }
                    else if (a.focus || b.focus)
                    {
                        _networkProperties.Links[link.linkIdx].State = NetworkContext3D.Link.LinkState.Focus2Context;
                    }
                    else
                    {
                        _networkProperties.Links[link.linkIdx].State = NetworkContext3D.Link.LinkState.Context;
                    }
                }
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