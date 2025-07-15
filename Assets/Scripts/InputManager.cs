using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using VidiGraph;

public class InputManager : MonoBehaviour
{

    [SerializeField] XRInputButtonReader LeftGrip = new XRInputButtonReader("LeftGrip");
    [SerializeField] XRInputButtonReader LeftTrigger = new XRInputButtonReader("LeftTrigger");
    [SerializeField] XRInputButtonReader LeftPrimary = new XRInputButtonReader("LeftPrimary");
    [SerializeField] XRInputButtonReader LeftSecondary = new XRInputButtonReader("LeftSecondary");
    [SerializeField] XRInputValueReader<Vector2> LeftJoystick = new XRInputValueReader<Vector2>("LeftJoystick");
    [SerializeField] XRInputButtonReader LeftJoystickClick = new XRInputButtonReader("LeftJoystickClick");

    [SerializeField] XRInputButtonReader RightGrip = new XRInputButtonReader("RightGrip");
    [SerializeField] XRInputButtonReader RightTrigger = new XRInputButtonReader("RightTrigger");
    [SerializeField] XRInputButtonReader RightPrimary = new XRInputButtonReader("RightPrimary");
    [SerializeField] XRInputButtonReader RightSecondary = new XRInputButtonReader("RightSecondary");
    [SerializeField] XRInputValueReader<Vector2> RightJoystick = new XRInputValueReader<Vector2>("RightJoystick");
    [SerializeField] XRInputButtonReader RightJoystickClick = new XRInputButtonReader("RightJoystickClick");

    public event Action LeftGripListener;
    public event Action LeftTriggerListener;
    public event Action LeftPrimaryListener;
    public event Action LeftSecondaryListener;
    public event Action LeftJoystickClickListener;
    public event Action RightGripListener;
    public event Action RightTriggerListener;
    public event Action RightPrimaryListener;
    public event Action RightSecondaryListener;
    public event Action RightJoystickClickListener;

    NetworkManager _networkManager;
    SurfaceManager _surfaceManager;

    void OnEnable()
    {
        LeftGrip.EnableDirectActionIfModeUsed();
        LeftTrigger.EnableDirectActionIfModeUsed();
        LeftPrimary.EnableDirectActionIfModeUsed();
        LeftSecondary.EnableDirectActionIfModeUsed();
        LeftJoystick.EnableDirectActionIfModeUsed();
        LeftJoystickClick.EnableDirectActionIfModeUsed();
        RightGrip.EnableDirectActionIfModeUsed();
        RightTrigger.EnableDirectActionIfModeUsed();
        RightPrimary.EnableDirectActionIfModeUsed();
        RightSecondary.EnableDirectActionIfModeUsed();
        RightJoystick.EnableDirectActionIfModeUsed();
        RightJoystickClick.EnableDirectActionIfModeUsed();
    }

    // Start is called before the first frame update
    void Start()
    {
        _networkManager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
        _surfaceManager = GameObject.Find("/Surface Manager").GetComponent<SurfaceManager>();
    }

    // Update is called once per frame
    void Update()
    {
        HandleInput()?.Invoke();
    }

    Action HandleInput()
    {
        if (LeftGrip.ReadWasCompletedThisFrame()) return LeftGripAction;
        if (LeftTrigger.ReadWasPerformedThisFrame()) return LeftTriggerAction;
        if (LeftPrimary.ReadWasPerformedThisFrame()) return LeftPrimaryAction;
        if (LeftSecondary.ReadWasPerformedThisFrame()) return LeftSecondaryAction;
        if (LeftJoystickClick.ReadWasPerformedThisFrame()) return LeftJoystickClickAction;

        if (RightGrip.ReadWasCompletedThisFrame()) return RightGripAction;
        if (RightTrigger.ReadWasPerformedThisFrame()) return RightTriggerAction;
        if (RightPrimary.ReadWasPerformedThisFrame()) return RightPrimaryAction;
        if (RightSecondary.ReadWasPerformedThisFrame()) return RightSecondaryAction;
        if (RightJoystickClick.ReadWasPerformedThisFrame()) return RightJoystickClickAction;

        return () => { };
    }

    void LeftGripAction()
    {
        LeftGripListener?.Invoke();
        _networkManager.ToggleBigNetworkSphericalAndHairball();
    }

    void LeftTriggerAction()
    {
        LeftTriggerListener?.Invoke();

    }

    void LeftPrimaryAction()
    {
        LeftPrimaryListener?.Invoke();

    }

    void LeftSecondaryAction()
    {
        LeftSecondaryListener?.Invoke();

    }

    void LeftJoystickClickAction()
    {
        LeftJoystickClickListener?.Invoke();

    }

    void RightGripAction()
    {
        RightGripListener?.Invoke();

        if (_networkManager.NetworkGlobal.HoveredCommunity == null
            && _networkManager.NetworkGlobal.HoveredNode == null)
        {
            _networkManager.ClearSelection();
        }
    }

    void RightTriggerAction()
    {
        RightTriggerListener?.Invoke();

    }

    void RightPrimaryAction()
    {
        RightPrimaryListener?.Invoke();

        if (_surfaceManager.CurHoveredSurface == -1)
            _surfaceManager.SpawnSurfaceFromPointer();
        else
            _surfaceManager.DeleteSurface(_surfaceManager.CurHoveredSurface);
    }

    void RightSecondaryAction()
    {
        RightSecondaryListener?.Invoke();
        var nodeIDs1 = _networkManager.NetworkGlobal.RealNodes.GetRange(0, 10);
        var nodeIDs2 = _networkManager.NetworkGlobal.RealNodes.GetRange(10, 10);
        var linkIDs1 = _networkManager.NetworkGlobal.Links.Values.ToList().GetRange(0, 10).Select(l => l.ID);
        var linkIDs2 = _networkManager.NetworkGlobal.Links.Values.ToList().GetRange(10, 10).Select(l => l.ID);

        _networkManager.SetMLNodesSize(nodeIDs1, 4);
        _networkManager.SetMLNodesColor(nodeIDs2, "#FF0000");
        _networkManager.SetMLLinksWidth(linkIDs1, 3f);
        _networkManager.SetMLLinksColorStart(linkIDs2, "#00FF00");
        _networkManager.SetMLLinksColorEnd(linkIDs1, "#00FFFF");
        _networkManager.SetMLLinksAlpha(linkIDs2, 0.4f);
    }

    void RightJoystickClickAction()
    {
        RightJoystickClickListener?.Invoke();

    }

}
