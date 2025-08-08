/*
*
* NetworkInput is the base class for inputs of networks.
*
*/

using UnityEngine;

namespace VidiGraph
{
    public abstract class NetworkInput : MonoBehaviour
    {
        public bool Enabled { get; set; } = true;
    }

}