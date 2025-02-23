using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnatomyCarve.Runtime
{
    public class SobelNormals : ComputeRoutine
    {
        public const RenderTextureFormat FORMAT_NORMALS = RenderTextureFormat.ARGB32;

        private void Awake()
        {
            VolumeRendering.OnVolumeRenderingReady += OnVolumeRenderingReady;
        }

        private void OnVolumeRenderingReady(VolumeRendering volumeRendering)
        {
            InitializeShader();
        }

        private void Start()
        {

        }

        public void OnVolumeLoaded(Volume volume)
        {

        }

        public void Perform(Volume volume)
        {
            if (volume.normalsChecker.CalculatedThisFrame())
            {
                return;
            }

            // Set Opacity parameters
            shader.SetTexture(kernel, "Opacities", volume.opacityOutput);
            shader.SetTexture(kernel, "Result", volume.normals);
            shader.SetVector("Dimensions", (Vector3)volume.info.dimensions);

            // Execute compute shaders
            TimeMeasuring.Start("SobelNormals");
            ExecuteShader(volume.info.dimensions);
            TimeMeasuring.Pause("SobelNormals");
        }
    }
}