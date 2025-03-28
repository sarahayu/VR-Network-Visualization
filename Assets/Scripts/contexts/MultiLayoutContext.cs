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
        public class Settings
        {
            public float NodeScale = 1f;
            public float LinkWidth = 0.0025f;
            public float EdgeBundlingStrength = 0.8f;

            public Color NodeHighlightColor;
            public Color CommHighlightColor;
            public Color LinkHighlightColor;
            public Color LinkFocusColor;

            public float LinkMinimumAlpha = 0.01f;
            public float LinkNormalAlphaFactor = 0.05f;
            public float LinkContextAlphaFactor = 0.5f;
            public float LinkContext2FocusAlphaFactor = 0.8f;
        }

        public class Node
        {
            public float Size = 1f;
            public Vector3 Position = Vector3.zero;
            public Color Color;

            // detect if node needs to be rerendered
            public bool Dirty = false;
        }

        public class Link
        {
            public float OverrideBundlingStrength = -1f;
            public float Width = 1f;
            public Color ColorStart;
            public Color ColorEnd;
            public float Alpha = 1f;

            // detect if link needs to be rerendered
            public bool Dirty = false;
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

            // detect if link needs to be rerendered
            public bool Dirty = false;
        }

        public Settings ContextSettings = new Settings();

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
