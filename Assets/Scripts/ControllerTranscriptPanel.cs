using UnityEngine;

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
    [SerializeField] private Transform customPositionReference;
    [SerializeField] private Transform customRotationReference;

    [Header("Reference Modes")]
    [SerializeField] private ReferenceMode positionReferenceMode = ReferenceMode.MainCamera;
    [SerializeField] private ReferenceMode rotationReferenceMode = ReferenceMode.MainCamera;

    [Header("Position")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.08f, 0.0f);

    [Header("Lag")]
    [SerializeField] private float movementLagSeconds = 0.05f;
    [SerializeField] private float maxLagDistance = 0.04f;

    [Header("Rotation")]
    [SerializeField] private Vector3 rotationOffsetEuler = new Vector3(0f, 180f, 0f);

    [Header("Smoothing")]
    [SerializeField] private float positionSmooth = 20f;
    [SerializeField] private float rotationSmooth = 20f;

    private Vector3 previousReferencePosition;
    private bool hasPreviousReferencePosition;

    private void LateUpdate()
    {
        Transform positionReference = GetReference(positionReferenceMode, customPositionReference);
        Transform rotationReference = GetReference(rotationReferenceMode, customRotationReference);

        if (positionReference == null)
            return;

        Vector3 targetPosition = positionReference.position + positionReference.TransformDirection(localOffset);
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
        previousReferencePosition = positionReference.position;
        hasPreviousReferencePosition = true;

        Quaternion baseRotation = rotationReference != null ? rotationReference.rotation : positionReference.rotation;
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
}
