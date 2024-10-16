using UnityEngine;

namespace VidiGraph
{
    abstract public class NetworkRenderer : MonoBehaviour
    {
        public abstract void Initialize();

        // call when networkdatastructure has updates that need to be known by renderer e.g. position, color
        public abstract void UpdateRenderElements();
        public abstract void Draw();
    }

}