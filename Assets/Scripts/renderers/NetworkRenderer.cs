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
        public abstract Transform GetNetworkTransform();

        // start events

        public delegate void CommunityHoverEnterEvent(Community community, HoverEnterEventArgs evt);
        public event CommunityHoverEnterEvent OnCommunityHoverEnter;
        protected void CallCommunityHoverEnter(Community community, HoverEnterEventArgs evt)
        {
            OnCommunityHoverEnter?.Invoke(community, evt);
        }

        public delegate void CommunityHoverExitEvent(Community community, HoverExitEventArgs evt);
        public event CommunityHoverExitEvent OnCommunityHoverExit;
        protected void CallCommunityHoverExit(Community community, HoverExitEventArgs evt)
        {
            OnCommunityHoverExit?.Invoke(community, evt);
        }

        public delegate void CommunitySelectEnterEvent(Community community, SelectEnterEventArgs evt);
        public event CommunitySelectEnterEvent OnCommunityGrabEnter;
        protected void CallCommunitySelectEnter(Community community, SelectEnterEventArgs evt)
        {
            OnCommunityGrabEnter?.Invoke(community, evt);
        }

        public delegate void CommunitySelectExitEvent(Community community, SelectExitEventArgs evt);
        public event CommunitySelectExitEvent OnCommunityGrabExit;
        protected void CallCommunitySelectExit(Community community, SelectExitEventArgs evt)
        {
            OnCommunityGrabExit?.Invoke(community, evt);
        }

        public delegate void NodeHoverEnterEvent(Node node, HoverEnterEventArgs evt);
        public event NodeHoverEnterEvent OnNodeHoverEnter;
        protected void CallNodeHoverEnter(Node node, HoverEnterEventArgs evt)
        {
            OnNodeHoverEnter?.Invoke(node, evt);
        }

        public delegate void NodeHoverExitEvent(Node node, HoverExitEventArgs evt);
        public event NodeHoverExitEvent OnNodeHoverExit;
        protected void CallNodeHoverExit(Node node, HoverExitEventArgs evt)
        {
            OnNodeHoverExit?.Invoke(node, evt);
        }

        public delegate void NodeSelectEnterEvent(Node node, SelectEnterEventArgs evt);
        public event NodeSelectEnterEvent OnNodeGrabEnter;
        protected void CallNodeSelectEnter(Node node, SelectEnterEventArgs evt)
        {
            OnNodeGrabEnter?.Invoke(node, evt);
        }

        public delegate void NodeSelectExitEvent(Node node, SelectExitEventArgs evt);
        public event NodeSelectExitEvent OnNodeGrabExit;
        protected void CallNodeSelectExit(Node node, SelectExitEventArgs evt)
        {
            OnNodeGrabExit?.Invoke(node, evt);
        }

        public delegate void NetworkHoverEnterEvent(NetworkContext network, HoverEnterEventArgs evt);
        public event NetworkHoverEnterEvent OnNetworkHoverEnter;
        protected void CallNetworkHoverEnter(NetworkContext network, HoverEnterEventArgs evt)
        {
            OnNetworkHoverEnter?.Invoke(network, evt);
        }

        public delegate void NetworkHoverExitEvent(NetworkContext network, HoverExitEventArgs evt);
        public event NetworkHoverExitEvent OnNetworkHoverExit;
        protected void CallNetworkHoverExit(NetworkContext network, HoverExitEventArgs evt)
        {
            OnNetworkHoverExit?.Invoke(network, evt);
        }

        public delegate void NetworkSelectEnterEvent(NetworkContext network, SelectEnterEventArgs evt);
        public event NetworkSelectEnterEvent OnNetworkGrabEnter;
        protected void CallNetworkSelectEnter(NetworkContext network, SelectEnterEventArgs evt)
        {
            OnNetworkGrabEnter?.Invoke(network, evt);
        }

        public delegate void NetworkSelectExitEvent(NetworkContext network, SelectExitEventArgs evt);
        public event NetworkSelectExitEvent OnNetworkGrabExit;
        protected void CallNetworkSelectExit(NetworkContext network, SelectExitEventArgs evt)
        {
            OnNetworkGrabExit?.Invoke(network, evt);
        }
    }

}