using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VidiGraph;

namespace VidiGraph
{
    public class ConsoleInput : MonoBehaviour
    {
        [SerializeField] XRGrabInteractable _button1;
        [SerializeField] XRGrabInteractable _button2;
        [SerializeField] XRGrabInteractable _button3;
        [SerializeField] XRGrabInteractable _button4;
        [SerializeField] TextMeshPro _text;

        NetworkManager _networkManager;

        float _pressOffset = 0.005f;
        float _btnEaseTime = 0.5f;
        Coroutine _curRoutine = null;
        Action _undoCB = null;

        public enum ButtonLabel
        {
            NewQuery = 1 << 1,
            ModifyQuery = 1 << 2,
            DuplicateGraph = 1 << 3,
            DeleteGraph = 1 << 4
        }

        void Start()
        {
            _networkManager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();

            BindClick(_button1, OnNewQuery);
            BindClick(_button2, OnModifyQuery);
            BindClick(_button3, OnDuplicateGraph);
            BindClick(_button4, OnDeleteGraph);

            _button1.gameObject.SetActive(false);
            _button2.gameObject.SetActive(false);
            _button3.gameObject.SetActive(false);
            _button4.gameObject.SetActive(false);
        }

        public void SetText(string text)
        {
            _text.text = text;
        }

        public void SetActive(int buttons, bool isActive)
        {
            if ((buttons & (int)ButtonLabel.NewQuery) != 0) _button1.gameObject.SetActive(isActive);
            if ((buttons & (int)ButtonLabel.ModifyQuery) != 0) _button2.gameObject.SetActive(isActive);
            if ((buttons & (int)ButtonLabel.DuplicateGraph) != 0) _button3.gameObject.SetActive(isActive);
            if ((buttons & (int)ButtonLabel.DeleteGraph) != 0) _button4.gameObject.SetActive(isActive);
        }

        void BindClick(XRGrabInteractable intrble, Action callback)
        {
            intrble.selectEntered.AddListener(evt =>
            {
                if (CoroutineUtils.StopIfRunning(this, ref _curRoutine))
                    _undoCB.Invoke();

                // have to translate interactable object's child's transform, since XRInteractionToolkit
                // has its own behavior with the interactable object's transform
                var btnVisual = evt.interactableObject.transform.gameObject.GetNamedChild("Visuals").transform;
                btnVisual.position += new Vector3(0, -_pressOffset, 0);

                _undoCB = () => btnVisual.position += new Vector3(0, _pressOffset, 0);
                _curRoutine = StartCoroutine(Delay(_undoCB, _btnEaseTime));

                callback.Invoke();
            });
        }

        IEnumerator Delay(Action callback, float secs)
        {
            yield return new WaitForSeconds(secs);

            callback.Invoke();

            _curRoutine = null;
        }

        void OnNewQuery()
        {
            _networkManager.SetQueryMode(true);
        }

        void OnModifyQuery()
        {
            _networkManager.ReplaceCurWorkingSubgraph(null);
        }

        void OnDuplicateGraph()
        {
            _networkManager.DuplicateCurWorkingGraph();
        }

        void OnDeleteGraph()
        {
            _networkManager.DeleteCurWorkingGraph();

        }
    }
}