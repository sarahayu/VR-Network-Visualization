using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    abstract public class NetworkRenderer : MonoBehaviour
    {
        public abstract void Initialize();
        public abstract void Update();
        public abstract void Draw();
    }

}