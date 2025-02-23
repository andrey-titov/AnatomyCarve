using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnatomyCarve.Runtime
{
    public class RayCasting : ComputeRoutine
    {
#if AC_INSPECT_TEXTURES
        public RenderTexture positionDepth;
        public RenderTexture directionSteps;
#else
        public RenderTexture positionDepth { get; set; }
        public RenderTexture directionSteps { get; set; }
#endif

        private VolumeCamera volumeCamera;

        private const string DITHERING_KEYWORD = "DITHERING";
        private const string OVERWRITE_OPACITY_KEYWORD = "OVERWRITE_OPACITY";
        private const string SSAO_KEYWORD = "SSAO";

        private void Awake()
        {

            VolumeRendering.OnVolumeRenderingReady += OnVolumeRenderingReady;
            ResolutionObservable.OnResolutionChanged += OnResolutionChanged;
        }

        private void OnVolumeRenderingReady(VolumeRendering volumeRendering)
        {
            volumeCamera = GetComponent<VolumeCamera>();
            InitializeShader();
        }

        // Start is called before the first frame update
        void Start()
        {
            // Warning: Can be called after Update() in ResolutionObservable
        }

        public void OnVolumeLoaded(Volume volume)
        {

        }

        public void Perform(Volume volume, RenderTexture depthMapSSAO)
        {
            // Init Shader
            shader.SetTexture(kernel, "Volume", volume.intensities);
            //shader.SetFloat("IntensityThreshold", volume.transferFunction.GetIntensityThreshold(volume.info));

            shader.SetFloat("StepSize", volume.raySamplingStep * volume.sampleStepSize);

            Matrix4x4 matrixPinv = volumeCamera.GetProjectionMatrix().inverse;
            Matrix4x4 matrixV = volumeCamera.boundariesFrontCamera.worldToCameraMatrix;
            Matrix4x4 matrixM = volume.raycastedVolume.transform.GetComponent<Renderer>().localToWorldMatrix;
            shader.SetMatrix("MatrixMV", matrixV * matrixM);
            shader.SetMatrix("MatrixPinv", matrixPinv);

            shader.SetFloat("CameraFarClip", Camera.main.farClipPlane);

            RectInt rect = volume.CalculateClosestDepthAndBoundingBox();

            shader.SetInt("Xstart", rect.xMin);
            shader.SetInt("Ystart", rect.yMin);

            if (volume.dithering)
            {
                shader.EnableKeyword(DITHERING_KEYWORD);
            }
            else
            {
                shader.DisableKeyword(DITHERING_KEYWORD);
            }

            //// Overwrite Opacity
            //if (volume.segmentation.SegmentationEnabled())
            //{
            //    shader.EnableKeyword(OVERWRITE_OPACITY_KEYWORD);
            //    shader.SetTexture(kernel, "OpacityMap", volume.segmentation.opacityMap);
            //}
            //else
            //{
            //    shader.DisableKeyword(OVERWRITE_OPACITY_KEYWORD);
            //}
            shader.SetTexture(kernel, "OpacityOutput", volume.opacityOutput);

            // Overwrite Opacity
            if (volume.shadingMode == VolumeShadingMode.PhongAndSSAO)
            {
                shader.EnableKeyword(SSAO_KEYWORD);
                shader.SetTexture(kernel, "DepthMapSSAO", depthMapSSAO);
                shader.SetTexture(kernel, "OpacityTF", volume.opacityTexture);
            }
            else
            {
                shader.DisableKeyword(SSAO_KEYWORD);
            }

            ExecuteShader(rect.width, rect.height, 1);
        }

        private void OnResolutionChanged(Vector2Int newRes)
        {
            shader.SetTexture(kernel, "BoundariesFront", volumeCamera.boundariesFrontCamera.targetTexture);
            shader.SetTexture(kernel, "BoundariesBack", volumeCamera.boundariesBackCamera.targetTexture);
            shader.SetTexture(kernel, "Occluders", volumeCamera.occludersCamera.targetTexture);

            if (positionDepth != null)
            {
                Destroy(positionDepth);
                Destroy(directionSteps);
            }

            positionDepth = new RenderTexture(newRes.x, newRes.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            positionDepth.filterMode = FilterMode.Point;
            positionDepth.enableRandomWrite = true;
            positionDepth.Create();

            directionSteps = new RenderTexture(newRes.x, newRes.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            directionSteps.filterMode = FilterMode.Point;
            directionSteps.enableRandomWrite = true;
            directionSteps.Create();

            shader.SetTexture(kernel, "PositionDepth", positionDepth);
            shader.SetTexture(kernel, "DirectionSteps", directionSteps);

        }

        private void OnDestroy()
        {
            if (positionDepth)
            {
                Destroy(positionDepth);
                Destroy(directionSteps);
            }
        }
    }
}