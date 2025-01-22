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
        Dictionary<int, bool> _focusCommunitiesToUpdate = new Dictionary<int, bool>();

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
                if (!_focusCommunitiesToUpdate.ContainsKey(communityIdx)) continue;

                // move to spider position
                if (_focusCommunitiesToUpdate[communityIdx])
                {
                    foreach (var node in community.Nodes)
                    {
                        var spiderPos = spiderNodes[spiderIdToIdx[node.ID]].spiderPos;
                        _networkProperties.Nodes[node.ID].Position = new Vector3(spiderPos.x, spiderPos.y, spiderPos.z);
                    }

                    _focusCommunities.Add(communityIdx);
                }
                // reset to original position
                else
                {
                    foreach (var node in community.Nodes)
                    {
                        var sphericalPos = sphericalNodes[sphericalIdToIdx[node.ID]]._position3D;
                        _networkProperties.Nodes[node.ID].Position = sphericalPos;
                    }

                    _focusCommunities.Remove(communityIdx);
                }

                _focusCommunitiesToUpdate.Remove(communityIdx);
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
                    var a = _network.Communities[link.SourceNode.CommunityID];
                    var b = _network.Communities[link.TargetNode.CommunityID];
                    if (a.Focus && b.Focus)
                    {
                        _networkProperties.Links[link.ID].State = NetworkContext3D.Link.LinkState.Normal;
                    }
                    else if (a.Focus || b.Focus)
                    {
                        _networkProperties.Links[link.ID].State = NetworkContext3D.Link.LinkState.Focus2Context;
                    }
                    else
                    {
                        _networkProperties.Links[link.ID].State = NetworkContext3D.Link.LinkState.Context;
                    }
                }
            }
        }

        public override LayoutInterpolator GetInterpolator()
        {
            return new SpiderInterpolator(_network, _networkProperties, _fileLoader, _focusCommunities, _focusCommunitiesToUpdate);
        }

        public void SetFocusCommunity(int focusCommunity, bool isFocused)
        {
            if (isFocused != _focusCommunities.Contains(focusCommunity))
            {
                _focusCommunitiesToUpdate[focusCommunity] = isFocused;
            }
        }
    }

    public class SpiderInterpolator : LayoutInterpolator
    {
        NetworkContext3D _networkProperties;
        Dictionary<int, Vector3> _startPositions = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> _endPositions = new Dictionary<int, Vector3>();

        public SpiderInterpolator(NetworkDataStructure networkData, NetworkContext3D networkProperties,
            NetworkFilesLoader fileLoader, HashSet<int> focusCommunities, Dictionary<int, bool> focusCommunitiesToUpdate)
        {
            _networkProperties = networkProperties;

            var spiderNodes = fileLoader.SpiderData.nodes;
            var spiderIdToIdx = fileLoader.SpiderData.idToIdx;

            var sphericalNodes = fileLoader.SphericalLayout.nodes;
            var sphericalIdToIdx = fileLoader.SphericalLayout.idToIdx;

            foreach (var (communityIdx, community) in networkData.Communities)
            {
                if (!focusCommunitiesToUpdate.ContainsKey(communityIdx)) continue;

                // move to spider position
                if (focusCommunitiesToUpdate[communityIdx])
                {
                    foreach (var node in community.Nodes)
                    {
                        _startPositions[node.ID] = networkProperties.Nodes[node.ID].Position;
                        // TODO calculate at runtime
                        var spiderPos = spiderNodes[spiderIdToIdx[node.ID]].spiderPos;
                        _endPositions[node.ID] = new Vector3(spiderPos.x, spiderPos.y, spiderPos.z);
                    }

                    focusCommunities.Add(communityIdx);
                }
                // reset to original position
                else
                {
                    foreach (var node in community.Nodes)
                    {
                        _startPositions[node.ID] = networkProperties.Nodes[node.ID].Position;
                        // TODO calculate at runtime
                        _endPositions[node.ID] = sphericalNodes[sphericalIdToIdx[node.ID]]._position3D;
                    }

                    focusCommunities.Remove(communityIdx);
                }

                focusCommunitiesToUpdate.Remove(communityIdx);
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
                    var a = networkData.Communities[link.SourceNode.CommunityID];
                    var b = networkData.Communities[link.TargetNode.CommunityID];
                    if (a.Focus && b.Focus)
                    {
                        _networkProperties.Links[link.ID].State = NetworkContext3D.Link.LinkState.Normal;
                    }
                    else if (a.Focus || b.Focus)
                    {
                        _networkProperties.Links[link.ID].State = NetworkContext3D.Link.LinkState.Focus2Context;
                    }
                    else
                    {
                        _networkProperties.Links[link.ID].State = NetworkContext3D.Link.LinkState.Context;
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