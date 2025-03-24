/*
* NetworkContext is for any data that is representation-specific, e.g. 3D position, state
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class NetworkContext
    {
        protected NetworkContext()
        {
            // prevent initialization of NetworkContext; use only to derive
        }
    }
}
