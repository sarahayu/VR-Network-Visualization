using UnityEngine;

public class WristFlipPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform wristTransform;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform headTransform;

    [Header("Wrist Detection")]
    [SerializeField] private Vector3 palmNormalLocalAxis = Vector3.left;
    [SerializeField] private float showThreshold = 0.65f;
    [SerializeField] private float hideThreshold = 0.45f;

    [Header("Panel Placement")]
    [SerializeField] private bool attachPanelToWrist = true;
    [SerializeField] private Vector3 localPositionOffset = new Vector3(0f, 0.08f, 0.12f);
    [SerializeField] private Vector3 localRotationOffsetEuler = Vector3.zero;
    [SerializeField] private bool faceHead = true;

    private bool isVisible;

    void Start()
    {
        SetVisible(false);
    }

    void Update()
    {
        if (wristTransform == null || panelRoot == null)
            return;

        float wristUpAmount = Vector3.Dot(wristTransform.TransformDirection(palmNormalLocalAxis.normalized), Vector3.up);

        if (!isVisible && wristUpAmount > showThreshold)
            SetVisible(true);
        else if (isVisible && wristUpAmount < hideThreshold)
            SetVisible(false);

        if (isVisible && attachPanelToWrist)
            UpdatePanelPose();
    }

    void SetVisible(bool visible)
    {
        isVisible = visible;

        if (panelRoot != null)
            panelRoot.SetActive(visible);
    }

    void UpdatePanelPose()
    {
        panelRoot.transform.position = wristTransform.position + wristTransform.TransformDirection(localPositionOffset);

        if (faceHead)
        {
            Transform lookTarget = headTransform != null ? headTransform : Camera.main?.transform;
            if (lookTarget != null)
            {
                Vector3 forward = panelRoot.transform.position - lookTarget.position;
                if (forward.sqrMagnitude > 0.0001f)
                    panelRoot.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            }
        }
        else
        {
            panelRoot.transform.rotation = wristTransform.rotation;
        }

        panelRoot.transform.rotation *= Quaternion.Euler(localRotationOffsetEuler);
    }
}
