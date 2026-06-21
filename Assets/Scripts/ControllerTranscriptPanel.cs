using UnityEngine;
using UnityEngine.InputSystem;
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

    [Header("Right Joystick Forward/Back")]
    [SerializeField] private bool enableRightJoystickDepthControl = true;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private float joystickMoveSpeed = 0.25f;
    [SerializeField] private float joystickDeadzone = 0.2f;
    [SerializeField] private float minLocalForwardOffset = 0.08f;
    [SerializeField] private float maxLocalForwardOffset = 0.55f;

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
    }

    private void LateUpdate()
    {
        Transform positionReference = GetReference(positionReferenceMode, customPositionReference);
        Transform rotationReference = GetReference(rotationReferenceMode, customRotationReference);

        if (positionReference == null)
            return;

        ApplyRightJoystickDepthControl();

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

    private void ApplyRightJoystickDepthControl()
    {
        if (!enableRightJoystickDepthControl || rightJoystick == null)
            return;

        float forwardBackInput = rightJoystick.ReadValue().y;

        if (Mathf.Abs(forwardBackInput) < joystickDeadzone)
            return;

        localOffset.z = Mathf.Clamp(
            localOffset.z + forwardBackInput * joystickMoveSpeed * Time.deltaTime,
            minLocalForwardOffset,
            maxLocalForwardOffset
        );
    }

    private bool IsHeightButtonHeld()
    {
        if (!enableHeightButton)
            return false;

        bool xrHeld = heightButton != null && heightButton.ReadValue<float>() > 0.5f;
        bool keyboardHeld = enableKeyboardHeightTest && Input.GetKey(keyboardHeightKey);

        return xrHeld || keyboardHeld;
    }

    private Vector3 GetCurrentLocalOffset(Transform positionReference)
    {
        if (!IsHeightButtonHeld())
            return localOffset;

        Transform heightReference = GetHeightReference();

        if (heightReference == null)
            return localOffset;

        Vector3 worldOffset = positionReference.TransformDirection(localOffset);
        worldOffset.y = heightReference.position.y - positionReference.position.y;

        return positionReference.InverseTransformDirection(worldOffset);
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
