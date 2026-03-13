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
        public virtual TransformInterpolator GetInterpolator()
        {
            return new TransformInterpolator();
        }
    }

    public class TransformInterpolator
    {
        public virtual void Interpolate(float t)
        {
            // leave empty
        }
    }
}