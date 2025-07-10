using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GK;
using UnityEngine;

namespace VidiGraph
{

    public static class CommunityRenderUtils
    {

        public static GameObject MakeCommunity(GameObject prefab, Transform transform, Community commGlobal,
            MultiLayoutContext.Community commProps, int subnetworkID)
        {
            GameObject commObj = UnityEngine.Object.Instantiate(prefab, transform);

            // community won't be selected anyways so just set select color to transparent
            return UpdateCommunity(commObj, commGlobal, commProps, Color.clear, subnetworkID);
        }

        public static GameObject UpdateCommunity(GameObject commObj, Community commGlobal,
            MultiLayoutContext.Community commProps, Color selectColor, int subnetworkID, Renderer renderer = null)
        {
            commObj.transform.localPosition = commProps.MassCenter;
            commObj.transform.localScale = Vector3.one;

            if (!renderer)
                renderer = commObj.GetComponentInChildren<Renderer>();

            MaterialPropertyBlock props = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(props);
            props.SetColor("_Color", commGlobal.SelectedOnSubnetworks.Contains(subnetworkID) ? selectColor : new Color(0f, 0f, 0f, 0f));
            renderer.SetPropertyBlock(props);


            // have to modify mesh to account for rotation due to grabbing events
            var invRot = Quaternion.Inverse(commObj.transform.rotation);

            var newMesh = new Mesh();
            newMesh.vertices = commProps.Mesh.vertices.Select(v => invRot * v).ToArray();
            newMesh.triangles = commProps.Mesh.triangles;

            commObj.GetComponent<MeshFilter>().sharedMesh = newMesh;
            commObj.GetComponent<MeshCollider>().sharedMesh = newMesh;

            return commObj;
        }

        public static GameObject SetCommunityColor(GameObject commObj, Color color, Renderer renderer = null)
        {
            if (!renderer)
                renderer = commObj.GetComponentInChildren<Renderer>();

            MaterialPropertyBlock props = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(props);
            props.SetColor("_Color", color);
            renderer.SetPropertyBlock(props);

            return commObj;
        }
    }

}