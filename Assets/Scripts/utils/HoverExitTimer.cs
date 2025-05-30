using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VidiGraph
{
    public class HoverExitTimer
    {
        const int WRAP_VAL = 1000;

        int _timeSinceExit = WRAP_VAL;
        int _timeSinceAnotherInteraction = WRAP_VAL;

        public bool TickAndCheckHoverExit()
        {
            bool isValidExit = false;

            // hover exit and another interaction detected within acceptable frame timespan. ignore hover exit
            if (_timeSinceExit < 2 && _timeSinceAnotherInteraction < 2)
            {
                _timeSinceExit = WRAP_VAL;
            }
            // if sufficient time has passed after hover exit without another interaction, register it
            else if (_timeSinceExit == 1)
            {
                isValidExit = true;
            }

            if (_timeSinceExit < WRAP_VAL)
            {
                _timeSinceExit += 1;
            }
            if (_timeSinceAnotherInteraction < WRAP_VAL)
            {
                _timeSinceAnotherInteraction += 1;
            }

            return isValidExit;
        }

        public void DidHoverExit()
        {
            _timeSinceExit = 0;
        }

        public void DidOtherInteraction()
        {
            _timeSinceAnotherInteraction = 0;
        }
    }
}