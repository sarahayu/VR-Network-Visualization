/*
This class is to allow nodes to be hoverable even if community is being hovered
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
    protected override float CalculateNormalizedScore(IXRInteractor interactor, IXRInteractable target)
    {
        return (float)target.transform.gameObject.layer / LayerMask.NameToLayer("MaxLayer");
    }
}
