using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class BasicCommNetworkRenderer : NetworkRenderer
    {
        public GameObject NodePrefab;
        public GameObject StraightLinkPrefab;

        [Range(0.0f, 100f)]
        public float NodeScale = 1f;
        [Range(0.0f, 0.1f)]
        public float LinkWidth = 0.005f;
        public Transform NetworkTransform;

        Dictionary<int, GameObject> _nodeGameObjs = new Dictionary<int, GameObject>();
        Dictionary<Tuple<int, int>, GameObject> _linkGameObjs = new Dictionary<Tuple<int, int>, GameObject>();
        NetworkGlobal _networkData;
        MinimapContext _networkProperties;

        void Reset()
        {
            if (Application.isEditor)
            {
                GameObjectUtils.ChildrenDestroyImmediate(NetworkTransform);
            }
            else
            {
                GameObjectUtils.ChildrenDestroy(NetworkTransform);
            }

            _nodeGameObjs.Clear();
            _linkGameObjs.Clear();
        }

        public override void Initialize(NetworkContext networkContext)
        {
            Reset();

            _networkData = GameObject.Find("/Network Manager").GetComponent<NetworkGlobal>();
            _networkProperties = (MinimapContext)networkContext;

            CreateNodes();
            CreateLinks();

        }

        public override void UpdateRenderElements()
        {
            // do nothing
        }

        public override void Draw()
        {
            // nothing to call
        }

        void CreateNodes()
        {
            foreach (var commPair in _networkProperties.CommunityNodes)
            {
                int communityID = commPair.Key;
                var community = commPair.Value;

                var nodeObj = NodeLinkRenderUtils.MakeCommunityNode(NodePrefab, NetworkTransform, community, NodeScale);

                _nodeGameObjs[communityID] = nodeObj;
            }
        }

        void CreateLinks()
        {
            foreach (var linkpair in _networkProperties.Links)
            {
                var linkID = linkpair.Key;
                var link = linkpair.Value;

                int c1 = linkID.Item1, c2 = linkID.Item2;

                Vector3 startPos = _networkProperties.CommunityNodes[c1].Position,
                    endPos = _networkProperties.CommunityNodes[c2].Position;
                var linkObj = NodeLinkRenderUtils.MakeStraightLink(StraightLinkPrefab, NetworkTransform,
                    startPos, endPos, LinkWidth * link.Weight);
                _linkGameObjs[linkID] = linkObj;
            }
        }

        public override Transform GetNodeTransform(int nodeID)
        {
            return _nodeGameObjs[nodeID].transform;
        }

        public override Transform GetCommTransform(int commID)
        {
            // nothing to implement
            return transform;
        }
    }

}