using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class SphericalLayout : NetworkLayout
    {
        public override void Initialize()
        {
            // leave empty for now
        }

        public override void ApplyLayout()
        {
            var networkData = GetComponentInParent<NetworkDataStructure>();

            ApplySphericalPositions(networkData);
        }

        void ApplySphericalPositions(NetworkDataStructure networkData)
        {
            // TODO calculate at runtime
            var fileLoader = GetComponentInParent<NetworkFilesLoader>();

            foreach (var node in fileLoader.SphericalLayout.nodes)
            {
                networkData.nodes[node.idx].Position3D = node._position3D;
            }
        }
    }

}