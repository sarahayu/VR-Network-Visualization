using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace VidiGraph
{
    abstract public class NetworkRenderer : MonoBehaviour
    {
        public abstract void Initialize(NetworkContext networkContext);

        // call when networkdatastructure has updates that need to be known by renderer e.g. position, color
        public abstract void UpdateRenderElements();
        public abstract void Draw();

        // start events

        public delegate void CommunityHoverEnterEvent(Community community, HoverEnterEventArgs evt);
        public event CommunityHoverEnterEvent OnCommunityHoverEnter;
        protected void CallCommunityHoverEnter(Community community, HoverEnterEventArgs evt)
        {
            OnCommunityHoverEnter(community, evt);
        }

        public delegate void CommunityHoverExitEvent(Community community, HoverExitEventArgs evt);
        public event CommunityHoverExitEvent OnCommunityHoverExit;
        protected void CallCommunityHoverExit(Community community, HoverExitEventArgs evt)
        {
            OnCommunityHoverExit(community, evt);
        }
    }

}