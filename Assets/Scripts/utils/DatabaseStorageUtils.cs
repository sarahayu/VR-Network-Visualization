using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Neo4j.Driver;
using UnityEditor;
using UnityEngine;

namespace VidiGraph
{
    public class DatabaseStorageUtils
    {
        public static void BulkInitNetwork(NetworkGlobal networkGlobal, MultiLayoutContext context, IDriver driver, bool convertWinPaths)
        {
            DumpNetwork(networkGlobal, context, out var fc, out var fn, out var fn2n);

            bool shutdown = false;

            try
            {
                var sess = driver.Session();

                var nfc = ConvertToNeoPath(fc, convertWinPaths);
                var nfn = ConvertToNeoPath(fn, convertWinPaths);
                var nfn2n = ConvertToNeoPath(fn2n, convertWinPaths);

                sess.ExecuteWrite(
                    tx =>
                    {
                        var result = tx.Run(
                            "CREATE CONSTRAINT NodeID IF NOT EXISTS FOR (n:Node) REQUIRE n.nodeId IS UNIQUE "
                            );

                        return "success";
                    });

                sess.ExecuteWrite(
                    tx =>
                    {
                        var result = tx.Run(
                            "CREATE CONSTRAINT CommID IF NOT EXISTS FOR (c:Community) REQUIRE c.commId IS UNIQUE "
                            );

                        return "success";
                    });

                sess.ExecuteWrite(
                    tx =>
                    {
                        var result = tx.Run(
                            "CREATE CONSTRAINT PointsTo IF NOT EXISTS FOR ()-[p:POINTS_TO]-() REQUIRE p.linkId IS UNIQUE "
                            );

                        return "success";
                    });

                sess.Run(
                            "LOAD CSV WITH HEADERS FROM $filename AS row FIELDTERMINATOR ';'" +
                            "CALL (row) { " +
                                "MERGE (c:Community { commId: toInteger(row.commId) }) " +
                                "SET c.selected = toBoolean(row.selected) " +
                                "SET c.render_mass = toFloat(row.render_mass) " +
                                "SET c.render_massCenter = row.render_massCenter " +
                                "SET c.render_size = toFloat(row.render_size) " +
                                "SET c.render_state = row.render_state " +
                            "} IN TRANSACTIONS OF 500 ROWS",
                            new
                            {
                                filename = nfc
                            });

                sess.Run(
                            "LOAD CSV WITH HEADERS FROM $filename AS row FIELDTERMINATOR ';'" +
                            "CALL (row) { " +
                                "MERGE (n:Node { nodeId: toInteger(row.nodeId) }) " +
                                "SET n.label = row.label " +
                                "SET n.degree = toFloat(row.degree) " +
                                "SET n.selected = toBoolean(row.selected) " +
                                "SET n.render_size = toFloat(row.render_size) " +
                                "SET n.render_pos = row.render_pos " +
                                "SET n.render_color = row.render_color " +
                                "WITH * " +
                                "MATCH (c:Community { commId: toInteger(row.commId) }) " +
                                "MERGE (n)-[:PART_OF]->(c) " +
                            "} IN TRANSACTIONS OF 500 ROWS",
                            new
                            {
                                filename = nfn
                            });

                sess.Run(
                            "LOAD CSV WITH HEADERS FROM $filename AS row FIELDTERMINATOR ';'" +
                            "CALL (row) { " +
                                "MATCH (from:Node { nodeId: toInteger(row.sourceNodeId) }) " +
                                "MATCH (to:Node { nodeId: toInteger(row.targetNodeId) }) " +
                                "MERGE (from)-[l:POINTS_TO { linkId: toInteger(row.linkId) } ]->(to) " +
                                "SET l.selected = toBoolean(row.selected) " +
                                "SET l.render_bundlingStrength = toFloat(row.render_bundlingStrength) " +
                                "SET l.render_width = toFloat(row.render_width) " +
                                "SET l.render_colorStart = row.render_colorStart " +
                                "SET l.render_colorEnd = row.render_colorEnd " +
                                "SET l.render_alpha = toFloat(row.render_alpha) " +
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
            catch (ClientException)
            {
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

        static string _fc = null, _fn = null, _fn2n = null;

        public static void BulkUpdateNetwork(NetworkGlobal networkGlobal, MultiLayoutContext context, IDriver driver, bool convertWinPaths)
        {
            if (_fc == null)
            {
                _fc = FileUtil.GetUniqueTempPathInProject();
                _fn = FileUtil.GetUniqueTempPathInProject();
                _fn2n = FileUtil.GetUniqueTempPathInProject();
            }

            DumpNetwork(networkGlobal, context, _fc, _fn, _fn2n, true);

            bool shutdown = false;

            try
            {
                var sess = driver.Session();

                var nfc = ConvertToNeoPath(_fc, convertWinPaths);
                var nfn = ConvertToNeoPath(_fn, convertWinPaths);
                var nfn2n = ConvertToNeoPath(_fn2n, convertWinPaths);

                sess.Run(
                            "LOAD CSV WITH HEADERS FROM $filename AS row FIELDTERMINATOR ';'" +
                            "CALL (row) { " +
                                "MERGE (c:Community { commId: toInteger(row.commId) }) " +
                                "SET c.selected = toBoolean(row.selected) " +
                                "SET c.render_mass = toFloat(row.render_mass) " +
                                "SET c.render_massCenter = row.render_massCenter " +
                                "SET c.render_size = toFloat(row.render_size) " +
                                "SET c.render_state = row.render_state " +
                            "} IN TRANSACTIONS OF 500 ROWS",
                            new
                            {
                                filename = nfc
                            });

                sess.Run(
                            "LOAD CSV WITH HEADERS FROM $filename AS row FIELDTERMINATOR ';'" +
                            "CALL (row) { " +
                                "MERGE (n:Node { nodeId: toInteger(row.nodeId) }) " +
                                "SET n.label = row.label " +
                                "SET n.degree = toFloat(row.degree) " +
                                "SET n.selected = toBoolean(row.selected) " +
                                "SET n.render_size = toFloat(row.render_size) " +
                                "SET n.render_pos = row.render_pos " +
                                "SET n.render_color = row.render_color " +
                                "WITH * " +
                                "MATCH (c:Community { commId: toInteger(row.commId) }) " +
                                "MERGE (n)-[:PART_OF]->(c) " +
                            "} IN TRANSACTIONS OF 500 ROWS",
                            new
                            {
                                filename = nfn
                            });

                sess.Run(
                            "LOAD CSV WITH HEADERS FROM $filename AS row FIELDTERMINATOR ';'" +
                            "CALL (row) { " +
                                "MATCH (from:Node { nodeId: toInteger(row.sourceNodeId) }) " +
                                "MATCH (to:Node { nodeId: toInteger(row.targetNodeId) }) " +
                                "MERGE (from)-[l:POINTS_TO { linkId: toInteger(row.linkId) } ]->(to) " +
                                "SET l.selected = toBoolean(row.selected) " +
                                "SET l.render_bundlingStrength = toFloat(row.render_bundlingStrength) " +
                                "SET l.render_width = toFloat(row.render_width) " +
                                "SET l.render_colorStart = row.render_colorStart " +
                                "SET l.render_colorEnd = row.render_colorEnd " +
                                "SET l.render_alpha = toFloat(row.render_alpha) " +
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
            catch (ClientException)
            {
                Debug.LogError("Could not load files to Neo4J database. Did you remove the setting `server.directories.import`?\n" +
                    "https://neo4j.com/docs/cypher-manual/current/clauses/load-csv/#_configuration_settings_for_file_urls");

                shutdown = true;
            }
            finally
            {
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

        static void DumpNetwork(NetworkGlobal networkGlobal, MultiLayoutContext context,
            out string commFile, out string nodeFile, out string nodeToNodeFile, bool onlyDirty = false)
        {
            commFile = FileUtil.GetUniqueTempPathInProject();
            nodeFile = FileUtil.GetUniqueTempPathInProject();
            nodeToNodeFile = FileUtil.GetUniqueTempPathInProject();

            DumpNetwork(networkGlobal, context, commFile, nodeFile, nodeToNodeFile, onlyDirty);
        }

        static void DumpNetwork(NetworkGlobal networkGlobal, MultiLayoutContext context,
            string commFile, string nodeFile, string nodeToNodeFile, bool onlyDirty = false)
        {

            using (StreamWriter cFile = new StreamWriter(commFile, append: false),
                                    nFile = new StreamWriter(nodeFile, append: false),
                                    n2nFile = new StreamWriter(nodeToNodeFile, append: false))
            {
                cFile.WriteLine("commId;selected;render_mass;render_massCenter;render_size;render_state");
                nFile.WriteLine("nodeId;label;degree;selected;commId;render_size;render_pos;render_color");
                n2nFile.WriteLine("linkId;sourceNodeId;targetNodeId;selected;render_bundlingStrength;render_width;render_colorStart;render_colorEnd;render_alpha");

                foreach (var (commID, commGlobal) in networkGlobal.Communities)
                {
                    var commContext = context.Communities[commID];

                    if (onlyDirty && !commGlobal.Dirty && !commContext.Dirty) continue;


                    var commId = commID;
                    var selected = commGlobal.Selected;
                    var render_mass = commContext.Mass;
                    var render_massCenter = commContext.MassCenter;
                    var render_size = commContext.Size;
                    var render_state = commContext.State;

                    cFile.WriteLine($"{commId};{selected};{render_mass};{render_massCenter};{render_size};{render_state}");
                }

                foreach (var nodeGlobal in networkGlobal.Nodes)
                {
                    var nodeID = nodeGlobal.ID;
                    var nodeContext = context.Nodes[nodeID];

                    if (nodeGlobal.IsVirtualNode) continue;
                    if (onlyDirty && !nodeGlobal.Dirty && !nodeContext.Dirty) continue;

                    var nodeId = nodeID;
                    var label = nodeGlobal.Label;
                    var degree = nodeGlobal.Degree;
                    var selected = nodeGlobal.Selected;
                    var commId = nodeGlobal.CommunityID;
                    var render_size = nodeContext.Size;
                    var render_pos = nodeContext.Position.ToString();
                    var render_color = nodeContext.Color.ToString();

                    nFile.WriteLine($"{nodeId};{label};{degree};{selected};{commId};{render_size};{render_pos};{render_color}");
                }

                foreach (var linkGlobal in networkGlobal.Links)
                {
                    var linkID = linkGlobal.ID;
                    var linkContext = context.Links[linkID];

                    if (linkGlobal.SourceNode.IsVirtualNode || linkGlobal.TargetNode.IsVirtualNode) continue;
                    if (onlyDirty && !linkGlobal.Dirty && !linkContext.Dirty) continue;

                    var linkId = linkID;
                    var sourceNodeId = linkGlobal.SourceNodeID;
                    var targetNodeId = linkGlobal.TargetNodeID;
                    var selected = linkGlobal.Selected;
                    var render_bundlingStrength = linkContext.BundlingStrength;
                    var render_width = linkContext.Width;
                    var render_colorStart = linkContext.ColorStart;
                    var render_colorEnd = linkContext.ColorEnd;
                    var render_alpha = linkContext.Alpha;

                    n2nFile.WriteLine($"{linkId};{sourceNodeId};{targetNodeId};{selected};{render_bundlingStrength};{render_width};{render_colorStart};{render_colorEnd};{render_alpha}");
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

        public static void ExecuteCommand(string command, IDriver driver, bool convertWinPaths = true)
        {
            try
            {
                var session = driver.Session();
                session.Run(command);
                Debug.Log($"Command executed successfully: {command}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Execution Error: {e.Message}");
            }
        }
    }
}