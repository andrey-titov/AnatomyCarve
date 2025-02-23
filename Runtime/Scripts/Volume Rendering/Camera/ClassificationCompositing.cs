using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AnatomyCarve.Runtime
{
    public class ClassificationCompositing : ComputeRoutine
    {
        // Properties of camera
        private VolumeCamera volumeCamera;

#if AC_INSPECT_TEXTURES
        public RenderTexture volumeImage;
#else
        public RenderTexture volumeImage { get; set; }
#endif

        private Dictionary<VolumeShadingMode, string> shadingModesKeywords;
        private Dictionary<Segmentation.ColorRendering, string> segmentKeywords;

        private List<Volume> volumes = new List<Volume>();

        // Start is called before the first frame update
        protected void Awake()
        {
            shadingModesKeywords = new Dictionary<VolumeShadingMode, string> 
            {
                {VolumeShadingMode.SolidColor, "SOLID_COLOR" },
                {VolumeShadingMode.Phong, "PHONG" },
                {VolumeShadingMode.PhongAndSSAO, "PHONG_SSAO" },
                {VolumeShadingMode.CAO, "LAO" },
                {VolumeShadingMode.PhongAndLAO, "PHONG_LAO" },
                {VolumeShadingMode.LAO, "LAO" },
            };

            segmentKeywords = new Dictionary<Segmentation.ColorRendering, string>
            {
                {Segmentation.ColorRendering.Label, "COLOR_LABEL" },
                {Segmentation.ColorRendering.Histogram, "COLOR_HISTOGRAM" },
                {Segmentation.ColorRendering.UniqueLabel, "COLOR_UNIQUE_LABEL" },
            };

            VolumeRendering.OnVolumeRenderingReady += OnVolumeRenderingReady;
            ResolutionObservable.OnResolutionChanged += OnResolutionChanged;
        }

        private void OnVolumeRenderingReady(VolumeRendering volumeRendering)
        {
            volumeCamera = GetComponent<VolumeCamera>();
            InitializeShader(shadingModesKeywords[VolumeShadingMode.SolidColor]);
        }

        private void Start()
        {
            // Warning: Can be called after Update() in ResolutionObservable
        }

        public void OnVolumeLoaded(Volume volume)
        {
            if (!volumes.Contains(volume))
            {
                if (volumeImage)
                {
                    volume.volumeImage = Instantiate(volumeImage);
                    volume.volumeImage.enableRandomWrite = true;
                    volume.volumeImage.Create();
                    volume.raycastedVolume.GetComponent<MeshRenderer>().material.SetTexture("_RaycastedImage", volume.volumeImage);
                }

                volumes.Add(volume);
            }
        }

        public void OnVolumeDestroyed(Volume volume)
        {
            volumes.Remove(volume);
        }

        private void ApplyVolume(Volume volume)
        {
            // Init Shader
            shader.SetTexture(kernel, "Intensities", volume.intensities);
            shader.SetVector("Dimensions", (Vector3)volume.info.dimensions);
            shader.SetFloat("IntensityThreshold", volume.transferFunction.GetIntensityThreshold(volume.info));
            ApplyTransferFunction(volume);
            ApplySegmentation(volume);
        }

        //public Texture2D colorsOpacityTexture;

        private void ApplyTransferFunction(Volume volume)
        {
            TransferFunction vp = volume.transferFunction;
            shader.SetTexture(kernel, "ColorOpacityTF", volume.colorOpacityTexture);

            // Phong
            shader.SetFloat("AmbientFactor", vp.ambientReflection);
            shader.SetFloat("DiffuseFactor", vp.diffuseReflection);
            shader.SetFloat("SpecularFactor", vp.specularReflection);
            shader.SetFloat("SpecularExponent", vp.specularReflectionPower);
        }

        private void ApplySegmentation(Volume volume) 
        {
            //shader.DisableKeyword("SEGMENT_CARVING");

            // Segmentation rendering type
            if (volume.segmentation.SegmentationEnabled())
            {
                EnableSingleKeyword(segmentKeywords.Values, segmentKeywords[volume.segmentation.colorRendering]);

                switch (volume.segmentation.colorRendering)
                {
                    case Segmentation.ColorRendering.Label:
                        shader.SetTexture(kernel, "LabelMap", volume.segmentation.labelMap);
                        shader.SetTexture(kernel, "SegmentColors", volume.segmentation.segmentColors);
                        break;                    
                    case Segmentation.ColorRendering.Histogram:
                        shader.SetTexture(kernel, "ColorMap", volume.segmentation.colorMap);
                        break;
                    case Segmentation.ColorRendering.UniqueLabel:
                        shader.SetTexture(kernel, "LabelMap", volume.segmentation.labelMap);
                        if (volume.segmentation.carvingMap != null && volume.segmentation.carving.SegmentCarvingEnabled()) //&& clipPerSegment) 
                        {
                            shader.EnableKeyword("SEGMENT_CARVING");
                            shader.SetTexture(kernel, "CarvingMap", volume.segmentation.carvingMap);
                            //Debug.Log("1");
                        }
                        else
                        {
                            shader.DisableKeyword("SEGMENT_CARVING");
                            //Debug.Log("2");
                        }
                        break;
                    default:
                        break;
                }
            }
            else
            {
                DisableKeywords(segmentKeywords.Values);
            }

            if (volume.segmentation.SegmentationEnabled())
            {
                shader.EnableKeyword("SEGMENTED");
                shader.SetTexture(kernel, "OpacityMap", volume.segmentation.opacityMap);                
            }
            else
            {
                shader.DisableKeyword("SEGMENTED");
            }
        }

        public void Perform(Volume volume, RenderTexture positionDepth, RenderTexture directionSteps, RenderTexture ssao)
        {
            TextureHelper.ClearOutRenderTexture(volume.volumeImage);

            ApplyVolume(volume);

            shader.SetTexture(kernel, "VolumeImage", volume.volumeImage);

            VolumeCao vc = volume.GetComponent<VolumeCao>();
            if (vc != null && vc.laoOutput != null)
            {
                shader.SetTexture(kernel, "Lao", vc.laoOutput);
                shader.SetFloat("LaoBrightness", volume.LAOBrightness);
            }

            if (volume.shadingMode == VolumeShadingMode.Phong
                || volume.shadingMode == VolumeShadingMode.PhongAndLAO
                || volume.shadingMode == VolumeShadingMode.PhongAndSSAO)
            {
                shader.SetTexture(kernel, "Normals", volume.normals);
            }

            if (volume.shadingMode == VolumeShadingMode.PhongAndSSAO)
            {
                shader.SetTexture(kernel, "SSAO", ssao);
            }

            shader.SetFloat("SampleStepSize", volume.sampleStepSize);

            shader.SetTexture(kernel, "PositionDepth", positionDepth);
            shader.SetTexture(kernel, "DirectionSteps", directionSteps);

            // Set matrices
            Matrix4x4 matrixP = volumeCamera.boundariesFrontCamera.projectionMatrix;
            Matrix4x4 matrixV = volumeCamera.boundariesFrontCamera.worldToCameraMatrix;
            Matrix4x4 matrixM = volume.raycastedVolume.transform.GetComponent<Renderer>().localToWorldMatrix;
            shader.SetMatrix("MatrixM", matrixM);
            shader.SetMatrix("MatrixMV", matrixV * matrixM);

            shader.SetFloat("CameraFarClip", Camera.main.farClipPlane);
            shader.SetVector("ViewerPositionW", Camera.main.transform.position);
            shader.SetVector("LightPositionW", Camera.main.transform.position);

            var rect = volume.CalculateClosestDepthAndBoundingBox();

            shader.SetInt("Xstart", rect.xMin);
            shader.SetInt("Ystart", rect.yMin);

            // Shading Mode
            EnableSingleKeyword(shadingModesKeywords.Values, shadingModesKeywords[volume.shadingMode]);

            // Set matrix for clipping
            volume.carving.UpdateIfNeeded();
            Matrix4x4[] matrixCarvingMVP = volume.carving.GetCarvingMatrices();
            shader.SetMatrixArray("CarvingMatrixMVP", matrixCarvingMVP);

            //// Set carving depth buffers
            if (matrixCarvingMVP.Length > 0)
            {
                shader.DisableKeyword(VoxelClipping.NO_CARVING_KEYWORD);
                shader.SetTexture(kernel, "CarvingArray", volume.carving.carvingArray);
            }
            else
            {
                shader.EnableKeyword(VoxelClipping.NO_CARVING_KEYWORD);
            }

            ExecuteShader(rect.width, rect.height, 1);
        }

        private void OnResolutionChanged(Vector2Int newRes)
        {
            //volumeShading.SetTexture(kernelInit, "PositionDepth", shadingTexture);
            if (volumeImage != null)
            {
                Destroy(volumeImage);
            }

            volumeImage = new RenderTexture(newRes.x, newRes.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            volumeImage.filterMode = FilterMode.Point;
            volumeImage.enableRandomWrite = true;
            volumeImage.Create();


            //shader.SetTexture(kernel, "CarvingNormals", volumeCamera.carvingCamera.targetTexture);
            //shader.SetTexture(kernel, "BoundariesFront", volumeCamera.boundariesFrontCamera.targetTexture);

            // Visualization of created image
            foreach (Volume v in volumes)
            {
                if (v.volumeImage)
                {
                    Destroy(v.volumeImage);
                }

                v.volumeImage = Instantiate(volumeImage);
                v.volumeImage.enableRandomWrite = true;
                v.volumeImage.Create();

                //vs.volumeImage = new RenderTexture(newRes.x, newRes.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                //vs.volumeImage.filterMode = FilterMode.Point;
                //vs.volumeImage.enableRandomWrite = true;
                //vs.volumeImage.Create();

                v.raycastedVolume.GetComponent<MeshRenderer>().material.SetTexture("_RaycastedImage", v.volumeImage);
            }
        }
        private void OnDestroy()
        {
            if (volumeImage)
            {
                Destroy(volumeImage);
            }

        }
    }
}