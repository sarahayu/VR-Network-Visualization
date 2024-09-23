/*
*
* Custom Collection to access nodes by their idx (so I never EVER shoot myself in the foot by using list indices)
*
*/


using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class NodeCollection : IEnumerable
    {
        public List<Node> nodeList { get; } = new List<Node>();

        // A inverted index allow reference the node by its own idx
        public IDictionary<int, int> nodeInvertedIndex = new Dictionary<int, int>();

        public void Clear()
        {
            nodeList.Clear();
            nodeInvertedIndex.Clear();
        }

        public void Add(Node node)
        {
            nodeInvertedIndex.Add(node.idx, nodeList.Count);

            nodeList.Add(node);
        }

        public IEnumerator<Node> GetEnumerator()
        {
            return new NodeEnumerator { nodeList = nodeList };
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Node this[int index]
        {
            get { return nodeList[nodeInvertedIndex[index]]; }
            set { }
        }
        public int Count { get { return nodeList.Count; } }

        private class NodeEnumerator : IEnumerator<Node>
        {
            public List<Node> nodeList;
            int position = -1;

            public Node Current
            {
                get { return nodeList[position]; }
            }

            object IEnumerator.Current
            {
                get { return Current; }
            }

            void IDisposable.Dispose() { }

            public bool MoveNext()
            {
                position++;
                return position < nodeList.Count;
            }

            public void Reset()
            {
                position = -1;
            }
        }
    }
}