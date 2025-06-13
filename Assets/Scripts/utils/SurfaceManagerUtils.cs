using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public static class SurfaceManagerUtils
    {
        public static List<Vector3> CalcProjected(int surfaceID, IEnumerable<Node> nodes, SurfaceManager surfaceManager, NetworkManager networkManager)
        {
            var surface = surfaceManager.Surfaces[surfaceID].gameObject.transform;
            var flatNodes = networkManager.FileLoader.FlatLayout.nodes;

            var projecteds = new List<Vector3>();

            foreach (var node in nodes)
            {
                int commID = networkManager.NetworkGlobal.Nodes[node.ID].CommunityID;

                var pos2D = flatNodes[node.IdxProcessed]._position3D;
                var currentCommPos = networkManager.GetMLCommTransform(commID).position;

                projecteds.Add(CalcProjected(currentCommPos, surface, pos2D));
            }

            return projecteds;

        }

        public static void SetSurfaceColor(Renderer renderer, Color color)
        {
            MaterialPropertyBlock props = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(props);
            props.SetColor("_Color", color);
            renderer.SetPropertyBlock(props);
        }

        public static void HighlightSurface(Renderer renderer, Color highlightCol)
        {
            SetSurfaceColor(renderer, highlightCol);

        }

        public static void UnhighlightSurface(Renderer renderer)
        {

            SetSurfaceColor(renderer, Color.clear);
        }

        static Vector3 CalcProjected(Vector3 point, Transform surface, Vector3 pos2D)
        {
            Vector3 planePoint = surface.position + surface.up * 0.001f;
            Vector3 normal = surface.up;

            Vector3 projCommPos = point - Vector3.Dot(normal, point - planePoint) * normal;

            return surface.rotation * pos2D + projCommPos;
        }
    }
}