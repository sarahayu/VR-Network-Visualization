/*
*
* NetworkFileLoader loads ONLY data from files (NetworkDataStrcture should contain any additional properties needed by application)
*
*/

using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VidiGraph
{
    public class NetworkFilesLoader : MonoBehaviour
    {

        static readonly string layoutSuffix = "-layout.json";
        static readonly string spider2DSuffix = "2D-spiders.dat";
        static readonly string spider3DSuffix = "-spiders.dat";
        static readonly string flatSuffix = "-layout.json-flat.json";
        static readonly string sphericalSuffix = "-layout.json-spherical.json";
        static readonly string hairballSuffix = "-layout.json-hairball.json";

        public string datasetName;
        public bool is2D = false;

        [HideInInspector]
        public NetworkFileData GraphData { get; private set; }
        [HideInInspector]
        public RNetwork SpiderData { get; private set; }

        [HideInInspector]
        public NetworkFileData FlatLayout { get; private set; }
        [HideInInspector]
        public NetworkFileData SphericalLayout { get; private set; }
        [HideInInspector]
        public NetworkFileData HairballLayout { get; private set; }

        public void LoadFiles()
        {
            print($"loading dataset {datasetName}");

            string layoutFile = $"{datasetName}{layoutSuffix}";
            string spiderFile = $"{datasetName}{(is2D ? spider2DSuffix : spider3DSuffix)}";
            string flatFile = $"{datasetName}{flatSuffix}";
            string sphericalFile = $"{datasetName}{sphericalSuffix}";
            string hairballFile = $"{datasetName}{hairballSuffix}";

            GraphData = Decode<NetworkFileData>(layoutFile);
            SpiderData = SpiderFileDecoder.Decode(spiderFile);

            FlatLayout = Decode<NetworkFileData>(flatFile);
            SphericalLayout = Decode<NetworkFileData>(sphericalFile);
            HairballLayout = Decode<NetworkFileData>(hairballFile);

        }

        T Decode<T>(string filename)
        {
            string filepath = $"{Application.streamingAssetsPath}/{filename}";

            return JsonUtility.FromJson<T>(File.ReadAllText(filepath));
        }
    }
}