using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnatomyCarve.Runtime
{
    public class SegmentationCarving : CarvingAggregation
    {
        protected override void Awake()
        {
            base.Awake();
            aggregateDilation = false;
        }
    }
}
