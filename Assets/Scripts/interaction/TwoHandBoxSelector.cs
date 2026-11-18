// Previews a world-aligned node-selection box between two tracked pinch midpoints.
// Pinch activation/release thresholds and minimum thickness are configurable.
// Owns temporary box edges, count text, and node previews; coordinates with the
// node interactor so touch and box selection do not compete.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace VidiGraph
{
    /// <summary>World-aligned selection volume between two tracked pinch midpoints.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class TwoHandBoxSelector : MonoBehaviour
    {
        [Min(0.005f)] public float PinchStartDistance = 0.025f;
        [Min(0.005f)] public float PinchReleaseDistance = 0.04f;
        [Min(0.01f)] public float MinimumThickness = 0.05f;
        [Min(0)] public float ActivationSeconds = 0.12f;
        [SerializeField] bool selecting;
        [SerializeField] int previewCount;
        public bool IsSelecting => selecting;
        public int PreviewCount => previewCount;

        HandNodeInteractor interactor;
        readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
        readonly HashSet<string> candidates = new HashSet<string>();
        readonly Dictionary<string, NodeHighlightHalo> previews = new Dictionary<string, NodeHighlightHalo>();
        readonly List<string> removed = new List<string>();
        readonly LineRenderer[] edges = new LineRenderer[12];
        readonly Vector3[] corners = new Vector3[8];
        GameObject visuals;
        TextMesh countLabel;
        Material material;
        bool leftPinched, rightPinched, needsRelease;
        float held;

        void Start()
        {
            interactor = GetComponent<HandNodeInteractor>();
            visuals = new GameObject("Pinch selection box");
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) material = new Material(shader);
            for (int i = 0; i < edges.Length; i++)
            {
                var edge = new GameObject("Box edge");
                edge.transform.SetParent(visuals.transform, false);
                var line = edge.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.startWidth = line.endWidth = 0.002f;
                line.startColor = line.endColor = new Color(0, 1, 1, 0.65f);
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                edges[i] = line;
            }
            var label = new GameObject("Enclosed node count");
            label.transform.SetParent(visuals.transform, false);
            countLabel = label.AddComponent<TextMesh>();
            countLabel.anchor = TextAnchor.LowerCenter;
            countLabel.characterSize = 0.008f;
            countLabel.fontSize = 48;
            countLabel.color = Color.cyan;
            visuals.SetActive(false);
        }

        // Track both pinch midpoints, update the preview box, and commit the enclosed set on release.
        void Update()
        {
            if (interactor == null || interactor.TrackingSpace == null || !interactor.isActiveAndEnabled || interactor.NetworkManager == null)
            { Cancel(); return; }
            SubsystemManager.GetSubsystems(subsystems);
            XRHandSubsystem subsystem = null;
            foreach (var item in subsystems) if (item.running) { subsystem = item; break; }
            Vector3 left = default, right = default;
            if (subsystem == null || !ReadPinch(subsystem.leftHand, ref leftPinched, out left)
                || !ReadPinch(subsystem.rightHand, ref rightPinched, out right))
            { Cancel(); return; }
            if (needsRelease)
            {
                if (!leftPinched && !rightPinched) needsRelease = false;
                return;
            }
            if (!selecting)
            {
                held = leftPinched && rightPinched ? held + Time.unscaledDeltaTime : 0;
                if (!leftPinched || !rightPinched || held < ActivationSeconds) return;
                selecting = true;
                visuals.SetActive(true);
            }
            if (!leftPinched || !rightPinched)
            {
                // Commit the last pinched volume, avoiding a jump caused by opening the fingers.
                var manager = interactor.NetworkManager;
                var previous = manager.SelectedNodeGUIDs;
                if (previous.Count > 0) manager.SetSelectedNodes(previous, false);
                var valid = new List<string>();
                foreach (var id in manager.NodeIDToGUID.Values) if (candidates.Contains(id)) valid.Add(id);
                if (valid.Count > 0) manager.SetSelectedNodes(valid, true);
                Cancel();
                return;
            }
            Vector3 a = interactor.TrackingSpace.TransformPoint(left), b = interactor.TrackingSpace.TransformPoint(right);
            Vector3 size = b - a;
            size = new Vector3(Mathf.Max(Mathf.Abs(size.x), MinimumThickness),
                Mathf.Max(Mathf.Abs(size.y), MinimumThickness), Mathf.Max(Mathf.Abs(size.z), MinimumThickness));
            Bounds volume = new Bounds((a + b) * 0.5f, size);
            UpdatePreview(volume);
            DrawBox(volume);
        }

        // Use separate start/release distances to avoid flickering at the pinch threshold.
        bool ReadPinch(XRHand hand, ref bool pinched, out Vector3 midpoint)
        {
            midpoint = default;
            if (!hand.isTracked || !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb)
                || !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index)) return false;
            float threshold = pinched ? Mathf.Max(PinchReleaseDistance, PinchStartDistance) : PinchStartDistance;
            pinched = Vector3.Distance(thumb.position, index.position) <= threshold;
            midpoint = (thumb.position + index.position) * 0.5f;
            return true;
        }

        // Preview active rendered node centers inside the box; remove highlights for nodes that leave it.
        void UpdatePreview(Bounds volume)
        {
            candidates.Clear();
            foreach (var entry in interactor.NetworkManager.NodeIDToGUID)
            {
                var node = interactor.NetworkManager.GetMLNodeTransform(entry.Key.Item2, entry.Key.Item1);
                if (node == null || !node.gameObject.activeInHierarchy || !volume.Contains(node.position)) continue;
                var visual = node.GetComponent<Renderer>();
                if (visual == null || !visual.enabled || visual.forceRenderingOff) continue;
                candidates.Add(entry.Value);
                if (!previews.ContainsKey(entry.Value))
                    previews.Add(entry.Value, NodeHighlightHalo.SetPreview(node.gameObject, visual, true));
            }
            removed.Clear();
            foreach (var item in previews)
                if (!candidates.Contains(item.Key))
                {
                    if (item.Value != null) item.Value.ClearPreview();
                    removed.Add(item.Key);
                }
            foreach (var id in removed) previews.Remove(id);
            previewCount = candidates.Count;
        }

        void DrawBox(Bounds volume)
        {
            Vector3 min = volume.min, max = volume.max;
            for (int i = 0; i < 8; i++) corners[i] = new Vector3((i & 1) == 0 ? min.x : max.x,
                (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
            int edge = 0;
            for (int i = 0; i < 8; i++)
                for (int bit = 1; bit <= 4; bit *= 2)
                    if ((i & bit) == 0)
                    {
                        edges[edge].SetPosition(0, corners[i]);
                        edges[edge++].SetPosition(1, corners[i | bit]);
                    }
            countLabel.text = previewCount + " nodes";
            countLabel.transform.position = new Vector3(volume.center.x, max.y + 0.015f, volume.center.z);
            if (Camera.main != null) countLabel.transform.rotation = Camera.main.transform.rotation;
        }

        // Discard previews and require a release before a new selection gesture.
        void Cancel()
        {
            selecting = false;
            held = 0;
            needsRelease = true;
            foreach (var halo in previews.Values) if (halo != null) halo.ClearPreview();
            previews.Clear();
            candidates.Clear();
            previewCount = 0;
            if (visuals != null) visuals.SetActive(false);
        }
        void OnDisable() { Cancel(); }
        void OnDestroy()
        {
            if (visuals != null) Destroy(visuals);
            if (material != null) Destroy(material);
        }
    }
}
