using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnatomyCarve.Runtime
{
    public class CalculatedChecker
    {
        int lastFrameOfExecution = -1;

        public bool CalculatedThisFrame()
        {
            if (lastFrameOfExecution != Time.frameCount)
            {
                lastFrameOfExecution = Time.frameCount;
                return false;
            }
            return true;
        }

        public void Clear()
        {
            lastFrameOfExecution = -1;
        }
    }
}