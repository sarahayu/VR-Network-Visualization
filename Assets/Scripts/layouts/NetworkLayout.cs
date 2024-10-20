using UnityEngine;

namespace VidiGraph
{
    abstract public class NetworkLayout : MonoBehaviour
    {
        public abstract void Initialize();
        public abstract void ApplyLayout();
        public abstract LayoutInterpolator GetInterpolator();
    }

    abstract public class LayoutInterpolator
    {
        public abstract void Interpolate(float t);
    }
}