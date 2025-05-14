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

    IDriver _driver;
    // Start is called before the first frame update
    void Start()
    {
        _driver = GraphDatabase.Driver(_uri, AuthTokens.Basic(_user, _password));

        var message = "hello world";

        using var session = _driver.Session();
        var greeting = session.ExecuteWrite(
            tx =>
            {
                var result = tx.Run(
                    "CREATE (a:Greeting) " +
                    "SET a.message = $message " +
                    "RETURN a.message + ', from node ' + id(a)",
                    new { message });

                return result.Single()[0].As<string>();
            });

        print(greeting);
    }

    void OnApplicationQuit()
    {
        _driver?.Dispose();
    }

    // Update is called once per frame
    void Update()
    {

    }
}
