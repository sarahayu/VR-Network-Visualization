using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public static class SurfaceManagerUtils
    {
        public static List<Vector3> CalcProjected(int surfaceID, IEnumerable<string> nodeGUIDs, SurfaceManager surfaceManager, NetworkManager networkManager)
        {
            var surface = surfaceManager.Surfaces[surfaceID].gameObject.transform;
            var flatNodes = networkManager.FileLoader.FlatLayout.nodes;

            var projecteds = new List<Vector3>();

            var midpoint = GetMidpoint(nodeGUIDs, networkManager);

            foreach (var (subnID, nodeIDs) in networkManager.SortNodeGUIDs(nodeGUIDs))
            {
                foreach (var nodeID in nodeIDs)
                {
                    var nodeGlobal = networkManager.NetworkGlobal.Nodes[nodeID];
                    int commID = nodeGlobal.CommunityID;

                    var pos2D = flatNodes[nodeGlobal.IdxProcessed]._position3D;

                    projecteds.Add(CalcProjected(midpoint, surface, pos2D));
                }

            }

            return projecteds;

        }

        static Vector3 GetMidpoint(IEnumerable<string> nodeGUIDs, NetworkManager networkManager)
        {
            Vector3 pos = Vector3.zero;
            int count = 0;

            foreach (var nGUID in nodeGUIDs)
            {
                pos += networkManager.GetMLNodeTransform(nGUID).position;
                count += 1;
            }

            return count != 0 ? pos / count : pos;
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
            Vector3 planePoint = surface.position + surface.up * 0.01f;
            Vector3 normal = surface.up;

            Vector3 projCommPos = point - Vector3.Dot(normal, point - planePoint) * normal;

            return surface.rotation * pos2D + projCommPos;
        }
    }
}