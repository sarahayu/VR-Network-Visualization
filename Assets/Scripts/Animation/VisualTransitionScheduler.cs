using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    // Run command pipelines in arrival order, including their asynchronous preparation.
    public sealed class VisualTransitionScheduler : MonoBehaviour
    {
        readonly Queue<IEnumerator> _pending = new();
        readonly Stack<IEnumerator> _active = new();
        bool _running;

        public IEnumerator Schedule(IEnumerator command)
        {
            bool complete = false;
            IEnumerator Run()
            {
                try { yield return command; }
                finally { complete = true; }
            }
            Enqueue(Run());
            while (!complete && isActiveAndEnabled) yield return null;
        }

        public void Enqueue(IEnumerator command)
        {
            _pending.Enqueue(command);
            if (_running) return;
            _running = true;
            StartCoroutine(Drain());
        }

        IEnumerator Drain()
        {
            while (_pending.Count > 0)
            {
                _active.Push(_pending.Dequeue());
                while (_active.Count > 0)
                {
                    object next = null;
                    bool moved = false;
                    try
                    {
                        moved = _active.Peek().MoveNext();
                        if (moved) next = _active.Peek().Current;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, this);
                        DisposeActive();
                        break;
                    }
                    if (!moved)
                    {
                        (_active.Pop() as IDisposable)?.Dispose();
                        continue;
                    }
                    // Traverse nested routines so failures release step state as well.
                    if (next is IEnumerator nested) _active.Push(nested);
                    else yield return next;
                }
            }
            _running = false;
        }

        void DisposeActive()
        {
            while (_active.Count > 0) (_active.Pop() as IDisposable)?.Dispose();
        }

        void OnDisable()
        {
            StopAllCoroutines();
            DisposeActive();
            while (_pending.Count > 0) (_pending.Dequeue() as IDisposable)?.Dispose();
            _running = false;
        }
    }
}
