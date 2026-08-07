/*
*
* BundledNetworkRenderer is a renderer optimized for bundled links for multilayout networks (and subnetworks).
*
*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VidiGraph
{
    public class BundledNetworkRenderer : NetworkRenderer
    {
        [SerializeField] GameObject NodePrefab;
        [SerializeField] GameObject CommunityPrefab;
        [SerializeField] GameObject StraightLinkPrefab;
        [SerializeField] GameObject NetworkPrefab;

        [SerializeField] bool DrawVirtualNodes = true;
        [SerializeField] bool DrawTreeStructure = false;
        [SerializeField] ComputeShader SplineComputeShader;

        [Header("Node Mesh Quality")]
        [Tooltip("IcoSphere subdivision passes: 1=80 tri (faceted), 2=320 tri (smooth), 3=1280 tri (very smooth)")]
        [Range(1, 4)] [SerializeField] int sphereSubdivisions = 2;

        [Header("Node Specular")]
        [Range(0f, 1f)] [SerializeField] float nodeMetallic = 0.0f;
        [Tooltip("0 = matte, 1 = mirror. ~0.4 gives a subtle highlight without heavy reflections.")]
        [Range(0f, 1f)] [SerializeField] float nodeSmoothness = 0.4f;

        [Header("Link Endpoints")]
        [Tooltip("Marker diameter (bowl rim) as a multiple of the link width — keep close to 1 so the marker reads as a cap on the ribbon, not a bead.")]
        [SerializeField] float linkEndpointWidthFactor = 1.4f;
        [Tooltip("Bowl depth as a fraction of its rim diameter — small values read as a shallow dish, large values as a deep cup.")]
        [SerializeField] float linkEndpointDepthFactor = 0.35f;
        [Tooltip("Optional override material. Leave empty to automatically copy the material from the node the bowl is attached to.")]
        [SerializeField] Material linkEndpointMaterial;

        float _endpointDiameter;   // = LinkWidth * linkEndpointWidthFactor, resolved once in Initialize
        float _endpointDepth;      // = _endpointDiameter * linkEndpointDepthFactor
        static Mesh _bowlMesh;     // shared concave-hemisphere mesh, rim radius 0.5 at y=0, pole at y=-0.5

        // Fraction of the bowl's depth left sunk into the node surface; the rest
        // protrudes outward so the bowl is actually visible against the node.
        const float EndpointEmbedFraction = 0.25f;

        Dictionary<int, GameObject> _nodeGameObjs = new Dictionary<int, GameObject>();
        Dictionary<int, GameObject> _linkGameObjs = new Dictionary<int, GameObject>();
        Dictionary<int, GameObject> _communityGameObjs = new Dictionary<int, GameObject>();
        struct EndpointMarker { public Renderer rend; public int nodeID; public int otherNodeID; public int linkID; public bool isStart; }
        List<EndpointMarker> _endpointMarkers = new List<EndpointMarker>();
        GameObject _networkGameObj;
        Dictionary<int, List<Vector3>> _controlPointsMap = new Dictionary<int, List<Vector3>>();
        Material _batchSplineMaterial;


        Dictionary<int, Renderer> _nodeRenderers = new Dictionary<int, Renderer>();
        Dictionary<int, Renderer> _commRenderers = new Dictionary<int, Renderer>();
        Renderer _networkRenderer;

        BSplineShaderWrapper _shaderWrapper = new BSplineShaderWrapper();
        NetworkManager _networkManager;
        NetworkGlobal _networkGlobal;
        MultiLayoutContext _networkContext;
        int _lastHoveredNode = -1;
        int _lastHoveredComm = -1;

        void Reset()
        {
            if (Application.isEditor)
            {
                GameObjectUtils.ChildrenDestroyImmediate(transform);
            }
            else
            {
                GameObjectUtils.ChildrenDestroy(transform);
            }

            _nodeGameObjs.Clear();
            _linkGameObjs.Clear();
            _communityGameObjs.Clear();
            _endpointMarkers.Clear();
        }

        public override void Initialize(NetworkContext networkContext)
        {
            Reset();

            _networkManager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = _networkManager.NetworkGlobal;
            _networkContext = (MultiLayoutContext)networkContext;
            _endpointDiameter = _networkContext.ContextSettings.LinkWidth * linkEndpointWidthFactor;
            _endpointDepth = _endpointDiameter * linkEndpointDepthFactor;

            InitializeShaders();

            CreateNodes();
            CreateCommunities();
            CreateMeshLinks();
            CreateGPULinks();
            CreateLinkEndpoints();
            CreateShell();

            UpdateRenderElements();
        }

        public override void Destroy()
        {
            GameObjectUtils.ChildrenDestroy(transform);
            _nodeGameObjs.Clear();
            _linkGameObjs.Clear();
            _communityGameObjs.Clear();
            _endpointMarkers.Clear();

            // TODO release shader resources
        }

        public override void UpdateRenderElements()
        {
            UpdateNodes();
            UpdateCommunities();
            UpdateMeshLinks();
            UpdateGPULinks();
            UpdateLinkEndpoints();
            UpdateShell();
        }

        public override void Draw()
        {
            _shaderWrapper.Draw();
        }

        public override Transform GetNodeTransform(int nodeID)
        {
            return _nodeGameObjs.TryGetValue(nodeID, out var nodeObj) ? nodeObj.transform : null;
        }

        public override Transform GetCommTransform(int commID)
        {
            return _communityGameObjs[commID].transform;
        }

        public override Transform GetNetworkTransform()
        {
            return _networkGameObj.transform;
        }

        void InitializeShaders()
        {
            _batchSplineMaterial = new Material(Shader.Find("Custom/Batch BSpline Unlit"));
            _batchSplineMaterial.SetFloat("_LineWidth", _networkContext.ContextSettings.LinkWidth);

            _shaderWrapper.Initialize(SplineComputeShader, _batchSplineMaterial, _networkContext);
        }

        void CreateNodes()
        {
            foreach (var (nodeID, nodeProps) in _networkContext.Nodes)
            {
                var node = _networkGlobal.Nodes[nodeID];

                if (DrawVirtualNodes || !node.IsVirtualNode)
                {
                    var nodeObj = NodeLinkRenderUtils.MakeNode(NodePrefab, transform, node, nodeProps, sphereSubdivisions);

                    _nodeGameObjs[nodeID] = nodeObj;
                    var nodeRenderer = nodeObj.GetComponentInChildren<Renderer>();
                    _nodeRenderers[nodeID] = nodeRenderer;

                    var mpb = new MaterialPropertyBlock();
                    nodeRenderer.GetPropertyBlock(mpb);
                    mpb.SetFloat("_Metallic", nodeMetallic);
                    mpb.SetFloat("_Glossiness", nodeSmoothness);   // Built-in pipeline
                    mpb.SetFloat("_Smoothness", nodeSmoothness);   // URP
                    mpb.SetFloat("_GlossyReflections", 0f);        // disable env reflections (Built-in)
                    mpb.SetFloat("_EnvironmentReflections", 0f);   // disable env reflections (URP)
                    nodeRenderer.SetPropertyBlock(mpb);

                    AddNodeInteraction(nodeObj, node);
                }
            }
        }

        void CreateCommunities()
        {
            foreach (var (commID, communityProps) in _networkContext.Communities)
            {
                var community = _networkGlobal.Communities[commID];
                var commObj = CommunityRenderUtils.MakeCommunity(CommunityPrefab, transform, communityProps);

                _communityGameObjs[commID] = commObj;
                _commRenderers[commID] = commObj.GetComponentInChildren<Renderer>();

                AddCommunityInteraction(commObj, community);
            }
        }

        void CreateMeshLinks()
        {
            if (DrawTreeStructure)
            {
                foreach (var link in _networkGlobal.TreeLinks)
                {
                    Vector3 startPos = _networkContext.Nodes[link.SourceNodeID].Position,
                        endPos = _networkContext.Nodes[link.TargetNodeID].Position;
                    var linkObj = NodeLinkRenderUtils.MakeStraightLink(StraightLinkPrefab, transform,
                        startPos, endPos, _networkContext.ContextSettings.LinkWidth);
                    _linkGameObjs[link.ID] = linkObj;
                }
            }
        }

        void CreateGPULinks()
        {
            ComputeControlPoints();
            PrepareBuffers();
        }

        void CreateShell()
        {
            if (_networkContext.UseShell)
            {
                var nwObj = CommunityRenderUtils.MakeNetwork(NetworkPrefab, transform, _networkContext);

                _networkGameObj = nwObj;
                _networkRenderer = nwObj.GetComponentInChildren<Renderer>();

                AddNetworkInteraction(nwObj, _networkContext);
            }
        }

        void CreateLinkEndpoints()
        {
            foreach (var linkID in _networkContext.Links.Keys)
            {
                var link = _networkGlobal.Links[linkID];
                int srcID = link.SourceNodeID;
                int tgtID = link.TargetNodeID;

                if (_networkGlobal.Nodes[srcID].IsVirtualNode || _networkGlobal.Nodes[tgtID].IsVirtualNode)
                    continue;

                _endpointMarkers.Add(new EndpointMarker { rend = MakeEndpointDisk(srcID), nodeID = srcID, otherNodeID = tgtID, linkID = linkID, isStart = true });
                _endpointMarkers.Add(new EndpointMarker { rend = MakeEndpointDisk(tgtID), nodeID = tgtID, otherNodeID = srcID, linkID = linkID, isStart = false });
            }

            UpdateLinkEndpoints();
        }

        Renderer MakeEndpointDisk(int nodeID)
        {
            var bowl = new GameObject("LinkEndpointBowl");
            bowl.transform.SetParent(transform);
            bowl.transform.localScale = new Vector3(_endpointDiameter, _endpointDepth * 2f, _endpointDiameter);

            bowl.AddComponent<MeshFilter>().sharedMesh = GetOrCreateBowlMesh();
            var rend = bowl.AddComponent<MeshRenderer>();

            // Use the node's own material so lighting matches exactly.
            // Fall back to the user-assigned override or the node shader if no renderer found.
            Material srcMat;
            if (linkEndpointMaterial != null)
                srcMat = linkEndpointMaterial;
            else if (_nodeRenderers.TryGetValue(nodeID, out var nodeRend))
                srcMat = nodeRend.sharedMaterial;
            else
            {
                var shader = Shader.Find("Custom/Node Color Instanced") ?? Shader.Find("Standard");
                srcMat = shader != null ? new Material(shader) : null;
            }

            if (srcMat != null)
                rend.material = new Material(srcMat);

            return rend;
        }

        // Builds (and caches) a small concave-hemisphere "bowl" mesh: rim circle of
        // radius 0.5 at y=0, curving down to a pinched pole at y=-0.5. Only the
        // curved wall is generated — the rim is left open so the concave interior
        // is visible. Local +Y is the bowl's opening direction.
        //
        // The shell is double-sided: a single-sided open cup is only ever visible
        // from the narrow cone of angles looking into its opening — from every
        // other direction it's a backface-culled, literally empty gap (which is
        // exactly what made the first version disappear). Front and back use
        // separate duplicated vertices so RecalculateNormals doesn't average their
        // opposite-facing normals into garbage.
        static Mesh GetOrCreateBowlMesh()
        {
            if (_bowlMesh != null) return _bowlMesh;

            const int segments = 12;
            const int rings = 4;
            const float radius = 0.5f;

            var baseVerts = new List<Vector3>(1 + rings * segments);
            var outwardTris = new List<int>(); // winds with front faces pointing outward (convex side)

            baseVerts.Add(new Vector3(0f, -radius, 0f)); // bottom pole

            for (int ring = 1; ring <= rings; ring++)
            {
                float phi = (float)ring / rings * (Mathf.PI * 0.5f); // 0 at pole .. PI/2 at rim
                float y = -radius * Mathf.Cos(phi);
                float ringRadius = radius * Mathf.Sin(phi);

                for (int seg = 0; seg < segments; seg++)
                {
                    float theta = (float)seg / segments * Mathf.PI * 2f;
                    baseVerts.Add(new Vector3(ringRadius * Mathf.Cos(theta), y, ringRadius * Mathf.Sin(theta)));
                }
            }

            // Fan connecting the pole to the first ring.
            for (int seg = 0; seg < segments; seg++)
            {
                int b = 1 + seg;
                int c = 1 + (seg + 1) % segments;
                outwardTris.Add(0); outwardTris.Add(b); outwardTris.Add(c);
            }

            // Quad strips between consecutive rings.
            for (int ring = 1; ring < rings; ring++)
            {
                int ringStart = 1 + (ring - 1) * segments;
                int nextStart = 1 + ring * segments;
                for (int seg = 0; seg < segments; seg++)
                {
                    int a = ringStart + seg;
                    int b = ringStart + (seg + 1) % segments;
                    int c = nextStart + seg;
                    int d = nextStart + (seg + 1) % segments;
                    outwardTris.Add(a); outwardTris.Add(b); outwardTris.Add(d);
                    outwardTris.Add(a); outwardTris.Add(d); outwardTris.Add(c);
                }
            }

            // Inward-facing copy (reverse winding) — visible looking into the cup.
            var inwardTris = new List<int>(outwardTris.Count);
            for (int i = 0; i < outwardTris.Count; i += 3)
            {
                inwardTris.Add(outwardTris[i]);
                inwardTris.Add(outwardTris[i + 2]);
                inwardTris.Add(outwardTris[i + 1]);
            }

            int vertCount = baseVerts.Count;
            var vertices = new List<Vector3>(vertCount * 2);
            vertices.AddRange(baseVerts);
            vertices.AddRange(baseVerts);

            var triangles = new List<int>(inwardTris.Count + outwardTris.Count);
            triangles.AddRange(inwardTris);                                  // front: seen looking into the cup
            foreach (var idx in outwardTris) triangles.Add(idx + vertCount); // back: seen from outside

            var mesh = new Mesh { name = "LinkEndpointBowl" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            _bowlMesh = mesh;
            return mesh;
        }

        void UpdateLinkEndpoints()
        {
            int subnID = _networkContext.SubnetworkID;

            // Markers are opaque geometry, so they can't fade with the ribbon.
            // Instead, hide them once their link drops below normal opacity
            // (e.g. de-emphasized links in a highlight state), or when either
            // node it connects to has been swapped onto the transparent material.
            float visibilityThreshold = _networkContext.ContextSettings.LinkNormalAlphaFactor * 0.5f;

            foreach (var marker in _endpointMarkers)
            {
                var linkCtx = _networkContext.Links[marker.linkID];
                var c = marker.isStart ? linkCtx.ColorStart : linkCtx.ColorEnd;

                bool linkVisible = c.a * linkCtx.Alpha >= visibilityThreshold;
                bool nodesOpaque = !_networkManager.IsNodeTransparent(marker.nodeID, subnID)
                    && !_networkManager.IsNodeTransparent(marker.otherNodeID, subnID);
                bool visible = linkVisible && nodesOpaque;

                if (marker.rend.enabled != visible) marker.rend.enabled = visible;
                if (!visible) continue;

                var nodeCtx  = _networkContext.Nodes[marker.nodeID];
                var otherCtx = _networkContext.Nodes[marker.otherNodeID];
                var dir = otherCtx.Position - nodeCtx.Position;
                if (dir.sqrMagnitude < 1e-6f) dir = Vector3.up;
                dir.Normalize();

                string shape = _networkManager.GetNodeShape(marker.nodeID, subnID);
                float surfaceDist = SurfaceDistance(nodeCtx.Size, shape, dir);

                // The bowl's local origin is its rim (the wide open end — mesh rim
                // sits at local y=0, pole at y=-0.5 before scaling), so `position`
                // places the rim, not the shape's center. The wide rim sits against
                // (slightly sunk into) the node surface, and the narrow pole tapers
                // outward along the link — like a small funnel mounted on the node.
                marker.rend.transform.position = nodeCtx.Position + dir * (surfaceDist - _endpointDepth * EndpointEmbedFraction);
                marker.rend.transform.rotation = Quaternion.FromToRotation(Vector3.down, dir);

                // Color follows the link's start/end color (same gradient the edge ribbon uses).
                c.a = 1f; // marker geometry itself stays opaque
                if (marker.rend.material.HasProperty("_Color"))     marker.rend.material.SetColor("_Color", c);
                if (marker.rend.material.HasProperty("_BaseColor")) marker.rend.material.SetColor("_BaseColor", c);
            }
        }

        // Distance from a node's center to its surface along dir, shape-aware:
        // a sphere's surface is a constant radius in every direction, but a
        // cube's surface distance shrinks toward its corners. Assumes an
        // axis-aligned node mesh (node transforms are never rotated).
        static float SurfaceDistance(float size, string shape, Vector3 dir)
        {
            float halfExtent = size * 0.5f;
            return shape == "cube"
                ? halfExtent / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z), 1e-4f)
                : halfExtent;
        }

        void ComputeControlPoints()
        {
            int subnID = _networkContext.SubnetworkID;

            foreach (var (linkID, linkProps) in _networkContext.Links)
            {
                var link = _networkGlobal.Links[linkID];
                float beta = linkProps.BundlingStrength;

                Vector3[] cp = BSplineMathUtils.ControlPoints(link, _networkGlobal, _networkContext);
                int length = cp.Length;

                // cp[0]/cp[length-1] are raw node-CENTER positions. Trim them out to
                // the node's actual surface (shape-aware) along the tangent the curve
                // approaches from, so the ribbon geometrically ends at the surface
                // instead of relying on the opaque node mesh to hide the rest. A round
                // sphere hides this gap from any angle, but a cube's flat faces and
                // sharp edges don't — off-axis, the ribbon can visibly poke past a
                // corner instead of being cleanly covered.
                if (length >= 2)
                {
                    var srcDir = cp[1] - cp[0];
                    if (srcDir.sqrMagnitude > 1e-6f)
                    {
                        srcDir.Normalize();
                        var srcShape = _networkManager.GetNodeShape(link.SourceNodeID, subnID);
                        float srcDist = SurfaceDistance(_networkContext.Nodes[link.SourceNodeID].Size, srcShape, srcDir);
                        cp[0] += srcDir * srcDist;
                    }

                    var tgtDir = cp[length - 2] - cp[length - 1];
                    if (tgtDir.sqrMagnitude > 1e-6f)
                    {
                        tgtDir.Normalize();
                        var tgtShape = _networkManager.GetNodeShape(link.TargetNodeID, subnID);
                        float tgtDist = SurfaceDistance(_networkContext.Nodes[link.TargetNodeID].Size, tgtShape, tgtDir);
                        cp[length - 1] += tgtDir * tgtDist;
                    }
                }

                Vector3 source = cp[0];
                Vector3 target = cp[length - 1];
                Vector3 dVector3 = target - source;

                Vector3[] cpDistributed = new Vector3[length];

                cpDistributed[0] = source;

                for (int i = 1; i < length - 1; i++)
                {
                    Vector3 point = cp[i];

                    cpDistributed[i].x = beta * point.x + (1 - beta) * (source.x + (i) * dVector3.x / length);
                    cpDistributed[i].y = beta * point.y + (1 - beta) * (source.y + (i) * dVector3.y / length);
                    cpDistributed[i].z = beta * point.z + (1 - beta) * (source.z + (i) * dVector3.z / length);
                }
                cpDistributed[length - 1] = target;

                _controlPointsMap[link.ID] = new List<Vector3>(cpDistributed);
            }
        }

        void PrepareBuffers()
        {
            if (_controlPointsMap.Count == 0) return;
            _shaderWrapper.PrepareBuffers(_networkGlobal, _networkContext, _controlPointsMap);
        }

        void UpdateNodes()
        {
            foreach (var (nodeID, contextNode) in _networkContext.Nodes)
            {
                Node globalNode = _networkGlobal.Nodes[nodeID];

                if (DrawVirtualNodes || !globalNode.IsVirtualNode)
                {
                    if ((nodeID == _networkGlobal.HoveredNode?.ID)
                        && !_networkContext.SelectedNodes.Contains(nodeID))
                    {
                        var hoverCol = _networkContext.ContextSettings.NodeHoverColor;
                        NodeLinkRenderUtils.SetNodeColor(_nodeGameObjs[nodeID], Color.Lerp(hoverCol, contextNode.Color, 0.5f), _nodeRenderers[nodeID]);
                    }

                    if (NodeNeedsRenderUpdate(nodeID))
                    {
                        NodeLinkRenderUtils.UpdateNode(_nodeGameObjs[nodeID], globalNode, contextNode, _nodeRenderers[nodeID]);

                        bool hovered = nodeID == _networkGlobal.HoveredNode?.ID;
                        bool selected = _networkContext.SelectedNodes.Contains(nodeID);

                        var hoverCol = _networkContext.ContextSettings.NodeHoverColor;
                        var selectCol = _networkContext.ContextSettings.NodeSelectColor;
                        var defaultCol = _networkContext.ContextSettings.NodeDefaultColor;
                        bool hasCustomColor = contextNode.Color != defaultCol;

                        Color finalColor = contextNode.Color;

                        if (hovered && selected)
                        {
                            // For custom-colored nodes, only apply hover — skip selection yellow blend
                            if (hasCustomColor)
                                finalColor = Color.Lerp(hoverCol, contextNode.Color, 0.5f);
                            else
                                finalColor = Color.Lerp(Color.Lerp(hoverCol, contextNode.Color, 0.5f), selectCol, 0.67f);
                        }
                        else if (hovered)
                        {
                            finalColor = Color.Lerp(hoverCol, contextNode.Color, 0.5f);
                        }
                        else if (selected)
                        {
                            // Only apply selection yellow tint for default-colored nodes
                            if (!hasCustomColor)
                                finalColor = Color.Lerp(selectCol, contextNode.Color, 0.5f);
                        }

                        NodeLinkRenderUtils.SetNodeColor(_nodeGameObjs[nodeID], finalColor, _nodeRenderers[nodeID]);

                        globalNode.Dirty = contextNode.Dirty = false;
                    }
                }
            }

            BookkeepHoverNode();
        }

        void UpdateCommunities()
        {
            foreach (var commID in _networkContext.Communities.Keys)
            {
                Community globalComm = _networkGlobal.Communities[commID];
                if (commID == _networkGlobal.HoveredCommunity?.ID
                        && !_networkContext.SelectedCommunities.Contains(commID))
                {
                    var hoverCol = _networkContext.ContextSettings.CommHoverColor;
                    CommunityRenderUtils.SetCommunityColor(_communityGameObjs[commID], hoverCol, _commRenderers[commID]);
                }

                if (CommNeedsRenderUpdate(commID))
                {
                    MultiLayoutContext.Community contextComm = _networkContext.Communities[commID];

                    CommunityRenderUtils.UpdateCommunity(_communityGameObjs[commID], contextComm,
                        _networkContext.SelectedCommunities.Contains(commID), _networkContext.ContextSettings.CommSelectColor, _commRenderers[commID]);

                    if (commID == _networkGlobal.HoveredCommunity?.ID)
                    {
                        var hoverCol = _networkContext.ContextSettings.CommHoverColor;
                        CommunityRenderUtils.SetCommunityColor(_communityGameObjs[commID], hoverCol, _commRenderers[commID]);
                    }

                    if (_networkContext.SelectedCommunities.Contains(commID))
                        CommunityRenderUtils.SetCommunityColor(_communityGameObjs[commID], _networkContext.ContextSettings.CommSelectColor, _commRenderers[commID]);
                    globalComm.Dirty = contextComm.Dirty = false;
                }


            }

            BookkeepHoverCommunity();
        }

        void UpdateMeshLinks()
        {
            if (DrawTreeStructure)
            {
                foreach (var link in _networkGlobal.TreeLinks)
                {
                    Vector3 startPos = _networkContext.Nodes[link.SourceNodeID].Position,
                        endPos = _networkContext.Nodes[link.TargetNodeID].Position;
                    NodeLinkRenderUtils.UpdateStraightLink(_linkGameObjs[link.ID],
                        startPos, endPos, _networkContext.ContextSettings.LinkWidth);
                }
            }
        }

        void UpdateGPULinks()
        {
            ComputeControlPoints();
            if (_controlPointsMap.Count == 0) return;
            _shaderWrapper.UpdateBuffers(_networkGlobal, _networkContext,
                _networkContext.SelectedNodes,
                _controlPointsMap);
        }

        void UpdateShell()
        {
            if (!_networkContext.UseShell) return;

            if (_networkManager.HoveredNetwork == _networkContext.SubnetworkID)
            {
                var hoverCol = _networkContext.ContextSettings.CommHoverColor;
                CommunityRenderUtils.SetNetworkColor(_networkGameObj, hoverCol, _networkRenderer);
            }

            if (true /* always update for now */)
            {
                CommunityRenderUtils.UpdateNetwork(_networkGameObj, _networkContext,
                    _networkContext.Selected, _networkContext.ContextSettings.CommSelectColor, _networkRenderer);

                if (_networkManager.HoveredNetwork == _networkContext.SubnetworkID)
                {
                    var hoverCol = _networkContext.ContextSettings.CommHoverColor;
                    CommunityRenderUtils.SetNetworkColor(_networkGameObj, hoverCol, _networkRenderer);
                }

                if (_networkContext.Selected)
                    CommunityRenderUtils.SetNetworkColor(_networkGameObj, _networkContext.ContextSettings.CommSelectColor, _networkRenderer);
            }

        }

        bool NodeNeedsRenderUpdate(int nodeID)
        {
            var globalNode = _networkGlobal.Nodes[nodeID];
            var contextNode = _networkContext.Nodes[nodeID];

            return globalNode.Dirty
                || contextNode.Dirty
                || _lastHoveredNode == nodeID
                || _lastHoveredComm == globalNode.CommunityID;
        }

        bool CommNeedsRenderUpdate(int commID)
        {
            return _networkGlobal.Communities[commID].Dirty
                || _networkContext.Communities[commID].Dirty
                || _lastHoveredComm == commID;
        }

        void BookkeepHoverNode()
        {
            if (_networkGlobal.HoveredNode != null)
            {
                _lastHoveredNode = _networkGlobal.HoveredNode.ID;
            }
            else if (_lastHoveredNode != -1)
            {
                _lastHoveredNode = -1;
            }
        }

        void BookkeepHoverCommunity()
        {
            if (_networkGlobal.HoveredCommunity != null)
            {
                _lastHoveredComm = _networkGlobal.HoveredCommunity.ID;
            }
            else if (_lastHoveredComm != -1)
            {
                _lastHoveredComm = -1;
            }
        }

        void AddCommunityInteraction(GameObject gameObject, Community community)
        {
            XRGrabInteractable xrInteractable = gameObject.GetComponent<XRGrabInteractable>();

            xrInteractable.hoverEntered.AddListener(evt =>
            {
                CallCommunityHoverEnter(community, evt);
            });

            xrInteractable.hoverExited.AddListener(evt =>
            {
                CallCommunityHoverExit(community, evt);
            });

            xrInteractable.selectEntered.AddListener(evt =>
            {
                CallCommunitySelectEnter(community, evt);
            });

            xrInteractable.selectExited.AddListener(evt =>
            {
                CallCommunitySelectExit(community, evt);

            });
        }

        void AddNodeInteraction(GameObject gameObject, Node node)
        {
            XRGrabInteractable xrInteractable = gameObject.GetComponentInChildren<XRGrabInteractable>();

            xrInteractable.hoverEntered.AddListener(evt =>
            {
                CallNodeHoverEnter(node, evt);
            });

            xrInteractable.hoverExited.AddListener(evt =>
            {
                CallNodeHoverExit(node, evt);
            });

            xrInteractable.selectEntered.AddListener(evt =>
            {
                CallNodeSelectEnter(node, evt);
            });

            xrInteractable.selectExited.AddListener(evt =>
            {
                CallNodeSelectExit(node, evt);
            });
        }

        void AddNetworkInteraction(GameObject gameObject, MultiLayoutContext network)
        {
            XRGrabInteractable xrInteractable = gameObject.GetComponentInChildren<XRGrabInteractable>();

            xrInteractable.hoverEntered.AddListener(evt =>
            {
                CallNetworkHoverEnter(network, evt);
            });

            xrInteractable.hoverExited.AddListener(evt =>
            {
                CallNetworkHoverExit(network, evt);
            });

            xrInteractable.selectEntered.AddListener(evt =>
            {
                CallNetworkSelectEnter(network, evt);
            });

            xrInteractable.selectExited.AddListener(evt =>
            {
                CallNetworkSelectExit(network, evt);
            });
        }

    }
}