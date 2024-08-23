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
        public int idx;
        public string color = null;
        public bool virtualNode;
        public double degree = 0;
        public int height;
        public int ancIdx;
        public int[] childIdx;

        // precomputed 2D layout position
        public float[] pos2D;
        public float[] pos3D;
        public List<int> onRunChildIdx;
        public IList<int> ancIdxOrderList = new List<int>();
        public bool isSpider = false;

        public Vector3 currentMove;

        // For position bias caused by interaction
        public Vector3 positionChange = Vector3.zero;

        private Vector3 _lastPosition3D;
        public Vector3 LastPosition3D
        {
            get => _lastPosition3D;
            set => _lastPosition3D = value;
        }

        // Target position after transformation
        public Vector3 targetPosition3D;
        public Vector4 targetPosition4D;
        // unity movement when doing transformation
        private Vector3 _transformStep;
        public Vector4 TransformStep
        {
            get => _transformStep;
            set => _transformStep = value;
        }

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
                _position3D.x = _position4D.x / _position4D.w;
                _position3D.y = _position4D.y / _position4D.w;
                _position3D.z = _position4D.z / _position4D.w;

            }
        }

        public Vector3 _position3D;

        public Vector3 spiderPos = Vector3.zero;

        public Vector3 Position3D
        {
            get => _position3D;
            set => _position3D = value;
        }

        // the sphere radius for the nodex
        public double radius;

        public double euclideanRadius;
        // the position in parent's hemisphere
        private double _theta;

        public double Theta
        {
            get => _theta;
            set => _theta = value;
        }

        private double _phi;

        public double Phi
        {
            get => _phi;
            set => _phi = value;
        }

    }
}