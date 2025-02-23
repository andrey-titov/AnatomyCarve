using itk.simple;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace AnatomyCarve.Runtime
{
    public class VolumeReader : MonoBehaviour
    {
        public enum FileLocation
        {
            //Resources,
            StreamingAssets,
            StreamingAssetsSpecific,
            Path,
        }

        public enum RenderFormat
        {
            UINT8 = TextureFormat.R8,
            UINT16 = TextureFormat.R16,
            FLOAT16 = TextureFormat.RHalf,
            FLOAT32 = TextureFormat.RFloat,
        }

        const string VOLUME_FOLDER = "Volumes";
        const string TF_FOLDER = "Transfer Functions";
        const string SEGMENTATIONS_FOLDER = "Segmentations";

        public FileLocation fileLocation = FileLocation.StreamingAssets;
        public RenderFormat renderFormat = RenderFormat.UINT16;
        public bool loadRaw = false;
        public int transferFunction1DLength = 512;

        [SerializeField] string volumeFile;
        [SerializeField] string transferFunctionFile;
        [SerializeField] string segmentationFile;

        private Volume volume;


        private void Awake()
        {
            volume = GetComponent<Volume>();
        }

        private void Start()
        {
            // Read current file if it exists
            if (!string.IsNullOrWhiteSpace(volumeFile)
                && !string.IsNullOrWhiteSpace(transferFunctionFile))
            {
                //TimeMeasuring.Start("Read File" + gameObject.name);
                ReadFile();
                //TimeMeasuring.End("Read File" + gameObject.name);
            }
        }

        public void ReadFile()
        {
            // Read volume file
            string volumeFilePath = GetFileFolder(VOLUME_FOLDER) + volumeFile;
            ImageFileReader readerVolume = new ImageFileReader();
            //reader.SetImageIO("NiftiImageIO");
            readerVolume.SetFileName(volumeFilePath);
            Image fileVolume = readerVolume.Execute();

            // Read property file
            string propertyFilePath = GetFileFolder(TF_FOLDER) + transferFunctionFile;
            TransferFunction transferFunction = new TransferFunction(propertyFilePath, transferFunction1DLength);        

            // Read data file
            VolumeInfo info = ReadVolumeInfo(fileVolume);
            Array voxels = ReadVoxelData(fileVolume, info);      
            Texture3D intensities = CreateDataTexture(voxels, info);

            // Read segmentation
            if (!string.IsNullOrWhiteSpace(segmentationFile))
            {
                string segmentationFilePath = GetFileFolder(SEGMENTATIONS_FOLDER) + segmentationFile;
                ImageFileReader readerSegmentation = new ImageFileReader();
                readerSegmentation.SetFileName(segmentationFilePath);
                Image fileSegmentation = readerSegmentation.Execute();

                VolumeInfo infoSegmentation = ReadVolumeInfo(fileSegmentation);
                Segmentation segmentation = gameObject.GetComponent<Segmentation>();
                segmentation.Load(fileSegmentation, infoSegmentation);

                //segmentation = new Segmentation(fileSegmentation, infoSegmentation);
                segmentation.labelMap = ReadLabelMap(fileSegmentation, infoSegmentation);
                segmentation.GenerateSegmentOpacities();
                segmentation.GenerateSegmentColors();                
                //segmentation.GenerateSegmentColors();
                segmentation.GenerateSegmentHistogram(intensities);
                segmentation.GenerateDilatedLabelMap();
                segmentation.CreateOpacity(intensities);
                segmentation.CreateColor(intensities);
                //segmentation.GenerateSegmentClipMask();
                fileSegmentation.Dispose();
            }

            fileVolume.Dispose();

            volume.LoadVolume(info, intensities, transferFunction);
        }

        private string GetFileFolder(string specificFolder)
        {
            switch (fileLocation)
            {
                //case FileLocation.Resources:
                //    return Application.dataPath + "/Resources";
                case FileLocation.StreamingAssets:
                    return Application.streamingAssetsPath + "/";
                case FileLocation.StreamingAssetsSpecific:
                    return Application.streamingAssetsPath + "/" + specificFolder + "/";
                case FileLocation.Path:
                    return "";
                default:
                    return "";
            }
        }

        private VolumeInfo ReadVolumeInfo(Image file)
        {
            VolumeInfo info = new VolumeInfo();

            // Size
            var imageDimensions = file.GetSize();
            info.dimensions.x = (int)imageDimensions[0];
            info.dimensions.y = (int)imageDimensions[1];
            info.dimensions.z = (int)imageDimensions[2];

            // Spacing
            var imageSpacing = file.GetSpacing();
            info.spacing.x = (float)imageSpacing[0];
            info.spacing.y = (float)imageSpacing[1];
            info.spacing.z = (float)imageSpacing[2];

            // Origin
            var imageOrigin = file.GetOrigin();
            info.origin.x = (float)imageOrigin[0];
            info.origin.y = (float)imageOrigin[0];
            info.origin.z = (float)imageOrigin[0];

            // Pixel Count
            info.voxelCount = (int)file.GetNumberOfPixels();

            if (file.GetNumberOfComponentsPerPixel() > 1)
            {
                throw new Exception("Number of components in medical image bigger than 1 is not supported. Are you perhaps trying to read a segmentation file with more than one layer? If yes, collapse those layers first.");
            }

            // Min-Max values
            MinimumMaximumImageFilter minMaxFilter = new MinimumMaximumImageFilter();
            minMaxFilter.Execute(file);
            info.min = (float)minMaxFilter.GetMinimum();
            info.max = (float)minMaxFilter.GetMaximum();
            minMaxFilter.Dispose();

            return info;
        }

        private Array ReadVoxelData(Image file, VolumeInfo info)
        {
            if (file.GetPixelID() == PixelIDValueEnum.sitkFloat32) // Float
            {
                return ReadImageData<float>(info, file.GetConstBufferAsFloat(), (src, temp) => Marshal.Copy(src, temp, 0, temp.Length));
            }
            else if (file.GetPixelID() == PixelIDValueEnum.sitkFloat64)
            {
                return ReadImageData<double>(info, file.GetConstBufferAsDouble(), (src, temp) => Marshal.Copy(src, temp, 0, temp.Length));
            }
            //else if (file.GetPixelID() == PixelIDValueEnum.sitkInt8) // Int
            //{
            //    return ReadImageData<sbyte>(info, file.GetConstBufferAsInt8(), (src, temp) => Marshal.Copy(src, temp, 0, temp.Length));
            //}
            else if (file.GetPixelID() == PixelIDValueEnum.sitkInt16)
            {
                return ReadImageData<short>(info, file.GetConstBufferAsInt16(), (src, temp) => Marshal.Copy(src, temp, 0, temp.Length));
            }
            else if (file.GetPixelID() == PixelIDValueEnum.sitkInt32)
            {
                return ReadImageData<int>(info, file.GetConstBufferAsInt32(), (src, temp) => Marshal.Copy(src, temp, 0, temp.Length));
            }
            else if (file.GetPixelID() == PixelIDValueEnum.sitkUInt8) // Uint
            {
                return ReadImageData<byte>(info, file.GetConstBufferAsUInt8(), (src, temp) => Marshal.Copy(src, temp, 0, temp.Length));
            }
            else if (file.GetPixelID() == PixelIDValueEnum.sitkUInt16)
            {
                return ReadImageData<ushort>(info, file.GetConstBufferAsUInt16(), (src, temp) =>
                {
                    short[] signed = new short[temp.Length];
                    Marshal.Copy(src, signed, 0, temp.Length);

                    for (int i = 0; i < temp.Length; i++)
                    {
                        temp[i] = unchecked((ushort)signed[i]);
                    }

                    //temp = Array.ConvertAll(signed, b => unchecked((ushort)b));

                    //int a = 0;
                    //for (int i = 0; i < temp.Length; i++)
                    //{
                    //    if (temp[i] != 0)
                    //    {
                    //        a++;
                    //    }
                    //}

                    //Debug.Log("a: " + a);
                });
            }
            //else if (file.GetPixelID() == PixelIDValueEnum.sitkUInt32)
            //{
            //    return ReadImageData<uint>(info, file.GetConstBufferAsUInt32(), (src, temp) => Marshal.Copy(src, temp, 0, temp.Length));
            //}
            else
            {
                throw new Exception($"Reading of format {file.GetPixelID()} is not implemented.");
            }
        }

        private Texture3D CreateDataTexture(Array voxels, VolumeInfo info)
        {
            Texture3D dataTexture = new Texture3D(info.dimensions.x, info.dimensions.y, info.dimensions.z, (TextureFormat)renderFormat, false);
            dataTexture.wrapMode = TextureWrapMode.Clamp;
            dataTexture.filterMode = FilterMode.Bilinear;
            dataTexture.anisoLevel = 0;

            switch (renderFormat)
            {
                case RenderFormat.UINT8:
                    dataTexture.SetPixelData((byte[])voxels, 0);
                    break;
                case RenderFormat.UINT16:
                    dataTexture.SetPixelData((ushort[])voxels, 0);
                    break;
                case RenderFormat.FLOAT16:
                    dataTexture.SetPixelData((ushort[])voxels, 0);
                    break;
                case RenderFormat.FLOAT32:
                    dataTexture.SetPixelData((float[])voxels, 0);
                    break;
                default:
                    break;
            }

            dataTexture.Apply();

            return dataTexture;
        }

        private Array ReadImageData<T>(VolumeInfo info, IntPtr src, Action<IntPtr, T[]> action) where T : IConvertible
        {
            T[] imageData = new T[info.voxelCount];
            action.Invoke(src, imageData);
            Array voxels = null;

            switch (renderFormat)
            {
                case RenderFormat.UINT8:
                    if (loadRaw)
                    {
                        voxels = imageData;
                    }
                    else
                    {
                        byte[] voxelsByte = new byte[info.voxelCount];
                        imageData.Iterate3D(info.dimensions, (x, y, z, i) =>
                        {
                            float rawIntensity = imageData[i].ToSingle(null);
                            float normalizedIntensity = (rawIntensity - info.min) / (info.max - info.min);

                            voxelsByte[i] = (byte)(normalizedIntensity * 255f);
                        });
                        voxels = voxelsByte;
                    }
                    break;
                case RenderFormat.UINT16:
                    if (loadRaw)
                    {
                        voxels = imageData;
                    }
                    else
                    {
                        ushort[] voxelsShort = new ushort[info.voxelCount];
                        imageData.Iterate3D(info.dimensions, (x, y, z, i) =>
                        {
                            float rawIntensity = imageData[i].ToSingle(null);
                            float normalizedIntensity = (rawIntensity - info.min) / (info.max - info.min);

                            voxelsShort[i] = (ushort)(normalizedIntensity * 65535f);
                        });
                        voxels = voxelsShort;
                    }
                    break;
                case RenderFormat.FLOAT16:
                    ushort[] voxelsHalf = new ushort[info.voxelCount];
                    imageData.Iterate3D(info.dimensions, (x, y, z, i) =>
                    {
                        float rawIntensity = imageData[i].ToSingle(null);
                        float normalizedIntensity = (rawIntensity - info.min) / (info.max - info.min);

                        voxelsHalf[i] = Mathf.FloatToHalf(normalizedIntensity);
                    });
                    voxels = voxelsHalf;
                    break;
                case RenderFormat.FLOAT32:
                    float[] voxelsFloat = new float[info.voxelCount];
                    imageData.Iterate3D(info.dimensions, (x, y, z, i) =>
                    {
                        float rawIntensity = imageData[i].ToSingle(null);
                        float normalizedIntensity = (rawIntensity - info.min) / (info.max - info.min);

                        voxelsFloat[i] = normalizedIntensity;
                    });
                    voxels = voxelsFloat;
                    break;
                default:
                    break;
            }

            return voxels;
        }

        

        private Texture3D ReadLabelMap(Image file, VolumeInfo info)
        {
            if (file.GetPixelID() == PixelIDValueEnum.sitkInt16)
            {
                return ReadLabelMapData<short>(info, file.GetConstBufferAsInt16(), TextureFormat.R16, (src, temp) => Marshal.Copy(src, temp, 0, temp.Length));
            }
            //else if (file.GetPixelID() == PixelIDValueEnum.sitkInt32)
            //{
            //    return ReadLabelMapData<int>(info, file.GetConstBufferAsInt32(), (src, temp) => Marshal.Copy(src, temp, 0, temp.Length));
            //}
            else if (file.GetPixelID() == PixelIDValueEnum.sitkUInt8) // Uint
            {
                return ReadLabelMapData<byte>(info, file.GetConstBufferAsUInt8(), TextureFormat.R8, (src, temp) => Marshal.Copy(src, temp, 0, temp.Length));
            }
            else if (file.GetPixelID() == PixelIDValueEnum.sitkUInt16)
            {
                return ReadLabelMapData<ushort>(info, file.GetConstBufferAsUInt16(), TextureFormat.R16, (src, temp) =>
                {
                    short[] signed = new short[temp.Length];
                    Marshal.Copy(src, signed, 0, temp.Length);

                    for (int i = 0; i < temp.Length; i++)
                    {
                        temp[i] = unchecked((ushort)signed[i]);
                    }
                });
            }
            else
            {
                throw new Exception($"Reading of format {file.GetPixelID()} is not implemented for labelmaps.");
            }
        }

        private Texture3D ReadLabelMapData<T>(VolumeInfo info, IntPtr src, TextureFormat textureFormat, Action<IntPtr, T[]> action) where T : IConvertible
        {
            //TimeMeasuring.Start("Image data read");

            T[] imageData = new T[info.voxelCount];
            action.Invoke(src, imageData);

            Color[] voxels = new Color[info.voxelCount];

            //TimeMeasuring.Start("ReadLabelMapData");
            //Dictionary<int, List<Vector3>> labelVoxels = new();
            //imageData.Iterate3D(info.dimensions, (x, y, z, i) =>
            //{
            //    int label = imageData[i].ToInt32(null);
            //    voxels[i].r = label;

            //    if (label != 0)
            //    {
            //        if (!labelVoxels.ContainsKey(label))
            //        {
            //            labelVoxels[label] = new List<Vector3>();
            //        }

            //        //labelVoxels[label].Add(new Vector3(x, y, z));
            //        labelVoxels[label].Add(new Vector3(x, y, z));
            //    }                
            //});
            //TimeMeasuring.End("ReadLabelMapData");

            imageData.Iterate3D(info.dimensions, (x, y, z, i) =>
            {
                voxels[i].r = imageData[i].ToSingle(null);
            });

            Texture3D texture = CreateLabelMapTexture<T>(voxels, info, textureFormat);

            return texture;
        }

        private Texture3D CreateLabelMapTexture<T>(Color[] voxels, VolumeInfo info, TextureFormat textureFormat)
        {
            Texture3D dataTexture = new Texture3D(info.dimensions.x, info.dimensions.y, info.dimensions.z, TextureFormat.RHalf, false);
            dataTexture.wrapMode = TextureWrapMode.Clamp;
            dataTexture.filterMode = FilterMode.Point;
            dataTexture.anisoLevel = 0;
            dataTexture.SetPixels(voxels, 0);
            dataTexture.Apply();

            return dataTexture;
        }
    }
}