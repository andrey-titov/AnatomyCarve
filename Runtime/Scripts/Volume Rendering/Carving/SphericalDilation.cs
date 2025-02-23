using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnatomyCarve.Runtime
{
    public class SphericalDilation : ComputeRoutine
    {
        private Camera carvingCamera;

        public void PreAwake()
        {
            InitializeShader();
            carvingCamera = GetComponent<Camera>();
        }

        public CarvingConfiguration CreateCarvingConfiguration()
        {
            RenderTexture dilationFront = new RenderTexture(ClippingMesh.FBO_RESOLUTION, ClippingMesh.FBO_RESOLUTION, 0, RenderTextureFormat.R16, RenderTextureReadWrite.Linear);
            dilationFront.filterMode = FilterMode.Point;
            dilationFront.enableRandomWrite = true;
            dilationFront.Create();

            RenderTexture dilationBack = new RenderTexture(ClippingMesh.FBO_RESOLUTION, ClippingMesh.FBO_RESOLUTION, 0, RenderTextureFormat.R16, RenderTextureReadWrite.Linear);
            dilationBack.filterMode = FilterMode.Point;
            dilationBack.enableRandomWrite = true;
            dilationBack.Create();

            CarvingConfiguration carvingConfiguration = new CarvingConfiguration
            {
                dilationFront = dilationFront,
                dilationBack = dilationBack,
            };

            return carvingConfiguration;
        }

        public CarvingConfiguration DilateAndUnite(Dilation dilationKey, RenderTexture depthFront, RenderTexture depthBack, CarvingConfiguration carvingConfiguration = null)
        {
            if (carvingConfiguration == null)
            {
                carvingConfiguration = CreateCarvingConfiguration();
            }            

            UpdateDilationParameters(dilationKey);

            Perform(depthFront, carvingConfiguration.dilationFront, true);
            Perform(depthBack, carvingConfiguration.dilationBack, false);

            return carvingConfiguration;
        }

        private void UpdateDilationParameters(Dilation dilationKey)
        {
            float dilationMm = dilationKey.spacingMagnitude; // Initial offset
            dilationMm += dilationKey.rayStepCountLAO;

            // XY
            float dilationXYratio = (dilationMm * 0.001f) / (carvingCamera.orthographicSize * 2f);

            float dilationXYpixels = dilationXYratio * ClippingMesh.FBO_RESOLUTION;
            int dilationXYpixelsInt = (int)Mathf.Ceil(dilationXYpixels);

            shader.SetInt("DilationXY", dilationXYpixelsInt);

            // Z
            float dilationZratio = (dilationMm * 0.001f) / (carvingCamera.farClipPlane - carvingCamera.nearClipPlane);

            shader.SetFloat("DilationZ", dilationZratio);
        }

        private void Perform(RenderTexture depth, RenderTexture dilation, bool isFront)
        {
            shader.SetTexture(kernel, "DepthBuffer", depth);
            shader.SetTexture(kernel, "Result", dilation);

            if (isFront)
            {
                shader.EnableKeyword("FRONT_FACES");
            }
            else
            {
                shader.DisableKeyword("FRONT_FACES");
            }

            ExecuteShader(depth.width, depth.height, 1);
        }
    }

    public struct Dilation
    {
        public int rayStepCountLAO;
        public float spacingMagnitude;

        public Dilation(int rayStepCountLAO, float spacingMagnitude)
        {
            this.rayStepCountLAO = rayStepCountLAO;
            this.spacingMagnitude = spacingMagnitude;
        }

        public override bool Equals(object obj)
        {
            Dilation k = (Dilation)obj;
            return k.rayStepCountLAO == rayStepCountLAO && k.spacingMagnitude == spacingMagnitude;
        }

        public override int GetHashCode()
        {
            return rayStepCountLAO.GetHashCode() + spacingMagnitude.GetHashCode();
        }
    }
}