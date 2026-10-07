using System;
using UnityEngine;

namespace VidiGraph
{
    public enum VisualProperty { NodeColor, NodeSize, LinkColorStart, LinkColorEnd, LinkWidth, LinkOpacity }

    // Common timing and identity for one changed property.
    public abstract class VisualTransition
    {
        public readonly int ElementID;
        public readonly VisualProperty Property;
        public readonly float Duration;

        protected VisualTransition(int id, VisualProperty property, float duration)
        {
            ElementID = id;
            Property = property;
            Duration = Mathf.Max(0f, duration);
        }

        public abstract void Apply(float elapsed);
    }

    // Keep colors and scalar properties typed instead of encoding them in shared fields.
    public sealed class VisualTransition<T> : VisualTransition
    {
        public readonly T From;
        public readonly T To;
        readonly Func<T, T, float, T> _interpolate;
        readonly Action<T> _apply;

        public VisualTransition(int id, VisualProperty property, T from, T to,
            float duration, Func<T, T, float, T> interpolate, Action<T> apply)
            : base(id, property, duration)
        {
            From = from;
            To = to;
            _interpolate = interpolate;
            _apply = apply;
        }

        public override void Apply(float elapsed)
        {
            float t = Duration <= 0f ? 1f : Mathf.Clamp01(elapsed / Duration);
            _apply(t >= 1f ? To : _interpolate(From, To, Mathf.SmoothStep(0f, 1f, t)));
        }
    }
}
