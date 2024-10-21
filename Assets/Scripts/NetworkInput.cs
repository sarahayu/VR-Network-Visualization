using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace VidiGraph
{
    public class NetworkInput : MonoBehaviour
    {

        public XRInputValueReader<float> GripInput = new XRInputValueReader<float>("Grip");

        int lastGripValue = 0;

        Network network;

        public void Initialize()
        {
            network = GetComponent<Network>();
        }

        void OnEnable()
        {
            GripInput.EnableDirectActionIfModeUsed();
        }

        void Start()
        {
        }

        void Update()
        {
            var gripVal = (int)GripInput.ReadValue();

            // pressed
            if (gripVal == 1 && gripVal != lastGripValue)
            {
                // if (network.CurLayout == "spherical")
                // {
                //     network.ChangeToLayout("hairball");
                // }
                // else if (network.CurLayout == "hairball")
                // {
                //     network.ChangeToLayout("spherical");
                // }

                network.ToggleCommunityFocus(1);
            }

            lastGripValue = gripVal;
        }
    }

}