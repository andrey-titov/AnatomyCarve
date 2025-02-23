using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AnatomyCarve.Runtime
{
    public class CarvingAggregation : ComputeRoutine
    {
        public ClippingMesh[] clippingMeshes;
#if AC_INSPECT_TEXTURES
        public RenderTexture carvingArray;
        public RenderTexture carvingDilationArray;
#else
        public RenderTexture carvingArray { get; set; }
        public RenderTexture carvingDilationArray { get; set; }
#endif

        public bool aggregateDilation { get; set; }

        ClippingMesh[] clippingMeshesSaved;
        Volume volume { get; set; }

        const string AGGREGATE_DILATION_KEYWORD = "AGGREGATE_DILATION";

        protected virtual void Awake()
        {
            InitializeShader();
            DeepCopyIfClone();
            volume = GetComponent<Volume>();
        }

        private void DeepCopyIfClone()
        {
            if (carvingArray)
            {
                carvingArray = Instantiate(carvingArray);
            }
            if (carvingDilationArray)
            {
                carvingDilationArray = Instantiate(carvingDilationArray);
            }
        }

        public bool UpdateIfNeeded()
        {
            if (CarvingChanged())
            {
                UpdateCarving2DArray();
                return true;
            }

            return false;
        }

        public IEnumerable<ClippingMesh> GetActiveCarvingObjects()
        {
            Dilation dilation = volume.GetDilationKey();

            return clippingMeshes
                .Where(c => c != null)
                //.Where(c => c.isActiveAndEnabled)
                .Where(c => c.carvingConfigurations.ContainsKey(dilation))
                .Distinct();
        }

        private bool CarvingChanged()
        {
            IEnumerable<ClippingMesh> carvingCurrent = GetActiveCarvingObjects();

            if (clippingMeshesSaved != null)
            {
                return !carvingCurrent.SequenceEqual(clippingMeshesSaved);
            }
            else
            {
                return carvingCurrent.Count() != 0;
            }
        }

        public bool SegmentCarvingEnabled()
        {
            IEnumerable<ClippingMesh> clippingMeshes = GetActiveCarvingObjects();
            return clippingMeshes.Count() >= 1;
        }

        private void UpdateCarving2DArray()
        {
            if (carvingArray != null)
            {
                Destroy(carvingArray);
            }

            if (!SegmentCarvingEnabled())
            {
                return;
            }

            clippingMeshesSaved = GetActiveCarvingObjects().ToArray();

            if (clippingMeshesSaved.Length == 0)
            {
                return;
            }

            carvingArray = TextureHelper.CreateRenderTexture2DArray(ClippingMesh.FBO_RESOLUTION, ClippingMesh.FBO_RESOLUTION, clippingMeshesSaved.Length, RenderTextureFormat.RG32);

            shader.SetTexture(kernel, "CarvingArray", carvingArray);

            if (aggregateDilation)
            {
                carvingDilationArray = TextureHelper.CreateRenderTexture2DArray(ClippingMesh.FBO_RESOLUTION, ClippingMesh.FBO_RESOLUTION, clippingMeshesSaved.Length, RenderTextureFormat.ARGB64);
                shader.EnableKeyword(AGGREGATE_DILATION_KEYWORD);
                shader.SetTexture(kernel, "CarvingDilationArray", carvingDilationArray);
            }
            else
            {
                shader.DisableKeyword(AGGREGATE_DILATION_KEYWORD);
            }

            Dilation dilationKey = volume.GetDilationKey();

            for (int i = 0; i < clippingMeshesSaved.Length; i++)
            {
                ClippingMesh clippingMesh = clippingMeshesSaved[i];

                shader.SetInt("Slice", i);

                shader.SetTexture(kernel, "DepthFront", clippingMesh.depthFront);
                shader.SetTexture(kernel, "DepthBack", clippingMesh.depthBack);

                if (aggregateDilation)
                {
                    shader.SetTexture(kernel, "DilationFront", clippingMesh.carvingConfigurations[dilationKey].dilationFront);
                    shader.SetTexture(kernel, "DilationBack", clippingMesh.carvingConfigurations[dilationKey].dilationBack);
                }

                ExecuteShader(clippingMesh.depthFront.width, clippingMesh.depthFront.height, 1);
            }
        }

        public Matrix4x4[] GetCarvingMatrices()
        {
            IEnumerable<ClippingMesh> carvingCameras = GetActiveCarvingObjects();
            int carvingObjectsCount = carvingCameras.Count();

            Matrix4x4 matrixM = volume.raycastedVolume.transform.GetComponent<Renderer>().localToWorldMatrix;
            Matrix4x4[] matrixCarvingMVP = new Matrix4x4[carvingObjectsCount];

            int i = 0;
            foreach (ClippingMesh cm in carvingCameras)
            {
                Matrix4x4 matrixCarvingP = cm.GetProjectionMatrix();
                Matrix4x4 matrixCarvingV = cm.camera.worldToCameraMatrix;
                matrixCarvingMVP[i++] = matrixCarvingP * matrixCarvingV * matrixM;
            }

            return matrixCarvingMVP;
        }
    }
}