using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VidiGraph;
using UnityEngine.UI;

public class Query : MonoBehaviour
{

    // TODO: Required Queries, 
    public void exec_query(NodeCollection Nodes, List<Link> Links, string Query)
    {
        // should do the database query here
        Debug.Log("Query: " + Query);
    }
}
