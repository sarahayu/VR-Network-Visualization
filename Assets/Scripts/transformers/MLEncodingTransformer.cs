using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Assertions;

namespace VidiGraph
{
    public class MLEncodingTransformer : NetworkContextTransformer
    {
        NetworkGlobal _networkGlobal;
        MultiLayoutContext _networkContext;
        MultiLayoutNetworkReflector _utils;

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkGlobal = networkGlobal;
            _networkContext = (MultiLayoutContext)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _utils = new MultiLayoutNetworkReflector(_networkContext, manager);
        }

        public override void ApplyTransformation()
        {
            TimerUtils.StartTime("MLEncodingTransformer.ApplyTransformation");
            foreach (var (nodeID, _) in _networkContext.Nodes)
            {
                var node = _networkGlobal.Nodes[nodeID];
                if (node.IsVirtualNode) continue;
                var nodeContext = _networkContext.Nodes[node.ID];

                nodeContext.Size = _networkContext.GetNodeSize(node);
                nodeContext.Color = _networkContext.GetNodeColor(node);

                nodeContext.Dirty = true;
                _networkContext.Communities[_networkContext.Nodes[nodeID].CommunityID].Dirty = true;
            }

            foreach (var (linkID, _) in _networkContext.Links)
            {
                var link = _networkGlobal.Links[linkID];
                var linkContext = _networkContext.Links[link.ID];

                linkContext.Width = _networkContext.GetLinkWidth(link);
                linkContext.BundlingStrength = _networkContext.GetLinkBundlingStrength(link);
                linkContext.ColorStart = _networkContext.GetLinkColorStart(link);
                linkContext.ColorEnd = _networkContext.GetLinkColorEnd(link);
                linkContext.BundleStart = _networkContext.GetLinkBundleStart(link);
                linkContext.BundleEnd = _networkContext.GetLinkBundleEnd(link);
                linkContext.Alpha = _networkContext.GetLinkAlpha(link);

                linkContext.Dirty = true;
            }
            TimerUtils.EndTime("MLEncodingTransformer.ApplyTransformation");
        }

        public bool SetNodeColorEncoding(string prop, float min, float max, string color)
        {
            if (!_utils.TryCastNodeProp<float?>(prop)) return false;

            _networkContext.GetNodeColor = node =>
            {
                var propVal = _utils.CastNodeProp<float?>(node.ID, prop);

                if (propVal == null) return Color.gray;

                var t = Mathf.InverseLerp(min, max, (float)propVal);
                return Color.Lerp(Color.white, ColorUtils.StringToColor(color), t);
            };

            return true;
        }

        public bool SetNodeColorEncoding(string prop, Dictionary<string, string> valueToColor)
        {
            return TrySetNodeColorEncoding(prop, valueToColor);
        }

        public bool SetNodeColorEncoding(string prop, Dictionary<bool?, string> valueToColor)
        {
            return TrySetNodeColorEncoding(prop, valueToColor);
        }

        public bool SetNodeSizeEncoding(string prop, float min, float max)
        {
            if (!_utils.TryCastNodeProp<float?>(prop)) return false;

            _networkContext.GetNodeSize = node =>
            {
                var propVal = _utils.CastNodeProp<float?>(node.ID, prop);

                if (propVal == null) return 0.01f * _networkContext.ContextSettings.NodeScale;

                var t = Mathf.InverseLerp(min, max, (float)propVal);
                return t * _networkContext.ContextSettings.NodeScale;
            };

            return true;
        }

        public bool SetNodeSizeEncoding(string prop, Dictionary<string, float> valueToSize)
        {
            return TrySetNodeSizeEncoding(prop, valueToSize);
        }

        public bool SetNodeSizeEncoding(string prop, Dictionary<bool?, float> valueToSize)
        {
            return TrySetNodeSizeEncoding(prop, valueToSize);
        }

        public bool SetLinkWidthEncoding(string prop, float min, float max)
        {
            if (!_utils.TryCastLinkProp<float?>(prop)) return false;

            _networkContext.GetLinkWidth = link =>
            {
                var propVal = _utils.CastLinkProp<float?>(link.ID, prop);

                if (propVal == null) return 0.01f * _networkContext.ContextSettings.LinkWidth;

                var t = Mathf.InverseLerp(min, max, (float)propVal);
                return t * _networkContext.ContextSettings.LinkWidth;
            };

            return true;
        }

        public bool SetLinkWidthEncoding(string prop, Dictionary<string, float> valueToWidth)
        {
            return TrySetLinkWidthEncoding(prop, valueToWidth);
        }

        public bool SetLinkWidthEncoding(string prop, Dictionary<bool?, float> valueToWidth)
        {
            return TrySetLinkWidthEncoding(prop, valueToWidth);
        }

        public bool SetLinkBundlingStrengthEncoding(string prop, float min, float max)
        {
            if (!_utils.TryCastLinkProp<float?>(prop)) return false;

            _networkContext.GetLinkBundlingStrength = link =>
            {
                var propVal = _utils.CastLinkProp<float?>(link.ID, prop);

                if (propVal == null) return 0.01f * _networkContext.ContextSettings.EdgeBundlingStrength;

                var t = Mathf.InverseLerp(min, max, (float)propVal);
                return t * _networkContext.ContextSettings.EdgeBundlingStrength;
            };

            return true;
        }

        public bool SetLinkBundlingStrengthEncoding(string prop, Dictionary<string, float> valueToBundlingStrength)
        {
            return TrySetLinkBundlingStrengthEncoding(prop, valueToBundlingStrength);
        }

        public bool SetLinkBundlingStrengthEncoding(string prop, Dictionary<bool?, float> valueToBundlingStrength)
        {
            return TrySetLinkBundlingStrengthEncoding(prop, valueToBundlingStrength);
        }

        public bool SetLinkColorStartEncoding(string prop, float min, float max, string color)
        {
            if (!_utils.TryCastLinkProp<float?>(prop)) return false;

            _networkContext.GetLinkColorStart = link =>
            {
                var propVal = _utils.CastLinkProp<float?>(link.ID, prop);

                if (propVal == null) return Color.gray;

                var t = Mathf.InverseLerp(min, max, (float)propVal);
                return Color.Lerp(Color.white, ColorUtils.StringToColor(color), t);
            };

            return true;
        }

        public bool SetLinkColorStartEncoding(string prop, Dictionary<string, string> valueToColor)
        {
            return TrySetLinkColorStartEncoding(prop, valueToColor);
        }

        public bool SetLinkColorStartEncoding(string prop, Dictionary<bool?, string> valueToColor)
        {
            return TrySetLinkColorStartEncoding(prop, valueToColor);
        }

        public bool SetLinkColorEndEncoding(string prop, float min, float max, string color)
        {
            if (!_utils.TryCastLinkProp<float?>(prop)) return false;

            _networkContext.GetLinkColorEnd = link =>
            {
                var propVal = _utils.CastLinkProp<float?>(link.ID, prop);

                if (propVal == null) return Color.gray;

                var t = Mathf.InverseLerp(min, max, (float)propVal);
                return Color.Lerp(Color.white, ColorUtils.StringToColor(color), t);
            };

            return true;
        }

        public bool SetLinkColorEndEncoding(string prop, Dictionary<string, string> valueToColor)
        {
            return TrySetLinkColorEndEncoding(prop, valueToColor);
        }

        public bool SetLinkColorEndEncoding(string prop, Dictionary<bool?, string> valueToColor)
        {
            return TrySetLinkColorEndEncoding(prop, valueToColor);
        }

        public bool SetLinkBundleStartEncoding(string prop, Dictionary<string, bool> valueToDoBundle)
        {
            return TrySetLinkBundleStartEncoding(prop, valueToDoBundle);
        }

        public bool SetLinkBundleStartEncoding(string prop, Dictionary<bool?, bool> valueToDoBundle)
        {
            return TrySetLinkBundleStartEncoding(prop, valueToDoBundle);
        }

        public bool SetLinkBundleEndEncoding(string prop, Dictionary<string, bool> valueToDoBundle)
        {
            return TrySetLinkBundleEndEncoding(prop, valueToDoBundle);
        }

        public bool SetLinkBundleEndEncoding(string prop, Dictionary<bool?, bool> valueToDoBundle)
        {
            return TrySetLinkBundleEndEncoding(prop, valueToDoBundle);
        }

        public bool SetLinkAlphaEncoding(string prop, float min, float max)
        {
            if (!_utils.TryCastLinkProp<float?>(prop)) return false;

            _networkContext.GetLinkAlpha = link =>
            {
                var propVal = _utils.CastLinkProp<float?>(link.ID, prop);

                if (propVal == null) return 0.01f * _networkContext.ContextSettings.LinkNormalAlphaFactor;

                var t = Mathf.InverseLerp(min, max, (float)propVal);
                return t * _networkContext.ContextSettings.LinkNormalAlphaFactor;
            };

            return true;
        }

        public bool SetLinkAlphaEncoding(string prop, Dictionary<string, float> valueToAlpha)
        {
            return TrySetLinkAlphaEncoding(prop, valueToAlpha);
        }

        public bool SetLinkAlphaEncoding(string prop, Dictionary<bool?, float> valueToAlpha)
        {
            return TrySetLinkAlphaEncoding(prop, valueToAlpha);
        }

        public override TransformInterpolator GetInterpolator()
        {
            return new MLEncodingInterpolator();
        }

        /*=============== start private methods ===================*/

        bool TrySetNodeColorEncoding<T>(string prop, Dictionary<T, string> valueToColor)
        {
            if (!_utils.TryCastNodeProp<T>(prop)) return false;

            var valueToColorObj = valueToColor.ToDictionary(vc => vc.Key, vc => ColorUtils.StringToColor(vc.Value));

            _networkContext.GetNodeColor = node => _utils.GetNodeProp(node.ID, prop,
                valueToColorObj, Color.gray);

            return true;
        }

        bool TrySetNodeSizeEncoding<T>(string prop, Dictionary<T, float> valueToSize)
        {
            if (!_utils.TryCastNodeProp<bool?>(prop)) return false;

            _networkContext.GetNodeSize = node => _utils.GetNodeProp(node.ID, prop,
                valueToSize, 0.01f * _networkContext.ContextSettings.NodeScale);

            return true;
        }

        bool TrySetLinkWidthEncoding<T>(string prop, Dictionary<T, float> valueToSize)
        {
            if (!_utils.TryCastLinkProp<bool?>(prop)) return false;

            _networkContext.GetLinkWidth = link => _utils.GetLinkProp(link.ID, prop,
                valueToSize, 0.01f * _networkContext.ContextSettings.LinkWidth);

            return true;
        }

        bool TrySetLinkBundlingStrengthEncoding<T>(string prop, Dictionary<T, float> valueToSize)
        {
            if (!_utils.TryCastLinkProp<bool?>(prop)) return false;

            _networkContext.GetLinkBundlingStrength = link => _utils.GetLinkProp(link.ID, prop,
                valueToSize, 0.01f * _networkContext.ContextSettings.EdgeBundlingStrength);

            return true;
        }

        bool TrySetLinkColorStartEncoding<T>(string prop, Dictionary<T, string> valueToColor)
        {
            if (!_utils.TryCastLinkProp<T>(prop)) return false;

            var valueToColorObj = valueToColor.ToDictionary(vc => vc.Key, vc => ColorUtils.StringToColor(vc.Value));

            _networkContext.GetLinkColorStart = link => _utils.GetLinkProp(link.ID, prop,
                valueToColorObj, Color.gray);

            return true;
        }

        bool TrySetLinkColorEndEncoding<T>(string prop, Dictionary<T, string> valueToColor)
        {
            if (!_utils.TryCastLinkProp<T>(prop)) return false;

            var valueToColorObj = valueToColor.ToDictionary(vc => vc.Key, vc => ColorUtils.StringToColor(vc.Value));

            _networkContext.GetLinkColorEnd = link => _utils.GetLinkProp(link.ID, prop,
                valueToColorObj, Color.gray);

            return true;
        }

        bool TrySetLinkBundleStartEncoding<T>(string prop, Dictionary<T, bool> valueToDoBundle)
        {
            if (!_utils.TryCastLinkProp<T>(prop)) return false;

            _networkContext.GetLinkBundleStart = link => _utils.GetLinkProp(link.ID, prop,
                valueToDoBundle, false);

            return true;
        }

        bool TrySetLinkBundleEndEncoding<T>(string prop, Dictionary<T, bool> valueToDoBundle)
        {
            if (!_utils.TryCastLinkProp<T>(prop)) return false;

            _networkContext.GetLinkBundleEnd = link => _utils.GetLinkProp(link.ID, prop,
                valueToDoBundle, false);

            return true;
        }

        bool TrySetLinkAlphaEncoding<T>(string prop, Dictionary<T, float> valueToAlpha)
        {
            if (!_utils.TryCastLinkProp<bool?>(prop)) return false;

            _networkContext.GetLinkAlpha = link => _utils.GetLinkProp(link.ID, prop,
                valueToAlpha, 0.01f * _networkContext.ContextSettings.LinkNormalAlphaFactor);

            return true;
        }
    }

    public class MLEncodingInterpolator : TransformInterpolator
    {
        public override void Interpolate(float t)
        {
            // leave empty, encodings shouldn't be interpolatable... for now
        }
    }

}