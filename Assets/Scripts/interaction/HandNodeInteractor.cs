// Resolves the right-hand aim ray into node hover and optional dwell selection.
// Configure node and blocking layers to avoid selecting through opaque UI/world
// colliders. Dwell selection is optional; two-hand box selection is the default
// selection gesture. Movement is delegated to RightHandNodeMover.

using UnityEngine;

namespace VidiGraph
{
    /// <summary>Point to hover and dwell to replace the node selection. No movement is performed.</summary>
    [RequireComponent(typeof(HandGestureInput), typeof(LineRenderer))]
    public sealed class HandNodeInteractor : MonoBehaviour
    {
        public HandGestureInput HandInput;
        public NetworkManager NetworkManager;
        public LayerMask NodeLayers = 1 << 10;
        [Tooltip("Opaque world/UI collider layers. Exclude hands, nodes, community and network shells.")]
        public LayerMask BlockingLayers;
        [Min(0.01f)] public float MaxDistance = 10f;
        [Min(0.0001f)] public float LineWidth = 0.003f;
        public Color RayColor = Color.cyan;
        public Color HoverColor = Color.yellow;
        public Color SelectionColor = Color.green;
        [Tooltip("Off by default: use the two-hand pinch box to select, pointing to hover.")]
        public bool EnableDwellSelection;
        TwoHandBoxSelector boxSelector;
        RightHandNodeMover mover;
        [Min(0.01f)] public float DwellSeconds = 0.35f;
        [SerializeField, Range(0, 1)] float dwellProgress;
        [SerializeField] bool selectionLatched;
        public float DwellProgress => dwellProgress;
        [SerializeField] string hoveredNodeGuid;
        public string HoveredNodeGuid => hoveredNodeGuid;

        LineRenderer line;
        LineRenderer progressRing;
        Material ownedMaterial;
        int hoveredNodeId;
        float dwellElapsed;
        string confirmedGuid;

        void Reset()
        {
            HandInput = GetComponent<HandGestureInput>();
            NodeLayers = LayerMask.GetMask("Node");
        }

        void Awake()
        {
            if (HandInput == null) HandInput = GetComponent<HandGestureInput>();
            boxSelector = GetComponent<TwoHandBoxSelector>();
            if (boxSelector == null) boxSelector = gameObject.AddComponent<TwoHandBoxSelector>();
            if (GetComponent<RightHandNodeMover>() == null) gameObject.AddComponent<RightHandNodeMover>();
            mover = GetComponent<RightHandNodeMover>();
            if (NetworkManager == null) NetworkManager = FindObjectOfType<NetworkManager>();
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.enabled = false;
            if (line.sharedMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null) { ownedMaterial = new Material(shader); line.sharedMaterial = ownedMaterial; }
            }
            if (NetworkManager == null) Debug.LogError("HandNodeInteractor needs the scene's Network Manager.", this);
            var progressObject = new GameObject("Dwell progress");
            progressObject.transform.SetParent(transform, false);
            progressRing = progressObject.AddComponent<LineRenderer>();
            progressRing.useWorldSpace = true;
            progressRing.sharedMaterial = line.sharedMaterial;
            progressRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            progressRing.receiveShadows = false;
            progressRing.enabled = false;
        }

        // Resolve hover/dwell only when the ray is valid and competing box/movement gestures permit it.
        void Update()
        {
            if (mover != null && mover.GestureTarget != null && NetworkManager != null)
            {
                var target = NetworkManager.GetMLNodeTransform(mover.GestureTarget);
                if (hoveredNodeGuid != mover.GestureTarget)
                {
                    ClearHover();
                    foreach (var entry in NetworkManager.NodeIDToGUID)
                    {
                        if (entry.Value != mover.GestureTarget) continue;
                        NetworkManager.HoverNode(entry.Key.Item2, entry.Key.Item1);
                        hoveredNodeGuid = entry.Value;
                        hoveredNodeId = entry.Key.Item2;
                        break;
                    }
                }
                line.enabled = target != null && HandInput != null && HandInput.IsTracked;
                progressRing.enabled = false;
                if (line.enabled)
                {
                    line.startWidth = line.endWidth = LineWidth * 0.5f;
                    line.startColor = line.endColor = mover.IsPulling ? SelectionColor : HoverColor;
                    line.SetPosition(0, mover.GestureOrigin);
                    line.SetPosition(1, target.position);
                }
                return;
            }
            // Losing aim or missing a node resets dwell, but does not re-arm a completed
            // selection. The user must release the pointing pose while hand tracking is valid.
            if (HandInput != null && HandInput.isActiveAndEnabled
                && HandInput.IsTracked && !HandInput.IsPointing)
            {
                selectionLatched = false;
                confirmedGuid = null;
            }
            if ((boxSelector != null && boxSelector.IsSelecting) || HandInput == null || !HandInput.isActiveAndEnabled || !HandInput.IsPointing
                || !HandInput.AimValid || NetworkManager == null)
            {
                line.enabled = false;
                ClearHover();
                return;
            }
            Ray ray = HandInput.AimRay;
            float distance = MaxDistance;
            if (Physics.Raycast(ray, out var blocker, distance, BlockingLayers, QueryTriggerInteraction.Collide))
                distance = blocker.distance;

            string guid = null;
            int nodeId = 0, networkId = 0;
            if (Physics.Raycast(ray, out var hit, distance, NodeLayers, QueryTriggerInteraction.Collide))
            {
                distance = hit.distance;
                // Resolve against current graph identities: works with dynamically created subnetworks
                // and does not depend on collider names or a modification to node prefabs.
                foreach (var entry in NetworkManager.NodeIDToGUID)
                {
                    Transform node = NetworkManager.GetMLNodeTransform(entry.Key.Item2, entry.Key.Item1);
                    if (node != null && (hit.transform == node || hit.transform.IsChildOf(node)))
                    {
                        guid = entry.Value;
                        networkId = entry.Key.Item1;
                        nodeId = entry.Key.Item2;
                        break;
                    }
                }
            }
            var selected = NetworkManager.HandSelectedNodeGUIDs;
            if (selected.Count > 1 || (selected.Count == 1 && !selected.Contains(guid))) guid = null;
            if (guid != hoveredNodeGuid)
            {
                ClearHover();
                if (guid != null)
                {
                    NetworkManager.HoverNode(nodeId, networkId);
                    hoveredNodeGuid = guid;
                    hoveredNodeId = nodeId;
                }
            }
            if (EnableDwellSelection && guid != null && !selectionLatched)
            {
                dwellElapsed += Time.unscaledDeltaTime;
                dwellProgress = Mathf.Clamp01(dwellElapsed / Mathf.Max(0.01f, DwellSeconds));
                if (dwellProgress >= 1f)
                {
                    // Snapshot before mutating selection; preserve selections of other element types.
                    var previousNodes = NetworkManager.HandSelectedNodeGUIDs;
                    if (previousNodes.Count > 0) NetworkManager.SetSelectedNodes(previousNodes, false);
                    NetworkManager.SetSelectedNodes(new[] { guid }, true);
                    selectionLatched = true;
                    confirmedGuid = guid;
                }
            }
            line.enabled = true; // Show accepted pointing even when no node is hit.
            line.startWidth = line.endWidth = LineWidth * 0.5f;
            line.startColor = line.endColor = guid == null ? RayColor
                : guid == confirmedGuid ? SelectionColor
                : Color.Lerp(HoverColor, SelectionColor, dwellProgress);
            line.SetPosition(0, ray.origin);
            line.SetPosition(1, ray.GetPoint(distance));
            var camera = Camera.main;
            progressRing.enabled = camera != null && guid != null && !selectionLatched && dwellProgress > 0;
            if (progressRing.enabled)
            {
                progressRing.startWidth = progressRing.endWidth = LineWidth;
                progressRing.startColor = progressRing.endColor = HoverColor;
                NodeHighlightHalo.DrawArc(progressRing, ray.GetPoint(Mathf.Max(0, distance - 0.005f)),
                    camera.transform.right, camera.transform.up, Mathf.Max(0.008f, distance * 0.008f), dwellProgress);
            }
        }

        // Remove transient hover feedback and reset dwell state without changing stored node colors.
        void ClearHover()
        {
            dwellElapsed = dwellProgress = 0f;
            if (progressRing != null) progressRing.enabled = false;
            // Do not clear a hover subsequently acquired by a different input source.
            if (hoveredNodeGuid != null && NetworkManager != null && NetworkManager.HoveredNode == hoveredNodeGuid)
                NetworkManager.UnhoverNode(hoveredNodeId);
            hoveredNodeGuid = null;
        }

        void OnDisable()
        {
            ClearHover();
            selectionLatched = false;
            confirmedGuid = null;
            if (line != null) line.enabled = false;
        }
        void OnDestroy()
        {
            if (progressRing != null) Destroy(progressRing.gameObject);
            if (ownedMaterial != null) Destroy(ownedMaterial);
        }
    }
}
