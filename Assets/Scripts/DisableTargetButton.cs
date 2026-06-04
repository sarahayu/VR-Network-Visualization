using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class DisableTargetButton : MonoBehaviour
{
    private enum ActionMode
    {
        Disable,
        Enable,
        Toggle
    }

    [Header("Targets")]
    [SerializeField] private GameObject[] targetObjects;
    [SerializeField] private Behaviour[] targetComponents;

    [Header("Action")]
    [SerializeField] private ActionMode actionMode = ActionMode.Toggle;

    void Start()
    {
        XRBaseInteractable interactable = GetComponent<XRBaseInteractable>();
        if (interactable != null)
            interactable.selectEntered.AddListener(_ => Apply());
    }

    public void Apply()
    {
        foreach (GameObject targetObject in targetObjects)
        {
            if (targetObject == null)
                continue;

            targetObject.SetActive(GetNextState(targetObject.activeSelf));
        }

        foreach (Behaviour targetComponent in targetComponents)
        {
            if (targetComponent == null)
                continue;

            targetComponent.enabled = GetNextState(targetComponent.enabled);
        }
    }

    bool GetNextState(bool currentState)
    {
        switch (actionMode)
        {
            case ActionMode.Disable:
                return false;
            case ActionMode.Enable:
                return true;
            case ActionMode.Toggle:
                return !currentState;
            default:
                return currentState;
        }
    }
}
