/*
*
* Network is the base class for any custom networks.
*
*/

using System;
using UnityEngine;

namespace VidiGraph
{
    public abstract class Network : MonoBehaviour
    {
        public int ID { get { return _id; } }
        protected int _id;
        protected Action StorageUpdateFn = null;
        public void UpdateStorage() { StorageUpdateFn?.Invoke(); }
        public void SetStorageUpdateCallback(Action fn) { StorageUpdateFn = fn; }

        public abstract void UpdateRenderElements();
        public abstract void DrawPreview();
    }
}