using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class SurfaceInput : MonoBehaviour
{
    [SerializeField]
    XRInputButtonReader _commandPress = new XRInputButtonReader("CommandPress");
    bool _commandPressed = false;

    [SerializeField]
    Transform _spawnOrigin;

    SurfaceManager _surfManager;

    // Start is called before the first frame update
    void Start()
    {
        _surfManager = GetComponent<SurfaceManager>();
    }

    void OnEnable()
    {
        _commandPress.EnableDirectActionIfModeUsed();
    }

    // Update is called once per frame
    void Update()
    {
        if (_commandPress.ReadWasPerformedThisFrame())
        {
            if (!_commandPressed)
            {
                if (_surfManager.Surfaces.Count == 0)
                    _surfManager.SpawnSurface(_spawnOrigin.position, _spawnOrigin.rotation);
                else
                    _surfManager.DeleteSurface(_surfManager.Surfaces.Keys.First());
                _commandPressed = true;
            }
        }
        else
        {
            _commandPressed = false;
        }
    }
}
