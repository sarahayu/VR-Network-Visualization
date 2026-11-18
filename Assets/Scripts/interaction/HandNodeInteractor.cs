// Select nodes by touching their colliders with either index fingertip.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace VidiGraph
{
    [DefaultExecutionOrder(-30)]
    public sealed class HandNodeInteractor : MonoBehaviour
    {
        [Tooltip("Tracking-space parent of the headset camera (Camera Offset).")]
        public Transform TrackingSpace;
        public NetworkManager NetworkManager;
        public LayerMask NodeLayers = 1 << 10;
        [Tooltip("Opaque world/UI layers; exclude hands, nodes and network shells.")]
        public LayerMask BlockingLayers;
        [Tooltip("Maximum range for movement gestures.")]
        [Min(0.01f)] public float MaxDistance = 10f;
        [Tooltip("World-space fingertip contact radius in metres.")]
        [Min(0.001f)] public float TouchRadius = 0.008f;
        [Min(0)] public float ReleaseMargin = 0.005f;
        public bool IsTouchingNode { get; private set; }

        readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
        readonly bool[] armed = new bool[2];
        readonly bool[] hasPrevious = new bool[2];
        readonly Vector3[] previous = new Vector3[2];
        TwoHandBoxSelector boxSelector;
        RightHandNodeMover mover;

        void Reset() { TrackingSpace = transform.parent; NodeLayers = LayerMask.GetMask("Node"); }

        void Awake()
        {
            boxSelector = GetComponent<TwoHandBoxSelector>();
            if (boxSelector == null) boxSelector = gameObject.AddComponent<TwoHandBoxSelector>();
            mover = GetComponent<RightHandNodeMover>();
            if (mover == null) mover = gameObject.AddComponent<RightHandNodeMover>();
            if (NetworkManager == null) NetworkManager = FindObjectOfType<NetworkManager>();
        }

        void Update()
        {
            if (!Application.isFocused || TrackingSpace == null || NetworkManager == null
                || boxSelector.IsSelecting || (mover != null && mover.IsPulling)
                || NetworkManager.IsLayoutAnimating || NetworkManager.HasActiveVisualAnimation
                || NetworkManager.DraggedNodeGUID != null)
            { Clear(); return; }
            SubsystemManager.GetSubsystems(subsystems);
            XRHandSubsystem running = null;
            foreach (var candidate in subsystems)
                if (candidate.running) { running = candidate; break; }
            if (running == null) { Clear(); return; }
            Physics.SyncTransforms();
            IsTouchingNode = false;
            Touch(running.leftHand, 0);
            Touch(running.rightHand, 1);
        }

        void Touch(XRHand hand, int index)
        {
            if (!hand.isTracked || !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var tip)
                || !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb))
            { armed[index] = hasPrevious[index] = false; return; }
            // Yield to pinches before the box selector's activation delay finishes.
            if (Vector3.Distance(tip.position, thumb.position) <= boxSelector.PinchReleaseDistance)
            { armed[index] = hasPrevious[index] = false; return; }
            Vector3 position = TrackingSpace.TransformPoint(tip.position);
            Vector3 start = hasPrevious[index] && Time.unscaledDeltaTime <= 0.1f
                && Vector3.Distance(previous[index], position) < 0.15f ? previous[index] : position;
            previous[index] = position;
            hasPrevious[index] = true;
            float radius = Mathf.Max(0.001f, TouchRadius);
            var hits = Physics.OverlapCapsule(start, position,
                radius + (armed[index] ? 0 : Mathf.Max(0, ReleaseMargin)),
                NodeLayers, QueryTriggerInteraction.Collide);
            string closest = null;
            float bestDistance = float.PositiveInfinity;
            foreach (var entry in NetworkManager.NodeIDToGUID)
            {
                var node = NetworkManager.GetMLNodeTransform(entry.Key.Item2, entry.Key.Item1);
                if (node == null || !node.gameObject.activeInHierarchy) continue;
                var visual = node.GetComponent<Renderer>();
                if (visual == null || !visual.enabled || visual.forceRenderingOff) continue;
                foreach (var hit in hits)
                {
                    if (hit.transform != node && !hit.transform.IsChildOf(node)) continue;
                    Vector3 toSurface = hit.ClosestPoint(start) - start;
                    float distance = toSurface.magnitude;
                    if (distance >= bestDistance) continue;
                    if (distance > 0.001f && Physics.Raycast(start, toSurface / distance, distance,
                        BlockingLayers, QueryTriggerInteraction.Collide)) continue;
                    closest = entry.Value;
                    bestDistance = distance;
                }
            }
            if (closest == null) { armed[index] = true; return; }
            IsTouchingNode = true;
            // Require separation before another tap, including after tracking resumes.
            if (!armed[index]) return;
            armed[index] = false;
            var selected = NetworkManager.HandSelectedNodeGUIDs;
            if (selected.Count == 1 && selected.Contains(closest)) return;
            if (selected.Count > 0) NetworkManager.SetSelectedNodes(selected, false);
            NetworkManager.SetSelectedNodes(new[] { closest }, true);
        }

        void Clear()
        {
            IsTouchingNode = false;
            for (int i = 0; i < armed.Length; i++) armed[i] = hasPrevious[i] = false;
        }
        void OnDisable() { Clear(); }
        void OnApplicationFocus(bool focused) { if (!focused) Clear(); }
    }
}
