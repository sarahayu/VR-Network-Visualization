using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    // Collect one command's appearance changes and play them alongside its explanation.
    public sealed class VisualCommandStep : IDisposable
    {
        sealed class NetworkChanges
        {
            public readonly List<VisualTransition> Transitions = new();
            public readonly Dictionary<int, (Color color, float size)> Nodes = new();
            public readonly Dictionary<int, (Color start, Color end, float width, float alpha)> Links = new();
            public readonly HashSet<int> ChangedNodes = new();
            public readonly HashSet<int> ChangedLinks = new();
            public bool GeometryChanged;
            public bool Store;
        }

        readonly Dictionary<NodeLinkNetwork, NetworkChanges> _networks = new();
        readonly Action _showMessage;
        bool _shown;

        public VisualCommandStep(Action showMessage) => _showMessage = showMessage;

        public void ShowMessage()
        {
            if (_shown) return;
            _shown = true;
            _showMessage?.Invoke();
        }

        public void Capture(NodeLinkNetwork network, bool store)
        {
            if (_networks.TryGetValue(network, out var existing))
            {
                existing.Store |= store;
                return;
            }

            // Capture once so repeated assignments in one action share the same start.
            var changes = new NetworkChanges { Store = store };
            foreach (var pair in network.Context.Nodes)
                changes.Nodes[pair.Key] = (pair.Value.Color, pair.Value.Size);
            foreach (var pair in network.Context.Links)
                changes.Links[pair.Key] = (pair.Value.ColorStart, pair.Value.ColorEnd, pair.Value.Width, pair.Value.Alpha);
            _networks.Add(network, changes);
            network.SetAppearanceDeferred(true);
        }

        public IEnumerator Play()
        {
            float duration = 0f;
            foreach (var pair in _networks)
            {
                if (pair.Key == null) continue;
                BuildTransitions(pair.Key, pair.Value);
                foreach (var transition in pair.Value.Transitions)
                {
                    duration = Mathf.Max(duration, transition.Duration);
                    transition.Apply(0f);
                }
            }

            // Initialization stays frame-budgeted and cannot consume animation time.
            foreach (var pair in _networks)
                while (pair.Key != null && !pair.Key.IsVisualReady) yield return null;

            ShowMessage();
            float elapsed = 0f;
            while (elapsed < duration)
            {
                Apply(elapsed);
                yield return null;
                elapsed += Time.deltaTime;
            }
            Apply(float.MaxValue);
        }

        void Apply(float elapsed)
        {
            foreach (var pair in _networks)
            {
                if (pair.Key == null) continue;
                foreach (var transition in pair.Value.Transitions) transition.Apply(elapsed);
                if (pair.Value.Transitions.Count > 0)
                    pair.Key.RenderAppearance(pair.Value.ChangedNodes, pair.Value.ChangedLinks, pair.Value.GeometryChanged);
            }
        }

        static void BuildTransitions(NodeLinkNetwork network, NetworkChanges changes)
        {
            void Track(int id, VisualProperty property)
            {
                if (property == VisualProperty.NodeColor || property == VisualProperty.NodeSize)
                    changes.ChangedNodes.Add(id);
                else changes.ChangedLinks.Add(id);
                changes.GeometryChanged |= property == VisualProperty.NodeSize;
            }

            void AddColor(int id, VisualProperty property, Color from, Color to, Action<Color> apply)
            {
                if (from.Equals(to)) return;
                changes.Transitions.Add(new VisualTransition<Color>(id, property, from, to,
                    0.4f, Color.LerpUnclamped, apply));
                Track(id, property);
            }

            void AddFloat(int id, VisualProperty property, float from, float to, Action<float> apply)
            {
                if (from.Equals(to)) return;
                changes.Transitions.Add(new VisualTransition<float>(id, property, from, to,
                    property == VisualProperty.LinkOpacity ? 0.3f : 0.4f, Mathf.LerpUnclamped, apply));
                Track(id, property);
            }

            foreach (var pair in changes.Nodes)
            {
                if (!network.Context.Nodes.TryGetValue(pair.Key, out var node)) continue;
                AddColor(pair.Key, VisualProperty.NodeColor, pair.Value.color, node.Color,
                    value => { node.Color = value; node.Dirty = true; });
                AddFloat(pair.Key, VisualProperty.NodeSize, pair.Value.size, node.Size,
                    value => { node.Size = value; node.Dirty = true; });
            }
            foreach (var pair in changes.Links)
            {
                if (!network.Context.Links.TryGetValue(pair.Key, out var link)) continue;
                AddColor(pair.Key, VisualProperty.LinkColorStart, pair.Value.start, link.ColorStart,
                    value => { link.ColorStart = value; link.Dirty = true; });
                AddColor(pair.Key, VisualProperty.LinkColorEnd, pair.Value.end, link.ColorEnd,
                    value => { link.ColorEnd = value; link.Dirty = true; });
                AddFloat(pair.Key, VisualProperty.LinkWidth, pair.Value.width, link.Width,
                    value => { link.Width = value; link.Dirty = true; });
                AddFloat(pair.Key, VisualProperty.LinkOpacity, pair.Value.alpha, link.Alpha,
                    value => { link.Alpha = value; link.Dirty = true; });
            }
            changes.Nodes.Clear();
            changes.Links.Clear();
        }

        public void Dispose()
        {
            NodeLinkNetwork storageOwner = null;
            foreach (var pair in _networks)
            {
                if (pair.Key == null) continue;
                // Keep the currently displayed state if playback is interrupted.
                pair.Key.SetAppearanceDeferred(false);
                pair.Key.UpdateRenderElements();
                if (pair.Value.Store) storageOwner = pair.Key;
            }
            _networks.Clear();
            // Storage callbacks save the whole network, so commit only once per step.
            if (storageOwner != null) storageOwner.UpdateStorage();
        }
    }
}
