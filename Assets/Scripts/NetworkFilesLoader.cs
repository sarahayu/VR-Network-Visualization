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

        const string LayoutSuffix = "-layout.json";
        const string Spider2DSuffix = "2D-spiders.dat";
        const string Spider3DSuffix = "-spiders.dat";
        const string FlatSuffix = "-layout.json-flat.json";
        const string SphericalSuffix = "-layout.json-spherical.json";
        const string HairballSuffix = "-layout.json-hairball.json";

        public string DatasetName;
        public bool Is2D = false;

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
            print($"loading dataset {DatasetName}");

            string layoutFile = $"{DatasetName}{LayoutSuffix}";
            string spiderFile = $"{DatasetName}{(Is2D ? Spider2DSuffix : Spider3DSuffix)}";
            string flatFile = $"{DatasetName}{FlatSuffix}";
            string sphericalFile = $"{DatasetName}{SphericalSuffix}";
            string hairballFile = $"{DatasetName}{HairballSuffix}";

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