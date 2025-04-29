using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using UnityEngine;
using VidiGraph;

public class Query : MonoBehaviour
{
    public NetworkManager _networkManager;

    [SerializeField]
    private string _queryString;

    private void Update() // Since the voice detection sucks ... Used to test the query
    {
        var queryable = _networkManager.NetworkGlobal.Nodes.GetNodeArray().AsQueryable();
        if (_queryString == "1")
        {
            var list = _networkManager.NetworkGlobal.Nodes.GetNodeArray();
            try
            {
                var result = list.AsQueryable()
                        .OrderBy("Degree descending")
                        .Take(1)
                        .ToList();

                foreach (var node in result)
                {
                    Debug.Log($"Query result: {node.ID}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"LINQ query failed: {ex.Message}");
                return;
            }
            _queryString = ""; // Clear after executing
        }
        else if (_queryString == "2")
        {
            _queryString = "list.AsQueryable().OrderBy(\"Height\").Take(5).ToList()";
            var list = _networkManager.NetworkGlobal.Nodes.GetNodeArray();
            try
            {
                // Detect if OrderBy exists
                if (_queryString.Contains("OrderBy"))
                {
                    var start = _queryString.IndexOf("OrderBy(\"") + "OrderBy(\"".Length;
                    var end = _queryString.IndexOf("\")", start);
                    var expression = _queryString.Substring(start, end - start);

                    queryable = queryable.OrderBy(expression);
                }

                // Detect if Take exists
                if (_queryString.Contains("Take("))
                {
                    var start = _queryString.IndexOf("Take(") + "Take(".Length;
                    var end = _queryString.IndexOf(")", start);
                    var takeCountStr = _queryString.Substring(start, end - start);

                    if (int.TryParse(takeCountStr, out int takeCount))
                    {
                        queryable = queryable.Take(takeCount);
                    }
                }

                // Finalize
                var result = queryable.ToList();

                // Print result
                foreach (var node in result)
                {
                    Debug.Log($"Query result: {node.ID}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Query execution failed: {ex.Message}");
            }
        }

    }

    public void ExecuteQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Debug.LogWarning("Empty Query");
            return;
        }

        var list = _networkManager.NetworkGlobal.Nodes.GetNodeArray();
        var queryable = list.AsQueryable();

        try
        {
            // Detect if OrderBy exists
            if (query.Contains("OrderBy"))
            {
                var start = query.IndexOf("OrderBy(\"") + "OrderBy(\"".Length;
                var end = query.IndexOf("\")", start);
                var expression = query.Substring(start, end - start);

                queryable = queryable.OrderBy(expression);
            }

            // Detect if Take exists
            if (query.Contains("Take("))
            {
                var start = query.IndexOf("Take(") + "Take(".Length;
                var end = query.IndexOf(")", start);
                var takeCountStr = query.Substring(start, end - start);

                if (int.TryParse(takeCountStr, out int takeCount))
                {
                    queryable = queryable.Take(takeCount);
                }
            }

            // Finalize
            var result = queryable.ToList();

            // Print result
            foreach (var node in result)
            {
                Debug.Log($"Query result: {node.ID}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Query execution failed: {ex.Message}");
        }


    }
}
