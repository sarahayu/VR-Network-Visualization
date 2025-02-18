using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransformInfo
{
    public Vector3 Position, Scale;

    public TransformInfo(Transform transform)
    {
        Position = transform.position;
        Scale = transform.localScale;
    }
}
