using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace VidiGraph
{
    public class SpiderLayoutTransformer : NetworkContextTransformer
    {
        public Transform SpiderPosition;

        NetworkGlobal _networkGlobal;
        MultiLayoutContext _networkContext;

        // TODO remove this when we are able to calc at runtime
        NetworkFilesLoader _fileLoader;
        // use hashset to prevent duplicates
        HashSet<int> _focusCommunities = new HashSet<int>();
        Dictionary<int, bool> _focusCommunitiesToUpdate = new Dictionary<int, bool>();
        TransformInfo _spiderTransform;

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkContext = (MultiLayoutContext)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = manager.NetworkGlobal;
            _fileLoader = manager.FileLoader;
            _spiderTransform = new TransformInfo(SpiderPosition);
        }

        public override void ApplyTransformation()
        {
            // TODO calculate at runtime
            var spiderNodes = _fileLoader.SpiderData.nodes;
            var spiderIdToIdx = _fileLoader.SpiderData.idToIdx;

            var sphericalNodes = _fileLoader.SphericalLayout.nodes;
            var sphericalIdToIdx = _fileLoader.SphericalLayout.idToIdx;

            foreach (var (communityIdx, community) in _networkGlobal.Communities)
            {
                if (!_focusCommunitiesToUpdate.ContainsKey(communityIdx)) continue;

                // move to spider position
                if (_focusCommunitiesToUpdate[communityIdx])
                {
                    foreach (var node in community.Nodes)
                    {
                        var spiderPos = spiderNodes[spiderIdToIdx[node.ID]].spiderPos;
                        _networkContext.Nodes[node.ID].Position = new Vector3(spiderPos.x, spiderPos.y, spiderPos.z);
                    }

                    _focusCommunities.Add(communityIdx);
                }
                // reset to original position
                else
                {
                    foreach (var node in community.Nodes)
                    {
                        var sphericalPos = sphericalNodes[sphericalIdToIdx[node.ID]]._position3D;
                        _networkContext.Nodes[node.ID].Position = sphericalPos;
                    }

                    _focusCommunities.Remove(communityIdx);
                }

                _focusCommunitiesToUpdate.Remove(communityIdx);
            }

            _networkContext.CurrentTransform.SetFromTransform(_spiderTransform);
        }

        public override TransformInterpolator GetInterpolator()
        {
            return new SpiderLayoutInterpolator(_spiderTransform, _networkGlobal, _networkContext, _fileLoader, _focusCommunities, _focusCommunitiesToUpdate);
        }

        public void UnfocusAllCommunities()
        {
            foreach (var c in _focusCommunities)
            {
                _focusCommunitiesToUpdate[c] = false;
            }
        }

        public void SetFocusCommunityQueue(int focusCommunity, bool isFocused)
        {
            if (isFocused != _focusCommunities.Contains(focusCommunity))
            {
                _focusCommunitiesToUpdate[focusCommunity] = isFocused;
            }
        }

        public void SetFocusCommunityImm(int focusCommunity, bool isFocused)
        {
            if (isFocused)
            {
                _focusCommunities.Add(focusCommunity);
            }
            else
            {
                _focusCommunities.Remove(focusCommunity);
            }

            if (_focusCommunitiesToUpdate.ContainsKey(focusCommunity))
            {
                _focusCommunitiesToUpdate.Remove(focusCommunity);
            }
        }
    }

    public class SpiderLayoutInterpolator : TransformInterpolator
    {
        MultiLayoutContext _networkContext;
        Dictionary<int, Vector3> _startPositions = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> _endPositions = new Dictionary<int, Vector3>();

        TransformInfo _startingContextTransform;
        TransformInfo _endingContextTransform;

        public SpiderLayoutInterpolator(TransformInfo endingContextTransform, NetworkGlobal networkGlobal, MultiLayoutContext networkContext,
            NetworkFilesLoader fileLoader, HashSet<int> focusCommunities, Dictionary<int, bool> focusCommunitiesToUpdate)
        {
            _networkContext = networkContext;

            var spiderNodes = fileLoader.SpiderData.nodes;
            var spiderIdToIdx = fileLoader.SpiderData.idToIdx;

            var sphericalNodes = fileLoader.SphericalLayout.nodes;
            var sphericalIdToIdx = fileLoader.SphericalLayout.idToIdx;

            foreach (var (communityIdx, community) in networkGlobal.Communities)
            {
                if (!focusCommunitiesToUpdate.ContainsKey(communityIdx)) continue;

                // move to spider position
                if (focusCommunitiesToUpdate[communityIdx])
                {
                    foreach (var node in community.Nodes)
                    {
                        _startPositions[node.ID] = networkContext.Nodes[node.ID].Position;
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
                        _startPositions[node.ID] = networkContext.Nodes[node.ID].Position;
                        // TODO calculate at runtime
                        _endPositions[node.ID] = sphericalNodes[sphericalIdToIdx[node.ID]]._position3D;
                    }

                    focusCommunities.Remove(communityIdx);
                }

                focusCommunitiesToUpdate.Remove(communityIdx);
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