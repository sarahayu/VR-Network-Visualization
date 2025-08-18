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

        public static GameObject MakeCommunity(GameObject prefab, Transform transform,
            MultiLayoutContext.Community commProps)
        {
            GameObject commObj = UnityEngine.Object.Instantiate(prefab, transform);

            // community won't be selected anyways so just set select color to transparent
            return UpdateCommunity(commObj, commProps, false, Color.clear);
        }

        public static GameObject UpdateCommunity(GameObject commObj, MultiLayoutContext.Community commProps,
            bool selected, Color selectColor, Renderer renderer = null)
        {
            commObj.transform.localPosition = commProps.MassCenter;
            commObj.transform.localScale = Vector3.one;

            if (!renderer)
                renderer = commObj.GetComponentInChildren<Renderer>();

            MaterialPropertyBlock props = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(props);
            props.SetColor("_Color", selected ? selectColor : new Color(0f, 0f, 0f, 0f));
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

        public static GameObject MakeNetwork(GameObject prefab, Transform transform,
            MultiLayoutContext network)
        {
            GameObject commObj = UnityEngine.Object.Instantiate(prefab, transform);

            // shell won't be selected anyways so just set select color to transparent
            return UpdateNetwork(commObj, network, false, Color.clear);
        }

        public static GameObject UpdateNetwork(GameObject commObj, MultiLayoutContext network,
            bool selected, Color selectColor, Renderer renderer = null)
        {
            commObj.transform.localPosition = network.MassCenter;
            commObj.transform.localScale = Vector3.one;

            if (!renderer)
                renderer = commObj.GetComponentInChildren<Renderer>();

            MaterialPropertyBlock props = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(props);
            props.SetColor("_Color", selected ? selectColor : new Color(0f, 0f, 0f, 0f));
            renderer.SetPropertyBlock(props);

            // have to modify mesh to account for rotation due to grabbing events
            var invRot = Quaternion.Inverse(commObj.transform.rotation);

            var newMesh = new Mesh();
            newMesh.vertices = network.Mesh.vertices.Select(v => invRot * v).ToArray();
            newMesh.triangles = network.Mesh.triangles;

            commObj.GetComponent<MeshFilter>().sharedMesh = newMesh;
            commObj.GetComponent<MeshCollider>().sharedMesh = newMesh;

            return commObj;
        }

        public static GameObject SetNetworkColor(GameObject networkObj, Color color, Renderer renderer = null)
        {
            if (!renderer)
                renderer = networkObj.GetComponentInChildren<Renderer>();

            MaterialPropertyBlock props = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(props);
            props.SetColor("_Color", color);
            renderer.SetPropertyBlock(props);

            return networkObj;
        }
    }

}