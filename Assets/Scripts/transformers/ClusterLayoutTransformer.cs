using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

namespace VidiGraph
{
    public class ClusterLayoutTransformer : NetworkContextTransformer
    {
        public Transform ClusterPosition;

        NetworkGlobal _networkGlobal;
        MultiLayoutContext _networkContext;

        // TODO remove this when we are able to calc at runtime
        NetworkFilesLoader _fileLoader;
        TransformInfo _clusterTransform;
        HashSet<int> _commsToUpdate = new HashSet<int>();

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkContext = (MultiLayoutContext)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = manager.NetworkGlobal;
            _fileLoader = manager.FileLoader;
            _clusterTransform = new TransformInfo(ClusterPosition);
        }

        public override void ApplyTransformation()
        {
            // TODO calculate at runtime
            var clusterNodes = _fileLoader.ClusterLayout.nodes;

            foreach (var commID in _commsToUpdate)
            {
                _networkGlobal.Communities[commID].Dirty = true;

                foreach (var node in _networkGlobal.Communities[commID].Nodes)
                {
                    var clusterPos = clusterNodes[node.IdxProcessed]._position3D;
                    _networkContext.Nodes[node.ID].Position = clusterPos;
                    _networkContext.Nodes[node.ID].Dirty = true;

                    foreach (var link in _networkGlobal.NodeLinkMatrix[node.ID])
                    {
                        var linkContext = _networkContext.Links[link.ID];
                        linkContext.Alpha = _networkContext.ContextSettings.LinkContext2FocusAlphaFactor;

                        if (link.SourceNodeID == node.ID)
                        {
                            linkContext.BundleStart = false;

                            if (_networkContext.Communities[link.TargetNode.CommunityID].State == MultiLayoutContext.CommunityState.Cluster)
                            {
                                linkContext.BundlingStrength = 0f;
                                linkContext.Alpha = _networkContext.ContextSettings.LinkNormalAlphaFactor;
                            }
                        }
                        else
                        {
                            linkContext.BundleEnd = false;

                            if (_networkContext.Communities[link.TargetNode.CommunityID].State == MultiLayoutContext.CommunityState.Cluster)
                            {
                                linkContext.BundlingStrength = 0f;
                                linkContext.Alpha = _networkContext.ContextSettings.LinkNormalAlphaFactor;
                            }
                        }

                        link.Dirty = true;
                    }

                }
            }

            _commsToUpdate.Clear();
        }

        public override TransformInterpolator GetInterpolator()
        {
            return new ClusterLayoutInterpolator(_clusterTransform, _networkGlobal, _networkContext, _fileLoader, _commsToUpdate);
        }

        public void UpdateOnNextApply(int commID)
        {
            _commsToUpdate.Add(commID);
        }

        public void UpdateOnNextApply(List<int> commIDs)
        {
            _commsToUpdate.UnionWith(commIDs);
        }
    }

    public class ClusterLayoutInterpolator : TransformInterpolator
    {
        MultiLayoutContext _networkContext;
        Dictionary<int, Vector3> _startPositions = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> _endPositions = new Dictionary<int, Vector3>();

        public ClusterLayoutInterpolator(TransformInfo endingContextTransform, NetworkGlobal networkGlobal, MultiLayoutContext networkContext,
            NetworkFilesLoader fileLoader, HashSet<int> toUpdate)
        {
            _networkContext = networkContext;

            var clusterNodes = fileLoader.ClusterLayout.nodes;

            foreach (var commID in toUpdate)
            {
                networkGlobal.Communities[commID].Dirty = true;

                foreach (var node in networkGlobal.Communities[commID].Nodes)
                {
                    var clusterPos = clusterNodes[node.IdxProcessed]._position3D;

                    _startPositions[node.ID] = networkContext.Nodes[node.ID].Position;
                    _endPositions[node.ID] = endingContextTransform.TransformPoint(clusterPos);
                    _networkContext.Nodes[node.ID].Dirty = true;

                    foreach (var link in networkGlobal.NodeLinkMatrix[node.ID])
                    {
                        var linkContext = _networkContext.Links[link.ID];
                        linkContext.Alpha = _networkContext.ContextSettings.LinkContext2FocusAlphaFactor;

                        if (link.SourceNodeID == node.ID)
                        {
                            linkContext.BundleStart = false;

                            if (_networkContext.Communities[link.TargetNode.CommunityID].State == MultiLayoutContext.CommunityState.Cluster)
                            {
                                linkContext.BundlingStrength = 0f;
                                linkContext.Alpha = _networkContext.ContextSettings.LinkNormalAlphaFactor;
                            }
                        }
                        else
                        {
                            linkContext.BundleEnd = false;

                            if (_networkContext.Communities[link.TargetNode.CommunityID].State == MultiLayoutContext.CommunityState.Cluster)
                            {
                                linkContext.BundlingStrength = 0f;
                                linkContext.Alpha = _networkContext.ContextSettings.LinkNormalAlphaFactor;
                            }
                        }

                        link.Dirty = true;
                    }
                }
            }

            toUpdate.Clear();
        }

        public override void Interpolate(float t)
        {
            foreach (var nodeID in _startPositions.Keys)
            {
                _networkContext.Nodes[nodeID].Position
                    = Vector3.Lerp(_startPositions[nodeID], _endPositions[nodeID], Mathf.SmoothStep(0f, 1f, t));
                _networkContext.Nodes[nodeID].Dirty = true;
            }
        }
    }

}