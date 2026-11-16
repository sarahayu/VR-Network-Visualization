// Coordinates right-hand node movement: aimed swipes, pinch/pull, near-hand grabs,
// and selected-node palm contact. Gesture thresholds and smoothing are Inspector
// settings. Shares state with the pointing, box-selection, and voice components
// to arbitrate interaction; releases temporary contact/visual state on cleanup.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using Whisper.Samples;

namespace VidiGraph
{
    /// <summary>Right-hand aimed swipes, free pinch grabs, and selected-node contact pushes.</summary>
    [DefaultExecutionOrder(-25)]
    public sealed class RightHandNodeMover : MonoBehaviour
    {
        [Min(0.01f)] public float MovementGain = 1f;
        [Min(0)] public float SmoothingSeconds = 0.04f;
        [Range(5, 60)] public float OpenFingerMaxBend = 30f;
        [Tooltip("World-space radius of the right palm's contact collider.")]
        [Min(0.005f)] public float PalmRadius = 0.045f;
        [Min(0.01f)] public float MomentumDecay = 3f;
        [Min(0.01f)] public float MaximumSpeed = 2f;
        [Min(0.005f)] public float PinchStartDistance = 0.025f;
        [Min(0.005f)] public float PinchReleaseDistance = 0.04f;
        [Min(0.01f)] public float PullGain = 2f;
        [Tooltip("Aim tolerance for distant gestures; no pointing pose or dwell is required.")]
        [Range(1, 30)] public float AimConeDegrees = 15f;
        [Min(0.01f)] public float SwipeSpeed = 0.2f;
        [Tooltip("Reach within this distance of a node surface, then close your right hand to grab it.")]
        [Min(0.01f)] public float GrabRadius = 0.1f;
        [Range(20, 90)] public float GrabBendDegrees = 40f;
        bool directGrab, grabNeedsOpen = true;
        Vector3 grabOffset;
        public string PullTarget { get; private set; }
        public bool IsPulling => PullTarget != null;
        public string GestureTarget => PullTarget ?? aimedNode;
        public Vector3 GestureOrigin => palmCollider != null ? palmCollider.transform.position : transform.position;
        string aimedNode;
        bool pinched, pinchNeedsRelease;
        bool swipeAvailable = true;
        [SerializeField] bool moving;
        [SerializeField] bool movingWholeGraph;
        public bool IsMoving => moving;
        HandNodeInteractor interactor;
        HandGestureInput input;
        TwoHandBoxSelector box;
        StreamingSampleMic voice;
        NetworkManager moveManager;
        readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
        readonly Dictionary<string, Vector3> targets = new Dictionary<string, Vector3>();
        HashSet<string> selection;
        string selectedNode;
        Vector3 nodeStart, offset, velocity, previousPalm, filteredVelocity;
        bool hasPalmSample;
        SphereCollider palmCollider;
        readonly HashSet<string> contacts = new HashSet<string>();
        readonly HashSet<string> nextContacts = new HashSet<string>();

        void Start()
        {
            interactor = GetComponent<HandNodeInteractor>();
            input = GetComponent<HandGestureInput>();
            box = GetComponent<TwoHandBoxSelector>();
            voice = FindObjectOfType<StreamingSampleMic>();
            var contactObject = new GameObject("Right palm contact");
            contactObject.layer = 2; // Ignore raycasts; contact is queried explicitly below.
            palmCollider = contactObject.AddComponent<SphereCollider>();
            palmCollider.isTrigger = true;
            palmCollider.enabled = false;
        }

        // Arbitrate movement gestures from tracking, selection, and competing interaction state.
        void Update()
        {
            if (!Application.isFocused || interactor == null || !interactor.isActiveAndEnabled || input == null
                || !input.isActiveAndEnabled || input.TrackingSpace == null || interactor.NetworkManager == null)
            { Cancel(); return; }
            var manager = interactor.NetworkManager;
            if ((box != null && box.IsSelecting) || manager.IsLayoutAnimating || manager.HasActiveVisualAnimation
                || manager.DraggedNodeGUID != null || (voice != null && voice.IsVoiceListening))
            { Cancel(); return; }
            var currentSelection = manager.HandSelectedNodeGUIDs;
            if (moving && (moveManager != manager || !selection.SetEquals(currentSelection)))
            { Cancel(); return; }
            SubsystemManager.GetSubsystems(subsystems);
            XRHandSubsystem running = null;
            foreach (var item in subsystems) if (item.running) { running = item; break; }
            if (running == null || !TryOpenPalm(running.rightHand, out var palm, out bool open))
            { Cancel(); return; }
            // Direct grabs take priority over pinch/aim and do not require a prior selection.
            if (UpdateDirectGrab(manager, currentSelection, running.rightHand, palm, open)) return;
            if (currentSelection.Count > 1) return;
            if (!TryPinchDistance(running.rightHand, out float pinchDistance)) { Cancel(); return; }
            bool wasPinched = pinched;
            pinched = pinchDistance <= (pinched ? Mathf.Max(PinchStartDistance, PinchReleaseDistance) : PinchStartDistance);
            // Yield immediately, including the box selector's activation delay.
            if (pinched && TryPinchDistance(running.leftHand, out float leftDistance)
                && leftDistance <= (box != null ? box.PinchReleaseDistance : PinchReleaseDistance))
            { Cancel(); return; }
            if (!pinched) pinchNeedsRelease = false;

            float dt = Time.unscaledDeltaTime;
            // Discard tracking discontinuities rather than interpreting them as a swipe.
            if (dt <= 0 || dt > 0.1f) { Cancel(); return; }
            Vector3 position = input.TrackingSpace.TransformPoint(palm.position);
            Vector3 displacement = hasPalmSample
                ? input.TrackingSpace.TransformVector(palm.position - previousPalm) : Vector3.zero;
            Vector3 palmVelocity = displacement / dt;
            previousPalm = palm.position;
            hasPalmSample = true;
            float blend = SmoothingSeconds <= 0 ? 1 : 1 - Mathf.Exp(-dt / SmoothingSeconds);
            filteredVelocity = Vector3.Lerp(filteredVelocity, palmVelocity, blend);
            palmCollider.transform.position = position;
            palmCollider.radius = Mathf.Max(0.005f, PalmRadius);
            if (!IsPulling) aimedNode = FindGestureTarget(manager, currentSelection, position);
            if (filteredVelocity.magnitude < SwipeSpeed * 0.4f) swipeAvailable = true;
            UpdatePull(manager, currentSelection, wasPinched);
            palmCollider.enabled = open && !pinched;
            if (open && !pinched && !IsPulling)
            {
                bool swiped = currentSelection.Count == 0 && SwipeGraph(manager, currentSelection);
                if (!swiped) PushOnContact(manager, currentSelection, position, displacement);
            }
            else contacts.Clear();
            if (!moving) return;
            if (selectedNode != null && manager.GetMLNodeTransform(selectedNode) == null) { Cancel(); return; }
            // Exact exponential drag keeps coasting distance independent of frame rate.
            float decay = Mathf.Max(0.01f, MomentumDecay);
            float damping = Mathf.Exp(-decay * dt);
            offset += velocity * (IsPulling ? dt : (1 - damping) / decay);
            if (!IsPulling) velocity *= damping;
            if (movingWholeGraph) manager.PreviewHandGraphTranslation(offset);
            else
            {
                targets[selectedNode] = nodeStart + offset;
                manager.ApplyHandNodePositions(targets, false);
            }
            if (!IsPulling && velocity.sqrMagnitude < 0.0001f) Finish();
        }

        // Acquire and maintain a near-hand grab; release conditions prevent unintended re-grabs.
        bool UpdateDirectGrab(NetworkManager manager, HashSet<string> currentSelection,
            XRHand hand, Pose palm, bool open)
        {
            Vector3 position = input.TrackingSpace.TransformPoint(palm.position);
            palmCollider.transform.position = position;
            if (open) grabNeedsOpen = false;
            float middle, ring, little;
            if (!Bend(hand, XRHandJointID.MiddleProximal, out middle)
                || !Bend(hand, XRHandJointID.RingProximal, out ring)
                || !Bend(hand, XRHandJointID.LittleProximal, out little))
            { Cancel(); return true; }
            float threshold = directGrab ? GrabBendDegrees - 15f : GrabBendDegrees;
            int curled = (middle >= threshold ? 1 : 0) + (ring >= threshold ? 1 : 0)
                + (little >= threshold ? 1 : 0);
            if (directGrab)
            {
                var node = manager.GetMLNodeTransform(selectedNode);
                if (curled < 2 || node == null || !node.gameObject.activeInHierarchy)
                { Cancel(); return true; }
                targets[selectedNode] = position + grabOffset;
                manager.ApplyHandNodePositions(targets, false);
                return true;
            }
            if (grabNeedsOpen || curled < 2) return false;
            grabNeedsOpen = true; // One acquisition per close, including a missed grab.
            Physics.SyncTransforms();
            var hits = Physics.OverlapSphere(position, GrabRadius, interactor.NodeLayers,
                QueryTriggerInteraction.Collide);
            string nearest = null;
            float bestDistance = float.PositiveInfinity;
            var movable = new HashSet<string>(manager.GetMoveableNodes(manager.NodeIDToGUID.Values));
            foreach (var entry in manager.NodeIDToGUID)
            {
                if (!movable.Contains(entry.Value)) continue;
                var node = manager.GetMLNodeTransform(entry.Key.Item2, entry.Key.Item1);
                if (node == null || !node.gameObject.activeInHierarchy) continue;
                foreach (var hit in hits)
                {
                    if (hit.transform != node && !hit.transform.IsChildOf(node)) continue;
                    Vector3 toSurface = hit.ClosestPoint(position) - position;
                    float distance = toSurface.magnitude;
                    if (distance >= bestDistance) continue;
                    if (distance > 0.001f && Physics.Raycast(position, toSurface / distance, distance,
                        interactor.BlockingLayers, QueryTriggerInteraction.Collide)) continue;
                    nearest = entry.Value;
                    bestDistance = distance;
                }
            }
            if (nearest == null) return false;
            Finish();
            selectedNode = PullTarget = nearest;
            aimedNode = null;
            var grabbedNode = manager.GetMLNodeTransform(nearest);
            grabOffset = grabbedNode.position - position;
            selection = new HashSet<string>(currentSelection);
            moveManager = manager;
            moving = directGrab = true;
            movingWholeGraph = false;
            targets[nearest] = grabbedNode.position;
            velocity = filteredVelocity = Vector3.zero;
            palmCollider.enabled = false;
            contacts.Clear();
            return true;
        }

        // Move the saved pull target from the current pinch gesture.
        void UpdatePull(NetworkManager manager, HashSet<string> currentSelection, bool wasPinched)
        {
            if (IsPulling)
            {
                if (manager.GetMLNodeTransform(PullTarget) == null) { Cancel(); return; }
                if (!pinched) { PullTarget = null; aimedNode = null; return; }
                // A locked grab follows hand motion in all three dimensions.
                velocity = Vector3.ClampMagnitude(filteredVelocity * PullGain, MaximumSpeed);
                return;
            }
            // Acquire directly on pinch onset, without depending on the pointing interactor's hover.
            if (!pinched || wasPinched || pinchNeedsRelease || aimedNode == null) return;
            if (currentSelection.Count == 1 && !currentSelection.Contains(aimedNode)) return;
            var target = manager.GetMLNodeTransform(aimedNode);
            if (target == null || !target.gameObject.activeInHierarchy) return;
            if (!moving && !BeginPush(manager, currentSelection)) return;
            PullTarget = aimedNode;
            swipeAvailable = false;
            velocity = filteredVelocity = Vector3.zero;
        }

        bool SwipeGraph(NetworkManager manager, HashSet<string> currentSelection)
        {
            if (!swipeAvailable || aimedNode == null
                || filteredVelocity.sqrMagnitude < SwipeSpeed * SwipeSpeed) return false;
            var target = manager.GetMLNodeTransform(aimedNode);
            if (target == null || !target.gameObject.activeInHierarchy) { swipeAvailable = false; return false; }
            if (!moving && !BeginPush(manager, currentSelection)) return false;
            velocity = Vector3.ClampMagnitude(velocity + filteredVelocity * MovementGain, MaximumSpeed);
            // One impulse per stroke. Slow the hand before making another swipe.
            swipeAvailable = false;
            aimedNode = null;
            return true;
        }

        string FindGestureTarget(NetworkManager manager, HashSet<string> currentSelection, Vector3 palmPosition)
        {
            // Head-to-hand aim remains stable when fingers open or curl into a pinch.
            var camera = Camera.main;
            Vector3 direction = camera != null ? palmPosition - camera.transform.position
                : input.AimValid ? input.AimRay.direction : Vector3.zero;
            if (direction.sqrMagnitude < 0.0001f) return null;
            direction.Normalize();
            string best = null;
            float bestAngle = AimConeDegrees;
            Physics.SyncTransforms();
            foreach (var entry in manager.NodeIDToGUID)
            {
                if (currentSelection.Count == 1 && !currentSelection.Contains(entry.Value)) continue;
                var node = manager.GetMLNodeTransform(entry.Key.Item2, entry.Key.Item1);
                if (node == null || !node.gameObject.activeInHierarchy) continue;
                Vector3 toNode = node.position - palmPosition;
                float distance = toNode.magnitude;
                if (distance > interactor.MaxDistance || distance < 0.001f) continue;
                float angle = Vector3.Angle(direction, toNode);
                if (angle > bestAngle) continue;
                if (Physics.Raycast(palmPosition, toNode / distance, distance, interactor.BlockingLayers,
                    QueryTriggerInteraction.Collide)) continue;
                bestAngle = angle;
                best = entry.Value;
            }
            return best;
        }

        static bool TryPinchDistance(XRHand hand, out float distance)
        {
            distance = 0;
            if (!hand.isTracked || !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb)
                || !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index)) return false;
            distance = Vector3.Distance(thumb.position, index.position);
            return true;
        }

        void PushOnContact(NetworkManager manager, HashSet<string> currentSelection, Vector3 position, Vector3 displacement)
        {
            // Sweep the palm volume so a fast hand cannot skip a small node between frames.
            Physics.SyncTransforms();
            var hits = Physics.OverlapCapsule(position - displacement, position, palmCollider.radius,
                interactor.NodeLayers, QueryTriggerInteraction.Collide);
            nextContacts.Clear();
            bool impact = false;
            foreach (var entry in manager.NodeIDToGUID)
            {
                if (currentSelection.Count == 1 && !currentSelection.Contains(entry.Value)) continue;
                var node = manager.GetMLNodeTransform(entry.Key.Item2, entry.Key.Item1);
                if (node == null) continue;
                foreach (var hit in hits)
                {
                    if (hit.transform != node && !hit.transform.IsChildOf(node)) continue;
                    nextContacts.Add(entry.Value);
                    // Only transfer motion towards the surface, never pull a node on withdrawal.
                    Vector3 toward = hit.ClosestPoint(position - displacement) - (position - displacement);
                    if (toward.sqrMagnitude < 0.000001f) toward = hit.bounds.center - (position - displacement);
                    if (!contacts.Contains(entry.Value) && Vector3.Dot(filteredVelocity, toward) > 0)
                        impact = true;
                    break;
                }
            }
            contacts.IntersectWith(nextContacts);
            if (!impact || filteredVelocity.sqrMagnitude < 0.0001f) return;
            if (!moving && !BeginPush(manager, currentSelection)) return;
            contacts.UnionWith(nextContacts);
            // One impulse per contact, regardless of the number of overlapping node colliders.
            velocity = Vector3.ClampMagnitude(velocity + filteredVelocity * MovementGain, MaximumSpeed);
        }

        bool BeginPush(NetworkManager manager, HashSet<string> currentSelection)
        {
            selectedNode = null;
            if (currentSelection.Count == 0)
            {
                if (!manager.BeginHandGraphTranslation()) return false;
                movingWholeGraph = true;
            }
            else
            {
                // Never expand a selection into neighbours or move multiple selected nodes.
                foreach (var guid in manager.GetMoveableNodes(currentSelection))
                {
                    var node = manager.GetMLNodeTransform(guid);
                    if (node == null) continue;
                    selectedNode = guid;
                    nodeStart = node.position;
                    break;
                }
                if (selectedNode == null) return false;
                movingWholeGraph = false;
            }
            selection = new HashSet<string>(currentSelection);
            moveManager = manager;
            offset = velocity = Vector3.zero;
            moving = true;
            return true;
        }

        bool TryOpenPalm(XRHand hand, out Pose palm, out bool open)
        {
            palm = default;
            open = false;
            if (!hand.isTracked || !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out palm)) return false;
            float index, middle, ring, little;
            bool valid = Bend(hand, XRHandJointID.IndexProximal, out index)
                & Bend(hand, XRHandJointID.MiddleProximal, out middle)
                & Bend(hand, XRHandJointID.RingProximal, out ring)
                & Bend(hand, XRHandJointID.LittleProximal, out little);
            open = valid && index < OpenFingerMaxBend && middle < OpenFingerMaxBend
                && ring < OpenFingerMaxBend && little < OpenFingerMaxBend;
            return valid;
        }

        static bool Bend(XRHand hand, XRHandJointID proximal, out float angle)
        {
            angle = 0;
            if (!hand.GetJoint(proximal).TryGetPose(out var a)
                || !hand.GetJoint((XRHandJointID)((int)proximal + 1)).TryGetPose(out var b)
                || !hand.GetJoint((XRHandJointID)((int)proximal + 2)).TryGetPose(out var c)) return false;
            Vector3 u = b.position - a.position, v = c.position - b.position;
            if (u.sqrMagnitude < 0.000001f || v.sqrMagnitude < 0.000001f) return false;
            angle = Vector3.Angle(u, v);
            return true;
        }

        // Complete the current movement and release its temporary state.
        void Finish()
        {
            if (!moving) return;
            moving = false;
            if (moveManager != null)
            {
                if (movingWholeGraph) moveManager.FinishHandGraphTranslation();
                else moveManager.ApplyHandNodePositions(targets, true);
            }
            moveManager = null;
            if (movingWholeGraph && voice != null) voice.SuppressFistUntilOpen();
            movingWholeGraph = false;
            selectedNode = null;
            selection = null;
            velocity = offset = Vector3.zero;
            targets.Clear();
        }

        // Clear active movement/contact state on interruption, disable, or focus loss.
        void Cancel()
        {
            directGrab = false;
            grabNeedsOpen = true;
            PullTarget = aimedNode = null;
            swipeAvailable = false;
            pinched = false;
            pinchNeedsRelease = true;
            Finish();
            hasPalmSample = false;
            filteredVelocity = Vector3.zero;
            contacts.Clear();
            if (palmCollider != null) palmCollider.enabled = false;
        }
        void OnDisable() { Cancel(); }
        void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
        void OnDestroy() { if (palmCollider != null) Destroy(palmCollider.gameObject); }
    }
}
