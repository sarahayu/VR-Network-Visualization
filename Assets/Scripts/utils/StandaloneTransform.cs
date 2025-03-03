using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StandaloneTransform
{
    public Vector3 position;
    public Vector3 scale;
    public Quaternion rotation;

    public StandaloneTransform(Transform trans)
    {
        position = trans.position;
        rotation = trans.rotation;
        scale = trans.localScale;
    }
}
