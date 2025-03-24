using System;
#if BURST_PRESENT
using Unity.Burst;
#endif
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.XR.Interaction.Toolkit.Utilities;
using UnityEngine.XR.Interaction.Toolkit.Utilities.Curves;
using UnityEngine.XR.Interaction.Toolkit.Utilities.Internal;

namespace UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals
{
    [DisallowMultipleComponent]
    [AddComponentMenu("XR/Visual/Curve Visual Controller", 11)]
#if BURST_PRESENT
    [BurstCompile]
#endif
    public class PointerVisualController : MonoBehaviour
    {
        [SerializeField]
        LineRenderer m_LineRenderer;

        public LineRenderer lineRenderer
        {
            get => m_LineRenderer;
            set
            {
                m_LineRenderer = value;
                m_LineRenderer.useWorldSpace = false;
            }
        }

        [SerializeField]
        NearFarInteractor m_CurveVisualObject;

        public ICurveInteractionDataProvider curveInteractionDataProvider
        {
            get => m_CurveVisualObject;
            set => m_CurveVisualObject = (NearFarInteractor)value;
        }

        [SerializeField]
        Transform m_LineOriginTransform;

        public Transform lineOriginTransform
        {
            get => m_LineOriginTransform;
            set => m_LineOriginTransform = value;
        }

        [SerializeField]
        int m_VisualPointCount = 20;

        public int visualPointCount
        {
            get => m_VisualPointCount;
            set => m_VisualPointCount = value;
        }

        [SerializeField]
        float m_PointerDistance = 10f;

        public float pointerDistance
        {
            get => m_PointerDistance;
            set => m_PointerDistance = value;
        }

        [SerializeField]
        float m_PointerWidth = 0.005f;

        public float pointerWidth
        {
            get => m_PointerWidth;
            set => m_PointerWidth = value;
        }

        [SerializeField]
        bool m_SnapToSelectedAttachIfAvailable = true;

        public bool snapToSelectedAttachIfAvailable
        {
            get => m_SnapToSelectedAttachIfAvailable;
            set => m_SnapToSelectedAttachIfAvailable = value;
        }

        [SerializeField]
        bool m_SnapToSnapVolumeIfAvailable = true;

        public bool snapToSnapVolumeIfAvailable
        {
            get => m_SnapToSnapVolumeIfAvailable;
            set => m_SnapToSnapVolumeIfAvailable = value;
        }

        [SerializeField]
        float m_CurveStartOffset;

        public float curveStartOffset
        {
            get => m_CurveStartOffset;
            set => m_CurveStartOffset = value;
        }

        [SerializeField]
        float m_CurveEndOffset = 0.005f;

        public float curveEndOffset
        {
            get => m_CurveEndOffset;
            set => m_CurveEndOffset = value;
        }

        [SerializeField]
        float m_LinePropertyAnimationSpeed = 8f;

        public float linePropertyAnimationSpeed
        {
            get => m_LinePropertyAnimationSpeed;
            set => m_LinePropertyAnimationSpeed = value;
        }

        [SerializeField]
        LineProperties m_NoValidHitProperties;

        public LineProperties noValidHitProperties
        {
            get => m_NoValidHitProperties;
            set => m_NoValidHitProperties = value;
        }

        // [SerializeField]
        // LineProperties m_UIHitProperties;

        // /// <summary>
        // /// Line properties when a valid UI hit is detected.
        // /// </summary>
        // public LineProperties uiHitProperties
        // {
        //     get => m_UIHitProperties;
        //     set => m_UIHitProperties = value;
        // }

        // [SerializeField]
        // LineProperties m_UIPressHitProperties;

        // /// <summary>
        // /// Line properties when a valid UI press hit is detected.
        // /// </summary>
        // public LineProperties uiPressHitProperties
        // {
        //     get => m_UIPressHitProperties;
        //     set => m_UIPressHitProperties = value;
        // }

        [SerializeField]
        LineProperties m_SelectHitProperties;

        public LineProperties selectHitProperties
        {
            get => m_SelectHitProperties;
            set => m_SelectHitProperties = value;
        }

        [SerializeField]
        LineProperties m_HoverHitProperties;

        public LineProperties hoverHitProperties
        {
            get => m_HoverHitProperties;
            set => m_HoverHitProperties = value;
        }

        NativeArray<Vector3> m_InternalSamplePoints;

        int m_LastPosCount;

        float m_EndPointTypeChangeTime;

        float m_LastBendRatio = 0.5f;
        Gradient m_LerpGradient;

        protected void Awake()
        {
            if (m_LineRenderer == null)
            {
                m_LineRenderer = GetComponentInChildren<LineRenderer>();
            }

            m_LineRenderer.useWorldSpace = true;

            if (m_LineOriginTransform == null)
                m_LineOriginTransform = transform;

            m_LineRenderer.startWidth = m_PointerWidth;
            m_LineRenderer.endWidth = m_PointerWidth;
            m_LerpGradient = m_LineRenderer.colorGradient;
        }

        protected void OnDestroy()
        {
            if (m_InternalSamplePoints.IsCreated)
                m_InternalSamplePoints.Dispose();
        }

        protected void LateUpdate()
        {
            var curveData = curveInteractionDataProvider;
            if (!curveData.isActive)
            {
                m_LineRenderer.enabled = false;
                return;
            }

            m_LineRenderer.enabled = true;
            m_LineRenderer.useWorldSpace = true;

            ValidatePointCount();

            Vector3 pointerOrigin = m_LineOriginTransform.position;
            Vector3 pointerDirection = m_LineOriginTransform.forward;

            GetEndpointInformation(pointerOrigin, out EndPointType endPointType, out Vector3 curveInteractionDirection);
            Vector3 pointerEndPoint = pointerOrigin + curveInteractionDirection * m_PointerDistance;

            UpdateGradient(endPointType);
            UpdateLinePoints(pointerOrigin, pointerEndPoint, pointerDirection, m_CurveStartOffset, m_CurveEndOffset);
        }

        void GetEndpointInformation(Vector3 worldOrigin, out EndPointType endPointType, out Vector3 castDirection)
        {
            endPointType = curveInteractionDataProvider.TryGetCurveEndPoint(out var _, m_SnapToSelectedAttachIfAvailable, m_SnapToSnapVolumeIfAvailable);
            castDirection = math.normalize(curveInteractionDataProvider.lastSamplePoint - worldOrigin);
        }

        void UpdateLinePoints(Vector3 worldOrigin, Vector3 worldEndPoint, Vector3 worldDirection, float startOffset = 0f, float endOffset = 0f, bool forceStraightLineFallback = false)
        {
            var float3TargetPoints = m_InternalSamplePoints.Reinterpret<float3>();
            float bendRatio = GetLineBendRatio(EndPointType.EmptyCastHit);
            bool shouldDrawCurve = forceStraightLineFallback || bendRatio < 1f;
            bool curveGenerated = false;

            Vector3 origin = worldOrigin;
            Vector3 endPoint = worldEndPoint;

            if (shouldDrawCurve)
            {
                Vector3 direction = worldDirection;

                curveGenerated = XRCurveUtility.TryGenerateCubicBezierCurve(
                    m_VisualPointCount, bendRatio, origin, direction, endPoint, ref float3TargetPoints, 0.06f, startOffset, endOffset);
            }

            if (!curveGenerated)
            {
                var float3FallBackPoints = m_InternalSamplePoints.Reinterpret<float3>();
                if (ComputeFallBackLine(origin, endPoint, startOffset, endOffset, ref float3FallBackPoints))
                    SetLinePositions(m_InternalSamplePoints, 3);
                else
                    m_LineRenderer.enabled = false;
                return;
            }

            SetLinePositions(m_InternalSamplePoints, m_VisualPointCount);
        }

        void GetLineProperties(EndPointType endPointType, out LineProperties properties)
        {
            properties = endPointType switch
            {
                EndPointType.None => m_NoValidHitProperties,
                EndPointType.EmptyCastHit => m_NoValidHitProperties,
                EndPointType.ValidCastHit => curveInteractionDataProvider.hasValidSelect ? m_SelectHitProperties : m_HoverHitProperties,
                EndPointType.AttachPoint => m_SelectHitProperties,
                _ => m_NoValidHitProperties
            };
        }

        float GetLineBendRatio(EndPointType endPointType)
        {
            GetLineProperties(endPointType, out var properties);
            if (!properties.smoothlyCurveLine)
                return 1f;

            if (m_LinePropertyAnimationSpeed > 0f)
            {
                m_LastBendRatio = Mathf.Lerp(m_LastBendRatio, properties.lineBendRatio, Time.unscaledDeltaTime * m_LinePropertyAnimationSpeed);
                return m_LastBendRatio;
            }

            return properties.lineBendRatio;
        }

        void UpdateGradient(EndPointType endPointType)
        {
            GetLineProperties(endPointType, out var properties);
            if (!properties.adjustGradient)
                return;

            var timeSinceLastChange = Time.unscaledTime - m_EndPointTypeChangeTime;
            if (m_LinePropertyAnimationSpeed > 0 && timeSinceLastChange < 1f)
                GradientUtility.Lerp(m_LerpGradient, properties.gradient, m_LerpGradient, Time.unscaledDeltaTime * m_LinePropertyAnimationSpeed);
            else
                GradientUtility.CopyGradient(properties.gradient, m_LerpGradient);

            m_LineRenderer.colorGradient = m_LerpGradient;
        }

        void SetLinePositions(NativeArray<Vector3> targetPoints, int numPoints)
        {
            if (numPoints != m_LastPosCount)
            {
                m_LineRenderer.positionCount = numPoints;
                m_LastPosCount = numPoints;
            }

            m_LineRenderer.SetPositions(targetPoints);
        }

        void ValidatePointCount()
        {
            bool isCreated = m_InternalSamplePoints.IsCreated;
            if (isCreated && m_InternalSamplePoints.Length == m_VisualPointCount)
                return;

            if (isCreated)
                m_InternalSamplePoints.Dispose();

            m_InternalSamplePoints = new NativeArray<Vector3>(m_VisualPointCount, Allocator.Persistent);
            m_LineRenderer.positionCount = m_VisualPointCount;
        }

#if UNITY_2022_2_OR_NEWER && BURST_PRESENT
        [BurstCompile]
#endif
        static bool ComputeFallBackLine(in float3 curveOrigin, in float3 endPoint, float startOffset, float endOffset, ref NativeArray<float3> fallBackTargetPoints)
        {
            var originToEnd = endPoint - curveOrigin;

            // If the distance is too small or zero, the line is not stable enough to draw.
            // This also avoids a division by zero in the normalize function producing NaN points.
            const float k_DisableSquaredLength = 0.01f * 0.01f;
            var squaredLength = math.lengthsq(originToEnd);
            if (squaredLength < k_DisableSquaredLength)
                return false;

            // Normalize the direction vector, equivalent to math.normalize(originToEnd)
            var normalizedDirection = math.rsqrt(squaredLength) * originToEnd;

            // Use linear interpolation between curveOrigin and endPoint to draw a straight line
            float3 startPoint = curveOrigin + (normalizedDirection * startOffset);
            float3 endPointOffset = endPoint - (normalizedDirection * endOffset);

            // Calculate direction vectors
            float3 directionToEnd = math.normalize(endPoint - startPoint);
            float3 directionToEndOffset = math.normalize(endPointOffset - startPoint);

            // Check if the offset end point is behind the start point using dot product, to determine if curve is reversed and invalid.
            if (math.dot(directionToEnd, directionToEndOffset) < 0f)
                return false;

            fallBackTargetPoints[0] = startPoint;
            fallBackTargetPoints[1] = math.lerp(startPoint, endPointOffset, 0.5f);
            fallBackTargetPoints[2] = endPointOffset;
            return true;
        }
    }
}