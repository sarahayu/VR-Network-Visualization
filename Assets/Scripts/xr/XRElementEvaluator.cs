/*
*
* XRElementEvaluator is a custom element evaluator for XR Controllers.
* This class is to allow nodes to be hoverable even if community is being hovered.
*
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[Serializable]
public class XRElementEvaluator : XRTargetEvaluator
{
    const float MAX_DISTANCE = 5f;

    protected override float CalculateNormalizedScore(IXRInteractor interactor, IXRInteractable target)
    {
        float baseFloat = LayerMask.NameToLayer("MaxLayer");

        return target.transform.gameObject.layer / baseFloat
            + (1f - Mathf.Clamp01(target.GetDistanceSqrToInteractor(interactor) / (MAX_DISTANCE * MAX_DISTANCE))) / (baseFloat * baseFloat);
    }
}
