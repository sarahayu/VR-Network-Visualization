using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Neo4j.Driver;
using UnityEngine;

namespace VidiGraph
{
    public abstract class NetworkStorage : MonoBehaviour
    {
        public abstract void InitialStore(NetworkGlobal networkGlobal, MultiLayoutContext networkContext);

        // will not un-dirty elements; that will happen in renderer
        public abstract void UpdateStore(NetworkGlobal networkGlobal, MultiLayoutContext networkContext);
    }
}
