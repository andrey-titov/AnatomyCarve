using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnatomyCarve.Runtime
{
    public class SSAO : ComputeRoutine
    {
#if AC_INSPECT_TEXTURES
        public RenderTexture depthMap;
        public RenderTexture result;
#else
        public RenderTexture depthMap { get; set; }
        public RenderTexture result { get; set; }
#endif

        void Awake()
        {
            VolumeRendering.OnVolumeRenderingReady += OnVolumeRenderingReady;
            ResolutionObservable.OnResolutionChanged += OnResolutionChanged;
        }

        private void OnVolumeRenderingReady(VolumeRendering volumeRendering)
        {
            InitializeShader();
        }

        private void OnResolutionChanged(Vector2Int newRes)
        {
            depthMap = new RenderTexture(newRes.x, newRes.y, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            depthMap.filterMode = FilterMode.Bilinear;
            depthMap.wrapMode = TextureWrapMode.Clamp;
            depthMap.enableRandomWrite = true;
            depthMap.Create();

            result = new RenderTexture(newRes.x, newRes.y, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            result.filterMode = FilterMode.Bilinear;
            result.enableRandomWrite = true;
            result.Create();

            shader.SetTexture(kernel, "DepthMap", depthMap);
            shader.SetTexture(kernel, "Result", result);

        }

        public void OnVolumeLoaded(Volume volume)
        {

        }

        public void Perform(Volume volume)
        {
            float dimMagnitude = new Vector2(depthMap.width, depthMap.height).magnitude;
            float kernelDimater = dimMagnitude * volume.SSAORadius * 2f;

            //shader.SetVectorArray("Samples", samples);
            shader.SetInt("KernelWidth", (int)kernelDimater);
            ExecuteShader(depthMap.width, depthMap.height, 1);
        }
    }
}