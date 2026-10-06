/*
*
* BasicNetworkRenderer is a basic node/link renderer for multilayout network (and subnetworks).
*
*/

using System.Collections;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;

namespace VidiGraph
{
    public class BasicNetworkRenderer : NetworkRenderer
    {
        public GameObject NodePrefab;
        public GameObject StraightLinkPrefab;

        [Range(0.0f, 100f)]
        public float NodeScale = 1f;
        [Range(0.0f, 0.1f)]
        public float LinkWidth = 0.005f;
        public bool DrawVirtualNodes = true;
        public bool DrawTreeStructure = false;
        public Transform NetworkTransform;
        [Range(0.25f, 8f)] [SerializeField] float renderBudgetMilliseconds = 2f;

        Dictionary<int, GameObject> _nodeGameObjs = new Dictionary<int, GameObject>();
        Dictionary<int, GameObject> _linkGameObjs = new Dictionary<int, GameObject>();
        NetworkGlobal _networkData;
        MultiLayoutContext _networkProperties;
        Coroutine _renderRoutine;
        bool _renderRequested;

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

            _networkData = GameObject.Find("/Network Manager").GetComponent<NetworkManager>().NetworkGlobal;
            _networkProperties = (MultiLayoutContext)networkContext;

            CreateNodes();
            CreateLinks();

        }

        public override void UpdateRenderElements()
        {
            if (AppearanceDeferred) return;
            _renderRequested = true;
            if (_renderRoutine == null && isActiveAndEnabled)
                _renderRoutine = StartCoroutine(UpdateAcrossFrames());
        }
        
        // Create a new frame for rendering nodes and links.
        // Delay the rendering to the next frame if necessary to maintain performance.
        public override void UpdateAnimationFrame()
        {
            if (_renderRoutine != null) StopCoroutine(_renderRoutine);
            _renderRoutine = null;
            _renderRequested = false;
           
            UpdateNodes();
            UpdateLinks();
        }

        // Set whether the appearance of nodes and links should be deferred (delayed).
        public override void SetAppearanceDeferred(bool deferred)
        {
            base.SetAppearanceDeferred(deferred);
            if (!deferred) return;
            
            if (_renderRoutine != null) StopCoroutine(_renderRoutine);
            
            _renderRoutine = null;
            _renderRequested = false;
        }

        // Update the appearance of nodes and links for the current frame.
        // Lets us update which one to render next.
        public override void UpdateAppearanceFrame(HashSet<int> nodes, HashSet<int> links, bool geometryChanged)
        {
            foreach (int id in nodes)
                if (_nodeGameObjs.TryGetValue(id, out var nodeObj))
                    NodeLinkRenderUtils.UpdateNode(nodeObj, _networkData.Nodes[id], _networkProperties.Nodes[id]);
            foreach (int id in links)
            {
                UpdateLink(_networkData.Links[id]);
            }
        }

        // Coroutine to update nodes and links across multiple frames to maintain performance.
        IEnumerator UpdateAcrossFrames()
        {
            while (_renderRequested)
            {
                _renderRequested = false;
                var budget = Stopwatch.StartNew();
                foreach (var node in _networkData.Nodes)
                {
                    if (DrawVirtualNodes || !node.IsVirtualNode)
                    {
                        var nodeProps = _networkProperties.Nodes[node.ID];
                        NodeLinkRenderUtils.UpdateNode(_nodeGameObjs[node.ID], node, nodeProps);
                    }
                    if (budget.Elapsed.TotalMilliseconds >= renderBudgetMilliseconds)
                    { yield return null; budget.Restart(); }
                }

                if (DrawTreeStructure)
                {
                    foreach (var link in _networkData.TreeLinks)
                    {
                        UpdateLink(link);
                        if (budget.Elapsed.TotalMilliseconds >= renderBudgetMilliseconds)
                        { yield return null; budget.Restart(); }
                    }
                }

                foreach (var link in _networkData.Links.Values)
                {
                    UpdateLink(link);
                    if (budget.Elapsed.TotalMilliseconds >= renderBudgetMilliseconds)
                    { yield return null; budget.Restart(); }
                }
            }
            _renderRoutine = null;
            if (_renderRequested && isActiveAndEnabled)
                _renderRoutine = StartCoroutine(UpdateAcrossFrames());
        }

        // Grabs a link and updates its visual representation based on the current network properties.
        void UpdateLink(Link link)
        {
            Vector3 startPos = _networkProperties.Nodes[link.SourceNodeID].Position;
            Vector3 endPos = _networkProperties.Nodes[link.TargetNodeID].Position;
            
            var linkObj = _linkGameObjs[link.ID];
            bool hasProps = _networkProperties.Links.TryGetValue(link.ID, out var props);
            
            NodeLinkRenderUtils.UpdateStraightLink(linkObj, startPos, endPos,
                hasProps ? props.Width : LinkWidth);
            
            if (!hasProps) return;
            var renderer = linkObj.GetComponentInChildren<Renderer>();
            
            if (renderer == null) return;
            // Straight mesh links have one material color rather than a ribbon gradient.
            
            var color = Color.Lerp(props.ColorStart, props.ColorEnd, 0.5f);
            color.a *= props.Alpha;
            
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            
            renderer.SetPropertyBlock(block);
        }

        public override void Draw()
        {
            // nothing to call
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

        public override Transform GetNetworkTransform()
        {
            // nothing to implement
            return transform;
        }

        void CreateNodes()
        {
            foreach (var node in _networkData.Nodes)
            {
                if (DrawVirtualNodes || !node.IsVirtualNode)
                {
                    var nodeProps = _networkProperties.Nodes[node.ID];
                    var nodeObj = NodeLinkRenderUtils.MakeNode(NodePrefab, NetworkTransform, node, nodeProps);

                    _nodeGameObjs[node.ID] = nodeObj;
                }
            }
        }

        void CreateLinks()
        {
            // This will draw a 'debug' tree structure to emphasize the underlying hierarchy...
            if (DrawTreeStructure)
            {
                foreach (var link in _networkData.TreeLinks)
                {
                    Vector3 startPos = _networkProperties.Nodes[link.SourceNodeID].Position,
                        endPos = _networkProperties.Nodes[link.TargetNodeID].Position;
                    var linkObj = NodeLinkRenderUtils.MakeStraightLink(StraightLinkPrefab, NetworkTransform,
                        startPos, endPos, LinkWidth);
                    _linkGameObjs[link.ID] = linkObj;
                }
            }
            // ...whereas this is concerned with the visible links between nodes in the graph

            foreach (var link in _networkData.Links.Values)
            {
                Vector3 startPos = _networkProperties.Nodes[link.SourceNodeID].Position,
                    endPos = _networkProperties.Nodes[link.TargetNodeID].Position;
                var linkObj = NodeLinkRenderUtils.MakeStraightLink(StraightLinkPrefab, NetworkTransform,
                    startPos, endPos, LinkWidth);
                _linkGameObjs[link.ID] = linkObj;
            }
        }

        void UpdateNodes()
        {
            foreach (var node in _networkData.Nodes)
            {
                if (DrawVirtualNodes || !node.IsVirtualNode)
                {
                    var nodeProps = _networkProperties.Nodes[node.ID];
                    NodeLinkRenderUtils.UpdateNode(_nodeGameObjs[node.ID], node, nodeProps);
                }
            }
        }

        void UpdateLinks()
        {
            // This will draw a 'debug' tree structure to emphasize the underlying hierarchy...
            if (DrawTreeStructure)
            {
                foreach (var link in _networkData.TreeLinks)
                {
                    UpdateLink(link);
                }
            }
            // ...whereas this is concerned with the visible links between nodes in the graph

            foreach (var link in _networkData.Links.Values)
            {
                UpdateLink(link);
            }
        }
    }

}
