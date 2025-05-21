/*
*
* NetworkFileLoader loads ONLY data from files (NetworkDataStrcture should contain any additional properties needed by application)
*
*/

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace VidiGraph
{
    public class NetworkFilesLoader : MonoBehaviour
    {
        const string ClusterSuffix = "-layout.json-cluster.json";
        const string FlatSuffix = "-layout.json-flat.json";
        const string SphericalSuffix = "-layout.json-spherical.json";
        const string HairballSuffix = "-layout.json-hairball.json";

        public string DatasetName;
        public NetworkFileData ClusterLayout { get; private set; }

        public NetworkFileData FlatLayout { get; private set; }
        public NetworkFileData SphericalLayout { get; private set; }
        public NetworkFileData HairballLayout { get; private set; }

        public void LoadFiles()
        {
            string clusterFile = $"{DatasetName}{ClusterSuffix}";
            string flatFile = $"{DatasetName}{FlatSuffix}";
            string sphericalFile = $"{DatasetName}{SphericalSuffix}";
            string hairballFile = $"{DatasetName}{HairballSuffix}";

            FlatLayout = Decode<NetworkFileData>(flatFile);
            SphericalLayout = Decode<NetworkFileData>(sphericalFile);
            HairballLayout = Decode<NetworkFileData>(hairballFile);
            ClusterLayout = Decode<NetworkFileData>(clusterFile);

            InitializeIdMap(SphericalLayout);
            InitializeIdMap(ClusterLayout);
            InitializeIdMap(FlatLayout);
            InitializeIdMap(SphericalLayout);
            InitializeIdMap(HairballLayout);

            print($"Loaded dataset {DatasetName}.");
        }

        void InitializeIdMap(NetworkFileData network)
        {
            network.idToIdx = new Dictionary<int, int>();

            for (int i = 0; i < network.nodes.Count(); i++)
            {
                network.idToIdx[network.nodes[i].idx] = i;
            }
        }

        T Decode<T>(string filename)
        {
            string filepath = $"{Application.streamingAssetsPath}/{filename}";

            return JsonConvert.DeserializeObject<T>(File.ReadAllText(filepath), new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            });
        }
    }
}