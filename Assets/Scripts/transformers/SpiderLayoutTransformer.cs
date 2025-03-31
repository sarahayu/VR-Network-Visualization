using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        TransformInfo _spiderTransform;
        HashSet<int> _commsToUpdate = new HashSet<int>();

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
            var spiderNodes = _fileLoader.SphericalLayout.nodes;
            var spiderIdToIdx = _fileLoader.SphericalLayout.idToIdx;

            foreach (var commID in _commsToUpdate)
            {
                _networkGlobal.Communities[commID].Dirty = true;

                foreach (var node in _networkGlobal.Communities[commID].Nodes)
                {
                    var spiderPos = spiderNodes[spiderIdToIdx[node.ID]]._position3D;
                    _networkContext.Nodes[node.ID].Position = new Vector3(spiderPos[0], spiderPos[1], spiderPos[2]);
                    _networkContext.Nodes[node.ID].Dirty = true;

                }

                foreach (var link in _networkGlobal.Communities[commID].InnerLinks)
                {
                    _networkContext.Links[link.ID].BundlingStrength = 0f;
                }
            }

            _commsToUpdate.Clear();

            _networkContext.CurrentTransform.SetFromTransform(_spiderTransform);
        }

        public override TransformInterpolator GetInterpolator()
        {
            return new SpiderLayoutInterpolator(_spiderTransform, _networkGlobal, _networkContext, _fileLoader, _commsToUpdate);
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

    public class SpiderLayoutInterpolator : TransformInterpolator
    {
        MultiLayoutContext _networkContext;
        Dictionary<int, Vector3> _startPositions = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> _endPositions = new Dictionary<int, Vector3>();

        TransformInfo _startingContextTransform;
        TransformInfo _endingContextTransform;

        public SpiderLayoutInterpolator(TransformInfo endingContextTransform, NetworkGlobal networkGlobal, MultiLayoutContext networkContext,
            NetworkFilesLoader fileLoader, HashSet<int> toUpdate)
        {
            _networkContext = networkContext;

            var spiderNodes = fileLoader.SphericalLayout.nodes;
            var spiderIdToIdx = fileLoader.SphericalLayout.idToIdx;


            foreach (var commID in toUpdate)
            {
                networkGlobal.Communities[commID].Dirty = true;

                foreach (var node in networkGlobal.Communities[commID].Nodes)
                {
                    var spiderPos = spiderNodes[spiderIdToIdx[node.ID]]._position3D;

                    _startPositions[node.ID] = networkContext.Nodes[node.ID].Position;
                    _endPositions[node.ID] = new Vector3(spiderPos[0], spiderPos[1], spiderPos[2]);
                }

                foreach (var link in networkGlobal.Communities[commID].InnerLinks)
                {
                    _networkContext.Links[link.ID].BundlingStrength = 0f;
                }
            }

            toUpdate.Clear();

            _startingContextTransform = networkContext.CurrentTransform.Copy();
            _endingContextTransform = endingContextTransform;
        }

        public override void Interpolate(float t)
        {
            foreach (var nodeID in _startPositions.Keys)
            {
                _networkContext.Nodes[nodeID].Position
                    = Vector3.Lerp(_startPositions[nodeID], _endPositions[nodeID], Mathf.SmoothStep(0f, 1f, t));
                _networkContext.Nodes[nodeID].Dirty = true;
            }

            GameObjectUtils.LerpTransform(_networkContext.CurrentTransform, _startingContextTransform, _endingContextTransform, t);
        }
    }

}