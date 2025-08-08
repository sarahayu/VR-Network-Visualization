using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public class HairballLayoutTransformer : NetworkContextTransformer
    {
        public Transform HairballPosition;
        NetworkGlobal _networkGlobal;
        MultiLayoutContext _networkContext;
        TransformInfo _hairballTransform;
        HashSet<int> _nodesToUpdate = new HashSet<int>();

        // TODO remove this when we are able to calc at runtime
        NetworkFilesLoader _fileLoader;

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkContext = (MultiLayoutContext)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = manager.NetworkGlobal;
            _fileLoader = manager.FileLoader;
            _hairballTransform = new TransformInfo(HairballPosition);
        }

        public override void ApplyTransformation()
        {
            var hairballNodes = _fileLoader.HairballLayout.nodes;

            foreach (var node in _nodesToUpdate.Select(nid => _networkGlobal.Nodes[nid]))
            {
                // TODO calculate at runtime
                if (node.IsVirtualNode) continue;

                var nodeContext = _networkContext.Nodes[node.ID];
                nodeContext.Position = _hairballTransform.TransformPoint(hairballNodes[node.IdxProcessed]._position3D);
                nodeContext.Dirty = true;
                _networkContext.Communities[nodeContext.CommunityID].Dirty = true;

                foreach (var link in _networkGlobal.NodeLinkMatrixDir[node.ID])
                {
                    if (_nodesToUpdate.Contains(link.TargetNodeID))
                    {
                        _networkContext.Links[link.ID].BundlingStrength = 0f;
                        _networkContext.Links[link.ID].Dirty = true;
                    }
                }
            }

            // just mark all communities as dirty
            // TODO optimize?
            foreach (var comm in _networkGlobal.Communities.Values)
            {
                comm.Dirty = true;
            }

            // _networkContext.CurrentTransform.SetFromTransform(_hairballTransform);
            _nodesToUpdate.Clear();
        }

        public void UpdateOnNextApply(int nodeID)
        {
            _nodesToUpdate.Add(nodeID);
        }

        public void UpdateOnNextApply(IEnumerable<int> nodeIDs)
        {
            _nodesToUpdate.UnionWith(nodeIDs);
        }

        public override TransformInterpolator GetInterpolator()
        {
            return new HairballLayoutInterpolator(_hairballTransform, _networkGlobal, _networkContext, _fileLoader, _nodesToUpdate);
        }
    }

    public class HairballLayoutInterpolator : TransformInterpolator
    {
        MultiLayoutContext _networkContext;
        Dictionary<int, Vector3> _startPositions = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> _endPositions = new Dictionary<int, Vector3>();

        public HairballLayoutInterpolator(TransformInfo endingContextTransform, NetworkGlobal networkGlobal, MultiLayoutContext networkContext,
            NetworkFilesLoader fileLoader, HashSet<int> toUpdate)
        {
            _networkContext = networkContext;

            var hairballNodes = fileLoader.HairballLayout.nodes;

            foreach (var node in toUpdate.Select(nid => networkGlobal.Nodes[nid]))
            {
                // TODO calculate at runtime
                if (node.IsVirtualNode) continue;

                _startPositions[node.ID] = networkContext.Nodes[node.ID].Position;
                _endPositions[node.ID] = endingContextTransform.TransformPoint(hairballNodes[node.IdxProcessed]._position3D);

                foreach (var link in networkGlobal.NodeLinkMatrixDir[node.ID])
                {
                    if (toUpdate.Contains(link.TargetNodeID))
                    {
                        _networkContext.Links[link.ID].BundlingStrength = 0f;
                        _networkContext.Links[link.ID].Dirty = true;
                    }
                }
            }

            // just mark all communities as dirty
            // TODO optimize?
            foreach (var comm in networkGlobal.Communities.Values)
            {
                comm.Dirty = true;
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
                _networkContext.Communities[_networkContext.Nodes[nodeID].CommunityID].Dirty = true;
            }
        }
    }

}