using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class TerrainLayoutTransformer : NetworkContextTransformer
    {
        public Transform SphericalPosition;

        NetworkGlobal _networkGlobal;
        MinimapContext _networkContext;
        TransformInfo _terrainTransform;

        // TODO remove this when we are able to calc at runtime
        NetworkFilesLoader _fileLoader;

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkContext = (MinimapContext)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = manager.NetworkGlobal;
            _fileLoader = manager.FileLoader;
            _terrainTransform = new TransformInfo(SphericalPosition);
        }

        public override void ApplyTransformation()
        {
            // TODO loop over context instead of global
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;
            foreach (var community in _networkGlobal.Communities.Values)
            {
                Vector3 center = Vector3.zero;
                foreach (var node in community.Nodes)
                {
                    // TODO calculate at runtime
                    float[] pos2D = _fileLoader.GraphData.nodes[node.ID].pos2D;
                    center += new Vector3(pos2D[0], pos2D[1], 0f);
                }
                center /= community.Nodes.Count;

                _networkContext.CommunityNodes[community.ID].Position = center;
                _networkContext.CommunityNodes[community.ID].Size = community.Nodes.Count;
                _networkContext.CommunityNodes[community.ID].Color = community.Nodes[0].ColorParsed;

                if (center.x < min.x || min == Vector3.zero)
                {
                    min.x = center.x;
                }
                else if (center.x > max.x || max == Vector3.zero)
                {
                    max.x = center.x;
                }

                if (center.y < min.y || min == Vector3.zero)
                {
                    min.y = center.y;
                }
                else if (center.y > max.y || max == Vector3.zero)
                {
                    max.y = center.y;
                }
            }

            double width = max.x - min.x;
            double height = max.y - min.y;

            double halfSideLen = Math.Max(width / 2, height / 2);

            foreach (var commPair in _networkContext.CommunityNodes)
            {
                var community = commPair.Value;

                var pos = community.Position;

                pos.x += (float)(-min.x + width / 2);
                pos.y += (float)(-min.y + height / 2);

                pos.x /= (float)halfSideLen;
                pos.y /= (float)halfSideLen;
            }

            _networkContext.CurrentTransform.SetFromTransform(_terrainTransform);
        }

        public override TransformInterpolator GetInterpolator()
        {
            return new TerrainLayoutInterpolator();
        }
    }

    public class TerrainLayoutInterpolator : TransformInterpolator
    {
        public override void Interpolate(float t)
        {
            // leave empty
        }
    }

}