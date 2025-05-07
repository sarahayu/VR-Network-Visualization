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

        public abstract Transform GetNodeTransform(int nodeID);

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

        public delegate void CommunitySelectEnterEvent(Community community, SelectEnterEventArgs evt);
        public event CommunitySelectEnterEvent OnCommunitySelectEnter;
        protected void CallCommunitySelectEnter(Community community, SelectEnterEventArgs evt)
        {
            OnCommunitySelectEnter(community, evt);
        }

        public delegate void CommunitySelectExitEvent(Community community, SelectExitEventArgs evt);
        public event CommunitySelectExitEvent OnCommunitySelectExit;
        protected void CallCommunitySelectExit(Community community, SelectExitEventArgs evt)
        {
            OnCommunitySelectExit(community, evt);
        }

        public delegate void NodeHoverEnterEvent(Node node, HoverEnterEventArgs evt);
        public event NodeHoverEnterEvent OnNodeHoverEnter;
        protected void CallNodeHoverEnter(Node node, HoverEnterEventArgs evt)
        {
            OnNodeHoverEnter(node, evt);
        }

        public delegate void NodeHoverExitEvent(Node node, HoverExitEventArgs evt);
        public event NodeHoverExitEvent OnNodeHoverExit;
        protected void CallNodeHoverExit(Node node, HoverExitEventArgs evt)
        {
            OnNodeHoverExit(node, evt);
        }

        public delegate void NodeSelectEnterEvent(Node node, SelectEnterEventArgs evt);
        public event NodeSelectEnterEvent OnNodeSelectEnter;
        protected void CallNodeSelectEnter(Node node, SelectEnterEventArgs evt)
        {
            OnNodeSelectEnter(node, evt);
        }

        public delegate void NodeSelectExitEvent(Node node, SelectExitEventArgs evt);
        public event NodeSelectExitEvent OnNodeSelectExit;
        protected void CallNodeSelectExit(Node node, SelectExitEventArgs evt)
        {
            OnNodeSelectExit(node, evt);
        }
    }

}