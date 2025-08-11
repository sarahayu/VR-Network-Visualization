using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neo4j.Driver;
using UnityEditor;
using UnityEngine;

namespace VidiGraph
{
    public static class NeoDBConverterUtils
    {
        public static string StoreNode(ISession session, Node node)
        {
            return session.ExecuteWrite(
                tx =>
                {
                    var result = tx.Run(
                        "MERGE (n:Node { nodeId: $ID }) " +
                        "SET n.label = $Label " +
                        "SET n.degree = $Degree " +
                        "SET n.selected = $Selected " +
                        "MERGE (c:Community { commId: $CommunityID })" +
                        "MERGE (n)-[:PART_OF]->(c)",
                        new
                        {
                            ID = node.ID,
                            Label = node.Label,
                            Degree = node.Degree,
                            // Selected = node.Selected,
                            CommunityID = node.CommunityID,
                        });

                    return "success";
                });
        }

        public static string StoreNode(ISession session, MultiLayoutContext.Node node, int ID)
        {
            return session.ExecuteWrite(
                tx =>
                {
                    var result = tx.Run(
                        "MATCH (n:Node { nodeId: $ID }) " +
                        "SET n.size = $Size " +
                        "SET n.pos = $Position " +
                        "SET n.color = $Color ",
                        new
                        {
                            ID,
                            Size = node.Size,
                            Position = node.Position.ToString(),
                            Color = node.Color.ToString()
                        });

                    return "success";
                });
        }

        public static string StoreLink(ISession session, Link link)
        {
            return session.ExecuteWrite(
                tx =>
                {
                    var result = tx.Run(
                        "MATCH (from:Node { nodeId: $FromID }) " +
                        "MATCH (to:Node { nodeId: $ToID }) " +
                        "MERGE (from)-[p:POINTS_TO]->(to) " +
                        "SET p.linkId = $ID " +
                        "SET p.selected = $Selected ",
                        new
                        {
                            ID = link.ID,
                            FromID = link.SourceNodeID,
                            ToID = link.TargetNodeID,
                            Selected = link.Selected,
                        });

                    return "success";
                });
        }

        public static string StoreLink(ISession session, MultiLayoutContext.Link link, int linkID)
        {
            return session.ExecuteWrite(
                tx =>
                {
                    var result = tx.Run(
                        "MATCH (:Node)-[p:POINTS_TO { linkId: $ID } ]->(:Node) " +
                        "SET p.bundlingStrength = $BundlingStrength " +
                        "SET p.width = $Width " +
                        "SET p.colorStart = $ColorStart " +
                        "SET p.colorEnd = $ColorEnd " +
                        "SET p.alpha = $Alpha ",
                        new
                        {
                            ID = linkID,
                            BundlingStrength = link.BundlingStrength,
                            Width = link.Width,
                            ColorStart = link.ColorStart.ToString(),
                            ColorEnd = link.ColorEnd.ToString(),
                            Alpha = link.Alpha,
                        });

                    return "success";
                });
        }

        public static string StoreCommunity(ISession session, Community community)
        {
            return session.ExecuteWrite(
                tx =>
                {
                    var result = tx.Run(
                        "MERGE (n:Community { commId: $ID }) " +
                        "SET n.selected = $Selected ",
                        new
                        {
                            ID = community.ID,
                            // Selected = community.Selected
                        });

                    return "success";
                });
        }

        public static string StoreCommunity(ISession session, MultiLayoutContext.Community community, int ID)
        {
            return session.ExecuteWrite(
                tx =>
                {
                    var result = tx.Run(
                        "MATCH (n:Community { commId: $ID }) " +
                        "SET n.mass = $Mass " +
                        "SET n.massCenter = $MassCenter " +
                        "SET n.size = $Size " +
                        "SET n.state = $State ",
                        new
                        {
                            ID,
                            Mass = community.Mass,
                            MassCenter = community.MassCenter.ToString(),
                            Size = community.Size,
                            State = (int)community.State
                        });

                    return "success";
                });
        }
    }

}