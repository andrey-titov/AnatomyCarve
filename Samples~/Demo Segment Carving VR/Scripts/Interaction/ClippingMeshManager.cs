using AnatomyCarve.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Windows;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

public class ClippingMeshManager : MonoBehaviour, IObservableScript
{
    public InputActionManager inputActionManager;

    public delegate void Loaded(ClippingMesh clippingMesh);
    public static event Loaded OnLoaded = delegate { };

    private void Awake()
    {
        InputActionMap actionMap = inputActionManager.actionAssets[1].FindActionMap("Main");

        VolumeRendering.AddObservableScript(this);
    }

    //private void PlaceCarvingMesh(InputAction.CallbackContext context)
    //{
    //    //Debug.Log("PlaceCarvingMesh");
    //}

    // Start is called before the first frame update
    void Start()
    {
        ClippingMesh clippingMesh = GetComponentInChildren<ClippingMesh>();
        OnLoaded.Invoke(clippingMesh);
    }

    private void OnDestroy()
    {
        ClearEvents();
    }

    public void ClearEvents()
    {
        OnLoaded = delegate { };
    }
}
