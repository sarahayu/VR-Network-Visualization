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
            MultiLayoutContext context, IEnumerable<MultiLayoutContext> subnetworkContexts,
            IDriver driver, bool convertWinPaths)
        {
            UpdateNetwork(networkFile, networkGlobal, context, subnetworkContexts, driver, convertWinPaths);
        }

        public static void BulkUpdateNetwork(NetworkGlobal networkGlobal, MultiLayoutContext context,
            IEnumerable<MultiLayoutContext> subnetworkContexts, IDriver driver, bool convertWinPaths)
        {
            UpdateNetwork(null, networkGlobal, context, subnetworkContexts, driver, convertWinPaths);
        }
        static void UpdateNetwork(NetworkFileData networkFile, NetworkGlobal networkGlobal,
            MultiLayoutContext context, IEnumerable<MultiLayoutContext> subnetworkContexts,
            IDriver driver, bool convertWinPaths)
        {
            bool isInitialUpdate = networkFile != null;
            bool onlyDirty = !isInitialUpdate;

            DumpNetwork(networkFile, networkGlobal, context, subnetworkContexts, out var fs, out var fc, out var fn, out var fn2n, onlyDirty);

            bool shutdown = false;

            try
            {
                var sess = driver.Session();

                var nfs = ConvertToNeoPath(fs, convertWinPaths);
                var nfc = ConvertToNeoPath(fc, convertWinPaths);
                var nfn = ConvertToNeoPath(fn, convertWinPaths);
                var nfn2n = ConvertToNeoPath(fn2n, convertWinPaths);

                if (isInitialUpdate)
                {
                    sess.ExecuteWrite(
                        tx =>
                        {
                            var result = tx.Run(
                                "CREATE CONSTRAINT NodeID IF NOT EXISTS FOR (n:Node) REQUIRE n.render_UUID IS UNIQUE "
                                );

                            return "success";
                        });

                    sess.ExecuteWrite(
                        tx =>
                        {
                            var result = tx.Run(
                                "CREATE CONSTRAINT CommID IF NOT EXISTS FOR (c:Community) REQUIRE c.render_UUID IS UNIQUE "
                                );

                            return "success";
                        });

                    sess.ExecuteWrite(
                        tx =>
                        {
                            var result = tx.Run(
                                "CREATE CONSTRAINT PointsTo IF NOT EXISTS FOR ()-[p:POINTS_TO]-() REQUIRE p.render_UUID IS UNIQUE "
                                );

                            return "success";
                        });
                }

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
                                "MERGE (c:Community { render_UUID: row.render_UUID }) " +
                                "SET c.commId = toInteger(row.commId) " +
                                "SET c.selected = toBoolean(row.selected) " +
                                "SET c.render_UUID = row.render_UUID " +
                                "SET c.render_mass = toFloat(row.render_mass) " +
                                "SET c.render_massCenter = row.render_massCenter " +
                                "SET c.render_size = toFloat(row.render_size) " +
                                "SET c.render_state = row.render_state " +
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
                                "MERGE (n:Node { render_UUID: row.render_UUID }) " +
                                "SET n.nodeId = toInteger(row.nodeId) " +
                                "SET n.label = row.label " +
                                "SET n.degree = toFloat(row.degree) " +
                                "SET n.selected = toBoolean(row.selected) " +
                                "SET n.render_UUID = row.render_UUID " +
                                "SET n.render_size = toFloat(row.render_size) " +
                                "SET n.render_pos = row.render_pos " +
                                "SET n.render_color = row.render_color " +
                                (isInitialUpdate ? ToQuery("n", networkFile.nodes[0].props) : "") +
                                "WITH * " +
                                "MATCH (c:Community { render_UUID: row.commRenderUUID }) " +
                                "MERGE (n)-[:PART_OF]->(c) " +
                            "} IN TRANSACTIONS OF 500 ROWS",
                            new
                            {
                                filename = nfn
                            });

                sess.Run(
                            "LOAD CSV WITH HEADERS FROM $filename AS row FIELDTERMINATOR ';'" +
                            "CALL (row) { " +
                                "MATCH (from:Node { render_UUID: row.sourceRenderUUID }) " +
                                "MATCH (to:Node { render_UUID: row.targetRenderUUID }) " +
                                "MERGE (from)-[l:POINTS_TO { render_UUID: row.render_UUID } ]->(to) " +
                                "SET l.linkId = toInteger(row.linkId) " +
                                "SET l.selected = toBoolean(row.selected) " +
                                "SET l.render_UUID = row.render_UUID " +
                                "SET l.render_bundlingStrength = toFloat(row.render_bundlingStrength) " +
                                "SET l.render_width = toFloat(row.render_width) " +
                                "SET l.render_colorStart = row.render_colorStart " +
                                "SET l.render_colorEnd = row.render_colorEnd " +
                                "SET l.render_alpha = toFloat(row.render_alpha) " +
                                (isInitialUpdate ? ToQuery("l", networkFile.links[0].props) : "") +
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
                cFile.WriteLine("commId;selected;subnetworkId;render_UUID;render_mass;render_massCenter;render_size;render_state");

                string nodeHeaders = "nodeId;label;degree;selected;commRenderUUID;render_UUID;render_size;render_pos;render_color";
                string linkHeaders = "linkId;sourceRenderUUID;targetRenderUUID;selected;render_UUID;render_bundlingStrength;render_width;render_colorStart;render_colorEnd;render_alpha";

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
        public static IEnumerable<Node> GetNodesFromStore(NetworkGlobal networkGlobal, string command, IDriver driver, bool convertWinPaths = true)
        {
            try
            {
                var session = driver.Session();
                var res = session.Run(command);

                TimerUtils.StartTime("session.Run");
                Debug.Log($"Command executed successfully: {command}");
                TimerUtils.EndTime("session.Run");

                TimerUtils.StartTime("res.Select");
                var nodes = res.Select(r => networkGlobal.Nodes[r[0].As<INode>().Properties["nodeId"].As<int>()]);
                TimerUtils.EndTime("res.Select");

                return nodes;
            }
            catch (Exception e)
            {
                Debug.LogError($"Execution Error: {e.Message}");
            }

            return new List<Node>();
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