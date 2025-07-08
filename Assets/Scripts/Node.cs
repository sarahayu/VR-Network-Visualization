using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class Node
    {
        public int ID { get; set; }
        public string Label { get; set; }
        public bool IsVirtualNode { get; set; }
        public int IdxProcessed { get; set; }

        public int CommunityID { get; set; } = -1;
        public string Color { get; set; }
        public Color ColorParsed { get; set; }
        public double Degree { get; set; } = 0;
        public int Height { get; set; }
        public int[] ChildIDs { get; set; }

        public int AncID { get; set; }
        public IList<int> AncIDsOrderList { get; set; } = new List<int>();

        public bool Dirty { get; set; } = false;
        public bool Selected { get { return SelectedOnSubnetworks.Count != 0; } }
        public IList<int> SelectedOnSubnetworks { get; set; } = new List<int>();
    }

}