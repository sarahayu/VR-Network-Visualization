using System;
using System.Collections;
using System.Collections.Generic;
using Neo4j.Driver;
using UnityEngine;

namespace VidiGraph
{
    public class DatabaseStorage : NetworkStorage
    {
        [SerializeField]
        string _uri = "bolt://localhost:7687";
        [SerializeField]
        string _user = "neo4j";
        [SerializeField]
        string _password = "neoneoneo";
        [SerializeField]
        bool _convertWinPaths = true;

        IDriver _driver;

        void Start()
        {
            _driver = GraphDatabase.Driver(_uri, AuthTokens.Basic(_user, _password), o => o.WithMaxTransactionRetryTime(TimeSpan.FromSeconds(5)));
        }

        void OnApplicationQuit()
        {
            _driver?.Dispose();
        }

        public override void InitialStore(NetworkFileData networkFile, NetworkGlobal networkGlobal, MultiLayoutContext networkContext)
        {
            DatabaseStorageUtils.BulkInitNetwork(networkFile, networkGlobal, networkContext, _driver, _convertWinPaths);
        }

        public override void UpdateStore(NetworkGlobal networkGlobal, MultiLayoutContext networkContext)
        {
            DatabaseStorageUtils.BulkUpdateNetwork(networkGlobal, networkContext, _driver, _convertWinPaths);
        }

        public void InteractStore(string command)
        {
            DatabaseStorageUtils.ExecuteCommand(command, _driver, _convertWinPaths);
        }
    }
}