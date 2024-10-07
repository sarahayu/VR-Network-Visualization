using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    abstract public class NetworkLayout : MonoBehaviour
    {
        public abstract void Initialize();
        public abstract void ApplyLayout();
    }

}