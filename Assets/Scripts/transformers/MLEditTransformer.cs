using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Assertions;

namespace VidiGraph
{
    public class MLEditTransformer : NetworkContextTransformer
    {
        NetworkGlobal _networkGlobal;
        MultiLayoutContext _networkContext;

        Dictionary<int, List<UpdatedAttr>> _nodeUpdates = new Dictionary<int, List<UpdatedAttr>>();
        Dictionary<int, List<UpdatedAttr>> _linkUpdates = new Dictionary<int, List<UpdatedAttr>>();

        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
            _networkContext = (MultiLayoutContext)networkContext;

            var manager = GameObject.Find("/Network Manager").GetComponent<NetworkManager>();
            _networkGlobal = manager.NetworkGlobal;
        }

        public override void ApplyTransformation()
        {
            foreach (var (nodeID, updates) in _nodeUpdates)
            {
                foreach (var update in updates)
                {
                    switch (update.attr)
                    {
                        case Attr.Size:
                            _networkContext.Nodes[nodeID].Size = update.valFloat;
                            break;
                        case Attr.Position:
                            _networkContext.Nodes[nodeID].Position = update.valVec;
                            break;
                        case Attr.Color:
                            _networkContext.Nodes[nodeID].Color = update.valColor;
                            break;
                    }
                }

                _networkContext.Nodes[nodeID].Dirty = true;
            }

            foreach (var (linkID, updates) in _linkUpdates)
            {
                foreach (var update in updates)
                {
                    switch (update.attr)
                    {
                        case Attr.Width:
                            _networkContext.Links[linkID].Width = update.valFloat;
                            break;
                        case Attr.BundlingStrength:
                            _networkContext.Links[linkID].BundlingStrength = update.valFloat;
                            break;
                        case Attr.ColorStart:
                            _networkContext.Links[linkID].ColorStart = update.valColor;
                            break;
                        case Attr.ColorEnd:
                            _networkContext.Links[linkID].ColorEnd = update.valColor;
                            break;
                        case Attr.BundleStart:
                            _networkContext.Links[linkID].BundleStart = update.valBool;
                            break;
                        case Attr.BundleEnd:
                            _networkContext.Links[linkID].BundleEnd = update.valBool;
                            break;
                        case Attr.Alpha:
                            _networkContext.Links[linkID].Alpha = update.valFloat;
                            break;
                    }
                }

                _networkContext.Links[linkID].Dirty = true;
            }

            _nodeUpdates.Clear();
            _linkUpdates.Clear();
        }

        public void SetNodesSize(IEnumerable<int> nodeIDs, float size)
        {
            var newAttr = new UpdatedAttr
            {
                attr = Attr.Size,
                valFloat = size * _networkContext.ContextSettings.NodeScale,
            };

            foreach (var nodeUpdate in nodeIDs.Select(nid => GetUpdates(_nodeUpdates, nid))) nodeUpdate.Add(newAttr);
        }

        public void SetNodesColor(IEnumerable<int> nodeIDs, Color color)
        {
            var newAttr = new UpdatedAttr
            {
                attr = Attr.Color,
                valColor = color,
            };

            foreach (var nodeUpdate in nodeIDs.Select(nid => GetUpdates(_nodeUpdates, nid))) nodeUpdate.Add(newAttr);
        }

        public void SetLinksWidth(IEnumerable<int> linkIDs, float width)
        {
            var newAttr = new UpdatedAttr
            {
                attr = Attr.Width,
                valFloat = width * _networkContext.ContextSettings.LinkWidth
            };

            foreach (var linkUpdate in linkIDs.Select(nid => GetUpdates(_linkUpdates, nid))) linkUpdate.Add(newAttr);
        }

        public void SetLinksColorStart(IEnumerable<int> linkIDs, Color color)
        {
            var newAttr = new UpdatedAttr
            {
                attr = Attr.ColorStart,
                valColor = color,
            };

            foreach (var linkUpdate in linkIDs.Select(nid => GetUpdates(_linkUpdates, nid))) linkUpdate.Add(newAttr);
        }

        public void SetLinksColorEnd(IEnumerable<int> linkIDs, Color color)
        {
            var newAttr = new UpdatedAttr
            {
                attr = Attr.ColorEnd,
                valColor = color,
            };

            foreach (var linkUpdate in linkIDs.Select(nid => GetUpdates(_linkUpdates, nid))) linkUpdate.Add(newAttr);
        }

        public void SetLinksAlpha(IEnumerable<int> linkIDs, float alpha)
        {
            var newAttr = new UpdatedAttr
            {
                attr = Attr.Alpha,
                valFloat = alpha * _networkContext.ContextSettings.LinkNormalAlphaFactor
            };

            foreach (var linkUpdate in linkIDs.Select(nid => GetUpdates(_linkUpdates, nid))) linkUpdate.Add(newAttr);
        }

        public void SetLinksBundlingStrength(IEnumerable<int> linkIDs, float bundlingStrength)
        {
            var newAttr = new UpdatedAttr
            {
                attr = Attr.BundlingStrength,
                valFloat = bundlingStrength * _networkContext.ContextSettings.EdgeBundlingStrength
            };

            foreach (var linkUpdate in linkIDs.Select(nid => GetUpdates(_linkUpdates, nid))) linkUpdate.Add(newAttr);
        }

        public void SetLinksBundleStart(IEnumerable<int> linkIDs, bool bundleStart)
        {
            var newAttr = new UpdatedAttr
            {
                attr = Attr.BundleStart,
                valBool = bundleStart,
            };

            foreach (var linkUpdate in linkIDs.Select(nid => GetUpdates(_linkUpdates, nid))) linkUpdate.Add(newAttr);
        }

        public void SetLinksBundleEnd(IEnumerable<int> linkIDs, bool bundleEnd)
        {
            var newAttr = new UpdatedAttr
            {
                attr = Attr.BundleEnd,
                valBool = bundleEnd,
            };

            foreach (var linkUpdate in linkIDs.Select(nid => GetUpdates(_linkUpdates, nid))) linkUpdate.Add(newAttr);
        }

        public void SetNodesPosition(IEnumerable<int> nodeIDs, Vector3 position)
        {
            var newAttr = new UpdatedAttr
            {
                attr = Attr.Position,
                valVec = position
            };

            foreach (var nodeUpdate in nodeIDs.Select(nid => GetUpdates(_nodeUpdates, nid))) nodeUpdate.Add(newAttr);
        }

        public void SetNodesPosition(IEnumerable<int> nodeIDs, IEnumerable<Vector3> positions)
        {

            foreach (var (nodeUpdate, position) in nodeIDs.Select(nid => GetUpdates(_nodeUpdates, nid)).Zip(positions, Tuple.Create))
            {
                nodeUpdate.Add(new UpdatedAttr
                {
                    attr = Attr.Position,
                    valVec = position
                });
            }
        }


        public override TransformInterpolator GetInterpolator()
        {
            return new MLEditInterpolator();
        }

        static List<UpdatedAttr> GetUpdates(Dictionary<int, List<UpdatedAttr>> dict, int idx)
        {
            List<UpdatedAttr> updates;
            if (!dict.TryGetValue(idx, out updates)) dict[idx] = updates = new List<UpdatedAttr>();

            return updates;
        }
    }

    public class MLEditInterpolator : TransformInterpolator
    {
        public override void Interpolate(float t)
        {
            // leave empty, edits shouldn't be interpolatable... for now
        }
    }

    enum Attr
    {
        Size,
        Position,
        Color,
        Width,
        BundlingStrength,
        ColorStart,
        ColorEnd,
        BundleStart,
        BundleEnd,
        Alpha
    }

    class UpdatedAttr
    {

        public Attr attr;

        public Vector4 valVec;

        public bool valBool
        {
            get { return float.IsFinite(valFloat); }
            set { valFloat = value ? 0f : float.PositiveInfinity; }
        }

        public float valFloat
        {
            get { return valVec.x; }
            set { valVec = new Vector4(value, 0f); }
        }

        public Color valColor
        {
            get { return new Color(valVec.x, valVec.y, valVec.z, valVec.w); }
            set { valVec = new Vector4(value.r, value.g, value.b, value.a); }
        }
    }
}