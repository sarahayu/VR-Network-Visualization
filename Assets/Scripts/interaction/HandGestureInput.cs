// Converts tracked right-hand joints or Meta Aim into a world-space aiming ray.
// Pointing detection uses bend thresholds, hysteresis, and a hold time; ray smoothing
// reduces jitter. TrackingSpace must be the tracking-space parent, not the camera.
// Tracking loss clears state so consumers cannot reuse a stale gesture.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace VidiGraph
{
    /// <summary>Right-hand pointing pose and selectable aiming ray, expressed in world space.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class HandGestureInput : MonoBehaviour
    {
        public enum RaySource { IndexFingertip, MetaAim }

        [Tooltip("Index Fingertip starts at the fingertip and follows the final finger segment. Meta Aim uses the runtime aiming pose.")]
        public RaySource AimSource = RaySource.IndexFingertip;
        [Tooltip("Camera Offset: the tracking-space parent of the headset camera, not the camera itself.")]
        public Transform TrackingSpace;
        [Range(0, 90)] public float IndexMaxBend = 35f;
        [Range(0, 120)] public float OtherFingersMinBend = 30f;
        [Range(0, 20)] public float PoseHysteresis = 10f;
        [Min(0)] public float PointingHoldSeconds = 0.12f;
        [Min(0)] public float RaySmoothingSeconds = 0.04f;

        [SerializeField] bool tracked;
        [SerializeField] bool aimValid;
        [SerializeField] bool pointing;
        public bool IsTracked => tracked;
        public bool AimValid => aimValid;
        public bool IsPointing => pointing;
        public Ray AimRay { get; private set; }

        readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
        XRHandSubsystem subsystem;
        float held;
        bool hasRay;
        RaySource previousSource;

        void Reset() { TrackingSpace = transform.parent; }

        // Read current tracking, apply pose hold/hysteresis, and update the aim ray only from valid input.
        void Update()
        {
            if (TrackingSpace == null) { Clear(); return; }
            if (subsystem == null || !subsystem.running)
            {
                subsystem = null;
                SubsystemManager.GetSubsystems(subsystems);
                foreach (var candidate in subsystems)
                    if (candidate.running) { subsystem = candidate; break; }
            }
            if (subsystem == null) { Clear(); return; }
            var hand = subsystem.rightHand;
            tracked = hand.isTracked;
            if (!tracked) { Clear(); return; }

            float index, middle, ring, little;
            bool validJoints = TryBend(hand, XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, out index)
                & TryBend(hand, XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, out middle)
                & TryBend(hand, XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, out ring)
                & TryBend(hand, XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, out little);
            float margin = pointing ? PoseHysteresis : 0f;
            // Allow one relaxed finger; an open palm still cannot qualify.
            int curled = (middle >= OtherFingersMinBend - margin ? 1 : 0)
                + (ring >= OtherFingersMinBend - margin ? 1 : 0)
                + (little >= OtherFingersMinBend - margin ? 1 : 0);
            bool pose = validJoints && index <= IndexMaxBend + margin && curled >= 2;
            held = pose ? held + Time.unscaledDeltaTime : 0f;
            pointing = pose && held >= PointingHoldSeconds;

            aimValid = false;
            if (previousSource != AimSource) hasRay = false;
            previousSource = AimSource;
            if (AimSource == RaySource.IndexFingertip)
            {
                if (hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var tip)
                    && hand.GetJoint(XRHandJointID.IndexDistal).TryGetPose(out var distal))
                {
                    Vector3 direction = tip.position - distal.position;
                    if (direction.sqrMagnitude >= 0.000001f)
                        SetRay(TrackingSpace.TransformPoint(tip.position),
                            TrackingSpace.TransformDirection(direction).normalized, false);
                }
            }
            else
            {
#if ENABLE_VR || UNITY_GAMECORE
            var aim = MetaAimHand.right;
            if (aim != null && aim.isTracked.isPressed
                && (((MetaAimFlags)aim.aimFlags.ReadValue() & MetaAimFlags.Valid) != 0))
            {
                Vector3 origin = TrackingSpace.TransformPoint(aim.devicePosition.ReadValue());
                Vector3 direction = TrackingSpace.TransformDirection(aim.deviceRotation.ReadValue() * Vector3.forward).normalized;
                SetRay(origin, direction, true);
            }
#endif
            }
            // Meta aim can be unavailable even when joint tracking is healthy.
            if (!aimValid && hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var fallbackTip)
                && hand.GetJoint(XRHandJointID.IndexDistal).TryGetPose(out var fallbackDistal))
            {
                Vector3 direction = fallbackTip.position - fallbackDistal.position;
                if (direction.sqrMagnitude >= 0.000001f)
                    SetRay(TrackingSpace.TransformPoint(fallbackTip.position),
                        TrackingSpace.TransformDirection(direction).normalized, false);
            }
            hasRay = aimValid && pointing;
        }

        // Smooth the aim ray while preserving a normalized world-space direction.
        void SetRay(Vector3 origin, Vector3 direction, bool smoothOrigin)
        {
            float blend = !hasRay || RaySmoothingSeconds <= 0 ? 1f
                : 1f - Mathf.Exp(-Time.unscaledDeltaTime / RaySmoothingSeconds);
            // Keep the fingertip origin exact; smoothing its position makes the ray trail the hand.
            AimRay = new Ray(smoothOrigin ? Vector3.Lerp(AimRay.origin, origin, blend) : origin,
                hasRay ? Vector3.Slerp(AimRay.direction, direction, blend).normalized : direction);
            aimValid = true;
        }

        static bool TryBend(XRHand hand, XRHandJointID a, XRHandJointID b, XRHandJointID c, out float bend)
        {
            bend = 0;
            if (!hand.GetJoint(a).TryGetPose(out var pa) || !hand.GetJoint(b).TryGetPose(out var pb)
                || !hand.GetJoint(c).TryGetPose(out var pc)) return false;
            Vector3 first = pb.position - pa.position, second = pc.position - pb.position;
            if (first.sqrMagnitude < 0.000001f || second.sqrMagnitude < 0.000001f) return false;
            bend = Vector3.Angle(first, second);
            return true;
        }

        // Invalidate the cached pose and ray when tracking or required joints are unavailable.
        void Clear() { tracked = aimValid = pointing = hasRay = false; held = 0; }
        void OnDisable() { Clear(); subsystem = null; }
    }
}
