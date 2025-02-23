using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace AnatomyCarve.Runtime
{
    public class ResolutionObservable : MonoBehaviour, IObservableScript
    {
        public delegate void ResolutionChanged(Vector2Int newRes);
        public static event ResolutionChanged OnResolutionChanged;

        private Vector2Int resolution = new Vector2Int(0, 0);

        private void Awake()
        {
            VolumeRendering.AddObservableScript(this);
        }

        // Update is called once per frame
        void Update()
        {
            if (XRSettings.isDeviceActive)
            {
                if (XRSettings.eyeTextureWidth != 0 && XRSettings.eyeTextureHeight != 0
                && (resolution.x != XRSettings.eyeTextureWidth || resolution.y != XRSettings.eyeTextureHeight))
                {
                    resolution = new Vector2Int(XRSettings.eyeTextureWidth, XRSettings.eyeTextureHeight);
                    Debug.Log("XR resolution: " + resolution);

                    if (OnResolutionChanged != null)
                    {
                        OnResolutionChanged(resolution);
                    }
                }
            }
            else
            {
                if (resolution.x != Screen.width || resolution.y != Screen.height)
                {
                    resolution = new Vector2Int(Screen.width, Screen.height);

                    if (OnResolutionChanged != null)
                    {
                        OnResolutionChanged(resolution);
                    }
                }
            }


        }

        private void OnDestroy()
        {
            ClearEvents();
        }

        public void ClearEvents()
        {
            OnResolutionChanged = delegate { };
        }
    }
}