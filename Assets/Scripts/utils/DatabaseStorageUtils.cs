using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Neo4j.Driver;
using UnityEditor;
using UnityEngine;

namespace VidiGraph
{
    public class DatabaseStorageUtils
    {
        static readonly Regex PropertyNamePattern = new(
            @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        static string ValidatePropertyName(string attribute)
        {
            if (string.IsNullOrWhiteSpace(attribute) || !PropertyNamePattern.IsMatch(attribute))
                throw new ArgumentException($"Invalid Neo4j property name: '{attribute}'.", nameof(attribute));
            return attribute;
        }

        // Non-blocking bulk initialization of the network
        public static async Task BulkInitNetworkAsync(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            MultiLayoutContext context, IEnumerable<MultiLayoutContext> subnetworkContexts,
            IDriver driver, bool convertWinPaths, int timeoutSeconds, CancellationToken cancellationToken)
        {
            await DeleteDatabaseContentsAsync(driver, timeoutSeconds, cancellationToken);
            await CreateConstraintsAsync(driver, timeoutSeconds, cancellationToken);
            await UpdateNetworkAsync(networkFile, networkGlobal, context, subnetworkContexts, driver,
                convertWinPaths, false, timeoutSeconds, cancellationToken);
        }

        // Non-blocking bulk update of the network
        public static Task BulkUpdateNetworkAsync(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            MultiLayoutContext context, IEnumerable<MultiLayoutContext> subnetworkContexts,
            IDriver driver, bool convertWinPaths, int timeoutSeconds, CancellationToken cancellationToken)
        {
            return UpdateNetworkAsync(networkFile, networkGlobal, context, subnetworkContexts, driver,
                convertWinPaths, true, timeoutSeconds, cancellationToken);
        }

        // Non-blocking deletion of the database contents
        public static async Task DeleteDatabaseContentsAsync(
            IDriver driver, int timeoutSeconds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RunAutoCommitAsync(driver, "MATCH (n) DETACH DELETE n", null, timeoutSeconds);
            await DeleteConstraintsAsync(driver, timeoutSeconds, cancellationToken);
        }

        static async Task CreateConstraintsAsync(IDriver driver, int timeoutSeconds, CancellationToken cancellationToken)
        {
            string[] commands = {
                "CREATE CONSTRAINT NodeID IF NOT EXISTS FOR (n:Node) REQUIRE n.GUID IS UNIQUE",
                "CREATE CONSTRAINT CommID IF NOT EXISTS FOR (c:Community) REQUIRE c.GUID IS UNIQUE",
                "CREATE CONSTRAINT PointsTo IF NOT EXISTS FOR ()-[p:POINTS_TO]-() REQUIRE p.GUID IS UNIQUE"
            };
            foreach (string command in commands)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RunAutoCommitAsync(driver, command, null, timeoutSeconds);
            }
        }

        static async Task DeleteConstraintsAsync(IDriver driver, int timeoutSeconds, CancellationToken cancellationToken)
        {
            string[] commands = {
                "DROP CONSTRAINT NodeID IF EXISTS",
                "DROP CONSTRAINT CommID IF EXISTS",
                "DROP CONSTRAINT PointsTo IF EXISTS"
            };
            foreach (string command in commands)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RunAutoCommitAsync(driver, command, null, timeoutSeconds);
            }
        }

        static async Task RunAutoCommitAsync(
            IDriver driver, string command, IDictionary<string, object> parameters, int timeoutSeconds)
        {
            if (driver == null) throw new InvalidOperationException("Neo4j driver is not initialized.");
            IAsyncSession session = driver.AsyncSession();
            try
            {
                IResultCursor cursor = parameters == null
                    ? await session.RunAsync(command, config => config.WithTimeout(TimeSpan.FromSeconds(timeoutSeconds)))
                    : await session.RunAsync(command, parameters, config => config.WithTimeout(TimeSpan.FromSeconds(timeoutSeconds)));
                await cursor.ConsumeAsync();
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        static async Task UpdateNetworkAsync(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            MultiLayoutContext context, IEnumerable<MultiLayoutContext> subnetworkContexts,
            IDriver driver, bool convertWinPaths, bool onlyDirty, int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            DumpNetwork(networkFile, networkGlobal, context, subnetworkContexts,
                out var fs, out var fc, out var fn, out var fn2n, onlyDirty);
            try
            {
                var imports = new (string Query, string Filename)[] {
                    ("LOAD CSV WITH HEADERS FROM $filename AS row FIELDTERMINATOR ';' CALL (row) { MERGE (s:Subnetwork { subnetworkId: toInteger(row.subnetworkId) }) SET s.subnetworkId = toInteger(row.subnetworkId) } IN TRANSACTIONS OF 500 ROWS", ConvertToNeoPath(fs, convertWinPaths)),
                    ("LOAD CSV WITH HEADERS FROM $filename AS row FIELDTERMINATOR ';' CALL (row) { MERGE (c:Community { GUID: row.GUID }) SET c.commId = toInteger(row.commId) SET c.selected = toBoolean(row.selected) SET c.GUID = row.GUID SET c.mass = toFloat(row.mass) SET c.massCenter = row.massCenter SET c.size = toFloat(row.size) SET c.state = row.state WITH * MATCH (s:Subnetwork { subnetworkId: toInteger(row.subnetworkId) }) MERGE (c)-[:PART_OF]->(s) } IN TRANSACTIONS OF 500 ROWS", ConvertToNeoPath(fc, convertWinPaths)),
                    ("LOAD CSV WITH HEADERS FROM $filename AS row FIELDTERMINATOR ';' CALL (row) { MERGE (n:Node { GUID: row.GUID }) SET n.nodeId = toInteger(row.nodeId) SET n.label = row.label SET n.degree = toFloat(row.degree) SET n.selected = toBoolean(row.selected) SET n.GUID = row.GUID SET n.size = toFloat(row.size) SET n.pos = row.pos SET n.color = row.color " + ToQuery("n", networkFile.nodes[0].props) + "WITH * MATCH (c:Community { GUID: row.commRenderGUID }) MERGE (n)-[:PART_OF]->(c) } IN TRANSACTIONS OF 500 ROWS", ConvertToNeoPath(fn, convertWinPaths)),
                    ("LOAD CSV WITH HEADERS FROM $filename AS row FIELDTERMINATOR ';' CALL (row) { MATCH (from:Node { GUID: row.sourceRenderGUID }) MATCH (to:Node { GUID: row.targetRenderGUID }) MERGE (from)-[l:POINTS_TO { GUID: row.GUID } ]->(to) SET l.linkId = toInteger(row.linkId) SET l.selected = toBoolean(row.selected) SET l.GUID = row.GUID SET l.bundlingStrength = toFloat(row.bundlingStrength) SET l.width = toFloat(row.width) SET l.colorStart = row.colorStart SET l.colorEnd = row.colorEnd SET l.alpha = toFloat(row.alpha) " + ToQuery("l", networkFile.links[0].props) + "} IN TRANSACTIONS OF 500 ROWS", ConvertToNeoPath(fn2n, convertWinPaths))
                };
                foreach (var import in imports)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await RunAutoCommitAsync(driver, import.Query,
                        new Dictionary<string, object> { ["filename"] = import.Filename }, timeoutSeconds);
                }
                Debug.Log("Loaded to Neo4J database.");
            }
            finally
            {
                FileUtil.DeleteFileOrDirectory(fs);
                FileUtil.DeleteFileOrDirectory(fc);
                FileUtil.DeleteFileOrDirectory(fn);
                FileUtil.DeleteFileOrDirectory(fn2n);
            }
        }

        public static void BulkInitNetwork(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            MultiLayoutContext context, IEnumerable<MultiLayoutContext> subnetworkContexts,
            IDriver driver, bool convertWinPaths)
        {
            DeleteDatabaseContents(driver);
            CreateConstraints(driver);
            UpdateNetwork(networkFile, networkGlobal, context, subnetworkContexts, driver, convertWinPaths, false);
        }

        public static void BulkUpdateNetwork(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            MultiLayoutContext context, IEnumerable<MultiLayoutContext> subnetworkContexts,
            IDriver driver, bool convertWinPaths)
        {
            UpdateNetwork(networkFile, networkGlobal, context, subnetworkContexts, driver, convertWinPaths, true);
        }

        public static void DeleteDatabaseContents(IDriver driver)
        {
            driver.Session().Run("MATCH (n) DETACH DELETE n");
            DeleteConstraints(driver);
        }

        static void CreateConstraints(IDriver driver)
        {
            var sess = driver.Session();

            sess.ExecuteWrite(
                tx =>
                {
                    var result = tx.Run(
                        "CREATE CONSTRAINT NodeID IF NOT EXISTS FOR (n:Node) REQUIRE n.GUID IS UNIQUE "
                        );

                    return "success";
                });

            sess.ExecuteWrite(
                tx =>
                {
                    var result = tx.Run(
                        "CREATE CONSTRAINT CommID IF NOT EXISTS FOR (c:Community) REQUIRE c.GUID IS UNIQUE "
                        );

                    return "success";
                });

            sess.ExecuteWrite(
                tx =>
                {
                    var result = tx.Run(
                        "CREATE CONSTRAINT PointsTo IF NOT EXISTS FOR ()-[p:POINTS_TO]-() REQUIRE p.GUID IS UNIQUE "
                        );

                    return "success";
                });
        }

        static void DeleteConstraints(IDriver driver)
        {
            var sess = driver.Session();

            sess.ExecuteWrite(
                tx =>
                {
                    var result = tx.Run(
                        "DROP CONSTRAINT NodeID IF EXISTS"
                        );

                    return "success";
                });

            sess.ExecuteWrite(
                tx =>
                {
                    var result = tx.Run(
                        "DROP CONSTRAINT CommID IF EXISTS"
                        );

                    return "success";
                });

            sess.ExecuteWrite(
                tx =>
                {
                    var result = tx.Run(
                        "DROP CONSTRAINT PointsTo IF EXISTS"
                        );

                    return "success";
                });
        }

        static void UpdateNetwork(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            MultiLayoutContext context, IEnumerable<MultiLayoutContext> subnetworkContexts,
            IDriver driver, bool convertWinPaths, bool onlyDirty)
        {
            DumpNetwork(networkFile, networkGlobal, context, subnetworkContexts, out var fs, out var fc, out var fn, out var fn2n, onlyDirty);

            bool shutdown = false;

            try
            {
                var sess = driver.Session();

                var nfs = ConvertToNeoPath(fs, convertWinPaths);
                var nfc = ConvertToNeoPath(fc, convertWinPaths);
                var nfn = ConvertToNeoPath(fn, convertWinPaths);
                var nfn2n = ConvertToNeoPath(fn2n, convertWinPaths);

                sess.Run(
                            "LOAD CSV WITH HEADERS FROM $filename AS row FIELDTERMINATOR ';'" +
                            "CALL (row) { " +
                                "MERGE (s:Subnetwork { subnetworkId: toInteger(row.subnetworkId) }) " +
                                "SET s.subnetworkId = toInteger(row.subnetworkId) " +
                            "} IN TRANSACTIONS OF 500 ROWS",
                            new
                            {
                                filename = nfs
                            });

                sess.Run(
                            "LOAD CSV WITH HEADERS FROM $filename AS row FIELDTERMINATOR ';'" +
                            "CALL (row) { " +
                                "MERGE (c:Community { GUID: row.GUID }) " +
                                "SET c.commId = toInteger(row.commId) " +
                                "SET c.selected = toBoolean(row.selected) " +
                                "SET c.GUID = row.GUID " +
                                "SET c.mass = toFloat(row.mass) " +
                                "SET c.massCenter = row.massCenter " +
                                "SET c.size = toFloat(row.size) " +
                                "SET c.state = row.state " +
                                "WITH * " +
                                "MATCH (s:Subnetwork { subnetworkId: toInteger(row.subnetworkId) }) " +
                                "MERGE (c)-[:PART_OF]->(s) " +
                            "} IN TRANSACTIONS OF 500 ROWS",
                            new
                            {
                                filename = nfc
                            });

                sess.Run(
                            "LOAD CSV WITH HEADERS FROM $filename AS row FIELDTERMINATOR ';'" +
                            "CALL (row) { " +
                                "MERGE (n:Node { GUID: row.GUID }) " +
                                "SET n.nodeId = toInteger(row.nodeId) " +
                                "SET n.label = row.label " +
                                "SET n.degree = toFloat(row.degree) " +
                                "SET n.selected = toBoolean(row.selected) " +
                                "SET n.GUID = row.GUID " +
                                "SET n.size = toFloat(row.size) " +
                                "SET n.pos = row.pos " +
                                "SET n.color = row.color " +
                                ToQuery("n", networkFile.nodes[0].props) +
                                "WITH * " +
                                "MATCH (c:Community { GUID: row.commRenderGUID }) " +
                                "MERGE (n)-[:PART_OF]->(c) " +
                            "} IN TRANSACTIONS OF 500 ROWS",
                            new
                            {
                                filename = nfn
                            });

                sess.Run(
                            "LOAD CSV WITH HEADERS FROM $filename AS row FIELDTERMINATOR ';'" +
                            "CALL (row) { " +
                                "MATCH (from:Node { GUID: row.sourceRenderGUID }) " +
                                "MATCH (to:Node { GUID: row.targetRenderGUID }) " +
                                "MERGE (from)-[l:POINTS_TO { GUID: row.GUID } ]->(to) " +
                                "SET l.linkId = toInteger(row.linkId) " +
                                "SET l.selected = toBoolean(row.selected) " +
                                "SET l.GUID = row.GUID " +
                                "SET l.bundlingStrength = toFloat(row.bundlingStrength) " +
                                "SET l.width = toFloat(row.width) " +
                                "SET l.colorStart = row.colorStart " +
                                "SET l.colorEnd = row.colorEnd " +
                                "SET l.alpha = toFloat(row.alpha) " +
                                ToQuery("l", networkFile.links[0].props) +
                            "} IN TRANSACTIONS OF 500 ROWS",
                            new
                            {
                                filename = nfn2n
                            });

                sess.Dispose();

                Debug.Log("Loaded to Neo4J database.");
            }
            catch (ServiceUnavailableException e)
            {
                Debug.LogError(e.Message);

                shutdown = true;
            }
            catch (ClientException e)
            {
                Debug.LogError(e.Message);
                Debug.LogError("Could not load files to Neo4J database. Did you remove the setting `server.directories.import`?\n" +
                    "https://neo4j.com/docs/cypher-manual/current/clauses/load-csv/#_configuration_settings_for_file_urls");

                shutdown = true;
            }
            finally
            {
                FileUtil.DeleteFileOrDirectory(fn);
                FileUtil.DeleteFileOrDirectory(fc);
                FileUtil.DeleteFileOrDirectory(fn2n);

                if (shutdown)
                {
#if UNITY_EDITOR
                    EditorApplication.isPlaying = false;
#elif UNITY_STANDALONE
                    Application.Quit();
#endif
                }
            }
        }

        static void DumpNetwork(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            MultiLayoutContext context, IEnumerable<MultiLayoutContext> subnetworkContexts,
            out string subnetworkFile, out string commFile, out string nodeFile, out string nodeToNodeFile, bool onlyDirty = false)
        {
            subnetworkFile = FileUtil.GetUniqueTempPathInProject();
            commFile = FileUtil.GetUniqueTempPathInProject();
            nodeFile = FileUtil.GetUniqueTempPathInProject();
            nodeToNodeFile = FileUtil.GetUniqueTempPathInProject();

            using (StreamWriter sFile = new StreamWriter(subnetworkFile, append: false),
                                    cFile = new StreamWriter(commFile, append: false),
                                    nFile = new StreamWriter(nodeFile, append: false),
                                    n2nFile = new StreamWriter(nodeToNodeFile, append: false))
            {
                bool dumpProps = networkFile != null;

                sFile.WriteLine("subnetworkId");
                cFile.WriteLine("commId;selected;subnetworkId;GUID;mass;massCenter;size;state");

                string nodeHeaders = "nodeId;label;degree;selected;commRenderGUID;GUID;size;pos;color";
                string linkHeaders = "linkId;sourceRenderGUID;targetRenderGUID;selected;GUID;bundlingStrength;width;colorStart;colorEnd;alpha";

                IEnumerable<string> nodeProps = null;
                IEnumerable<string> linkProps = null;

                if (dumpProps)
                {
                    nodeProps = GetHeaders(networkFile.nodes[0].props);
                    linkProps = GetHeaders(networkFile.links[0].props);

                    nodeHeaders += ";" + string.Join(";", nodeProps);
                    linkHeaders += ";" + string.Join(";", linkProps);
                }

                nFile.WriteLine(nodeHeaders);
                n2nFile.WriteLine(linkHeaders);

                var allContexts = new HashSet<MultiLayoutContext>() { context }.Union(subnetworkContexts);

                foreach (var subContext in allContexts)
                {
                    var dumper = new DatabaseDumper(
                        sFile: sFile,
                        cFile: cFile,
                        nFile: nFile,
                        n2nFile: n2nFile,
                        networkFile: networkFile,
                        nodeProps: nodeProps,
                        linkProps: linkProps,
                        onlyDirty: onlyDirty,
                        dumpProps: dumpProps,
                        networkContext: subContext,
                        networkGlobal: networkGlobal);

                    dumper.Dump();
                }

            }
        }

        static string ConvertToNeoPath(string filepath, bool convertWinPaths)
        {
            // using FileUtil.GetPhysicalPath doesn't work with my WSL setup so I'm using Path.GetFullPath instead
            var fullpath = Path.GetFullPath(filepath).Replace("\\", "/");

            if (convertWinPaths)
            {
                var pathparts = fullpath.Split(":/");
                var drivePath = pathparts[0];
                var remainingPath = pathparts[1];
                fullpath = $"mnt/{drivePath.ToLower()}/{remainingPath}";
            }

            fullpath = "file:///" + fullpath;

            return fullpath;
        }

        static IEnumerable<string> GetHeaders(object props)
        {
            return ObjectUtils.AsDictionary(props).Keys;
        }

        static string ToQuery(string varname, object obj)
        {
            var keys = GetHeaders(obj);

            string query = "";

            foreach (var key in keys)
            {
                query += $"SET {varname}.{key} = row.{key}" + " ";
            }

            return query;
        }

        ////////////////// start specialized functions for BullyProps ////////////////////

        static string ToQuery(string varname, BullyProps.Node obj)
        {
            string query = "" +
                $"SET {varname}.type = row.type" + " " +
                $"SET {varname}.grade = toInteger(row.grade)" + " " +
                $"SET {varname}.bully_victim_ratio = toFloat(row.bully_victim_ratio)" + " ";

            return query;
        }
        static string ToQuery(string varname, BullyProps.Link obj)
        {
            string query = "" +
                $"SET {varname}.type = row.type" + " ";

            return query;
        }

        ////////////////// end specialized functions for BullyProps ////////////////////

        ////////////////// start specialized functions for FriendProps ////////////////////

        static string ToQuery(string varname, SchoolProps.Node obj)
        {
            string query = "" +
                $"SET {varname}.sex = row.sex" + " " +
                $"SET {varname}.smoker = toBoolean(row.smoker)" + " " +
                $"SET {varname}.drinker = toBoolean(row.drinker)" + " " +
                $"SET {varname}.gpa = toFloat(row.gpa)" + " " +
                $"SET {varname}.grade = toInteger(row.grade)" + " ";

            return query;
        }
        static string ToQuery(string varname, SchoolProps.Link obj)
        {
            string query = "" +
                $"SET {varname}.type = row.type" + " ";

            return query;
        }

        ////////////////// end specialized functions for FriendProps ////////////////////

        // Execute the commands generated by agents
        public static IEnumerable<string> GetNodesFromStore(NetworkGlobal networkGlobal, string command, IDriver driver, bool convertWinPaths = true)
        {
            try
            {
                var session = driver.Session();
                var res = session.Run(command);

                TimerUtils.StartTime("session.Run");
                Debug.Log($"Command executed successfully: {command}");
                TimerUtils.EndTime("session.Run");

                TimerUtils.StartTime("res.Select");
                var nodes = res.Select(r => r[0].As<INode>().Properties["GUID"].As<string>());
                TimerUtils.EndTime("res.Select");

                // IMPORTANT: convert enumerable to list to allow multiple traversals, since IResult can only be traversed once
                return nodes.ToList();
            }
            catch (Exception e)
            {
                Debug.LogError($"Execution Error: {e.Message}");
            }

            return new List<string>();
        }

        // Execute the commands generated by agents
        public static IEnumerable<string> GetLinksFromStore(NetworkGlobal networkGlobal, string command, IDriver driver, bool convertWinPaths = true)
        {
            try
            {
                var session = driver.Session();
                var res = session.Run(command);

                TimerUtils.StartTime("session.Run");
                Debug.Log($"Command executed successfully: {command}");
                TimerUtils.EndTime("session.Run");

                TimerUtils.StartTime("res.Select");
                var links = res.Select(r => r[0].As<IRelationship>().Properties["GUID"].As<string>());
                TimerUtils.EndTime("res.Select");

                // IMPORTANT: convert enumerable to list to allow multiple traversals, since IResult can only be traversed once
                return links.ToList();
            }
            catch (Exception e)
            {
                Debug.LogError($"Execution Error: {e.Message}");
            }

            return new List<string>();
        }

        public static async Task<List<string>> GetNodesFromStoreAsync(
            NetworkGlobal networkGlobal, string command, IDriver driver, int timeoutSeconds = 10,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            IAsyncSession session = driver.AsyncSession();
            try
            {
                IResultCursor cursor = await session.RunAsync(command,
                    config => config.WithTimeout(TimeSpan.FromSeconds(timeoutSeconds)));
                List<IRecord> records = await cursor.ToListAsync();
                
                cancellationToken.ThrowIfCancellationRequested();
                
                return records.Select(record => record[0].As<INode>()
                    .Properties["GUID"].As<string>()).ToList();
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        public static async Task<List<string>> GetLinksFromStoreAsync(
            NetworkGlobal networkGlobal, string command, IDriver driver, int timeoutSeconds = 10,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IAsyncSession session = driver.AsyncSession();
            try
            {
                IResultCursor cursor = await session.RunAsync(command,
                    config => config.WithTimeout(TimeSpan.FromSeconds(timeoutSeconds)));
                
                List<IRecord> records = await cursor.ToListAsync();
                
                cancellationToken.ThrowIfCancellationRequested();
                
                var relationshipGuids = new List<string>();
                
                foreach (IRecord record in records)
                {
                    object value = record[0];
                    if (!(value is IRelationship relationship))
                    {
                        Debug.LogError(
                            $"Link query must return relationships, but returned " +
                            $"{value?.GetType().Name ?? "null"}: {command}"
                        );
                        continue;
                    }

                    if (relationship.Properties.TryGetValue("GUID", out object guid))
                        relationshipGuids.Add(guid.As<string>());
                }
                return relationshipGuids;
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        public static double GetValueFromStore(NetworkGlobal networkGlobal, string command, IDriver driver, bool convertWinPaths = true)
        {
            try
            {
                using var session = driver.Session();

                TimerUtils.StartTime("session.Run");
                var res = session.Run(command);
                TimerUtils.EndTime("session.Run");

                TimerUtils.StartTime("res.Single");
                var record = res.Single(); // Expecting a single row
                var value = record[0].As<double>(); // Return the first column as double
                TimerUtils.EndTime("res.Single");

                Debug.Log($"Arithmetic result: {value}");
                return value;
            }
            catch (Exception e)
            {
                Debug.LogError($"Execution Error: {e.Message}");
            }

            return 0.0; // Return default value on error
        }

        public static async Task<double> GetValueFromStoreAsync(
            NetworkGlobal networkGlobal,
            string command,
            IDriver driver,
            int timeoutSeconds = 10,
            bool convertWinPaths = true,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (driver == null)
                throw new InvalidOperationException("Neo4j driver is not initialized.");

            IAsyncSession session = driver.AsyncSession();
            try
            {
                IResultCursor cursor = await session.RunAsync(
                    command,
                    config => config.WithTimeout(TimeSpan.FromSeconds(timeoutSeconds))
                );
                IRecord record = await cursor.SingleAsync();
                cancellationToken.ThrowIfCancellationRequested();
                object rawValue = record[0];
                if (rawValue == null)
                {
                    Debug.LogWarning($"Numeric query returned no data: {command}");
                    return double.NaN;
                }

                double value = rawValue.As<double>();

                Debug.Log($"Arithmetic result: {value}");
                return value;
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        public struct MinMaxResult
        {
            public float minValue;
            public float maxValue;
        }

        public Dictionary<string, float> buffer_database = new Dictionary<string, float>();

        public static (float Min, float Max) GetMinMaxFromStore(NetworkGlobal networkGlobal, string command, IDriver driver, bool convertWinPaths = true)
        {
            float min = 0f;
            float max = 0f;

            try
            {
                using var session = driver.Session();

                var res = session.Run(command);
                var record = res.Single();

                min = (float)record["minValue"].As<double>();
                max = (float)record["maxValue"].As<double>();

                Debug.Log($"MinMax Query: min={min}, max={max}");
            }
            catch (Exception e)
            {
                Debug.LogError($"MinMax Error: {e.Message}");
            }

            return (min, max);
        }

        public static async Task<(float Min, float Max)> GetMinMaxFromStoreAsync(
            NetworkGlobal networkGlobal, string command, IDriver driver, int timeoutSeconds = 10,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            IAsyncSession session = driver.AsyncSession();
            
            try
            {
                IResultCursor cursor = await session.RunAsync(command,
                    config => config.WithTimeout(TimeSpan.FromSeconds(timeoutSeconds)));
                
                IRecord record = await cursor.SingleAsync();
                
                cancellationToken.ThrowIfCancellationRequested();
                
                return ((float)record["minValue"].As<double>(),
                    (float)record["maxValue"].As<double>());
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        public static List<string> GetDistinctValuesFromStore(NetworkGlobal networkGlobal, string command, IDriver driver, bool convertWinPaths = true)
        {
            List<string> distinctValues = new List<string>();

            try
            {
                using var session = driver.Session();

                TimerUtils.StartTime("GetDistinctValues.Run");
                var res = session.Run(command);
                TimerUtils.EndTime("GetDistinctValues.Run");

                TimerUtils.StartTime("GetDistinctValues.ProcessResults");

                // Iterate through all records and extract the "value" field
                foreach (var record in res)
                {
                    try
                    {
                        var value = record["value"];

                        // Convert to string representation based on type
                        string valueStr;
                        var objValue = value.As<object>();

                        if (objValue == null)
                        {
                            continue; // Skip null values
                        }
                        else if (objValue is string)
                        {
                            valueStr = $"'{objValue}'"; // String values need quotes for Cypher
                        }
                        else
                        {
                            valueStr = objValue.ToString(); // Numeric/boolean values don't need quotes
                        }

                        distinctValues.Add(valueStr);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Skipping value due to error: {ex.Message}");
                        continue;
                    }
                }

                TimerUtils.EndTime("GetDistinctValues.ProcessResults");

                Debug.Log($"Found {distinctValues.Count} distinct values");
            }
            catch (Exception e)
            {
                Debug.LogError($"GetDistinctValues Error: {e.Message}");
            }

            return distinctValues;
        }

        // Single query that returns all nodes grouped by a categorical attribute.
        // Keys are Cypher-safe value strings (strings wrapped in single quotes, numbers/booleans unquoted)
        // to match the format produced by GetDistinctValuesFromStore.
        // This replaces the N-round-trip pattern of calling GetDistinctValuesFromStore + one GetNodesFromStore per category.
        public static Dictionary<string, List<string>> GetNodesGroupedByAttribute(
            NetworkGlobal networkGlobal, string attribute, IDriver driver)
        {
            var result = new Dictionary<string, List<string>>();
            try
            {
                using var session = driver.Session();
                string command = $"MATCH (n:Node) WHERE n.{attribute} IS NOT NULL RETURN n.GUID AS guid, n.{attribute} AS value";

                TimerUtils.StartTime("GetNodesGroupedByAttribute.Run");
                var res = session.Run(command);
                TimerUtils.EndTime("GetNodesGroupedByAttribute.Run");

                TimerUtils.StartTime("GetNodesGroupedByAttribute.Group");
                foreach (var record in res)
                {
                    string guid = record["guid"].As<string>();
                    var raw = record["value"].As<object>();
                    if (raw == null) continue;

                    string key = raw is string s ? $"'{s}'" : raw.ToString();

                    if (!result.TryGetValue(key, out var list))
                    {
                        list = new List<string>();
                        result[key] = list;
                    }
                    list.Add(guid);
                }
                TimerUtils.EndTime("GetNodesGroupedByAttribute.Group");

                Debug.Log($"[GetNodesGroupedByAttribute] {result.Count} groups for '{attribute}', {result.Values.Sum(l => l.Count)} total nodes");
            }
            catch (Exception e)
            {
                Debug.LogError($"GetNodesGroupedByAttribute Error: {e.Message}");
            }
            return result;
        }

        // Async version of GetNodesGroupedByAttribute, returns a dictionary of attribute value → list of node GUIDs.
        public static async Task<Dictionary<string, List<string>>> GetNodesGroupedByAttributeAsync(
            NetworkGlobal networkGlobal, string attribute, IDriver driver, int timeoutSeconds = 10,
            CancellationToken cancellationToken = default)
        {
            // Check for cancellation before proceeding with the query.
            cancellationToken.ThrowIfCancellationRequested();
            attribute = ValidatePropertyName(attribute);
            
            var result = new Dictionary<string, List<string>>();
            
            string command = $"MATCH (n:Node) WHERE n.{attribute} IS NOT NULL RETURN n.GUID AS guid, n.{attribute} AS value";
            IAsyncSession session = driver.AsyncSession();
            try
            {
                IResultCursor cursor = await session.RunAsync(command,
                    config => config.WithTimeout(TimeSpan.FromSeconds(timeoutSeconds)));
                
                List<IRecord> records = await cursor.ToListAsync();
                cancellationToken.ThrowIfCancellationRequested();
                
                // For each record, extract the GUID and attribute value, and group by the attribute value.
                foreach (IRecord record in records)
                {
                    string guid = record["guid"].As<string>();
                    object raw = record["value"].As<object>();
                    if (raw == null) continue;
                    
                    string key = raw is string text ? $"'{text}'" : raw.ToString();
                    
                    if (!result.TryGetValue(key, out List<string> list))
                        result[key] = list = new List<string>();
                    
                    list.Add(guid);
                }
                return result;
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        // Single query that returns a dict of GUID → numeric attribute value for all nodes that have the attribute.
        // Used to do client-side bucketing for gradient color encodings, replacing N bucket queries.
        public static Dictionary<string, float> GetNodesWithNumericValues(
            NetworkGlobal networkGlobal, string attribute, IDriver driver)
        {
            var result = new Dictionary<string, float>();
            try
            {
                using var session = driver.Session();
                string command = $"MATCH (n:Node) WHERE n.{attribute} IS NOT NULL RETURN n.GUID AS guid, n.{attribute} AS value";

                TimerUtils.StartTime("GetNodesWithNumericValues.Run");
                var res = session.Run(command);
                TimerUtils.EndTime("GetNodesWithNumericValues.Run");

                TimerUtils.StartTime("GetNodesWithNumericValues.Collect");
                foreach (var record in res)
                {
                    string guid = record["guid"].As<string>();
                    float value = (float)record["value"].As<double>();
                    result[guid] = value;
                }
                TimerUtils.EndTime("GetNodesWithNumericValues.Collect");

                Debug.Log($"[GetNodesWithNumericValues] {result.Count} nodes with '{attribute}'");
            }
            catch (Exception e)
            {
                Debug.LogError($"GetNodesWithNumericValues Error: {e.Message}");
            }
            return result;
        }

        public static async Task<Dictionary<string, float>> GetNodesWithNumericValuesAsync(
            NetworkGlobal networkGlobal, string attribute, IDriver driver, int timeoutSeconds = 10,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attribute = ValidatePropertyName(attribute);
            var result = new Dictionary<string, float>();
            string command = $"MATCH (n:Node) WHERE n.{attribute} IS NOT NULL RETURN n.GUID AS guid, n.{attribute} AS value";
            IAsyncSession session = driver.AsyncSession();
            try
            {
                IResultCursor cursor = await session.RunAsync(command,
                    config => config.WithTimeout(TimeSpan.FromSeconds(timeoutSeconds)));
                List<IRecord> records = await cursor.ToListAsync();
                cancellationToken.ThrowIfCancellationRequested();
                foreach (IRecord record in records)
                {
                    string guid = record["guid"].As<string>();
                    result[guid] = Convert.ToSingle(record["value"].As<object>());
                }
                return result;
            }
            finally
            {
                await session.CloseAsync();
            }
        }
    }
}
