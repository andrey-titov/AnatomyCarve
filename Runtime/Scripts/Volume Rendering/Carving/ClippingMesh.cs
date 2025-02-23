using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Unity.VisualScripting;
using AnatomyCarve.Runtime;

namespace AnatomyCarve.Runtime
{
    public class ClippingMesh : MonoBehaviour, IObservableScript
    {
        [SerializeField] Material frontMaterial;
        [SerializeField] Material backMaterial;

        new public Camera camera { get; private set; }

        public RenderTexture depthFront { get; private set; }
        public RenderTexture depthBack { get; private set; }

        public SphericalDilation sphericalDilation { get; private set; }

        public const int FBO_RESOLUTION = 1024;

        private readonly Vector3 RENDERING_OFFSET = new Vector3(100, 100, 100);

        public List<Volume> volumes { get; set; } = new List<Volume>();
        public Dictionary<Dilation, CarvingConfiguration> carvingConfigurations { get; set; } = new();

        public delegate void ClippingMeshCreated(ClippingMesh clippingMesh);
        public static event ClippingMeshCreated OnCarvingCreated;

        public delegate void ClippingMeshDestroyed(ClippingMesh clippingMesh);
        public static event ClippingMeshDestroyed OnCarvingDestroyed;

        private VolumeRendering volumeRendering;
        Vector3 carvingCameraDimensions;

        Vector3 firstScale;

        float meshScale = 1f;
        float MeshScale
        {
            get { return meshScale; }
            set 
            {
                camera.nearClipPlane = carvingCameraDimensions.x * value;
                camera.farClipPlane = carvingCameraDimensions.y * value;
                camera.orthographicSize = carvingCameraDimensions.z * value;
                //transform.localScale = new Vector3(value, value, value);
                meshScale = value;
            }            
        }

        private void Awake()
        {
            sphericalDilation = GetComponent<SphericalDilation>();
            sphericalDilation.PreAwake();

            // Depth Render Textures
            depthFront = new RenderTexture(FBO_RESOLUTION, FBO_RESOLUTION, 16, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
            depthBack = new RenderTexture(FBO_RESOLUTION, FBO_RESOLUTION, 16, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);

            depthFront.filterMode = FilterMode.Bilinear;
            depthBack.filterMode = FilterMode.Bilinear;

            // Properly setting camera
            camera = GetComponent<Camera>();
            camera.targetTexture = depthFront;
            camera.enabled = false;
            firstScale = transform.lossyScale;

            carvingCameraDimensions = new Vector3(camera.nearClipPlane, camera.farClipPlane, camera.orthographicSize);

            LoadMeshes();

            Volume.OnVolumeLoaded += OnVolumeLoaded;
            Volume.OnVolumeDestroyed += OnVolumeDestroyed;
            VolumeRendering.AddObservableScript(this);
        }

        private void Start()
        {
            volumeRendering = FindObjectOfType<VolumeRendering>();

            foreach (Volume volume in volumeRendering.volumes)
            {
                GenerateDilationForVolume(volume);
            }

            if (OnCarvingCreated != null)
            {
                OnCarvingCreated(this);
            }            
        }

        private void LateUpdate()
        {
            if (firstScale != transform.lossyScale)
            {
                MeshScale = transform.lossyScale.magnitude / firstScale.magnitude;
            }
        }

        

        public void LoadMeshes()
        {
            RenderToDepthBuffers();

            foreach (var kv in carvingConfigurations)
            {
                Dilation dilationKey = new Dilation(kv.Key.rayStepCountLAO, kv.Key.spacingMagnitude);
                sphericalDilation.DilateAndUnite(dilationKey, depthFront, depthBack, kv.Value);                
            }
        }

        private void OnVolumeLoaded(Volume volume)
        {
            if (!volumes.Contains(volume))
            {
                volumes.Add(volume);
            }

            GenerateDilationForVolume(volume);
        }

        private void OnVolumeDestroyed(Volume volume)
        {
            Dilation dilationKey = new Dilation(volume.rayStepCountLAO, volume.info.spacing.magnitude);

            int volumesSameKey = volumeRendering.volumes
                .Where(v => v != volume)
                .Where(v => v.rayStepCountLAO == dilationKey.rayStepCountLAO && v.info.spacing.magnitude == dilationKey.spacingMagnitude)
                .Count();

            // Destroy configuration if no other volume uses it
            if (volumesSameKey == 0)
            {
                CarvingConfiguration config = carvingConfigurations[dilationKey];
                Destroy(config.dilationFront);
                Destroy(config.dilationBack);
                carvingConfigurations.Remove(dilationKey);
            }
        }

        private void GenerateDilationForVolume(Volume volume)
        {
            // Configuration for current LAO step count
            Dilation dilationKey = new Dilation(volume.rayStepCountLAO, volume.info.spacing.magnitude);

            if (!carvingConfigurations.ContainsKey(dilationKey))
            {
                carvingConfigurations[dilationKey] = sphericalDilation.DilateAndUnite(dilationKey, depthFront, depthBack);
            }
        }


        private void RenderToDepthBuffers()
        {
            MeshRenderer[] subMeshes = GetComponentsInChildren<MeshRenderer>(false).Where(m => LayerMask.LayerToName(m.gameObject.layer) == VolumeRendering.LAYER_VOLUME_CARVING).ToArray();

            transform.position += RENDERING_OFFSET;
            foreach (MeshRenderer mesh in subMeshes)
            {
                mesh.material = frontMaterial;
            }
            camera.targetTexture = depthFront;
            camera.Render();

            GL.invertCulling = true;
            foreach (MeshRenderer mesh in subMeshes)
            {
                mesh.material = backMaterial;
            }
            camera.targetTexture = depthBack;
            camera.Render();
            GL.invertCulling = false;

            transform.position -= RENDERING_OFFSET;
            foreach (MeshRenderer mesh in subMeshes)
            {
                mesh.material = frontMaterial;
            }
            camera.targetTexture = depthFront;

            //Graphics.Blit(depthFront, depthFrontSAMPLE);
        }

        public Matrix4x4 GetProjectionMatrix()
        {
            return GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
        }

        private void OnDestroy()
        {
            if (OnCarvingDestroyed != null)
            {
                OnCarvingDestroyed.Invoke(this);
            }

            Destroy(depthFront);
            Destroy(depthBack);

            ClearEvents();
        }

        public void ClearEvents()
        {
            OnCarvingCreated = delegate { };
            OnCarvingDestroyed = delegate { };
        }
    }

    public class CarvingConfiguration
    {
        public RenderTexture dilationFront { get; set; }
        public RenderTexture dilationBack { get; set; }
    }

}
