using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnatomyCarve.Runtime
{
    public static class TextureHelper
    {
        public static RenderTexture CreateRenderTexture3D(Vector3Int dimensions, RenderTextureFormat format, FilterMode filterMode = FilterMode.Bilinear)
        {
            return CreateRenderTexture3D(dimensions.x, dimensions.y, dimensions.z, format, filterMode);
        }

        public static RenderTexture CreateRenderTexture3D(int width, int height, int depth, RenderTextureFormat format, FilterMode filterMode = FilterMode.Bilinear)
        {
            RenderTexture renderTexture3d = new RenderTexture(width, height, 0, format);
            renderTexture3d.dimension = TextureDimension.Tex3D;
            renderTexture3d.wrapMode = TextureWrapMode.Clamp;
            renderTexture3d.filterMode = filterMode;
            renderTexture3d.anisoLevel = 0;
            renderTexture3d.enableRandomWrite = true;
            renderTexture3d.volumeDepth = depth;
            renderTexture3d.antiAliasing = 1;
            renderTexture3d.Create();

            return renderTexture3d;
        }

        public static void ClearOutRenderTexture(RenderTexture renderTexture)
        {
            RenderTexture rt = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = rt;
        }

        public static RenderTexture CreateRenderTexture2DArray(int width, int height, int depth, RenderTextureFormat format, FilterMode filterMode = FilterMode.Bilinear)
        {
            RenderTexture renderTexture2da = new RenderTexture(width, height, 0, format);
            renderTexture2da.dimension = TextureDimension.Tex2DArray;
            renderTexture2da.wrapMode = TextureWrapMode.Clamp;
            renderTexture2da.filterMode = filterMode;
            renderTexture2da.anisoLevel = 0;
            renderTexture2da.enableRandomWrite = true;
            renderTexture2da.volumeDepth = depth;
            renderTexture2da.antiAliasing = 1;
            renderTexture2da.Create();

            return renderTexture2da;
        }
    }
}