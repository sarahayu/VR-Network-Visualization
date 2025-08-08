/*
*
* NetworkFileLoader loads ONLY data from files (NetworkDataStrcture should contain any additional properties needed by application).
*
*/

using System;
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

            print($"Loaded dataset {DatasetName}.");
        }

        W Decode<W>(string filename)
        {
            string filepath = $"{Application.streamingAssetsPath}/{filename}";

            // TODO stream for large files
            return JsonConvert.DeserializeObject<W>(File.ReadAllText(filepath), new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            });
        }
    }
}