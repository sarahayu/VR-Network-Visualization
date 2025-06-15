using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class FriendProps
    {
        [Serializable]
        public class Node
        {
            public bool? smoker { get; set; } = null;
            public bool? drinker { get; set; } = null;
            public float? gpa { get; set; } = null;
            public int? grade { get; set; } = null;
        }

        [Serializable]
        public class Link
        {
        }
    }
}