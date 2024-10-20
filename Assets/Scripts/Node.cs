using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class Node
    {
        public int communityIdx = Int32.MaxValue;
        public string label;
        public int id;
        public string color = null;
        public Color colorParsed = Color.black;
        public bool virtualNode;
        public double degree = 0;
        public int height;
        public int ancIdx;
        public int[] childIdx;

        public Vector3 precompPos3D;
        public IList<int> ancIdxOrderList = new List<int>();
        public bool isSpider = false;

        public Vector3 currentMove;

        // For position bias caused by interaction
        public Vector3 positionChange = Vector3.zero;

        public Vector3 LastPosition3D;

        // Target position after transformation
        public Vector3 targetPosition3D;
        public Vector4 targetPosition4D;
        // unity movement when doing transformation
        public Vector4 TransformStep;

        public Vector3 transformStep3D;

        // the position of the node
        private Vector4 _position4D;

        public Vector4 Position4D
        {
            get => _position4D;
            set
            {
                _position4D = value;
                // transform from homogeneous coordinates
                Position3D.x = _position4D.x / _position4D.w;
                Position3D.y = _position4D.y / _position4D.w;
                Position3D.z = _position4D.z / _position4D.w;

            }
        }

        public Vector3 spiderPos = Vector3.zero;

        public Vector3 Position3D;

        // the sphere radius for the nodex
        public double radius;

        public double euclideanRadius;
        // the position in parent's hemisphere

        public double Theta;

        public double Phi;

        public Node(NodeFileData nodeData)
        {
            communityIdx = nodeData.communityIdx;
            label = nodeData.label;
            // idx gets remapped to node id, because we do not assume it's the same as the order stated in datafile. why? idk the original code didn't either.
            id = nodeData.idx;
            color = nodeData.color;

            if (color != null)
                colorParsed = ColorUtils.StringToColor(color.ToUpper());

            virtualNode = nodeData.virtualNode;
            degree = nodeData.degree;
            height = nodeData.height;
            ancIdx = nodeData.ancIdx;
            childIdx = nodeData.childIdx;
            precompPos3D = nodeData._position3D;
        }

    }
}