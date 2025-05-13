using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VidiGraph;

public class NetworkDatabase : MonoBehaviour
{
    public void Clear()
    {
        throw new NotImplementedException();
    }

    public void CreateNode(Node node)
    {
        throw new NotImplementedException();
    }

    public void SetNodeDegree(int sourceNodeID, double v)
    {
        throw new NotImplementedException();
    }

    public double GetNodeDegree(int sourceNodeID)
    {
        throw new NotImplementedException();
    }

    public void AddLink(Link link)
    {
        throw new NotImplementedException();
    }

    public void AddNodeAncList(int iD, int ancID)
    {
        throw new NotImplementedException();
    }

    public bool IsSelected(int nodeID)
    {
        throw new NotImplementedException();
    }

    public void SetNodeSelected(int nodeID, bool v)
    {
        throw new NotImplementedException();
    }

    public void SetNodeDirty(int nodeID, bool v)
    {
        throw new NotImplementedException();
    }

    public bool IsNodeVirtual(int nodeID)
    {
        throw new NotImplementedException();
    }

    public bool IsCommunitySelected(int commID)
    {
        throw new NotImplementedException();
    }

    public void SetCommunitySelected(int commID, bool v)
    {
        throw new NotImplementedException();
    }

    public void SetCommunityDirty(int commID, bool v)
    {
        throw new NotImplementedException();
    }

    public void SetNodesSelected(IEnumerable<int> nodeIDs, bool v)
    {
        throw new NotImplementedException();
    }

    public List<Node> GetCommunityNodes(int commID)
    {
        throw new NotImplementedException();
    }
}
