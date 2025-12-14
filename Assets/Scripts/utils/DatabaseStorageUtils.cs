using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neo4j.Driver;
using UnityEditor;
using UnityEngine;

namespace VidiGraph
{
    public class DatabaseStorageUtils
    {
        public static void BulkInitNetwork(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            NodeLinkContext context, IEnumerable<NodeLinkContext> subnetworkContexts,
            IDriver driver, bool convertWinPaths)
        {
            DeleteDatabaseContents(driver);
            CreateConstraints(driver);
            UpdateNetwork(networkFile, networkGlobal, context, subnetworkContexts, driver, convertWinPaths, false);
        }

        public static void BulkUpdateNetwork(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            NodeLinkContext context, IEnumerable<NodeLinkContext> subnetworkContexts,
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
            NodeLinkContext context, IEnumerable<NodeLinkContext> subnetworkContexts,
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
            NodeLinkContext context, IEnumerable<NodeLinkContext> subnetworkContexts,
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

                var allContexts = new HashSet<NodeLinkContext>() { context }.Union(subnetworkContexts);

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

    }
}