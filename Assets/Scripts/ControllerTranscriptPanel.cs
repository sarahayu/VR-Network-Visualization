using UnityEngine;
using UnityEngine.InputSystem;

public class ControllerTranscriptPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform controller;
    [SerializeField] private Transform cameraTransform;

    [Header("Position")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.08f, 0.18f);
    [SerializeField] private float adjustSpeed = 0.25f;

    [Header("Rotation")]
    [SerializeField] private Vector3 rotationOffsetEuler = new Vector3(0f, 180f, 0f);

    [Header("Smoothing")]
    [SerializeField] private float positionSmooth = 20f;
    [SerializeField] private float rotationSmooth = 20f;

    private InputAction leftTrigger;
    private InputAction rightTrigger;
    private InputAction leftGrip;
    private InputAction rightGrip;

    private void OnEnable()
    {
        leftTrigger = new InputAction("Left Trigger", binding: "<XRController>{LeftHand}/trigger");
        rightTrigger = new InputAction("Right Trigger", binding: "<XRController>{RightHand}/trigger");
        leftGrip = new InputAction("Left Grip", binding: "<XRController>{LeftHand}/grip");
        rightGrip = new InputAction("Right Grip", binding: "<XRController>{RightHand}/grip");

        leftTrigger.Enable();
        rightTrigger.Enable();
        leftGrip.Enable();
        rightGrip.Enable();
    }

    private void OnDisable()
    {
        leftTrigger?.Disable();
        rightTrigger?.Disable();
        leftGrip?.Disable();
        rightGrip?.Disable();

        leftTrigger?.Dispose();
        rightTrigger?.Dispose();
        leftGrip?.Dispose();
        rightGrip?.Dispose();
    }

    private void Update()
    {
        float zDelta = leftTrigger.ReadValue<float>() - rightTrigger.ReadValue<float>();
        float xDelta = leftGrip.ReadValue<float>() - rightGrip.ReadValue<float>();

        localOffset.z += zDelta * adjustSpeed * Time.deltaTime;
        localOffset.x += xDelta * adjustSpeed * Time.deltaTime;
    }

    private void LateUpdate()
    {
        if (controller == null)
            return;

        Vector3 targetPosition = controller.position + controller.TransformDirection(localOffset);

        Vector3 flatForward;

        if (cameraTransform != null)
        {
            flatForward = targetPosition - cameraTransform.position;
        }
        else
        {
            flatForward = controller.forward;
        }

        flatForward.y = 0f;

        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = controller.forward;

        flatForward.y = 0f;

        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = transform.forward;

        flatForward.Normalize();

        Quaternion uprightRotation = Quaternion.LookRotation(flatForward, Vector3.up);
        Quaternion targetRotation = uprightRotation * Quaternion.Euler(rotationOffsetEuler);

        float posT = 1f - Mathf.Exp(-positionSmooth * Time.deltaTime);
        float rotT = 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime);

        transform.position = Vector3.Lerp(transform.position, targetPosition, posT);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotT);
    }
}