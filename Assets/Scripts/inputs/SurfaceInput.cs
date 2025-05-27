using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class SurfaceInput : MonoBehaviour
{
    public Vector3 surfSpawnOffset = Vector3.zero;

    [SerializeField]
    XRInputButtonReader _commandPress = new XRInputButtonReader("CommandPress");

    [SerializeField]
    Transform _spawnOrigin;

    SurfaceManager _surfManager;

    int _curHoveredSurface = -1;

    void Start()
    {
        _surfManager = GetComponent<SurfaceManager>();

        _surfManager.OnSurfaceHoverEnter += RegisterHoveredSurface;
        _surfManager.OnSurfaceHoverExit += RegisterUnhoveredSurface;
    }

    void OnEnable()
    {
        _commandPress.EnableDirectActionIfModeUsed();
    }

    void Update()
    {
        if (_commandPress.ReadWasPerformedThisFrame())
        {
            if (_curHoveredSurface == -1)
                _surfManager.SpawnSurface(_spawnOrigin.position + _spawnOrigin.rotation * surfSpawnOffset, Quaternion.FromToRotation(Vector3.up, -_spawnOrigin.forward));
            else
                _surfManager.DeleteSurface(_curHoveredSurface);
        }
    }

    void RegisterHoveredSurface(int surfID, HoverEnterEventArgs evt)
    {
        _curHoveredSurface = surfID;
    }

    void RegisterUnhoveredSurface(int surfID, HoverExitEventArgs evt)
    {
        _curHoveredSurface = -1;
    }
}
