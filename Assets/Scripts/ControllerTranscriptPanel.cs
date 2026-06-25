using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

public class ControllerTranscriptPanel : MonoBehaviour
{
    private enum ReferenceMode
    {
        Controller,
        MainCamera,
        Custom
    }

    [Header("References")]
    [SerializeField] private Transform controller;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform playerHeightReference;
    [SerializeField] private Transform customPositionReference;
    [SerializeField] private Transform customRotationReference;

    [Header("Reference Modes")]
    [SerializeField] private ReferenceMode positionReferenceMode = ReferenceMode.Controller;
    [SerializeField] private ReferenceMode rotationReferenceMode = ReferenceMode.Controller;

    [Header("Position")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.08f, 0.18f);

    [Header("Spherical Placement")]
    [SerializeField] private bool useSphericalPlacement = true;
    [FormerlySerializedAs("enableRightJoystickDepthControl")]
    [SerializeField] private bool enableRightJoystickRadiusControl = true;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private float joystickMoveSpeed = 0.25f;
    [SerializeField] private float joystickDeadzone = 0.2f;
    [SerializeField] private float panelRadius = 0.2f;
    [FormerlySerializedAs("minLocalForwardOffset")]
    [SerializeField] private float minPanelRadius = 0.08f;
    [FormerlySerializedAs("maxLocalForwardOffset")]
    [SerializeField] private float maxPanelRadius = 0.55f;

    [Header("Collision")]
    [SerializeField] private bool preventClipping = true;
    [SerializeField] private LayerMask blockingLayers = ~0;
    [SerializeField] private float collisionRadius = 0.08f;
    [SerializeField] private float collisionPadding = 0.02f;
    [SerializeField] private Transform collisionIgnoreRoot;

    [Header("Height Button")]
    [SerializeField] private bool enableHeightButton = true;
    [SerializeField] private string heightButtonBinding = "<XRController>{LeftHand}/secondaryButton";
    [SerializeField] private bool enableKeyboardHeightTest = true;
    [SerializeField] private KeyCode keyboardHeightKey = KeyCode.H;

    [Header("Lag")]
    [SerializeField] private float movementLagSeconds = 0.05f;
    [SerializeField] private float maxLagDistance = 0.04f;

    [Header("Rotation")]
    [SerializeField] private Vector3 rotationOffsetEuler = new Vector3(0f, 180f, 0f);
    [SerializeField] private bool keepRotationUpright = true;

    [Header("Smoothing")]
    [SerializeField] private float positionSmooth = 20f;
    [SerializeField] private float rotationSmooth = 20f;

    private Vector3 previousReferencePosition;
    private bool hasPreviousReferencePosition;
    private InputAction heightButton;
    private XRInputValueReader<Vector2> rightJoystick;
    private bool matchPlayerHeightToggled;
    private bool keyboardHeightKeyWasDown;

    private void OnEnable()
    {
        heightButton = new InputAction(
            name: "MatchPlayerHeight",
            type: InputActionType.Button,
            binding: heightButtonBinding
        );

        heightButton.Enable();

        if (rightJoystick != null)
            rightJoystick.EnableDirectActionIfModeUsed();
    }

    private void OnDisable()
    {
        heightButton?.Disable();
        heightButton?.Dispose();
        heightButton = null;
    }

    private void Start()
    {
        if (inputManager == null)
            inputManager = GameObject.Find("/Input Manager")?.GetComponent<InputManager>();

        rightJoystick = inputManager != null ? inputManager.RightJoystick : null;
        rightJoystick?.EnableDirectActionIfModeUsed();

        panelRadius = Mathf.Clamp(
            panelRadius > 0f ? panelRadius : localOffset.magnitude,
            minPanelRadius,
            maxPanelRadius
        );
    }

    private void LateUpdate()
    {
        Transform positionReference = GetReference(positionReferenceMode, customPositionReference);
        Transform rotationReference = GetReference(rotationReferenceMode, customRotationReference);

        if (positionReference == null)
            return;

        ApplyRightJoystickRadiusControl();
        UpdateHeightToggle();

        Vector3 currentLocalOffset = GetCurrentLocalOffset(positionReference);
        Vector3 targetPosition = positionReference.position + positionReference.TransformDirection(currentLocalOffset);
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

        Quaternion baseRotation = GetBaseRotation(positionReference, rotationReference);
        Quaternion targetRotation = baseRotation * Quaternion.Euler(rotationOffsetEuler);

        float posT = 1f - Mathf.Exp(-positionSmooth * Time.deltaTime);
        float rotT = 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime);

        transform.position = Vector3.Lerp(transform.position, targetPosition, posT);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotT);
    }

    private Transform GetReference(ReferenceMode mode, Transform customReference)
    {
        switch (mode)
        {
            case ReferenceMode.Controller:
                return controller;
            case ReferenceMode.MainCamera:
                if (cameraTransform != null)
                    return cameraTransform;

                return Camera.main != null ? Camera.main.transform : null;
            case ReferenceMode.Custom:
                return customReference;
            default:
                return null;
        }
    }

    private void ApplyRightJoystickRadiusControl()
    {
        if (!enableRightJoystickRadiusControl || rightJoystick == null)
            return;

        float radiusInput = rightJoystick.ReadValue().y;

        if (Mathf.Abs(radiusInput) < joystickDeadzone)
            return;

        panelRadius = Mathf.Clamp(
            panelRadius + radiusInput * joystickMoveSpeed * Time.deltaTime,
            minPanelRadius,
            maxPanelRadius
        );
    }

    private void UpdateHeightToggle()
    {
        bool heightButtonPressed = enableHeightButton
            && heightButton != null
            && heightButton.WasPerformedThisFrame();

        bool keyboardHeightKeyDown = enableKeyboardHeightTest && Input.GetKey(keyboardHeightKey);
        bool keyboardHeightKeyPressed = keyboardHeightKeyDown && !keyboardHeightKeyWasDown;
        keyboardHeightKeyWasDown = keyboardHeightKeyDown;

        if (heightButtonPressed || keyboardHeightKeyPressed)
            matchPlayerHeightToggled = !matchPlayerHeightToggled;
    }

    private bool ShouldMatchPlayerHeight()
    {
        return matchPlayerHeightToggled;
    }

    private Vector3 GetCurrentLocalOffset(Transform positionReference)
    {
        Vector3 currentOffset = useSphericalPlacement
            ? GetSphericalLocalOffset()
            : localOffset;

        if (!ShouldMatchPlayerHeight())
            return currentOffset;

        Transform heightReference = GetHeightReference();

        if (heightReference == null)
            return currentOffset;

        Vector3 worldOffset = positionReference.TransformDirection(currentOffset);
        worldOffset.y = heightReference.position.y - positionReference.position.y;

        return positionReference.InverseTransformDirection(worldOffset);
    }

    private Vector3 GetSphericalLocalOffset()
    {
        Vector3 localDirection = localOffset.sqrMagnitude > 0.0001f
            ? localOffset.normalized
            : Vector3.forward;

        return localDirection * panelRadius;
    }

    private Transform GetHeightReference()
    {
        Transform heightReference = playerHeightReference;

        if (heightReference == null && cameraTransform != null)
            heightReference = cameraTransform;

        if (heightReference == null && Camera.main != null)
            heightReference = Camera.main.transform;

        return heightReference;
    }

    private Quaternion GetBaseRotation(Transform positionReference, Transform rotationReference)
    {
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

        if (controller != null && (hitTransform == controller || hitTransform.IsChildOf(controller)))
            return true;

        if (collisionIgnoreRoot != null && (hitTransform == collisionIgnoreRoot || hitTransform.IsChildOf(collisionIgnoreRoot)))
            return true;

        return false;
    }
}
