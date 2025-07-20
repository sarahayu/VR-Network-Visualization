/*
*
* NetworkRenderer is the base class for network renderers.
* It abstracts away render element events (e.g. hover, grab) from the rest of the application e.g. input systems, surface manager, etc.
*
*/

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
        public abstract Transform GetCommTransform(int commID);

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
        public event CommunitySelectEnterEvent OnCommunityGrabEnter;
        protected void CallCommunitySelectEnter(Community community, SelectEnterEventArgs evt)
        {
            OnCommunityGrabEnter(community, evt);
        }

        public delegate void CommunitySelectExitEvent(Community community, SelectExitEventArgs evt);
        public event CommunitySelectExitEvent OnCommunityGrabExit;
        protected void CallCommunitySelectExit(Community community, SelectExitEventArgs evt)
        {
            OnCommunityGrabExit(community, evt);
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
        public event NodeSelectEnterEvent OnNodeGrabEnter;
        protected void CallNodeSelectEnter(Node node, SelectEnterEventArgs evt)
        {
            OnNodeGrabEnter(node, evt);
        }

        public delegate void NodeSelectExitEvent(Node node, SelectExitEventArgs evt);
        public event NodeSelectExitEvent OnNodeGrabExit;
        protected void CallNodeSelectExit(Node node, SelectExitEventArgs evt)
        {
            OnNodeGrabExit(node, evt);
        }
    }

}