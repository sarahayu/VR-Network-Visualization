using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace VidiGraph
{
    public class NetworkInput : MonoBehaviour
    {

        public XRInputValueReader<float> GripInput = new XRInputValueReader<float>("Grip");

        public void Initialize()
        {
        }

        void OnEnable()
        {
            GripInput?.EnableDirectActionIfModeUsed();
        }

        // Start is called before the first frame update
        void Start()
        {
        }

        // Update is called once per frame
        void Update()
        {

            if (GripInput != null)
            {
                var gripVal = GripInput.ReadValue();
                print(gripVal);
            }
        }
    }

}