using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class HairballLayoutTransformer : NetworkContextTransformer
    {
        public Transform HairballPosition;
        NetworkGlobal _networkGlobal;
        MultiLayoutContext _networkContext;
        TransformInfo _hairballTransform;

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

            // TODO calculate at runtime
            foreach (var node in _networkGlobal.Nodes)
            {
                if (node.IsVirtualNode) continue;

                _networkContext.Nodes[node.ID].Position = _hairballTransform.TransformPoint(hairballNodes[node.IdxProcessed]._position3D);
                _networkContext.Nodes[node.ID].Dirty = true;
            }

            foreach (var link in _networkGlobal.Links)
            {
                _networkContext.Links[link.ID].BundlingStrength = 0f;
                _networkContext.Links[link.ID].Dirty = true;
            }

            // just mark all communities as dirty
            // TODO optimize?
            foreach (var comm in _networkGlobal.Communities.Values)
            {
                comm.Dirty = true;
            }

            // _networkContext.CurrentTransform.SetFromTransform(_hairballTransform);
        }

        public override TransformInterpolator GetInterpolator()
        {
            return new HairballLayoutInterpolator(_hairballTransform, _networkGlobal, _networkContext, _fileLoader);
        }
    }

    public class HairballLayoutInterpolator : TransformInterpolator
    {
        MultiLayoutContext _networkContext;
        Dictionary<int, Vector3> _startPositions = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> _endPositions = new Dictionary<int, Vector3>();

        public HairballLayoutInterpolator(TransformInfo endingContextTransform, NetworkGlobal networkGlobal, MultiLayoutContext networkContext, NetworkFilesLoader fileLoader)
        {
            _networkContext = networkContext;

            var hairballNodes = fileLoader.HairballLayout.nodes;

            foreach (var node in networkGlobal.Nodes)
            {
                if (node.IsVirtualNode) continue;

                _startPositions[node.ID] = networkContext.Nodes[node.ID].Position;
                // TODO calculate at runtime
                _endPositions[node.ID] = endingContextTransform.TransformPoint(hairballNodes[node.IdxProcessed]._position3D);
            }

            foreach (var link in networkGlobal.Links)
            {
                networkContext.Links[link.ID].BundlingStrength = 0f;
                networkContext.Links[link.ID].Dirty = true;
            }

            // just mark all communities as dirty
            // TODO optimize?
            foreach (var comm in networkGlobal.Communities.Values)
            {
                comm.Dirty = true;
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

            // just mark all communities as dirty
            // TODO optimize?
            foreach (var comm in _networkContext.Communities.Values)
            {
                comm.Dirty = true;
            }
        }
    }

}