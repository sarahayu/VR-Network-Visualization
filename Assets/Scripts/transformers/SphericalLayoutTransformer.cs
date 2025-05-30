using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace VidiGraph
{
    public class SphericalLayoutTransformer : NetworkContextTransformer
    {
        public Transform SphericalPosition;

        NetworkGlobal _networkGlobal;
        MultiLayoutContext _networkContext;
        TransformInfo _sphericalTransform;

        // TODO remove this when we are able to calc at runtime
        NetworkFilesLoader _fileLoader;
        HashSet<int> _nodesToUpdate = new HashSet<int>();

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkContext = (MultiLayoutContext)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = manager.NetworkGlobal;
            _fileLoader = manager.FileLoader;
            _sphericalTransform = new TransformInfo(SphericalPosition);
        }

        public override void ApplyTransformation()
        {
            // TODO calculate at runtime
            var sphericalNodes = _fileLoader.SphericalLayout.nodes;
            var sphericalIdToIdx = _fileLoader.SphericalLayout.idToIdx;

            foreach (var nodeID in _nodesToUpdate)
            {
                var sphericalPos = sphericalNodes[sphericalIdToIdx[nodeID]]._position3D;
                _networkContext.Nodes[nodeID].Position = _sphericalTransform.TransformPoint(new Vector3(sphericalPos.x, sphericalPos.y, sphericalPos.z));
                _networkContext.Nodes[nodeID].Dirty = true;

                foreach (var link in _networkGlobal.NodeLinkMatrix[nodeID])
                {
                    _networkContext.Links[link.ID].BundlingStrength = _networkContext.ContextSettings.EdgeBundlingStrength;
                }
            }

            // just mark all communities as dirty
            // TODO optimize?
            foreach (var comm in _networkGlobal.Communities.Values)
            {
                comm.Dirty = true;
            }

            _nodesToUpdate.Clear();
        }

        public override TransformInterpolator GetInterpolator()
        {
            return new SphericalLayoutInterpolator(_sphericalTransform, _networkGlobal, _networkContext, _fileLoader, _nodesToUpdate);
        }

        public void UpdateNodeOnNextApply(int nodeID)
        {
            _nodesToUpdate.Add(nodeID);
        }

        public void UpdateNodesOnNextApply(List<int> nodeIDs)
        {
            _nodesToUpdate.UnionWith(nodeIDs);
        }

        public void UpdateCommOnNextApply(int commID)
        {
            var rootNode = _networkGlobal.Communities[commID].RootNodeID;
            var ancestors = _networkGlobal.Nodes[rootNode].AncIDsOrderList;
            _nodesToUpdate.Add(rootNode);

            foreach (var virtualID in _networkGlobal.VirtualNodes)
            {
                if (ancestors.Contains(virtualID))
                {
                    _nodesToUpdate.Add(virtualID);
                }
            }

            _nodesToUpdate.UnionWith(_networkGlobal.Communities[commID].Nodes.Select(n => n.ID));
        }

        public void UpdateCommsOnNextApply(List<int> commIDs)
        {
            foreach (var commID in commIDs) UpdateCommOnNextApply(commID);
        }
    }

    public class SphericalLayoutInterpolator : TransformInterpolator
    {
        MultiLayoutContext _networkContext;
        Dictionary<int, Vector3> _startPositions = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> _endPositions = new Dictionary<int, Vector3>();

        public SphericalLayoutInterpolator(TransformInfo endingContextTransform, NetworkGlobal networkGlobal,
            MultiLayoutContext networkContext, NetworkFilesLoader fileLoader, HashSet<int> nodesToUpdate)
        {
            _networkContext = networkContext;
            // TODO calculate at runtime
            var sphericalNodes = fileLoader.SphericalLayout.nodes;
            var sphericalIdToIdx = fileLoader.SphericalLayout.idToIdx;

            foreach (var nodeID in nodesToUpdate)
            {
                var sphericalPos = sphericalNodes[sphericalIdToIdx[nodeID]]._position3D;

                _startPositions[nodeID] = networkContext.Nodes[nodeID].Position;
                _endPositions[nodeID] = endingContextTransform.TransformPoint(new Vector3(sphericalPos.x, sphericalPos.y, sphericalPos.z));

                foreach (var link in networkGlobal.NodeLinkMatrix[nodeID])
                {
                    _networkContext.Links[link.ID].BundlingStrength = _networkContext.ContextSettings.EdgeBundlingStrength;
                }
            }

            // just mark all communities as dirty
            // TODO optimize?
            foreach (var comm in networkGlobal.Communities.Values)
            {
                comm.Dirty = true;
            }

            nodesToUpdate.Clear();
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