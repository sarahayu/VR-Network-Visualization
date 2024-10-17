using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace VidiGraph
{
    public class NetworkInput : MonoBehaviour
    {
        public XRController LeftController;
        public XRController RightController;

        public void Initialize()
        {
        }
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if (LeftController.inputDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out var triggerValue) && triggerValue)
            {
                Debug.Log("Trigger button is pressed.");
            }
        }
    }

}