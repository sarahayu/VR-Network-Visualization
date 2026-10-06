/*
*
* DatabaseStorage saves network data to a neo4j database.
*
*/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        [SerializeField, Min(1)] int _queryTimeoutSeconds = 10;

        IDriver _driver;
        CancellationTokenSource _lifetimeCancellation;
        public bool IsReady { get; private set; }

        void Awake()
        {
            _lifetimeCancellation = new CancellationTokenSource();
            _driver = GraphDatabase.Driver(_uri, AuthTokens.Basic(_user, _password), o => o.WithMaxTransactionRetryTime(TimeSpan.FromSeconds(5)));
        }

        async void Start()
        {
            try
            {
                await _driver.VerifyConnectivityAsync();
                IsReady = true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not connect to Neo4j at {_uri}: {exception.Message}", this);
            }
        }

        void OnDestroy()
        {
            IsReady = false;
            _lifetimeCancellation?.Cancel();
            _lifetimeCancellation?.Dispose();
            _lifetimeCancellation = null;
            IDriver driver = _driver;
            _driver = null;
            if (driver != null) _ = DisposeDriverAsync(driver);
        }

        async Task DisposeDriverAsync(IDriver driver)
        {
            try { await driver.CloseAsync(); }
            catch (Exception exception) { Debug.LogWarning($"Neo4j driver disposal failed: {exception.Message}", this); }
        }

        public override async Task InitialStoreAsync(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            MultiLayoutContext networkContext, IEnumerable<MultiLayoutContext> subnetworkContexts,
            CancellationToken cancellationToken = default)
        {
            EnsureDriver();
            using var linked = CreateLinkedCancellation(cancellationToken);
            TimerUtils.StartTime("DatabaseStorage.InitialStoreAsync");
            try
            {
                await DatabaseStorageUtils.BulkInitNetworkAsync(networkFile, networkGlobal, networkContext,
                    subnetworkContexts, _driver, _convertWinPaths, _queryTimeoutSeconds, linked.Token);
            }
            finally { TimerUtils.EndTime("DatabaseStorage.InitialStoreAsync"); }
        }

        public override async Task UpdateStoreAsync(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            MultiLayoutContext networkContext, IEnumerable<MultiLayoutContext> subnetworkContexts,
            CancellationToken cancellationToken = default)
        {
            EnsureDriver();
            using var linked = CreateLinkedCancellation(cancellationToken);
            TimerUtils.StartTime("DatabaseStorage.UpdateStoreAsync");
            try
            {
                await DatabaseStorageUtils.BulkUpdateNetworkAsync(networkFile, networkGlobal, networkContext,
                    subnetworkContexts, _driver, _convertWinPaths, _queryTimeoutSeconds, linked.Token);
            }
            finally { TimerUtils.EndTime("DatabaseStorage.UpdateStoreAsync"); }
        }

        public override async Task DeleteContentsAsync(CancellationToken cancellationToken = default)
        {
            EnsureDriver();
            using var linked = CreateLinkedCancellation(cancellationToken);
            await DatabaseStorageUtils.DeleteDatabaseContentsAsync(_driver, _queryTimeoutSeconds, linked.Token);
        }

        CancellationTokenSource CreateLinkedCancellation(CancellationToken cancellationToken)
        {
            var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeCancellation.Token);
            linked.CancelAfter(TimeSpan.FromSeconds(_queryTimeoutSeconds));
            return linked;
        }

        void EnsureDriver()
        {
            if (_driver == null || _lifetimeCancellation == null || _lifetimeCancellation.IsCancellationRequested)
                throw new InvalidOperationException("Neo4j driver is not available.");
        }

        public IEnumerable<string> GetNodesFromStore(NetworkGlobal networkGlobal, string command)
        {
            return DatabaseStorageUtils.GetNodesFromStore(networkGlobal, command, _driver, _convertWinPaths);
        }

        // Async version of GetNodesFromStore, returns a Task that resolves to a list of node identifiers.
        public async Task<List<string>> GetNodesFromStoreAsync(NetworkGlobal networkGlobal, string command,
            CancellationToken cancellationToken = default)
        {
            EnsureDriver();
            using var linked = CreateLinkedCancellation(cancellationToken);
            return await DatabaseStorageUtils.GetNodesFromStoreAsync(
                networkGlobal, command, _driver, _queryTimeoutSeconds, linked.Token);
        }

        public IEnumerable<string> GetLinksFromStore(NetworkGlobal networkGlobal, string command)
        {
            return DatabaseStorageUtils.GetLinksFromStore(networkGlobal, command, _driver, _convertWinPaths);
        }

        public async Task<List<string>> GetLinksFromStoreAsync(NetworkGlobal networkGlobal, string command,
            CancellationToken cancellationToken = default)
        {
            EnsureDriver();
            using var linked = CreateLinkedCancellation(cancellationToken);
            return await DatabaseStorageUtils.GetLinksFromStoreAsync(
                networkGlobal, command, _driver, _queryTimeoutSeconds, linked.Token);
        }

        public double GetValueFromStore(NetworkGlobal networkGlobal, string command)
        {
            return DatabaseStorageUtils.GetValueFromStore(networkGlobal, command, _driver, _convertWinPaths);
        }

        // Async version of GetValueFromStore, returns a Task that resolves to the value.
        public async Task<double> GetValueFromStoreAsync(
            NetworkGlobal networkGlobal,
            string command,
            int timeoutSeconds = 10,
            CancellationToken cancellationToken = default)
        {
            EnsureDriver();
            using var linked = CreateLinkedCancellation(cancellationToken);
            return await DatabaseStorageUtils.GetValueFromStoreAsync(
                networkGlobal,
                command,
                _driver,
                timeoutSeconds,
                _convertWinPaths,
                linked.Token
            );
        }

        public (float minValue, float maxValue) GetMinMaxFromStore(NetworkGlobal networkGlobal, string command)
        {
            return DatabaseStorageUtils.GetMinMaxFromStore(networkGlobal, command, _driver, _convertWinPaths);
        }

        public async Task<(float minValue, float maxValue)> GetMinMaxFromStoreAsync(NetworkGlobal networkGlobal,
            string command, CancellationToken cancellationToken = default)
        {
            EnsureDriver();
            using var linked = CreateLinkedCancellation(cancellationToken);
            return await DatabaseStorageUtils.GetMinMaxFromStoreAsync(
                networkGlobal, command, _driver, _queryTimeoutSeconds, linked.Token);
        }

        public List<string> GetDistinctValuesFromStore(NetworkGlobal networkGlobal, string command)
        {
            return DatabaseStorageUtils.GetDistinctValuesFromStore(networkGlobal, command, _driver, _convertWinPaths);
        }

        // Returns all nodes grouped by a categorical attribute in one query.
        // Replaces the N-round-trip pattern of GetDistinctValuesFromStore + one GetNodesFromStore per category.
        public Dictionary<string, List<string>> GetNodesGroupedByAttribute(NetworkGlobal networkGlobal, string attribute)
        {
            return DatabaseStorageUtils.GetNodesGroupedByAttribute(networkGlobal, attribute, _driver);
        }

        public async Task<Dictionary<string, List<string>>> GetNodesGroupedByAttributeAsync(
            NetworkGlobal networkGlobal, string attribute, CancellationToken cancellationToken = default)
        {
            EnsureDriver();
            using var linked = CreateLinkedCancellation(cancellationToken);
            return await DatabaseStorageUtils.GetNodesGroupedByAttributeAsync(
                networkGlobal, attribute, _driver, _queryTimeoutSeconds, linked.Token);
        }

        // Returns GUID → numeric value for all nodes that have the attribute.
        // Used for client-side bucketing in gradient color encodings.
        public Dictionary<string, float> GetNodesWithNumericValues(NetworkGlobal networkGlobal, string attribute)
        {
            return DatabaseStorageUtils.GetNodesWithNumericValues(networkGlobal, attribute, _driver);
        }

        public async Task<Dictionary<string, float>> GetNodesWithNumericValuesAsync(
            NetworkGlobal networkGlobal, string attribute, CancellationToken cancellationToken = default)
        {
            EnsureDriver();
            using var linked = CreateLinkedCancellation(cancellationToken);
            return await DatabaseStorageUtils.GetNodesWithNumericValuesAsync(
                networkGlobal, attribute, _driver, _queryTimeoutSeconds, linked.Token);
        }
    }
}
