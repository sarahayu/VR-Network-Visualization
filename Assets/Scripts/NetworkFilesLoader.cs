/*
*
* NetworkFileLoader loads ONLY data from files (NetworkDataStrcture should contain any additional properties needed by application)
*
*/

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public class NetworkFilesLoader : MonoBehaviour
    {
        const string SpiderSuffix = "-layout.json-cluster.json";
        const string FlatSuffix = "-layout.json-flat.json";
        const string SphericalSuffix = "-layout.json-spherical.json";
        const string HairballSuffix = "-layout.json-hairball.json";

        public string DatasetName;
        public NetworkFileData SpiderLayout { get; private set; }

        public NetworkFileData FlatLayout { get; private set; }
        public NetworkFileData SphericalLayout { get; private set; }
        public NetworkFileData HairballLayout { get; private set; }

        public void LoadFiles()
        {
            print($"loading dataset {DatasetName}");
            string spiderFile = $"{DatasetName}{SpiderSuffix}";
            string flatFile = $"{DatasetName}{FlatSuffix}";
            string sphericalFile = $"{DatasetName}{SphericalSuffix}";
            string hairballFile = $"{DatasetName}{HairballSuffix}";

            FlatLayout = Decode<NetworkFileData>(flatFile);
            SphericalLayout = Decode<NetworkFileData>(sphericalFile);
            HairballLayout = Decode<NetworkFileData>(hairballFile);
            SpiderLayout = Decode<NetworkFileData>(spiderFile);

            InitializeIdMap(SphericalLayout);
            InitializeIdMap(SpiderLayout);
            InitializeIdMap(FlatLayout);
            InitializeIdMap(SphericalLayout);
            InitializeIdMap(HairballLayout);

        }

        void InitializeIdMap(NetworkFileData network)
        {
            network.idToIdx = new Dictionary<int, int>();

            for (int i = 0; i < network.nodes.Count(); i++)
            {
                network.idToIdx[network.nodes[i].idx] = i;
            }
        }

        void InitializeIdMap(RNetwork network)
        {
            network.idToIdx = new Dictionary<int, int>();

            for (int i = 0; i < network.nodes.Count(); i++)
            {
                if (network.nodes[i] == null) continue; // why would this be null??
                network.idToIdx[network.nodes[i].idx] = i;
            }
        }

        T Decode<T>(string filename)
        {
            string filepath = $"{Application.streamingAssetsPath}/{filename}";

            return JsonUtility.FromJson<T>(File.ReadAllText(filepath));
        }
    }
}