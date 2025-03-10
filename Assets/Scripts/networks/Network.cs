/*
*
* TODO Description goes here
*
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public abstract class Network : MonoBehaviour
    {
        public abstract void Initialize();
        public abstract void UpdateRenderElements();
        public abstract void DrawPreview();
    }
}