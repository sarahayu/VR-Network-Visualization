using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VidiGraph
{
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
                {
                    SurfaceInputUtils.CalcPosAndRot(_spawnOrigin, surfSpawnOffset, out var position, out var rotation);
                    _surfManager.SpawnSurface(position, rotation);

                }
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
}