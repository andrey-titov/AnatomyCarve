using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnatomyCarve.Runtime
{
    public class SphericalRaycast : ComputeRoutine
    {
        public int computeShaderParts { get; set; } = 1;

        private Dictionary<RayPatternLAO, string> rayPatternsKeywords;


        private void Awake()
        {
            rayPatternsKeywords = new Dictionary<RayPatternLAO, string> {
                {RayPatternLAO.Neighborhood6, "NEIGHBORS_6" },
                {RayPatternLAO.Neighborhood14, "NEIGHBORS_14" },
                {RayPatternLAO.Neighborhood26, "NEIGHBORS_26" },
                {RayPatternLAO.Rubiks54, "RUBIKS_54" },
                //{RayPatternLAO.Sphere512, "SPHERE_512" },
            };

            VolumeRendering.OnVolumeRenderingReady += OnVolumeRenderingReady;
        }

        private void OnVolumeRenderingReady(VolumeRendering volumeRendering)
        {
            InitializeShader(rayPatternsKeywords[RayPatternLAO.Neighborhood6]);
        }

        private void Start()
        {

        }

        public bool RequirePrecalculation(Volume volume)
        {
            VolumeCao vc = volume.GetComponent<VolumeCao>();
            Segmentation s = volume.segmentation;

            return !vc.rayCastLaoPrecalculated
                || !vc.precalculatedRayPattern.HasValue
                || vc.precalculatedRayPattern != volume.rayPatternLAO
                || !vc.precalculatedShadingMode.HasValue
                || vc.precalculatedShadingMode != volume.shadingMode
                || s.opacityChanged;
        }

        public void OnVolumeLoaded(Volume volume)
        {
            
        }

        public void PrecalculateLAO(Volume volume)
        {
            shader.SetInt("RayStepCount", volume.rayStepCountLAO);

            VolumeCao vc = volume.GetComponent<VolumeCao>();

            // Set LAO parameters
            shader.SetTexture(kernel, "Opacities", volume.opacityOutput);
            shader.SetTexture(kernel, "Mask", vc.laoMask);
            shader.SetVector("Dimensions", (Vector3)volume.info.dimensions);
            shader.SetVector("PhysicalSize", volume.physicalSizeMmWithBorder);
            shader.SetVector("StepForMm", volume.stepForMmInNcWithBorder);
            shader.SetFloat("DistanceToVoxel", volume.distanceToVoxelNcWithBorder.magnitude);

            EnableSingleKeyword(rayPatternsKeywords.Values, rayPatternsKeywords[volume.rayPatternLAO]);

            // LAO precalculation
            {
                shader.SetTexture(kernel, "Result", vc.laoPrecalculated);
                shader.SetTexture(kernel, "LaoPrecalculated", vc.laoOutput);
                ExecuteShaderInParts(volume.info.dimensions, computeShaderParts);
            }

            vc.precalculatedRayPattern = volume.rayPatternLAO;
            vc.precalculatedShadingMode = volume.shadingMode;
            vc.rayCastLaoPrecalculated = true;
        }

        public void Perform(Volume volume)
        {
            VolumeCao vc = volume.GetComponent<VolumeCao>();

            if (vc.rayCastLaoChecker.CalculatedThisFrame())
            {
                return;
            }

            shader.SetTexture(kernel, "Opacities", volume.opacityOutput);
            shader.SetTexture(kernel, "Mask", vc.laoMask);

            shader.SetInt("RayStepCount", volume.rayStepCountLAO);

            shader.SetTexture(kernel, "Result", vc.laoOutput);
            shader.SetTexture(kernel, "LaoPrecalculated", vc.laoPrecalculated);

            // Distances (correct)
            shader.SetVector("Dimensions", (Vector3)volume.info.dimensions);
            shader.SetVector("PhysicalSize", volume.physicalSizeMmWithBorder);
            shader.SetVector("StepForMm", volume.stepForMmInNcWithBorder);
            shader.SetFloat("DistanceToVoxel", volume.distanceToVoxelNcWithBorder.magnitude);
            //}

            if (volume.shadingMode == VolumeShadingMode.LAO)
            {
                shader.EnableKeyword("FULL_RECALCULATION");
            }
            else
            {
                shader.DisableKeyword("FULL_RECALCULATION");
            }

            EnableSingleKeyword(rayPatternsKeywords.Values, rayPatternsKeywords[volume.rayPatternLAO]);

            TimeMeasuring.Start("RayCastLAO");
            ExecuteShaderInParts(volume.info.dimensions, computeShaderParts);
            TimeMeasuring.Pause("RayCastLAO");
        }
    }
}