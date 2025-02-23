using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnatomyCarve.Runtime
{
    public class VolumeCao : MonoBehaviour
    {
        public CalculatedChecker rayCastLaoChecker { get; set; } = new CalculatedChecker();

#if AC_INSPECT_TEXTURES
        public RenderTexture laoMask;
        public RenderTexture laoOutput;
        public RenderTexture laoPrecalculated;
#else
        public RenderTexture laoMask { get; set; }
        public RenderTexture laoOutput { get; set; }
        public RenderTexture laoPrecalculated { get; set; }
#endif

        public RayPatternLAO? precalculatedRayPattern { get; set; }
        public VolumeShadingMode? precalculatedShadingMode { get; set; }
        public bool rayCastLaoPrecalculated { get; set; }

        private void Awake()
        {
            Volume volume = GetComponent<Volume>();

            if (volume.info == null)
            {
                DestroyImmediate(this);
                return;
            }

            laoMask = TextureHelper.CreateRenderTexture3D(volume.info.dimensions, RenderTextureFormat.R8, FilterMode.Point);
            laoOutput = TextureHelper.CreateRenderTexture3D(volume.info.dimensions, RenderTextureFormat.R8);
            laoPrecalculated = TextureHelper.CreateRenderTexture3D(volume.info.dimensions, RenderTextureFormat.R8);
        }

        //public void Clear()
        //{
        //    voxelClippingChecker.Clear();
        //    rayCastLaoChecker.Clear();
        //    normalsChecker.Clear();
        //    precalculatedRayPattern = null;
        //    precalculatedShadingMode = null;
        //    rayCastLaoPrecalculated = false;

        //    //Destroy(normals);
        //    //Destroy(opacityOutput);
        //    //Destroy(laoMask);
        //    //Destroy(laoOutput);
        //    //Destroy(laoPrecalculated);
        //}

        private void OnDestroy()
        {
            Destroy(laoMask);
            Destroy(laoOutput);
            Destroy(laoPrecalculated);
        }
    }
}