using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class HairballLayout : NetworkLayout
    {
        public override void Initialize()
        {
            // leave empty for now
        }

        public override void ApplyLayout()
        {
            var networkData = GetComponentInParent<NetworkDataStructure>();

            ApplyHairballPositions(networkData);
        }

        void ApplyHairballPositions(NetworkDataStructure networkData)
        {
            // TODO calculate at runtime
            var fileLoader = GetComponentInParent<NetworkFilesLoader>();

            foreach (var node in fileLoader.HairballLayout.nodes)
            {
                networkData.Nodes[node.idx].Position3D = node._position3D;
            }
        }
    }

}