using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class InteractionTimer
    {
        const int WRAP_VAL = 1000;

        int _timeSinceInteraction = WRAP_VAL;
        int _timeSinceCancellation = WRAP_VAL;

        public bool TickAndCheckDidInteract()
        {
            bool isValid = false;

            // hover exit and another interaction detected within acceptable frame timespan. ignore hover exit
            if (_timeSinceInteraction < 2 && _timeSinceCancellation < 2)
            {
                _timeSinceInteraction = WRAP_VAL;
                _timeSinceCancellation = WRAP_VAL;
            }
            // if sufficient time has passed after hover exit without another interaction, register it
            else if (_timeSinceInteraction == 1 && (_timeSinceCancellation >= 2))
            {
                isValid = true;
            }

            if (_timeSinceInteraction < WRAP_VAL)
            {
                _timeSinceInteraction += 1;
            }
            if (_timeSinceCancellation < WRAP_VAL)
            {
                _timeSinceCancellation += 1;
            }

            return isValid;
        }

        public void DidInteraction()
        {
            _timeSinceInteraction = 0;
        }

        public void DidCancel()
        {
            _timeSinceCancellation = 0;
        }
    }
}