using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class NetworkContext3D : NetworkContext
    {
        public class Node
        {
            public Vector3 Position;
        }

        public class Link
        {
            public enum LinkState
            {
                HighLight,
                Context,
                Focus2Context,
                Focus,
                Normal,
                HighLightFocus,
            }

            public LinkState State;
        }

        public class Community
        {
            public double Mass;
            public Vector3 MassCenter;
            public double Size;
        }

        public Dictionary<int, Node> Nodes = new Dictionary<int, Node>();
        public Dictionary<int, Link> Links = new Dictionary<int, Link>();
        public Dictionary<int, Community> Communities = new Dictionary<int, Community>();

        public NetworkContext3D()
        {
            // expose constructor
        }

        public void Update(NetworkDataStructure networkData)
        {
            Nodes.Clear();
            Links.Clear();
            Communities.Clear();

            foreach (var node in networkData.Nodes)
            {
                Nodes[node.id] = new Node();
            }

            foreach (var link in networkData.Links)
            {
                Links[link.linkIdx] = new Link() { State = Link.LinkState.Normal };
            }

            foreach (var community in networkData.Communities.Values)
            {
                Communities[community.communityIdx] = new Community();
            }
        }

        public void RecomputeGeometricProps(NetworkDataStructure networkData)
        {
            foreach (var community in networkData.Communities.Values)
            {
                var contextCommunity = Communities[community.communityIdx];

                CommunityMathUtils.ComputeMassProperties(community.communityNodes, Nodes,
                    out contextCommunity.Mass, out contextCommunity.MassCenter);

                contextCommunity.Size = CommunityMathUtils.ComputeSize(community.communityNodes, Nodes,
                    contextCommunity.MassCenter);
            }
        }
    }
}
