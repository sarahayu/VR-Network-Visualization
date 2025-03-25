/*
* NetworkTransformer is a base class that performs various operations on global and/or context networks. 
*/

using UnityEngine;

namespace VidiGraph
{
    abstract public class NetworkContextTransformer : MonoBehaviour
    {
        public abstract void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext);
        public abstract void ApplyTransformation();
        public abstract TransformInterpolator GetInterpolator();
    }

    abstract public class TransformInterpolator
    {
        public abstract void Interpolate(float t);
    }
}