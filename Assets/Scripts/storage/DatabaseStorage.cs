/*
*
* DatabaseStorage saves network data to a neo4j database.
*
*/

using System;
using System.Collections.Generic;
using Neo4j.Driver;
using UnityEngine;

namespace VidiGraph
{
    public class DatabaseStorage : NetworkStorage
    {
        [SerializeField] string _uri = "bolt://localhost:7687";
        [SerializeField] string _user = "neo4j";
        [SerializeField] string _password = "neoneoneo";
        [SerializeField] bool _convertWinPaths = true;

        IDriver _driver;

        void Start()
        {
            _driver = GraphDatabase.Driver(_uri, AuthTokens.Basic(_user, _password), o => o.WithMaxTransactionRetryTime(TimeSpan.FromSeconds(5)));
        }

        void OnApplicationQuit()
        {
            DeleteContents();
            _driver?.Dispose();
        }

        public override void InitialStore(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            MultiLayoutContext networkContext, IEnumerable<MultiLayoutContext> subnetworkContexts)
        {
            TimerUtils.StartTime("DatabaseStorage.InitialStore");
            DatabaseStorageUtils.BulkInitNetwork(networkFile, networkGlobal, networkContext, subnetworkContexts, _driver, _convertWinPaths);
            TimerUtils.EndTime("DatabaseStorage.InitialStore");
        }

        public override void UpdateStore(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            MultiLayoutContext networkContext, IEnumerable<MultiLayoutContext> subnetworkContexts)
        {
            TimerUtils.StartTime("DatabaseStorage.UpdateStore");
            DatabaseStorageUtils.BulkUpdateNetwork(networkFile, networkGlobal, networkContext, subnetworkContexts, _driver, _convertWinPaths);
            TimerUtils.EndTime("DatabaseStorage.UpdateStore");
        }

        public override void DeleteContents()
        {
            // print("Deleting database contents...");
            // DatabaseStorageUtils.DeleteDatabaseContents(_driver);
            // print("Deleted database contents!");
        }

        public IEnumerable<string> GetNodesFromStore(NetworkGlobal networkGlobal, string command)
        {
            return DatabaseStorageUtils.GetNodesFromStore(networkGlobal, command, _driver, _convertWinPaths);
        }

        public IEnumerable<string> GetLinksFromStore(NetworkGlobal networkGlobal, string command)
        {
            return DatabaseStorageUtils.GetLinksFromStore(networkGlobal, command, _driver, _convertWinPaths);
        }

        public double GetValueFromStore(NetworkGlobal networkGlobal, string command)
        {
            return DatabaseStorageUtils.GetValueFromStore(networkGlobal, command, _driver, _convertWinPaths);
        }
    }
}