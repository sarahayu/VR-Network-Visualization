using System;
using System.Collections.Generic;
using System.Linq;

namespace VidiGraph
{
    /// <summary>
    /// reference: https://github.com/mburst/dijkstras-algorithm/blob/master/dijkstras.cs
    /// </summary>
    public class Dijkstras
    {
        public Dictionary<int, int> N2CIdxMap = new Dictionary<int, int>();
        public Dictionary<int, int> C2NIdxMap = new Dictionary<int, int>();
        public int[,] matrix;

        public void BuildInvertedIdx(NetworkDataStructure _network, int _communityIdx)
        {
            var idx = 0;
            foreach (var node in _network.Communities[_communityIdx].communityNodes)
            {
                N2CIdxMap.Add(node.id, idx);
                C2NIdxMap.Add(idx, node.id);
                idx++;
            }
        }

        private static int[,] FindShortestPathFromMatrix(int[,] matrix, int nodesNumber)
        {
            for (int i = 0; i < nodesNumber; i++)
            {
                matrix[i, i] = 0;
                for (int j = i + 1; j < nodesNumber; j++)
                {
                    if (matrix[i, j] == 0)
                    {
                        matrix[i, j] = int.MaxValue;
                        matrix[j, i] = int.MaxValue;
                    }
                }
            }

            for (int k = 0; k < nodesNumber; k++)
            {
                for (int i = 0; i < nodesNumber; i++)
                {
                    if (matrix[i, k] == int.MaxValue)
                    {
                        continue;
                    }

                    for (int j = 0; j < nodesNumber; j++)
                    {
                        if (matrix[k, j] == int.MaxValue)
                        {
                            continue;
                        }

                        if (matrix[i, k] + matrix[k, j] < matrix[i, j])
                        {
                            matrix[i, j] = matrix[i, k] + matrix[k, j];
                        }
                    }
                }
            }

            return matrix;
        }

        public int[,] ShortestPathMatrix(Community community)
        {
            var nodesNumber = community.communityNodes.Count;
            var matrix = new int[nodesNumber, nodesNumber];

            foreach (var link in community.innerLinks)
            {
                matrix[N2CIdxMap[link.sourceIdx], N2CIdxMap[link.targetIdx]] = 1;
                matrix[N2CIdxMap[link.targetIdx], N2CIdxMap[link.sourceIdx]] = 1;
            }

            return FindShortestPathFromMatrix(matrix, nodesNumber);
        }
        public static int[,] ShortestPathMatrix(NetworkDataStructure network)
        {
            var nodesNumber = network.Nodes.Count;
            var matrix = new int[nodesNumber, nodesNumber];

            foreach (var link in network.Links)
            {
                matrix[link.sourceIdx, link.targetIdx] = 1;
                matrix[link.targetIdx, link.sourceIdx] = 1;
            }

            return FindShortestPathFromMatrix(matrix, nodesNumber);
        }

        public static int[,] ShortestPathAggregateMatrix(NetworkDataStructure network)
        {
            var nodesNumber = network.Communities.Count;
            var matrix = new int[nodesNumber, nodesNumber];

            foreach (var entry1 in network.Communities)
            {
                var communiity = entry1.Value;
                foreach (var entry in communiity.aggregateLinks)
                {
                    matrix[communiity.communityIdx, entry.Key] += entry.Value;
                    matrix[entry.Key, communiity.communityIdx] += entry.Value;
                }
            }

            return FindShortestPathFromMatrix(matrix, nodesNumber);
        }
    }
}