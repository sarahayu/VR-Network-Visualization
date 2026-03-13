/*
*
* NodeCollection custom collection to access nodes by their idx (so I never EVER shoot myself in the foot by using list indices).
* Ideally we'll remove this class and use a normal dictionary.
*
*/


using System;
using System.Collections;
using System.Collections.Generic;

namespace VidiGraph
{
    public class NodeCollection : IEnumerable
    {
        // leave this public in case you want access to underlying array (e.g. for array traversion)
        public List<Node> NodeArray { get; } = new List<Node>();

        // A inverted index allow reference the node by its own idx
        public IDictionary<int, int> IdToIndex { get; } = new Dictionary<int, int>();

        public List<Node> GetNodeArray()
        {
            return NodeArray;
        }

        public void Clear()
        {
            NodeArray.Clear();
            IdToIndex.Clear();
        }

        public void Add(Node node)
        {
            IdToIndex.Add(node.ID, NodeArray.Count);

            NodeArray.Add(node);
        }

        public IEnumerator<Node> GetEnumerator()
        {
            return new NodeEnumerator { nodeList = NodeArray };
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Node this[int id]
        {
            get { return NodeArray[IdToIndex[id]]; }
            set { }
        }
        public int Count { get { return NodeArray.Count; } }

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