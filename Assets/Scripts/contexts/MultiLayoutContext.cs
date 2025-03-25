/*
* NetworkContext3D contains network information specific to MultiLayoutNetwork.
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class MultiLayoutContext : NetworkContext
    {
        public class Node
        {
            public enum NodeState
            {
                None,
                Bring,
                NumStates,
            }
            public float Size = 1f;
            public Vector3 Position = Vector3.zero;
            public NodeState State = NodeState.None;
        }

        public class Link
        {
            public float OverrideBundlingStrength = -1f;
        }

        public class Community
        {
            public enum CommunityState
            {
                None,
                Spider,
                Floor,
                NumStates,
            }

            public double Mass;
            public Vector3 MassCenter;
            public double Size;

            public CommunityState State = CommunityState.None;
        }

        public Dictionary<int, Node> Nodes = new Dictionary<int, Node>();
        public Dictionary<int, Link> Links = new Dictionary<int, Link>();
        public Dictionary<int, Community> Communities = new Dictionary<int, Community>();

        [HideInInspector]
        public TransformInfo CurrentTransform = new TransformInfo();

        public MultiLayoutContext()
        {
            // expose constructor
        }

        public void SetFromGlobal(NetworkGlobal networkGlobal)
        {
            Nodes.Clear();
            Links.Clear();
            Communities.Clear();

            foreach (var node in networkGlobal.Nodes)
            {
                Nodes[node.ID] = new Node();
            }

            foreach (var link in networkGlobal.Links)
            {
                Links[link.ID] = new Link();
            }

            foreach (var community in networkGlobal.Communities.Values)
            {
                Communities[community.ID] = new Community();
            }
        }

        public void RecomputeGeometricProps(NetworkGlobal networkGlobal)
        {
            foreach (var community in networkGlobal.Communities.Values)
            {
                var contextCommunity = Communities[community.ID];

                CommunityMathUtils.ComputeMassProperties(community.Nodes, Nodes,
                    out contextCommunity.Mass, out contextCommunity.MassCenter);

                contextCommunity.Size = CommunityMathUtils.ComputeSize(community.Nodes, Nodes,
                    contextCommunity.MassCenter);
            }
        }
    }
}
