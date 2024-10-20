/*
*
* This should be central component for Network object
*
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class Network : MonoBehaviour
    {
        NetworkFilesLoader _fileLoader;
        NetworkDataStructure _dataStruct;
        NetworkInput _input;
        NetworkRenderer _renderer;

        Dictionary<string, NetworkLayout> _layouts = new Dictionary<string, NetworkLayout>();

        string _curLayout;

        public string CurLayout { get { return _curLayout; } }

        void Awake()
        {
        }

        void Start()
        {
            Initialize();
        }

        void Update()
        {
            Draw();
        }

        public void Initialize()
        {
            _fileLoader = GetComponent<NetworkFilesLoader>();
            _dataStruct = GetComponent<NetworkDataStructure>();
            _input = GetComponent<NetworkInput>();
            _renderer = GetComponentInChildren<NetworkRenderer>();

            _fileLoader.LoadFiles();
            _dataStruct.InitNetwork();
            _input.Initialize();
            _renderer.Initialize();

            InitializeLayouts();
            ChangeToLayout("spherical");
        }

        public void Draw()
        {
            _renderer.Draw();
        }

        void InitializeLayouts()
        {
            _layouts["hairball"] = GetComponentInChildren<HairballLayout>();
            _layouts["hairball"].Initialize();
            _layouts["spherical"] = GetComponentInChildren<SphericalLayout>();
            _layouts["spherical"].Initialize();
        }

        public void ChangeToLayout(string layout, bool animated = true)
        {
            _curLayout = layout;

            if (animated)
            {
                ChangeToLayoutAnimated(layout);
            }
            else
            {
                ChangeToLayoutUnanimated(layout);
            }
        }

        void ChangeToLayoutAnimated(string layout)
        {
            StartCoroutine(CRAnimateLayout(layout));
        }

        void ChangeToLayoutUnanimated(string layout)
        {
            _layouts[layout].ApplyLayout();
            _renderer.UpdateRenderElements();
        }

        IEnumerator CRAnimateLayout(string layout)
        {
            float dur = 1.0f;
            var interpolator = _layouts[layout].GetInterpolator();

            yield return AnimationUtils.Lerp(dur, t =>
            {
                interpolator.Interpolate(t);
                _renderer.UpdateRenderElements();
            });
        }
    }
}