using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

public class ControllerTranscriptPanel : MonoBehaviour
{
    private Transform cameraTransform;
    private Vector3 sphereReferenceForward = Vector3.forward;
    private bool hasSphereReferenceForward;

    [Header("Position")]
    [Tooltip("OFF (default): the panel stays exactly where you place it in the scene. " +
             "ON: the panel continuously trails the camera — intended for recording a demo video, " +
             "where the hint needs to stay in frame.")]
    [SerializeField] private bool followCamera;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.08f, 0.18f);

    [Header("Spherical Placement")]
    [SerializeField] private bool useSphericalPlacement = true;
    [FormerlySerializedAs("enableRightJoystickDepthControl")]
    [SerializeField] private bool enableRightJoystickRadiusControl = true;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private float joystickMoveSpeed = 0.25f;
    [SerializeField] private float joystickAngleSpeed = 90f;
    [SerializeField] private float joystickDeadzone = 0.2f;
    [SerializeField] private float panelRadius = 0.2f;
    [SerializeField] private float panelHorizontalAngle;
    [FormerlySerializedAs("minLocalForwardOffset")]
    [SerializeField] private float minPanelRadius = 0.08f;
    [FormerlySerializedAs("maxLocalForwardOffset")]
    [SerializeField] private float maxPanelRadius = 0.55f;

    [Header("Grip Height Control")]
    [SerializeField] private bool enableGripHeightControl = true;
    [SerializeField] private float joystickHeightSpeed = 0.25f;
    [SerializeField] private float panelHeightOffset;
    [SerializeField] private float maxHeightOffset = 0.55f;

    [Header("Collision")]
    [SerializeField] private bool preventClipping = true;
    [SerializeField] private LayerMask blockingLayers = ~0;
    [SerializeField] private float collisionRadius = 0.08f;
    [SerializeField] private float collisionPadding = 0.02f;
    [SerializeField] private Transform collisionIgnoreRoot;

    [Header("Lag")]
    [SerializeField] private float movementLagSeconds = 0.05f;
    [SerializeField] private float maxLagDistance = 0.04f;

    [Header("Rotation")]
    [SerializeField] private bool alwaysFacePlayer = true;
    [SerializeField] private Vector3 rotationOffsetEuler = new Vector3(0f, 180f, 0f);
    [SerializeField] private bool keepRotationUpright = true;

    [Header("Smoothing")]
    [SerializeField] private float positionSmooth = 20f;
    [SerializeField] private float rotationSmooth = 20f;

    [Header("Visibility")]
    [Tooltip("Assign the top-level Mic Hint panel containing the background, text, and all other visuals.")]
    [SerializeField] private GameObject panelVisualRoot;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private XRInputButtonReader micHintButton = new XRInputButtonReader("MicHintButton");
    [FormerlySerializedAs("dismissButton")]
    [SerializeField] private XRInputButtonReader visibilityToggleButton = new XRInputButtonReader("VisibilityToggleButton");
    [SerializeField] private bool startVisible = false;
    [FormerlySerializedAs("fadeDuration")]
    [SerializeField] private float popDuration = 0.25f;

    private Vector3 previousReferencePosition;
    private bool hasPreviousReferencePosition;
    private XRInputValueReader<Vector2> rightJoystick;
    private XRInputButtonReader rightGrip;
    private bool panelVisible;
    private Renderer[] panelRenderers;
    private bool[] panelRendererInitialStates;
    private Transform panelVisualTransform;
    private Vector3 panelVisualScale;
    private Coroutine visibilityRoutine;

    private void Awake()
    {
        GameObject canvasGroupTarget = panelVisualRoot != null ? panelVisualRoot : gameObject;
        panelVisualTransform = canvasGroupTarget.transform;
        panelVisualScale = panelVisualTransform.localScale;

        if (panelCanvasGroup == null)
            panelCanvasGroup = canvasGroupTarget.GetComponent<CanvasGroup>();

        if (panelCanvasGroup == null)
            panelCanvasGroup = canvasGroupTarget.AddComponent<CanvasGroup>();

        panelRenderers = canvasGroupTarget.GetComponentsInChildren<Renderer>(true);
        panelRendererInitialStates = new bool[panelRenderers.Length];
        for (int i = 0; i < panelRenderers.Length; i++)
            panelRendererInitialStates[i] = panelRenderers[i].enabled;
    }

    private void OnEnable()
    {
        micHintButton.EnableDirectActionIfModeUsed();
        visibilityToggleButton.EnableDirectActionIfModeUsed();

        panelVisible = startVisible;
        SetPanelVisibleImmediate(panelVisible);

        if (rightJoystick != null)
            rightJoystick.EnableDirectActionIfModeUsed();

        if (rightGrip != null)
            rightGrip.EnableDirectActionIfModeUsed();

    }

    private void Update()
    {
        if (visibilityToggleButton != null && visibilityToggleButton.ReadWasPerformedThisFrame())
        {
            panelVisible = !panelVisible;
            SetPanelVisible(panelVisible);
            return;
        }

        if (micHintButton != null && micHintButton.ReadWasPerformedThisFrame())
        {
            panelVisible = !panelVisible;

            if (panelVisible)
            {
                Transform sphereOrigin = GetSphereOrigin();
                CaptureSphereReferenceForward(sphereOrigin);
                panelHorizontalAngle = 0f;
            }

            SetPanelVisible(panelVisible);
        }
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelCanvasGroup == null)
            return;

        if (visibilityRoutine != null)
            StopCoroutine(visibilityRoutine);

        visibilityRoutine = StartCoroutine(PopPanel(visible));
    }

    private IEnumerator PopPanel(bool visible)
    {
        if (visible)
            SetRenderersVisible(true);

        float startAlpha = panelCanvasGroup.alpha;
        float targetAlpha = visible ? 1f : 0f;
        Vector3 startScale = panelVisualTransform.localScale;
        Vector3 targetScale = visible ? panelVisualScale : Vector3.zero;
        float elapsed = 0f;

        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;

        while (elapsed < popDuration)
        {
            float t = popDuration > 0f ? elapsed / popDuration : 1f;
            float smooth = Mathf.SmoothStep(0f, 1f, t);
            panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smooth);
            panelVisualTransform.localScale = Vector3.Lerp(startScale, targetScale, smooth);
            elapsed += Time.deltaTime;
            yield return null;
        }

        panelCanvasGroup.alpha = targetAlpha;
        panelVisualTransform.localScale = targetScale;
        panelCanvasGroup.interactable = visible;
        panelCanvasGroup.blocksRaycasts = visible;

        if (!visible)
            SetRenderersVisible(false);

        visibilityRoutine = null;
    }

    private void SetPanelVisibleImmediate(bool visible)
    {
        if (panelCanvasGroup == null)
            return;

        panelCanvasGroup.alpha = visible ? 1f : 0f;
        panelCanvasGroup.interactable = visible;
        panelCanvasGroup.blocksRaycasts = visible;
        panelVisualTransform.localScale = visible ? panelVisualScale : Vector3.zero;

        SetRenderersVisible(visible);
    }

    private void SetRenderersVisible(bool visible)
    {

        if (panelRenderers == null)
            return;

        for (int i = 0; i < panelRenderers.Length; i++)
        {
            if (panelRenderers[i] != null)
                panelRenderers[i].enabled = visible && panelRendererInitialStates[i];
        }
    }

    private void Start()
    {
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
            CaptureSphereReferenceForward(cameraTransform);
        }

        if (inputManager == null)
            inputManager = GameObject.Find("/Input Manager")?.GetComponent<InputManager>();

        rightJoystick = inputManager != null ? inputManager.RightJoystick : null;
        rightGrip = inputManager != null ? inputManager.RightGrip : null;
        rightJoystick?.EnableDirectActionIfModeUsed();
        rightGrip?.EnableDirectActionIfModeUsed();

        panelRadius = Mathf.Clamp(
            panelRadius > 0f ? panelRadius : localOffset.magnitude,
            minPanelRadius,
            maxPanelRadius
        );
    }

    private void LateUpdate()
    {
        // Parked mode: leave the panel at its authored transform. Everything below
        // (spherical placement, lag, collision, facing) only runs when the panel is
        // meant to follow the camera.
        if (!followCamera)
            return;

        Transform positionReference = GetSphereOrigin();
        Transform rotationReference = positionReference;

        if (positionReference == null)
            return;

        ApplyRightJoystickRadiusControl();

        Vector3 targetPosition = useSphericalPlacement
            ? positionReference.position + GetSphericalWorldOffset(positionReference)
            : positionReference.position + positionReference.TransformDirection(GetCurrentLocalOffset(positionReference));
        Vector3 referenceVelocity = Vector3.zero;

        if (hasPreviousReferencePosition && Time.deltaTime > 0f)
        {
            referenceVelocity = (positionReference.position - previousReferencePosition) / Time.deltaTime;
        }

        Vector3 lagOffset = Vector3.ClampMagnitude(
            -referenceVelocity * movementLagSeconds,
            maxLagDistance
        );

        targetPosition += lagOffset;
        targetPosition = GetCollisionSafePosition(positionReference.position, targetPosition);
        previousReferencePosition = positionReference.position;
        hasPreviousReferencePosition = true;

        Quaternion baseRotation = GetBaseRotation(positionReference, rotationReference, targetPosition);
        Quaternion targetRotation = baseRotation * Quaternion.Euler(rotationOffsetEuler);

        float posT = 1f - Mathf.Exp(-positionSmooth * Time.deltaTime);
        float rotT = 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime);

        transform.position = Vector3.Lerp(transform.position, targetPosition, posT);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotT);
    }

    private void ApplyRightJoystickRadiusControl()
    {
        if (!enableRightJoystickRadiusControl || rightJoystick == null)
            return;

        Vector2 joystickInput = rightJoystick.ReadValue();
        float angleInput = joystickInput.x;
        float radiusInput = joystickInput.y;
        bool gripHeld = enableGripHeightControl
            && rightGrip != null
            && rightGrip.ReadIsPerformed();

        if (gripHeld)
        {
            angleInput = 0f;
        }
        else if (Mathf.Abs(radiusInput) > Mathf.Abs(angleInput))
        {
            angleInput = 0f;
        }
        else
        {
            radiusInput = 0f;
        }

        if (Mathf.Abs(angleInput) >= joystickDeadzone)
        {
            panelHorizontalAngle = Mathf.Repeat(
                panelHorizontalAngle + angleInput * joystickAngleSpeed * Time.deltaTime + 180f,
                360f
            ) - 180f;
        }

        if (Mathf.Abs(radiusInput) >= joystickDeadzone)
        {
            if (gripHeld)
            {
                panelHeightOffset = Mathf.Clamp(
                    panelHeightOffset + radiusInput * joystickHeightSpeed * Time.deltaTime,
                    -maxHeightOffset,
                    maxHeightOffset
                );
            }
            else
            {
                panelRadius = Mathf.Clamp(
                    panelRadius + radiusInput * joystickMoveSpeed * Time.deltaTime,
                    minPanelRadius,
                    maxPanelRadius
                );
            }
        }
    }

    private Vector3 GetCurrentLocalOffset(Transform positionReference)
    {
        return localOffset;
    }

    private Vector3 GetSphericalWorldOffset(Transform sphereOrigin)
    {
        if (!hasSphereReferenceForward)
            CaptureSphereReferenceForward(sphereOrigin);

        Vector3 horizontalDirection = Quaternion.AngleAxis(
            panelHorizontalAngle,
            Vector3.up
        ) * sphereReferenceForward;

        return horizontalDirection.normalized * panelRadius
            + Vector3.up * panelHeightOffset;
    }

    private void CaptureSphereReferenceForward(Transform sphereOrigin)
    {
        if (sphereOrigin == null)
            return;

        Vector3 initialForward = sphereOrigin.forward;
        initialForward.y = 0f;

        if (initialForward.sqrMagnitude < 0.0001f)
            initialForward = Vector3.forward;

        sphereReferenceForward = initialForward.normalized;
        hasSphereReferenceForward = true;
    }

    private Transform GetHeightReference()
    {
        return GetSphereOrigin();
    }

    private Transform GetSphereOrigin()
    {
        if (cameraTransform != null)
            return cameraTransform;

        return Camera.main != null ? Camera.main.transform : null;
    }

    private Quaternion GetBaseRotation(Transform positionReference, Transform rotationReference, Vector3 targetPosition)
    {
        if (alwaysFacePlayer)
        {
            Vector3 playerFacingDirection = targetPosition - GetHeightReferencePosition();

            if (keepRotationUpright)
                playerFacingDirection.y = 0f;

            if (playerFacingDirection.sqrMagnitude < 0.0001f)
                playerFacingDirection = transform.forward;

            if (playerFacingDirection.sqrMagnitude < 0.0001f)
                playerFacingDirection = Vector3.forward;

            return Quaternion.LookRotation(playerFacingDirection.normalized, Vector3.up);
        }

        Transform reference = rotationReference != null ? rotationReference : positionReference;

        if (!keepRotationUpright)
            return reference.rotation;

        Vector3 forward = reference.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = positionReference.position - GetHeightReferencePosition();
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private Vector3 GetHeightReferencePosition()
    {
        Transform heightReference = GetHeightReference();
        return heightReference != null ? heightReference.position : Vector3.zero;
    }

    private Vector3 GetCollisionSafePosition(Vector3 fromPosition, Vector3 targetPosition)
    {
        if (!preventClipping)
            return targetPosition;

        Vector3 direction = targetPosition - fromPosition;
        float distance = direction.magnitude;

        if (distance <= 0.0001f)
            return targetPosition;

        direction /= distance;

        RaycastHit[] hits = Physics.SphereCastAll(
            fromPosition,
            collisionRadius,
            direction,
            distance + collisionPadding,
            blockingLayers,
            QueryTriggerInteraction.Ignore
        );

        RaycastHit closestHit = default;
        bool foundHit = false;
        float closestDistance = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            if (ShouldIgnoreCollisionHit(hits[i].collider))
                continue;

            if (hits[i].distance < closestDistance)
            {
                closestHit = hits[i];
                closestDistance = hits[i].distance;
                foundHit = true;
            }
        }

        if (foundHit)
        {
            float safeDistance = Mathf.Max(0f, closestHit.distance - collisionPadding);
            return fromPosition + direction * safeDistance;
        }

        return targetPosition;
    }

    private bool ShouldIgnoreCollisionHit(Collider hitCollider)
    {
        if (hitCollider == null)
            return true;

        Transform hitTransform = hitCollider.transform;

        if (hitTransform == transform || hitTransform.IsChildOf(transform))
            return true;

        if (collisionIgnoreRoot != null && (hitTransform == collisionIgnoreRoot || hitTransform.IsChildOf(collisionIgnoreRoot)))
            return true;

        return false;
    }
}
