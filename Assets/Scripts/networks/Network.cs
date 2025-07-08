/*
*
* TODO Description goes here
*
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public abstract class Network : MonoBehaviour
    {
        protected Action StorageUpdateFn = null;
        public void UpdateStorage() { StorageUpdateFn?.Invoke(); }
        public void SetStorageUpdateCallback(Action fn) { StorageUpdateFn = fn; }

        public abstract void UpdateRenderElements();
        public abstract void DrawPreview();
    }
}