/*
* MinimapContext contains network information specific to MultiLayoutNetwork.
*/

using System;
using System.Collections;
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

            public int Size = 1;
            public Color Color;
            public Vector3 Position;

            public NodeState State = NodeState.None;
        }

        public Dictionary<Tuple<int, int>, Link> Links = new Dictionary<Tuple<int, int>, Link>();
        public Dictionary<int, Node> CommunityNodes = new Dictionary<int, Node>();

        [HideInInspector]
        public TransformInfo CurrentTransform = new TransformInfo();

        public MinimapContext()
        {
            // expose constructor
        }

        public void SetFromGlobal(NetworkGlobal networkGlobal)
        {
            Links.Clear();
            CommunityNodes.Clear();

            foreach (var community in networkGlobal.Communities.Values)
            {
                CommunityNodes[community.ID] = new Node();
            }

            for (int i = 0; i < networkGlobal.Communities.Count; i++)
            {
                for (int j = i + 1; j < networkGlobal.Communities.Count; j++)
                {
                    int c1 = networkGlobal.Communities.ElementAt(i).Value.ID;
                    int c2 = networkGlobal.Communities.ElementAt(j).Value.ID;
                    Links[CommunityMathUtils.IDsToLinkKey(c1, c2)] = new Link();
                }
            }

            if (Links.Count > 100)
            {
                var delEntries = Links.Keys.OrderBy(x => UnityEngine.Random.value).ToList().GetRange(100, Links.Keys.Count - 100);

                foreach (var l in delEntries) Links.Remove(l);
            }

        }

        public void RecomputeProps(NetworkGlobal networkGlobal)
        {

            foreach (var community in networkGlobal.Communities.Values)
            {
                CommunityNodes[community.ID].Size = community.Nodes.Count;
            }

            foreach (var link in Links.Values) link.Weight = 0;

            foreach (var link in networkGlobal.Links)
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
