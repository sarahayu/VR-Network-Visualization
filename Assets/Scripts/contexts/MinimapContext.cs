/*
* MinimapContext contains network information specific to the minimap.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public class MinimapContext : NetworkContext
    {
        public class Link
        {
            public int Weight = 0;
        }

        public class Node
        {
            public enum NodeState
            {
                None,
                NumStates,
            }

            public int ID;
            public int Size = 1;
            public Color Color;
            public Vector3 Position;

            public NodeState State = NodeState.None;
        }

        public class Surface
        {
            public Vector3 Position;
            public Quaternion Rotation;
        }

        public Dictionary<Tuple<int, int>, Link> Links { get; } = new();
        public Dictionary<int, Node> CommunityNodes { get; } = new();
        public Dictionary<string, Node> NodeRenderables { get; } = new();
        public Dictionary<int, Surface> Surfaces { get; } = new();

        // for now, update all nodes if any of them are dirty.
        public bool NodesDirty { get; set; }

        public float Zoom { get; set; } = 1f;
        public float Scale { get; set; } = 1f;

        [HideInInspector]
        public TransformInfo CurrentTransform = new TransformInfo();

        public MinimapContext()
        {
            // expose constructor
        }

        public void RecomputeProps(NetworkGlobal networkGlobal)
        {
            foreach (var community in networkGlobal.Communities.Values)
            {
                CommunityNodes[community.ID].Size = community.Nodes.Count;
            }

            foreach (var link in Links.Values) link.Weight = 0;

            foreach (var link in networkGlobal.Links.Values)
            {
                int c1 = link.SourceNode.CommunityID;
                int c2 = link.TargetNode.CommunityID;

                if (c1 != c2)
                {
                    var key = CommunityMathUtils.IDsToLinkKey(c1, c2);
                    if (Links.ContainsKey(key))
                        Links[key].Weight += 1;
                }
            }
        }

    }
}
