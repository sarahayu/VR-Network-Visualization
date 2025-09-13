using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public static class ForceDirectUtils
    {
        // Fruchterman, T.M.J. and Reingold, E.M. (1991), Graph drawing by force-directed placement. Softw: Pract. Exper., 21: 1129-1164. https://doi.org/10.1002/spe.4380211102
        // iterations and temp parameters taken from python networkx implementation
        public static IEnumerable<Vector3> CalculateLayout(MultiLayoutContext network, NetworkGlobal networkGlobal)
        {

            int iterations = 50;
            float W = 5f, H = 5f;

            Dictionary<int, Node> nodeDatas = new();

            foreach (var (nodeID, node) in network.Nodes)
            {
                nodeDatas[nodeID] = new Node()
                {
                    pos = Random.insideUnitSphere,
                    disp = Vector3.zero,
                };
            }

            float k = Mathf.Sqrt(W * H / nodeDatas.Count());
            float temp = 0.1f;

            for (int i = 0; i < iterations; i++)
            {
                foreach (var (vID, vNode) in network.Nodes)
                {
                    if (networkGlobal.Nodes[vID].IsVirtualNode) continue;

                    var v = nodeDatas[vID];

                    v.disp = Vector3.zero;

                    foreach (var (uID, uNode) in network.Nodes)
                    {
                        if (networkGlobal.Nodes[uID].IsVirtualNode) continue;

                        var u = nodeDatas[uID];

                        if (uID != vID)
                        {
                            var delta = v.pos - u.pos;
                            var absDelta = delta.magnitude;

                            if (Mathf.Abs(absDelta) > 0.001f)
                                v.disp += delta / absDelta * (k * k / absDelta);
                        }
                    }
                }

                foreach (var eLink in network.Links.Keys)
                {
                    var e = networkGlobal.Links[eLink];

                    var u = nodeDatas[e.SourceNodeID];
                    var v = nodeDatas[e.TargetNodeID];

                    var delta = v.pos - u.pos;
                    var absDelta = delta.magnitude;

                    v.disp -= delta / absDelta * (absDelta * absDelta / k);
                    u.disp += delta / absDelta * (absDelta * absDelta / k);
                }

                foreach (var (vID, vNode) in network.Nodes)
                {
                    var v = nodeDatas[vID];
                    var absDisp = v.disp.magnitude;

                    if (Mathf.Abs(absDisp) > 0.001f)
                        v.pos += v.disp / absDisp * Mathf.Min(absDisp, temp);

                    v.pos.x = Mathf.Clamp(v.pos.x, -W / 2, W / 2);
                    v.pos.y = Mathf.Clamp(v.pos.y, -H / 2, H / 2);
                }
            }

            return nodeDatas.Values.Select(nd => nd.pos);
        }

        class Node
        {
            public Vector3 pos;
            public Vector3 disp;
        }
    }
}