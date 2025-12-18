/*
*
* WorldTransformTransformer (funky name) just applies a Unity transform on the graph
*
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public class WorldTransformTransformer : NetworkContextTransformer
    {
        public Transform FlattenedPosition;

        NetworkGlobal _networkGlobal;
        MultiLayoutContext _networkContext;
        TransformInfo _FlattenedTransform;

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkContext = (MultiLayoutContext)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = manager.NetworkGlobal;
            _FlattenedTransform = new TransformInfo(FlattenedPosition);
        }

        public override void ApplyTransformation()
        {
            foreach (var (nodeID, node) in _networkContext.Nodes)
            {
                if (_networkGlobal.Nodes[nodeID].IsVirtualNode) continue;

                var nodeContext = _networkContext.Nodes[nodeID];

                nodeContext.Position = _FlattenedTransform.TransformPoint(node.Position);
                nodeContext.Dirty = true;

                _networkContext.Communities[nodeContext.CommunityID].Dirty = true;
            }
        }

        public override TransformInterpolator GetInterpolator()
        {
            return new WorldTransformInterpolator(_FlattenedTransform, _networkGlobal, _networkContext);
        }
    }

    public class WorldTransformInterpolator : TransformInterpolator
    {
        MultiLayoutContext _networkContext;
        Dictionary<int, Vector3> _startPositions = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> _endPositions = new Dictionary<int, Vector3>();

        public WorldTransformInterpolator(TransformInfo endingContextTransform, NetworkGlobal networkGlobal,
            MultiLayoutContext networkContext)
        {
            _networkContext = networkContext;

            foreach (var (nodeID, node) in _networkContext.Nodes)
            {
                if (networkGlobal.Nodes[nodeID].IsVirtualNode) continue;

                _startPositions[nodeID] = _networkContext.Nodes[nodeID].Position;
                _endPositions[nodeID] = endingContextTransform.TransformPoint(node.Position);
            }
        }

        public override void Interpolate(float t)
        {
            foreach (var nodeID in _startPositions.Keys)
            {
                _networkContext.Nodes[nodeID].Position
                    = Vector3.Lerp(_startPositions[nodeID], _endPositions[nodeID], Mathf.SmoothStep(0f, 1f, t));
                _networkContext.Nodes[nodeID].Dirty = true;

                if (_networkContext.Nodes[nodeID].CommunityID != -1)
                    _networkContext.Communities[_networkContext.Nodes[nodeID].CommunityID].Dirty = true;
            }
        }
    }

}