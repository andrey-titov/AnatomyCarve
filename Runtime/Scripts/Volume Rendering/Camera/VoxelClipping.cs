using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.VisualScripting;
using System;

namespace AnatomyCarve.Runtime
{
    public class VoxelClipping : ComputeRoutine
    {
        public List<Volume> volumes { get; set; } = new List<Volume>();

        public const string LAO_KEYWORD = "LAO";
        public const string NO_CARVING_KEYWORD = "NO_CARVING";
        public const int MAX_CARVING_OBJECTS = 8;

        // Start is called before the first frame update
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

        public bool RequirePrecalculation(Volume volume)
        {
            Segmentation s = volume.segmentation;

            return s.opacityChanged;
        }

        public void OnVolumeLoaded(Volume volume)
        {
            //if (!volumes.Contains(volume))
            //{
            //vc = volume.gameObject.AddComponent<VolumeCao>();
            volumes.Add(volume);
            //}
            //else
            //{
            //    Destroy(volume.normals);
            //    Destroy(volume.opacityOutput);

            //    VolumeCao vc = null;
            //    if ((vc = volume.gameObject.AddComponent<VolumeCao>()) != null)
            //    {
            //        Destroy(vc.laoMask);
            //        Destroy(vc.laoOutput);
            //        Destroy(vc.laoPrecalculated);
            //    }                
            //}

            //volume.normals = TextureHelper.CreateRenderTexture3D(volume.info.dimensions, SobelNormals.FORMAT_NORMALS, FilterMode.Bilinear);
            //volume.opacityOutput = TextureHelper.CreateRenderTexture3D(volume.info.dimensions + new Vector3Int(2, 2, 2), RenderTextureFormat.R8, FilterMode.Bilinear);
            //vc.laoMask = TextureHelper.CreateRenderTexture3D(volume.info.dimensions, RenderTextureFormat.R8, FilterMode.Point);
            //vc.laoOutput = TextureHelper.CreateRenderTexture3D(volume.info.dimensions, RenderTextureFormat.R8);
            //vc.laoPrecalculated = TextureHelper.CreateRenderTexture3D(volume.info.dimensions, RenderTextureFormat.R8);
        }

        public void PrecalculateOpacity(Volume volume)
        {
            shader.SetTexture(kernel, "OpacityTF", volume.opacityTexture);

            VolumeCao vc = volume.GetComponent<VolumeCao>();
            shader.EnableKeyword(LAO_KEYWORD);
            shader.EnableKeyword(NO_CARVING_KEYWORD);

            // Set Opacity parameters
            shader.SetTexture(kernel, "Intensities", volume.intensities);
            shader.SetVector("Dimensions", (Vector3)volume.info.dimensions);

            // Output        
            shader.SetTexture(kernel, "ResultOpacities", volume.opacityOutput);
            shader.SetTexture(kernel, "ResultMask", vc.laoMask);

            // Segmentation
            if (volume.segmentation.SegmentationEnabled())
            {
                shader.EnableKeyword("SEGMENTED");
                shader.SetTexture(kernel, "OpacityMap", volume.segmentation.opacityMap);
            }
            else
            {
                shader.DisableKeyword("SEGMENTED");
            }

            // Precalculate output when no clipping is applied
            shader.EnableKeyword(NO_CARVING_KEYWORD);
            ExecuteShader(volume.info.dimensions);
            shader.DisableKeyword(NO_CARVING_KEYWORD);
        }

        public void Perform(Volume volume)
        {
            if (volume.voxelClippingChecker.CalculatedThisFrame())
            {
                return;
            }

            shader.SetTexture(kernel, "OpacityTF", volume.opacityTexture);

            // Set Opacity parameters
            shader.SetTexture(kernel, "Intensities", volume.intensities);
            shader.SetVector("Dimensions", (Vector3)volume.info.dimensions);

            // Output        
            shader.SetTexture(kernel, "ResultOpacities", volume.opacityOutput);

            if (volume.shadingMode == VolumeShadingMode.CAO
                || volume.shadingMode == VolumeShadingMode.PhongAndLAO
                || volume.shadingMode == VolumeShadingMode.LAO)
            {
                shader.EnableKeyword(LAO_KEYWORD);
                shader.SetTexture(kernel, "ResultMask", volume.GetComponent<VolumeCao>().laoMask);                
            }
            else
            {
                shader.DisableKeyword(LAO_KEYWORD);
            }

            // Segmentation
            if (volume.segmentation.SegmentationEnabled())
            {
                shader.EnableKeyword("SEGMENTED");
                shader.SetTexture(kernel, "OpacityMap", volume.segmentation.opacityMap);
            }
            else
            {
                shader.DisableKeyword("SEGMENTED");
            }

            volume.carving.UpdateIfNeeded();
            
            Matrix4x4[] matrixCarvingMVP = volume.carving.GetCarvingMatrices();
            shader.SetMatrixArray("CarvingMatrixMVP", matrixCarvingMVP);

            //// Set carving depth buffers            
            if (matrixCarvingMVP.Length > 0)
            {
                shader.DisableKeyword(NO_CARVING_KEYWORD);
                shader.SetTexture(kernel, "CarvingArray", volume.carving.carvingDilationArray);
            }
            else
            {
                shader.EnableKeyword(NO_CARVING_KEYWORD);
            }
            
            // Execute compute shaders
            TimeMeasuring.Start("VoxelClipping");
            ExecuteShader(volume.info.dimensions);
            TimeMeasuring.Pause("VoxelClipping");
        }
    }
}