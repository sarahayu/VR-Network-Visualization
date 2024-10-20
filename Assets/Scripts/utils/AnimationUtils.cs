using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{

    // code provided by the magnanimous mbitzos: https://michaelbitzos.com/devblog/programmatic-animation-using-coroutines
    public static class AnimationUtils
    {
        /// <summary>
        /// provides a util to easily control the timing of a lerp over a duration
        /// </summary>
        /// <param name="duration">How long our lerp will take</param>
        /// <param name="action">The action to perform per frame of the lerp, is given the progress t in [0,1]</param>
        /// <param name="realTime">If we want to run our lerp on real time</param>
        /// <param name="smooth">If we want our time curve to run on a smooth step</param>
        /// <param name="curve">If we want our time curve to follow a specific animation curve</param>
        /// <param name="inverse">If we want the time to be inversed such that it returns t-1</param>
        /// <returns></returns>
        public static IEnumerator Lerp(
          float duration,
          Action<float> action,
          bool realTime = false,
          bool smooth = false,
          AnimationCurve curve = null,
          bool inverse = false
        )
        {
            float time = 0;
            Func<float, float> tEval = t => t;
            if (smooth) tEval = t => Mathf.SmoothStep(0, 1, t);
            if (curve != null) tEval = t => curve.Evaluate(t);
            while (time < duration)
            {
                float delta = realTime ? Time.fixedDeltaTime : Time.deltaTime;
                float t = (time + delta > duration) ? 1 : (time / duration);
                if (inverse)
                    t = 1 - t;
                action(tEval(t));
                time += delta;
                yield return null;
            }
            action(tEval(inverse ? 0 : 1));
        }
    }

}