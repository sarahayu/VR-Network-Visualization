/*
*
* InputManager takes care of most input, especially filtering valid inputs. 
* Other classes may listen to input events via this manager's listeners.
*
*/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using VidiGraph;

public class InputManager : MonoBehaviour
{

    [SerializeField] XRInputButtonReader _leftGrip = new XRInputButtonReader("LeftGrip");
    [SerializeField] XRInputButtonReader _leftTrigger = new XRInputButtonReader("LeftTrigger");
    [SerializeField] XRInputButtonReader _leftPrimary = new XRInputButtonReader("LeftPrimary");
    [SerializeField] XRInputButtonReader _leftSecondary = new XRInputButtonReader("LeftSecondary");
    [SerializeField] XRInputValueReader<Vector2> _leftJoystick = new XRInputValueReader<Vector2>("LeftJoystick");
    [SerializeField] XRInputButtonReader _leftJoystickClick = new XRInputButtonReader("LeftJoystickClick");

    [SerializeField] XRInputButtonReader _rightGrip = new XRInputButtonReader("RightGrip");
    [SerializeField] XRInputButtonReader _rightTrigger = new XRInputButtonReader("RightTrigger");
    [SerializeField] XRInputButtonReader _rightPrimary = new XRInputButtonReader("RightPrimary");
    [SerializeField] XRInputButtonReader _rightSecondary = new XRInputButtonReader("RightSecondary");
    [SerializeField] XRInputValueReader<Vector2> _rightJoystick = new XRInputValueReader<Vector2>("RightJoystick");
    [SerializeField] XRInputButtonReader _rightJoystickClick = new XRInputButtonReader("RightJoystickClick");
    [SerializeField] GameObject _rightController;
    [SerializeField] GameObject _leftController;

    [SerializeField] TactileEditInteraction _mlInput;

    public XRInputButtonReader LeftGrip { get => _leftGrip; }
    public XRInputButtonReader LeftTrigger { get => _leftTrigger; }
    public XRInputButtonReader LeftPrimary { get => _leftPrimary; }
    public XRInputButtonReader LeftSecondary { get => _leftSecondary; }
    public XRInputValueReader<Vector2> LeftJoystick { get => _leftJoystick; }
    public XRInputButtonReader LeftJoystickClick { get => _leftJoystickClick; }
    public XRInputButtonReader RightGrip { get => _rightGrip; }
    public XRInputButtonReader RightTrigger { get => _rightTrigger; }
    public XRInputButtonReader RightPrimary { get => _rightPrimary; }
    public XRInputButtonReader RightSecondary { get => _rightSecondary; }
    public XRInputValueReader<Vector2> RightJoystick { get => _rightJoystick; }
    public XRInputButtonReader RightJoystickClick { get => _rightJoystickClick; }
    public GameObject RightController { get => _rightController; }
    public GameObject LeftController { get => _leftController; }

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
    DatabaseStorage _databaseStorage;


    void OnEnable()
    {
        _leftGrip.EnableDirectActionIfModeUsed();
        _leftTrigger.EnableDirectActionIfModeUsed();
        _leftPrimary.EnableDirectActionIfModeUsed();
        _leftSecondary.EnableDirectActionIfModeUsed();
        _leftJoystick.EnableDirectActionIfModeUsed();
        _leftJoystickClick.EnableDirectActionIfModeUsed();
        _rightGrip.EnableDirectActionIfModeUsed();
        _rightTrigger.EnableDirectActionIfModeUsed();
        _rightPrimary.EnableDirectActionIfModeUsed();
        _rightSecondary.EnableDirectActionIfModeUsed();
        _rightJoystick.EnableDirectActionIfModeUsed();
        _rightJoystickClick.EnableDirectActionIfModeUsed();
    }

    void Start()
    {
        _networkManager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
        _surfaceManager = GameObject.Find("/Surface Manager").GetComponent<SurfaceManager>();
        _databaseStorage = GameObject.Find("/Database")?.GetComponent<DatabaseStorage>();
    }

    void Update()
    {
        HandleInput().Invoke();
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
        // _networkManager.ToggleBigNetworkSphericalAndHairball();
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
            && _networkManager.NetworkGlobal.HoveredNode == null
            && _networkManager.HoveredNetwork == null)
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

        CallTestingFunctionWork1();

        // if (_surfaceManager.CurHoveredSurface == -1)
        //     _surfaceManager.SpawnSurfaceFromPointer();
        // else
        //     _surfaceManager.DeleteSurface(_surfaceManager.CurHoveredSurface);
    }

    void RightSecondaryAction()
    {
        RightSecondaryListener?.Invoke();

        CallTestingFunctionWork2();
    }

    void RightJoystickClickAction()
    {
        RightJoystickClickListener?.Invoke();

    }

    void CallTestingFunctionWork1()
    {
        if (_databaseStorage == null) return;

        string query = "MATCH (n: Node {smoker: TRUE}) return n";

        Debug.Log("Selecting nodes with query: " + query);

        var nodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, query);
        TimerUtils.StartTime("SetWorkingSubgraph");
        _networkManager.CreateWorkingSubgraph(_networkManager.SortNodeGUIDs(nodes)[0]);
        TimerUtils.EndTime("SetWorkingSubgraph");
    }

    void CallTestingFunctionWork2()
    {
        if (_databaseStorage == null) return;

        string query = "MATCH (n: Node {sex: \"female\"}) return n";

        Debug.Log("Selecting nodes with query: " + query);

        var nodes = _databaseStorage.GetNodesFromStore(_networkManager.NetworkGlobal, query);
        TimerUtils.StartTime("SetWorkingSubgraph");
        _networkManager.CreateWorkingSubgraph(_networkManager.SortNodeGUIDs(nodes)[0]);
        TimerUtils.EndTime("SetWorkingSubgraph");
    }

    void CallTestingFunctionSelect()
    {
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
}
