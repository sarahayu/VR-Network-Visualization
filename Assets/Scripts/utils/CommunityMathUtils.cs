using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VidiGraph
{
    public static class CommunityMathUtils
    {
        // Key should have the community ID that is smaller first so we don't have doubles of links
        public static Tuple<int, int> IDsToLinkKey(int c1, int c2)
        {
            if (c1 > c2)
                return new Tuple<int, int>(c2, c1);
            else
                return new Tuple<int, int>(c1, c2);
        }
    }

}