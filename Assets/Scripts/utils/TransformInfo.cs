using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransformInfo
{
    public Vector3 position;
    public Vector3 scale;
    public Quaternion rotation;

    public TransformInfo()
    {
        position = Vector3.zero;
        scale = Vector3.one;
        rotation = Quaternion.identity;
    }

    public TransformInfo(Transform trans)
    {
        SetFromTransform(trans);
    }

    public TransformInfo(TransformInfo trans)
    {
        SetFromTransform(trans);
    }

    public TransformInfo Copy()
    {
        return new TransformInfo(this);
    }

    public void SetFromTransform(Transform trans)
    {
        position = trans.position;
        rotation = trans.rotation;
        scale = trans.lossyScale;
    }

    public void SetFromTransform(TransformInfo trans)
    {
        position = trans.position;
        rotation = trans.rotation;
        scale = trans.scale;
    }

    public Vector3 TransformPoint(Vector3 vec)
    {
        return position + (rotation * Vector3.Scale(vec, scale));
    }

    public void AssignToTransform(Transform transform)
    {
        transform.localScale = scale;
        transform.localPosition = position;
        transform.localRotation = rotation;
    }
}
