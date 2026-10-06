/*
*
* NetworkStorage is base class for classes that store network data somewhere.
*
*/

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VidiGraph
{
    public abstract class NetworkStorage : MonoBehaviour
    {
        public abstract Task InitialStoreAsync(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            MultiLayoutContext networkContext, IEnumerable<MultiLayoutContext> subnetworkContexts,
            CancellationToken cancellationToken = default);

        // will not un-dirty elements; that will happen in renderer
        public abstract Task UpdateStoreAsync(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            MultiLayoutContext networkContext, IEnumerable<MultiLayoutContext> subnetworkContexts,
            CancellationToken cancellationToken = default);

        public abstract Task DeleteContentsAsync(CancellationToken cancellationToken = default);
    }
}
