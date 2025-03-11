using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class NetworkContext3D : NetworkContext
    {
        public class Node
        {
            public enum NodeState
            {
                None,
                Bring,
                NumStates,
            }
            public Vector3 Position = Vector3.zero;
            public NodeState State = NodeState.None;
        }

        public class Link
        {
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

        public NetworkContext3D()
        {
            // expose constructor
        }

        public void Update(NetworkGlobal networkData)
        {
            Nodes.Clear();
            Links.Clear();
            Communities.Clear();

            foreach (var node in networkData.Nodes)
            {
                Nodes[node.ID] = new Node();
            }

            foreach (var link in networkData.Links)
            {
                Links[link.ID] = new Link();
            }

            foreach (var community in networkData.Communities.Values)
            {
                Communities[community.ID] = new Community();
            }
        }

        public void RecomputeGeometricProps(NetworkGlobal networkData)
        {
            foreach (var community in networkData.Communities.Values)
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
