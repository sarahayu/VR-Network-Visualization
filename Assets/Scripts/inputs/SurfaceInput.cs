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
        Transform _spawnOrigin;

        SurfaceManager _surfManager;


        void Start()
        {
            _surfManager = GetComponent<SurfaceManager>();
        }

        void OnEnable()
        {
        }

        void Update()
        {
        }
    }
}