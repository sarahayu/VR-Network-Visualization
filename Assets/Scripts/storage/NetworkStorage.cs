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
        public abstract void InitialStore(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            MultiLayoutContext networkContext, IEnumerable<MultiLayoutContext> subnetworkContexts);

        // will not un-dirty elements; that will happen in renderer
        public abstract void UpdateStore(NetworkGlobal networkGlobal, MultiLayoutContext networkContext,
            IEnumerable<MultiLayoutContext> subnetworkContexts);
    }
}
