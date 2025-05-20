using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Neo4j.Driver;
using UnityEngine;

public class NetworkStorage : MonoBehaviour
{
    [SerializeField]
    String _uri = "bolt://localhost:7687";
    [SerializeField]
    String _user = "neo4j";
    [SerializeField]
    String _password = "neoneoneo";
    [SerializeField]
    bool _convertWinPaths = true;

    public bool ConvertWinPaths { get { return _convertWinPaths; } }

    IDriver _driver;
    // Start is called before the first frame update
    void Start()
    {
        _driver = GraphDatabase.Driver(_uri, AuthTokens.Basic(_user, _password), o => o.WithMaxTransactionRetryTime(TimeSpan.FromSeconds(5)));
    }

    void OnApplicationQuit()
    {
        _driver?.Dispose();
    }

    // Update is called once per frame
    void Update()
    {

    }

    public ISession StartSession()
    {

        return _driver.Session();
    }
}
