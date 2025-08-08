/*
*
* Node is NetworkGlobal's representation of a node.
*
*/

using System.Collections.Generic;

namespace VidiGraph
{
    public class Node
    {
        public int ID { get; set; }
        public string Label { get; set; }
        public bool IsVirtualNode { get; set; }
        public int IdxProcessed { get; set; }

        public int CommunityID { get; set; } = -1;
        public double Degree { get; set; } = 0;
        public int Height { get; set; }
        public int[] ChildIDs { get; set; }

        public int AncID { get; set; }
        public IList<int> AncIDsOrderList { get; set; } = new List<int>();

        public bool Dirty { get; set; } = false;
    }

}