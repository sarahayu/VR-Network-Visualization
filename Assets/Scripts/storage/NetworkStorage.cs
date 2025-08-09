/*
*
* NetworkStorage is base class for classes that store network data somewhere.
*
*/

using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public abstract class NetworkStorage : MonoBehaviour
    {
        public abstract void InitialStore(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            MultiLayoutContext networkContext, IEnumerable<MultiLayoutContext> subnetworkContexts);

        // will not un-dirty elements; that will happen in renderer
        public abstract void UpdateStore(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            MultiLayoutContext networkContext, IEnumerable<MultiLayoutContext> subnetworkContexts);

        public abstract void DeleteContents();
    }
}
