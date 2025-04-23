using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Assertions;

namespace VidiGraph
{
    public class BringNodeTransformer : NetworkContextTransformer
    {
        public Transform BringNodePosition;

        NetworkGlobal _networkGlobal;
        MultiLayoutContext _networkContext;

        // TODO remove this when we are able to calc at runtime
        NetworkFilesLoader _fileLoader;
        // use hashset to prevent duplicates
        HashSet<int> _focusNodes = new HashSet<int>();
        Dictionary<int, bool> _focusNodesToUpdate = new Dictionary<int, bool>();
        TransformInfo _bringNodeTransform;

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkContext = (MultiLayoutContext)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = manager.NetworkGlobal;
            _fileLoader = manager.FileLoader;
            _bringNodeTransform = new TransformInfo(BringNodePosition);
        }

        public override void ApplyTransformation()
        {
            // TODO calculate at runtime
            var sphericalNodes = _fileLoader.SphericalLayout.nodes;
            var sphericalIdToIdx = _fileLoader.SphericalLayout.idToIdx;

            foreach (var node in _networkGlobal.Nodes)
            {
                if (!_focusNodesToUpdate.ContainsKey(node.ID)) continue;

                // move to closer position
                if (_focusNodesToUpdate[node.ID])
                {
                    // TODO calculate at runtime
                    var bringNodePos = sphericalNodes[sphericalIdToIdx[node.ID]]._position3D * 0.2f;
                    _networkContext.Nodes[node.ID].Position = _bringNodeTransform.TransformPoint(new Vector3(bringNodePos.x, bringNodePos.y, bringNodePos.z));
                    _networkContext.Nodes[node.ID].Dirty = true;

                    _focusNodes.Add(node.ID);
                }
                // reset to original position
                else
                {
                    var sphericalPos = sphericalNodes[sphericalIdToIdx[node.ID]]._position3D;
                    _networkContext.Nodes[node.ID].Position = sphericalPos;
                    _networkContext.Nodes[node.ID].Dirty = true;

                    _focusNodes.Remove(node.ID);
                }

                _focusNodesToUpdate.Remove(node.ID);
            }

            // _networkContext.CurrentTransform.SetFromTransform(_bringNodeTransform);
        }

        public override TransformInterpolator GetInterpolator()
        {
            return new BringNodeInterpolator(_bringNodeTransform, _networkGlobal, _networkContext, _fileLoader, _focusNodes, _focusNodesToUpdate);
        }

        public void UnfocusAllNodes()
        {
            foreach (var c in _focusNodes)
            {
                _focusNodesToUpdate[c] = false;
            }
        }

        public void SetFocusNodeQueue(int nodeID, bool isFocused)
        {
            if (isFocused != _focusNodes.Contains(nodeID))
            {
                _focusNodesToUpdate[nodeID] = isFocused;
            }
        }

        public void SetFocusNodeImm(int nodeID, bool isFocused)
        {
            if (isFocused)
            {
                _focusNodes.Add(nodeID);
            }
            else
            {
                _focusNodes.Remove(nodeID);
            }

            if (_focusNodesToUpdate.ContainsKey(nodeID))
            {
                _focusNodesToUpdate.Remove(nodeID);
            }
        }
    }

    public class BringNodeInterpolator : TransformInterpolator
    {
        MultiLayoutContext _networkContext;
        Dictionary<int, Vector3> _startPositions = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> _endPositions = new Dictionary<int, Vector3>();

        public BringNodeInterpolator(TransformInfo endingContextTransform, NetworkGlobal networkGlobal, MultiLayoutContext networkContext,
            NetworkFilesLoader fileLoader, HashSet<int> focusNodes, Dictionary<int, bool> focusNodesToUpdate)
        {
            _networkContext = networkContext;
            var sphericalNodes = fileLoader.SphericalLayout.nodes;
            var sphericalIdToIdx = fileLoader.SphericalLayout.idToIdx;

            foreach (var node in networkGlobal.Nodes)
            {
                if (!focusNodesToUpdate.ContainsKey(node.ID)) continue;

                // move to closer position
                if (focusNodesToUpdate[node.ID])
                {
                    _startPositions[node.ID] = networkContext.Nodes[node.ID].Position;
                    // TODO calculate at runtime
                    var bringNodePos = sphericalNodes[sphericalIdToIdx[node.ID]]._position3D * 0.2f;
                    _endPositions[node.ID] = endingContextTransform.TransformPoint(new Vector3(bringNodePos.x, bringNodePos.y, bringNodePos.z));

                    focusNodes.Add(node.ID);
                }
                // reset to original position
                else
                {
                    _startPositions[node.ID] = networkContext.Nodes[node.ID].Position;
                    // TODO calculate at runtime
                    _endPositions[node.ID] = sphericalNodes[sphericalIdToIdx[node.ID]]._position3D;

                    focusNodes.Remove(node.ID);
                }

                focusNodesToUpdate.Remove(node.ID);
            }
        }

        public override void Interpolate(float t)
        {
            foreach (var nodeID in _startPositions.Keys)
            {
                _networkContext.Nodes[nodeID].Position
                    = Vector3.Lerp(_startPositions[nodeID], _endPositions[nodeID], Mathf.SmoothStep(0f, 1f, t));
                _networkContext.Nodes[nodeID].Dirty = true;
            }

            // GameObjectUtils.LerpTransform(_networkContext.CurrentTransform, _startingContextTransform, _endingContextTransform, t);
        }
    }

}