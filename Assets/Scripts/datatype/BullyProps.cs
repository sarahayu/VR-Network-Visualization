using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class BullyProps
    {
        [Serializable]
        public class Node
        {
            public string type { get; set; } = "none";
            public int grade { get; set; } = 0;
            public float bully_victim_ratio { get; set; } = 0f;

            // public object[] being_bullied = null;
            // public object[] bullying = null;
        }

        [Serializable]
        public class Link
        {
            public string type { get; set; } = "none";
        }
    }
}