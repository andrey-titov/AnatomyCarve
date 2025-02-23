using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnatomyCarve.Runtime
{
    public class VolumeRendering : MonoBehaviour, IObservableScript
    {        
        [SerializeField] ComputeShader voxelClipping;
        [SerializeField] ComputeShader rayCastLAO;
        [SerializeField] ComputeShader sobelNormals;
        [SerializeField] ComputeShader rayCasting;
        [SerializeField] ComputeShader classificationCompositing;
        [SerializeField] ComputeShader ssao;

        public List<Volume> volumes { get; set; } = new List<Volume>();

        private VolumeCamera volumeCamera;

        private static List<IObservableScript> observableScripts = new List<IObservableScript>();

        public const string LAYER_VOLUME_BOUNDARIES = "Volume Boundaries";
        public const string LAYER_VOLUME_CARVING = "Volume Carving";

        public delegate void VolumeRenderingReady(VolumeRendering volumeRendering);
        public static event VolumeRenderingReady OnVolumeRenderingReady = delegate { };

        void Awake()
        {
            Volume.OnVolumeLoaded += OnVolumeLoaded;
            Volume.OnVolumeDestroyed += OnVolumeDestroyed;
            VolumeRendering.AddObservableScript(this);
        }

        void Start()
        {
            volumeCamera = Camera.main.gameObject.AddComponent<VolumeCamera>();

            volumeCamera.gameObject.AddComponent<VoxelClipping>().shader = voxelClipping;
            volumeCamera.gameObject.AddComponent<SphericalRaycast>().shader = rayCastLAO;
            volumeCamera.gameObject.AddComponent<SobelNormals>().shader = sobelNormals;
            volumeCamera.gameObject.AddComponent<RayCasting>().shader = rayCasting;
            volumeCamera.gameObject.AddComponent<ClassificationCompositing>().shader = classificationCompositing;
            volumeCamera.gameObject.AddComponent<SSAO>().shader = ssao;

            if (OnVolumeRenderingReady != null)
            {
                OnVolumeRenderingReady.Invoke(this);
            }
        }

        private void OnVolumeLoaded(Volume volume)
        {
            if (!volumes.Contains(volume))
            {
                volumes.Add(volume);
            }

            if (volumeCamera != null)
            {
                volume.volumeCamera = volumeCamera;
            }
        }

        private void OnVolumeDestroyed(Volume volume)
        {
            volumes.Remove(volume);
        }

        private void OnDestroy()
        {
            ClearAllEventsInScene();
        }

        public void ClearEvents()
        {
            OnVolumeRenderingReady = delegate { };
        }

        public void ClearAllEventsInScene()
        {
            observableScripts.ForEach(script => script.ClearEvents());
            observableScripts = new List<IObservableScript>();
        }

        public static void AddObservableScript(IObservableScript observableScript)
        {
            observableScripts.Add(observableScript);
        }
    }
}