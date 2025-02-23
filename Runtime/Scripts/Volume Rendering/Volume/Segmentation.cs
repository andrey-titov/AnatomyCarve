using itk.simple;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Globalization;

namespace AnatomyCarve.Runtime
{
    public class Segmentation : ComputeRoutines, IObservableScript
    {
        public bool useSegmentation = true;
        public ColorRendering colorRendering = ColorRendering.Label;
        public bool opacityChanged { get; private set; } = false;
        public bool colorChanged { get; private set; } = false;
        public bool clipMaskChanged { get; private set; } = false;
        public NewCarvingMaskConfig newCarvingMaskConfig { get; set; } = NewCarvingMaskConfig.CarveAll;

#if AC_INSPECT_TEXTURES
        public Texture3D labelMap;

        // 2D segment maps
        public Texture2D segmentColors;
        public Texture2D segmentOpacities;
        public Texture2D segmentClipMask;
        public RenderTexture segmentHistogram;

        // Volume maps
        public RenderTexture labelMapDilated;
        public RenderTexture opacityMap;
        public RenderTexture colorMap;
        public RenderTexture carvingMap;

        
#else
        public Texture3D labelMap { get; set; }

        // 2D segment maps
        public Texture2D segmentColors { get; set; }
        public Texture2D segmentOpacities { get; set; }
        public Texture2D segmentClipMask { get; set; }
        public RenderTexture segmentHistogram { get; set; }

        // Volume maps
        public RenderTexture labelMapDilated { get; set; }
        public RenderTexture opacityMap { get; set; }
        public RenderTexture colorMap { get; set; }
        public RenderTexture carvingMap { get; set; }
#endif


        // Values from labelmap
        public int maxLabelValue { get; set; }
        //public Dictionary<int, Segment> segments { get; set; }
        public Dictionary<int, Segment> segmentsByLabel { get; set; }

        public string containedRepresentationNames { get; set; }
        public string conversionParameters { get; set; }
        public string masterRepresentation { get; set; }
        public Vector3 referenceImageExtentOffset { get; set; }

        CalculatedChecker carveSegmentsChecker = new CalculatedChecker();
        CalculatedChecker updateOpacityChecker = new CalculatedChecker();
        CalculatedChecker updateColorChecker = new CalculatedChecker();

        public int[][] touchingSegments { get; private set; }

        readonly Color DEFAULT_COLOR = new Color(1, 1, 1, 0);
        readonly Color MASK_VISIBLE = new Color(1, 0, 0, 1);
        readonly Color MASK_INVISIBLE = new Color(0, 0, 0, 1);
        const int NUMBER_OF_BARS = 64;

        private Volume volume;
        public SegmentationCarving carving { get; private set; }

        public delegate void ClipMaskGenerated(Segmentation segmentation);
        public static event ClipMaskGenerated OnClipMaskGenerated = delegate { };

        public delegate void ClipMaskChanged(int label, int carvingIndex, bool visibility);
        public static event ClipMaskChanged OnClipMaskChanged = delegate { };

        public delegate void OpacityChanged(int label, float visibility);
        public static event OpacityChanged OnOpacityChanged = delegate { };

        public enum ColorRendering
        {
            Label,
            Histogram,
            UniqueLabel,
        }

        public enum NewCarvingMaskConfig
        {
            CarveAll,
            CarveNone,
            CopyFirstElseCarveAll,
            CopyFirstElseCarveNone,
            CopyLastElseCarveAll,
            CopyLastElseCarveNone,
        }

        private void Awake()
        {
            InitializeShaders();
            DeepCopyIfClone();

            volume = GetComponent<Volume>();
            carving = GetComponent<SegmentationCarving>();

            VolumeRendering.AddObservableScript(this);
        }

        private void DeepCopyIfClone()
        {
            if (segmentClipMask)
            {
                segmentClipMask = Instantiate(segmentClipMask);
            }
        }

        public bool SegmentationEnabled()
        {
            return useSegmentation && labelMap != null;
        }

        public bool OpacityNeesUpdate()
        {
            return SegmentationEnabled() && opacityChanged;
        }

        public bool ColorNeedsUpdate()
        {
            return SegmentationEnabled() && colorChanged;
        }

        public void Load(Image file, VolumeInfo info)
        {
            maxLabelValue = (int)info.max;

            Dictionary<int, Segment> segmentsByIndex = new();            

            VectorString keys = file.GetMetaDataKeys();

            foreach (string key in keys) 
            {
                if (key.StartsWith("Segmentation"))
                {
                    string line = file.GetMetaData(key);
                    SaveSegmentationParams(key, line);
                }
                else if (key.StartsWith("Segment"))
                {
                    string line = file.GetMetaData(key);
                    FillSegment(segmentsByIndex, key, line);
                }                
            }

            segmentsByLabel = new Dictionary<int, Segment>();
            foreach (var item in segmentsByIndex)
            {
                segmentsByLabel[item.Value.labelValue] = item.Value;
            }
        }

        void SaveSegmentationParams(string key, string line)
        {
            string keySplit = key.Split("Segmentation_", System.StringSplitOptions.RemoveEmptyEntries)[0];

            switch (keySplit) 
            {
                case "ContainedRepresentationNames":
                    containedRepresentationNames = line;
                    break;
                case "ConversionParameters":
                    conversionParameters = line;
                    break;
                case "MasterRepresentation":
                    masterRepresentation = line;
                    break;
                case "ReferenceImageExtentOffset":
                    float[] offset = line.Split(' ').Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                    referenceImageExtentOffset = new Vector3(offset[0], offset[1], offset[2]);
                    break;
            }
        }

        void FillSegment(Dictionary<int, Segment> segmentsByIndex, string key, string line) 
        {
            string[] keySplit = key.Split(new string[] { "Segment", "_" }, System.StringSplitOptions.RemoveEmptyEntries);

            int index = int.Parse(keySplit[0], CultureInfo.InvariantCulture);

            if (!segmentsByIndex.ContainsKey(index))
            {
                segmentsByIndex[index] = new Segment { index = index };
            }

            Segment segment = segmentsByIndex[index];

            switch (keySplit[1])
            {
                case "Color":
                   float[] color = line
                        .Split(' ')
                        .Select(x => float.Parse(x, CultureInfo.InvariantCulture))
                        .ToArray();
                    segment.color = new Color(color[0], color[1], color[2]);
                    break;
                case "ColorAutoGenerated":
                    segment.colorAutoGenerated = int.Parse(line, CultureInfo.InvariantCulture) == 1;
                    break;
                case "Extent":
                    int[] extent = line
                        .Split(' ')
                        .Select(x => int.Parse(x, CultureInfo.InvariantCulture))
                        .ToArray();
                    segment.extentX = new Vector2Int(extent[0], extent[1]);
                    segment.extentY = new Vector2Int(extent[2], extent[3]);
                    segment.extentZ = new Vector2Int(extent[4], extent[5]);
                    break;
                case "ID":
                    segment.id = line;
                    break;
                case "LabelValue":
                    segment.labelValue = int.Parse(line, CultureInfo.InvariantCulture);
                    break;
                case "Layer":
                    segment.layer = int.Parse(line, CultureInfo.InvariantCulture);
                    break;
                case "Name":
                    segment.name = line;
                    break;
                case "NameAutoGenerated":
                    segment.nameAutoGenerated = int.Parse(line, CultureInfo.InvariantCulture) == 1;
                    break;
                case "Tags":
                    segment.tags = line;
                    break;
            }
        }

        // Generate segment 2D maps
        public void GenerateSegmentOpacities(TextureFormat format = TextureFormat.R8)
        {
            segmentOpacities = new Texture2D(maxLabelValue + 1, 1, format, false);
            segmentOpacities.wrapMode = TextureWrapMode.Clamp;
            segmentOpacities.filterMode = FilterMode.Point;
            segmentOpacities.anisoLevel = 0;

            bool[] filledPixels = new bool[segmentOpacities.width];

            foreach (var label in segmentsByLabel.Keys)
            {
                segmentOpacities.SetPixel(label, 0, MASK_VISIBLE);
                filledPixels[label] = true;
            }

            for (int i = 0; i < segmentOpacities.width; i++)
            {
                if (!filledPixels[i])
                {
                    segmentOpacities.SetPixel(i, 0, MASK_INVISIBLE);
                }
            }

            //segmentMask.SetPixel(0, 0, MASK_INVISIBLE);

            segmentOpacities.Apply();
        }

        public void GenerateSegmentColors(TextureFormat format = TextureFormat.ARGB32)
        {
            segmentColors = new Texture2D(maxLabelValue + 1, 1, format, false);
            segmentColors.wrapMode = TextureWrapMode.Clamp;
            segmentColors.filterMode = FilterMode.Point;
            segmentColors.anisoLevel = 0;

            bool[] filledPixels = new bool[segmentColors.width];


            foreach (var segment in segmentsByLabel)
            {
                segmentColors.SetPixel(segment.Key, 0, segment.Value.color);
                filledPixels[segment.Key] = true;
            }

            for (int i = 0; i < segmentColors.width; i++)
            {
                if (!filledPixels[i])
                {
                    segmentColors.SetPixel(i, 0, DEFAULT_COLOR);
                }
            }

            segmentColors.Apply();
        }

        private void GenerateSegmentClipMask()
        {
            int clippingMeshCount = carving.GetActiveCarvingObjects().Count();

            if (clippingMeshCount == 0)
            {
                if (segmentClipMask != null)
                {
                    Destroy(segmentClipMask);
                }
                
                return;
            }

            Texture2D segmentClipMaskOld = segmentClipMask;

            segmentClipMask = new Texture2D(maxLabelValue + 1, clippingMeshCount, TextureFormat.R8, false);
            segmentClipMask.wrapMode = TextureWrapMode.Clamp;
            segmentClipMask.filterMode = FilterMode.Point;
            segmentClipMask.anisoLevel = 0;

            // Copy old rows
            if (segmentClipMaskOld != null)
            {
                int rowsToCopy = Math.Min(segmentClipMaskOld.height, segmentClipMask.height);
                for (int j = 0; j < rowsToCopy; j++)
                {
                    for (int i = 0; i < segmentClipMask.width; i++)
                    {
                        segmentClipMask.SetPixel(i, j, segmentClipMaskOld.GetPixel(i, j));
                    }
                }
            }

            // Create new Rows
            int startNewRow = segmentClipMaskOld != null? segmentClipMaskOld.height : 0;
            for (int j = startNewRow; j < segmentClipMask.height; j++)
            {
                for (int i = 0; i < segmentClipMask.width; i++)
                {
                    switch (newCarvingMaskConfig)
                    {
                        case NewCarvingMaskConfig.CarveAll:
                            segmentClipMask.SetPixel(i, j, new Color(1.0f, 0, 0, 1.0f));
                            break;
                        case NewCarvingMaskConfig.CarveNone:
                            segmentClipMask.SetPixel(i, j, new Color(0.0f, 0, 0, 1.0f));
                            break;
                        case NewCarvingMaskConfig.CopyFirstElseCarveAll:
                            if (segmentClipMaskOld != null)
                            {
                                segmentClipMask.SetPixel(i, j, segmentClipMaskOld.GetPixel(i, 0));
                                break;
                            }
                            goto case NewCarvingMaskConfig.CarveAll;
                        case NewCarvingMaskConfig.CopyFirstElseCarveNone:
                            if (segmentClipMaskOld != null)
                            {
                                segmentClipMask.SetPixel(i, j, segmentClipMaskOld.GetPixel(i, 0));
                                break;
                            }
                            goto case NewCarvingMaskConfig.CarveNone;
                        case NewCarvingMaskConfig.CopyLastElseCarveAll:
                            if (segmentClipMaskOld != null && j < segmentClipMaskOld.height)
                            {
                                segmentClipMask.SetPixel(i, j, segmentClipMaskOld.GetPixel(i, segmentClipMaskOld.height - 1));
                                break;
                            }
                            goto case NewCarvingMaskConfig.CarveAll;
                        case NewCarvingMaskConfig.CopyLastElseCarveNone:
                            if (segmentClipMaskOld != null && j < segmentClipMaskOld.height)
                            {
                                segmentClipMask.SetPixel(i, j, segmentClipMaskOld.GetPixel(i, segmentClipMaskOld.height - 1));
                                break;
                            }
                            goto case NewCarvingMaskConfig.CarveNone;
                    }
                }
            }

            if (segmentClipMaskOld != null)
            {
                Destroy(segmentClipMaskOld);
            }

            segmentClipMask.Apply();

            OnClipMaskGenerated.Invoke(this);
        }

        public void GenerateSegmentHistogram(Texture3D intensities)
        {
            RenderTexture rawHistogram;

            // Calculate number of voxels per segment
            ComputeBuffer voxelsPerSegment = new ComputeBuffer(maxLabelValue + 1, sizeof(int));
            int[] voxelsPerSegmentArray = new int[maxLabelValue + 1];
            voxelsPerSegment.SetData(voxelsPerSegmentArray);

            // Calculate histogram
            {
                Vector3Int dimensions = new Vector3Int(labelMap.width, labelMap.height, labelMap.depth);

                rawHistogram = new RenderTexture(maxLabelValue + 1, NUMBER_OF_BARS + 1, 0, RenderTextureFormat.RInt, RenderTextureReadWrite.Linear);
                rawHistogram.enableRandomWrite = true;
                rawHistogram.Create();

                shader[0].SetVector("Dimensions", new Vector4(dimensions.x, dimensions.y, dimensions.z, 0));
                shader[0].SetTexture(kernel[0], "LabelMap", labelMap);
                shader[0].SetTexture(kernel[0], "Intensities", intensities);
                shader[0].SetTexture(kernel[0], "RawHistogram", rawHistogram);
                shader[0].SetBuffer(kernel[0], "VoxelsPerSegment", voxelsPerSegment);

                ExecuteShader(0, dimensions);
            }

            // Adjust histogram
            {
                segmentHistogram = new RenderTexture(rawHistogram.width, NUMBER_OF_BARS, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
                segmentHistogram.enableRandomWrite = true;
                segmentHistogram.filterMode = FilterMode.Bilinear;
                segmentHistogram.Create();

                shader[1].SetVector("Dimensions", new Vector4(rawHistogram.width, NUMBER_OF_BARS, 0, 0));
                shader[1].SetTexture(kernel[1], "RawHistogram", rawHistogram);
                shader[1].SetTexture(kernel[1], "SegmentHistogram", segmentHistogram);

                ExecuteShader(1, new Vector3Int(rawHistogram.width, NUMBER_OF_BARS, 1));                
            }

            voxelsPerSegment.GetData(voxelsPerSegmentArray);
            voxelsPerSegment.Release();
            foreach (var segment in segmentsByLabel)
            {
                segment.Value.voxelCount = voxelsPerSegmentArray[segment.Key];
            }
            //Debug.Log("Total number of voxels: " + voxelsPerSegmentArray.Sum());

            Destroy(rawHistogram);
        }

        public void GenerateDilatedLabelMap()
        {
            Vector3Int dimensions = new Vector3Int(labelMap.width, labelMap.height, labelMap.depth);

            labelMapDilated = TextureHelper.CreateRenderTexture3D(dimensions, RenderTextureFormat.ARGBFloat, FilterMode.Point);

            shader[2].SetVector("Dimensions", new Vector4(dimensions.x, dimensions.y, dimensions.z, 0));
            shader[2].SetTexture(kernel[2], "LabelMap", labelMap);
            shader[2].SetTexture(kernel[2], "LabelMapDilated", labelMapDilated);

            ExecuteShader(2, dimensions);
        }

        // Easy accessors
        public float GetOpacity(int label)
        {
            return segmentOpacities.GetPixel(label, 0).r;
        }

        public void SetOpacity(int label, float visibility)
        {
            segmentOpacities.SetPixel(label, 0, new Color(visibility, 0, 0, 1));
            opacityChanged = true;
            OnOpacityChanged(label, visibility);
        }

        public Color GetColor(int label)
        {
            return segmentColors.GetPixel(label, 0);
        }

        public void SetColor(int label, Color color)
        {
            segmentColors.SetPixel(label, 0, color);
            colorChanged = true;
        }

        public bool GetClipMask(int label, ClippingMesh carvingCamera)
        {
            int carvingIndex = Array.IndexOf(carving.GetActiveCarvingObjects().ToArray(), carvingCamera);
            return GetClipMask(label, carvingIndex);
        }

        public bool GetClipMask(int label, int carvingIndex)
        {            
            return segmentClipMask.GetPixel(label, carvingIndex).r > 0.5f;
        }

        public void SetClipMask(int label, ClippingMesh carvingCamera, bool visibility)
        {
            int carvingIndex = Array.IndexOf(carving.GetActiveCarvingObjects().ToArray(), carvingCamera);
            SetClipMask(label, carvingIndex, visibility);
        }

        public void SetClipMask(int label, int carvingIndex, bool visibility)
        {
            segmentClipMask.SetPixel(label, carvingIndex, new Color(visibility ? 1f : 0f, 0, 0, 1.0f));
            clipMaskChanged = true;
            OnClipMaskChanged(label, carvingIndex, visibility);
        }

        // Create/update 3D maps
        public void CreateOpacity(Texture3D intensities)
        {
            Vector3Int dimensions = new Vector3Int(labelMap.width, labelMap.height, labelMap.depth);

            opacityMap = TextureHelper.CreateRenderTexture3D(dimensions, RenderTextureFormat.R8, FilterMode.Bilinear);

            UpdateOpacity(intensities);
            updateOpacityChecker.Clear();
        }

        public void UpdateOpacity(Texture3D intensities)
        {
            if (updateOpacityChecker.CalculatedThisFrame())
            {
                return;
            }

            Vector3Int dimensions = new Vector3Int(labelMap.width, labelMap.height, labelMap.depth);

            shader[3].SetVector("Dimensions", new Vector4(dimensions.x, dimensions.y, dimensions.z, 0));
            shader[3].SetTexture(kernel[3], "LabelMap", labelMap);
            shader[3].SetTexture(kernel[3], "Intensities", intensities);
            shader[3].SetTexture(kernel[3], "LabelMapDilated", labelMapDilated);
            shader[3].SetTexture(kernel[3], "SegmentHistogram", segmentHistogram);
            shader[3].SetVector("SegmentHistogramDimensions", new Vector4(segmentHistogram.width, segmentHistogram.height));

            if (carvingMap != null) // && carving.SegmentCarvingEnabled()) //&& clipPerSegment) // TODO: check
            {
                shader[3].EnableKeyword("SEGMENT_CARVING");
                shader[3].SetTexture(kernel[3], "CarvingMap", carvingMap);
            }
            else
            {
                shader[3].DisableKeyword("SEGMENT_CARVING");
            }

            segmentOpacities.Apply();
            shader[3].SetTexture(kernel[3], "SegmentOpacities", segmentOpacities);
            shader[3].SetTexture(kernel[3], "OpacityMap", opacityMap);

            ExecuteShader(3, dimensions);
            opacityChanged = false;
        }

        public void CreateColor(Texture3D intensities)
        {
            Vector3Int dimensions = new Vector3Int(labelMap.width, labelMap.height, labelMap.depth);

            colorMap = TextureHelper.CreateRenderTexture3D(dimensions, RenderTextureFormat.ARGB32, FilterMode.Bilinear);

            UpdateColor(intensities);
            updateColorChecker.Clear();
        }

        public void UpdateColor(Texture3D intensities)
        {
            if (updateColorChecker.CalculatedThisFrame())
            {
                return;
            }

            Vector3Int dimensions = new Vector3Int(labelMap.width, labelMap.height, labelMap.depth);

            shader[4].SetVector("Dimensions", new Vector4(dimensions.x, dimensions.y, dimensions.z, 0));
            shader[4].SetTexture(kernel[4], "LabelMap", labelMap);
            shader[4].SetTexture(kernel[4], "Intensities", intensities);
            shader[4].SetTexture(kernel[4], "LabelMapDilated", labelMapDilated);
            shader[4].SetTexture(kernel[4], "SegmentHistogram", segmentHistogram);
            shader[4].SetVector("SegmentHistogramDimensions", new Vector4(segmentHistogram.width, segmentHistogram.height));

            shader[4].SetTexture(kernel[4], "SegmentOpacities", segmentOpacities);

            segmentColors.Apply();
            shader[4].SetTexture(kernel[4], "SegmentColors", segmentColors);            

            shader[4].SetTexture(kernel[4], "ColorMap", colorMap);

            ExecuteShader(4, dimensions);
            colorChanged = false;
        }

        public void CarveSegments()
        {
            if (carveSegmentsChecker.CalculatedThisFrame())
            {
                return;
            }

            if (carving.UpdateIfNeeded())
            {
                GenerateSegmentClipMask();
            }

            if (!carving.SegmentCarvingEnabled())
            {
                return;
            }

            if (clipMaskChanged)
            {
                segmentClipMask.Apply();
                clipMaskChanged = false;
            }

            int clippingMeshCount = carving.GetActiveCarvingObjects().Count();

            Volume volume = GetComponent<Volume>();

            Vector3Int dimensions = new Vector3Int(labelMap.width, labelMap.height, labelMap.depth);

            shader[5].SetVector("Dimensions", new Vector4(dimensions.x, dimensions.y, dimensions.z, 0));
            shader[5].SetTexture(kernel[5], "LabelMap", labelMap);
            shader[5].SetTexture(kernel[5], "SegmentClipMask", segmentClipMask);

            // Set matrices for clipping
            Matrix4x4[] matrixCarvingMVP = carving.GetCarvingMatrices();
            shader[5].SetMatrixArray("CarvingMatrixMVP", matrixCarvingMVP);

            if (carvingMap == null)
            {
                carvingMap = TextureHelper.CreateRenderTexture3D(dimensions, RenderTextureFormat.R8, FilterMode.Point);
            }

            shader[5].SetTexture(kernel[5], "CarvingMap", carvingMap);

            Dilation dilationKey = volume.GetDilationKey();

            shader[5].SetTexture(kernel[5], "CarvingArray", carving.carvingArray);

            // Finding touching segments
            int width = maxLabelValue + 1;
            int height = clippingMeshCount;
            ComputeBuffer touchingSegmentsBuffer = new ComputeBuffer(width * height, 4, ComputeBufferType.Default);
            shader[5].SetBuffer(kernel[5], "TouchingSegments", touchingSegmentsBuffer);
            shader[5].SetInt("TouchingSegmentsWidth", width);

            ExecuteShader(5, dimensions);

            int[] touchingSegmentsArray = (int[])Array.CreateInstance(typeof(int), width * height);
            touchingSegmentsBuffer.GetData(touchingSegmentsArray);
            touchingSegmentsBuffer.Release();

            touchingSegments = new int[height][];
            for (int c = 0; c < height; c++)
            {
                int touchingCurrentMesh = touchingSegmentsArray.Skip(c * width).Take(width).Where(l => l != 0).Count();
                touchingSegments[c] = new int[touchingCurrentMesh];
                int i = 0;
                for (int l = 0; l < width; l++)
                {
                    if (touchingSegmentsArray[c * width + l] != 0)
                    {
                        touchingSegments[c][i] = l;
                        i++;
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (labelMap)
                Destroy(labelMap);

            // 2D segment maps
            if (segmentColors)
                Destroy(segmentColors);
            if (segmentOpacities)
                Destroy(segmentOpacities);
            if (segmentClipMask)
                Destroy(segmentClipMask);
            if (segmentHistogram)
                Destroy(segmentHistogram);

            // Volume maps
            if (labelMapDilated)
                Destroy(labelMapDilated);
            if (opacityMap)
                Destroy(opacityMap);
            if (colorMap)
                Destroy(colorMap);
            if (carvingMap)
                Destroy(carvingMap);

            ClearEvents();
        }

        public void ClearEvents()
        {
            OnClipMaskGenerated = delegate { };
            OnClipMaskChanged = delegate { };
            OnOpacityChanged = delegate { };
        }
    }    

    public class Segment
    {
        public int index { get; set; }
        public Color color { get; set; }
        public bool colorAutoGenerated { get; set; }
        public Vector2Int extentX { get; set; }
        public Vector2Int extentY { get; set; }
        public Vector2Int extentZ { get; set; }
        public string id { get; set; }
        public int labelValue { get; set; }
        public int layer { get; set; }
        public string name { get; set; }
        public bool nameAutoGenerated { get; set; }
        public string tags { get; set; }
        public int voxelCount { get; set; }
    }
}