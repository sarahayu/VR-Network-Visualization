using System;
using UnityEngine;

namespace VidiGraph
{
    public class TerrainLayoutTransformer : NetworkContextTransformer
    {
        NetworkGlobal _networkGlobal;
        MinimapContext _networkContext;

        // TODO remove this when we are able to calc at runtime
        NetworkFilesLoader _fileLoader;

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkContext = (MinimapContext)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = manager.NetworkGlobal;
            _fileLoader = manager.FileLoader;
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
                    float[] pos2D = _fileLoader.SphericalLayout.nodes[node.ID].pos2D;
                    center += new Vector3(-pos2D[0], -pos2D[1], 0f);
                }
                center /= community.Nodes.Count;

                _networkContext.CommunityNodes[community.ID].Position = center;
                _networkContext.CommunityNodes[community.ID].Size = community.Nodes.Count;
                _networkContext.CommunityNodes[community.ID].Color = new Color(0.5f, 0.5f, 0.5f, 0.5f);

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

            // normalize so center is 0 and max distance of node is 1 from center
            double width = max.x - min.x;
            double height = max.y - min.y;

            double centerX = (min.x + max.x) / 2;
            double centerY = (min.y + max.y) / 2;

            float buf = 0.1f;

            double halfSideLen = Math.Max(width / 2, height / 2) * (1 + buf);

            foreach (var commPair in _networkContext.CommunityNodes)
            {
                var community = commPair.Value;

                var pos = community.Position;

                pos.x += (float)-centerX;
                pos.y += (float)-centerY;

                pos.x /= (float)halfSideLen;
                pos.y /= (float)halfSideLen;

                var angle = Mathf.Atan(pos.y / pos.x);
                var squareRad = Mathf.Abs(
                    Mathf.Abs(pos.x) > Mathf.Abs(pos.y)
                    ? 1f / Mathf.Cos(angle)
                    : 1f / Mathf.Sin(angle)
                );
                var posRad = Vector3.Magnitude(pos);
                var finalPos = Vector3.Normalize(pos) * posRad / squareRad;

                community.Position = finalPos;
            }
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