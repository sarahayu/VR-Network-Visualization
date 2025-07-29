using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Tooltip : MonoBehaviour
{
    Canvas _canvas;

    TextMeshProUGUI _infoCol1;
    TextMeshProUGUI _infoCol2;

    void OnEnable()
    {
        _canvas = GetComponent<Canvas>();
    }

    void Start()
    {
        Unshow();
    }

    void Update()
    {

    }

    public void Show()
    {
        _canvas.enabled = true;
    }

    public void Unshow()
    {
        _canvas.enabled = false;
    }
}
