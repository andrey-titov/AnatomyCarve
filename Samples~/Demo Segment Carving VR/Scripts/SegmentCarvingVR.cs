using AnatomyCarve.Runtime;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit;
using static Logging;
using System.IO;

public class SegmentCarvingVR : MonoBehaviour
{
    public VolumeRendering volumeRendering;
    public InputActionManager inputActionManager;
    public SegmentList segmentList;
    public GameObject leftController;
    public GameObject rightController;
    
    public float nextMeshScaleChange = -0.15f;
    
    public bool clipAll = true;
    public Interaction interaction;
    public bool asyncSegmentChoosing;

    private InputActionAsset inputs;

    ClippingMesh selectedMesh;
    Volume selectedVolume;
    int selectedLabel;
    //ClippingMesh selectedMesh;

    const float MESH_RESIZE_SPEED = 0.5f;
    //const float VOLUME_ROTATE_SPEED = 90.0f;

    float? leftTriggerOffset;
    //Vector2? rightTriggerOffset;
    //Vector3? previousHandPosition;
    //Quaternion? previousHandRotation;
    Vector3? previousPositionLeft;
    Vector3? previousPositionRight;
    Quaternion? previousRotationLeft;
    Quaternion? previousRotationRight;

    float meshScalePower = 0f;
    const float MIN_MESH_SCALE_POWER = -1.5f;
    //public float meshScale = 1f;

    List<int> segmentHidingStack = new();

    public Config config { get; private set; } 

    public const string CONFIG_FILE = "config.js";

    public enum Interaction
    { 
        SegmentHiding,
        Joshi,
        Both,
    }

    private void Awake()
    {
        using (StreamReader reader = new StreamReader(Path.Combine(Application.streamingAssetsPath, CONFIG_FILE)))
        {
            config = JsonUtility.FromJson<Config>(reader.ReadToEnd());
        }        

        inputs = inputActionManager.actionAssets[1];

        InputActionMap actionRight = config.laserPointerRightHand ? inputs.FindActionMap("Point Right Hand") : inputs.FindActionMap("Point Left Hand");
        actionRight.FindAction("Toggle Carving").started += ToggleCarving;
        actionRight.FindAction("Resize Clipping Mesh").started += ResizeClippingMesh;
        actionRight.FindAction("Resize Clipping Mesh").performed += ResizeClippingMesh;
        actionRight.FindAction("Resize Clipping Mesh").canceled += ResizeClippingMesh;
        actionRight.FindAction("Place Clipping Mesh").performed += PlaceClippingMesh;
        actionRight.FindAction("Remove Clipping Mesh").performed += RemoveClippingMesh;
        actionRight.FindAction("Reset Clip Mask").performed += ResetClipMask;
        actionRight.FindAction("Unhide Segment").performed += UnhideSegment;

        InputActionMap actionAmbidexter = inputs.FindActionMap("Ambidexter");
        actionAmbidexter.FindAction("Grip Volume Left").started += GripVolumeLeft;
        actionAmbidexter.FindAction("Grip Volume Left").canceled += GripVolumeLeft;
        actionAmbidexter.FindAction("Grip Volume Right").started += GripVolumeRight;
        actionAmbidexter.FindAction("Grip Volume Right").canceled += GripVolumeRight;

        SegmentChooser.OnSegmentChosen += OnSegmentChosen;
        ClippingMeshManager.OnLoaded += OnClippingMeshChanged;
        Volume.OnVolumeLoaded += OnVolumeLoaded;
        ClippingMesh.OnCarvingCreated += OnCarvingCreated;
    }

    private void OnVolumeLoaded(Volume volume)
    {
        if (selectedVolume == null)
        {
            selectedVolume = volume;
            Logging.Log("OnVolumeLoaded", new Parameters
            {
                strings = new[] { new KV<string>("volume.name", volume.name), new KV<string>("interaction", interaction.ToString()) },
                floats = new[] { new KV<float>("nextMeshScaleChange", nextMeshScaleChange) },
                bools = new[] { new KV<bool>("laserPointerRightHand", config.laserPointerRightHand), new KV<bool>("clipAll", clipAll) },
            });
        }
    }

    private void OnCarvingCreated(ClippingMesh clippingMesh)
    {
        if (selectedMesh == null)
        {
            selectedMesh = clippingMesh;

            if (interaction == Interaction.SegmentHiding)
            {
                meshScalePower = float.MinValue;
                UpdateMeshScale();
            }

            Logging.Log("OnCarvingCreated", new Parameters
            {
                vector3s = new[] {
                    new KV<Vector3>("clippingMesh.transform.position", clippingMesh.transform.position),
                    new KV<Vector3>("clippingMesh.transform.localScale", clippingMesh.transform.localScale),
                },
                quaternions = new[] {
                    new KV<Quaternion>("clippingMesh.transform.rotation", clippingMesh.transform.rotation)
                },
            });
        }
    }

    IEnumerator ResetClipMaskNextFrame(Volume volume)
    {
        yield return null;

        if (volume == selectedVolume)
        {
            if (!clipAll)
            {
                ResetClipMask(new InputAction.CallbackContext());
            }
        }
    }

    private void Start()
    {
        SetMainAndSecondaryHands();

        //SegmentList.Item item = new SegmentList.Item(segmentList);
        //segmentList.Add(item); // TODO
    }

    private void SetMainAndSecondaryHands()
    {
        XRInputModalityManager xr = FindFirstObjectByType<XRInputModalityManager>();
        GameObject carveHand;
        GameObject pointHand;

        if (config.laserPointerRightHand)
        {
            carveHand = xr.leftController;
            pointHand = xr.rightController;
        }
        else 
        {
            carveHand = xr.rightController;
            pointHand = xr.leftController;
        }

        carveHand.GetComponent<ActionBasedControllerManager>().enabled = false;
        pointHand.GetComponent<ActionBasedControllerManager>().enabled = false;

        pointHand.transform.Find("Poke Interactor").gameObject.SetActive(false);

        SegmentChooser segmentChooser = pointHand.GetComponentInChildren<SegmentChooser>();
        segmentChooser.asyncGpuRead = asyncSegmentChoosing;
        segmentChooser.enabled = true;
        carveHand.GetComponentInChildren<ClippingMeshManager>().enabled = true;

        if (!config.laserPointerRightHand) 
        {
            ClippingMesh clippingMesh = pointHand.transform.Find("Ray Interactor").GetChild(0).GetComponent<ClippingMesh>(); // .gameObject.SetActive(true);
            //clippingMesh.gameObject.SetActive(true);
            Vector3 localPosition = clippingMesh.transform.localPosition;
            clippingMesh.transform.parent = carveHand.transform.Find("Ray Interactor");
            clippingMesh.transform.localPosition = new Vector3(-localPosition.x, localPosition.y, localPosition.z);

            // Adjsut side of the segment info panel
            RectTransform rectTransform = segmentChooser.segmentPanel.GetComponent<RectTransform>();
            rectTransform.pivot = new Vector2(1, 0);
        }

        carveHand.GetComponentInChildren<XRRayInteractor>().enabled = false;
        pointHand.GetComponentInChildren<XRRayInteractor>().enabled = true;

        //selectedVolume.GetComponent<SegmentationCarving>().clippingMeshes[0] = clippingMesh;
    }

    private void OnSegmentChosen(Volume volume, int label, Vector3 rayCastHitPos)
    {
        selectedVolume = volume;
        selectedLabel = label;

        Logging.Log("OnSegmentChosen", new Parameters
        {
            ints = new[] { new KV<int>("label", label) }
        });
    }

    private void ToggleCarving(InputAction.CallbackContext context)
    {
        if (selectedLabel == 0)
        {
            return;
        }

        if (interaction == Interaction.Both)
        {
            bool newMask = !selectedVolume.segmentation.GetClipMask(selectedLabel, selectedMesh);
            selectedVolume.segmentation.SetColor(selectedLabel, newMask ? SegmentChooser.SELECTION_CLIPPABLE_COLOR : SegmentChooser.SELECTION_COLOR);
            InvertClipMaskValue(selectedLabel, false);
        }
        else if (interaction == Interaction.SegmentHiding)
        {
            selectedVolume.segmentation.SetOpacity(selectedLabel, 0);
            segmentHidingStack.Add(selectedLabel);
        }
    }

    public void InvertClipMaskValue(int label, bool fromSegmentList)
    {
        if (interaction == Interaction.Both)
        {
            bool newMask = !selectedVolume.segmentation.GetClipMask(label, selectedMesh);
            selectedVolume.segmentation.SetClipMask(label, selectedMesh, newMask);

            Logging.Log("InvertClipMaskValue", new Parameters
            {
                ints = new[] { new KV<int>("label", label) },
                bools = new[] { new KV<bool>("newMask", newMask), new KV<bool>("fromSegmentList", fromSegmentList) },
            });
        }
        else if (interaction == Interaction.SegmentHiding)
        {
            float oldOpacity = selectedVolume.segmentation.GetOpacity(label);
            float newOpacity = oldOpacity > 0.001 ? 0 : 1;
            selectedVolume.segmentation.SetOpacity(label, newOpacity);

            if (newOpacity == 1)
            {
                segmentHidingStack.Remove(label);
            }
            else
            {
                segmentHidingStack.Add(label);
            }
        }
             
        //segmentList.UpdateLabelMask(label);
    }

    private void ResizeClippingMesh(InputAction.CallbackContext context)
    {
        if (interaction != Interaction.Both && interaction != Interaction.Joshi)
        {
            return;
        }

        Vector2 triggerPos = context.ReadValue<Vector2>();

        if (context.started || context.performed)
        {
            leftTriggerOffset = triggerPos.y;

            Logging.Log("ResizeClippingMesh", new Parameters
            {
                bools = new[] { new KV<bool>("pressed", true) },
            });
        }
        if (context.canceled)
        {
            leftTriggerOffset = null;

            Logging.Log("ResizeClippingMesh", new Parameters
            {
                bools = new[] { new KV<bool>("pressed", false) },
            });
        }

        //Debug.Log(context.ReadValue<Vector2>());
    }

    //private void RotateVolume(InputAction.CallbackContext context)
    //{
    //    Vector2 triggerPos = context.ReadValue<Vector2>();

    //    if (context.started || context.performed)
    //    {
    //        rightTriggerOffset = triggerPos;
    //    }
    //    if (context.canceled)
    //    {
    //        rightTriggerOffset = null;
    //    }

    //    //Debug.Log("RotateVolume: " + triggerPos);
    //}

    private void GripVolumeLeft(InputAction.CallbackContext context)
    {
        if (context.started || context.performed)
        {
            previousPositionLeft = leftController.transform.position;
            previousRotationLeft = leftController.transform.rotation;

            Logging.Log("GripVolumeLeft", new Parameters
            {
                bools = new[] { new KV<bool>("pressed", true) },
            });
        }
        if (context.canceled)
        {
            previousPositionLeft = null;
            previousRotationLeft = null;

            Logging.Log("GripVolumeLeft", new Parameters
            {
                bools = new[] { new KV<bool>("pressed", false) },
            });
        }

        //Debug.Log($"GripVolumeLeft: {previousPositionLeft}" );
    }

    private void GripVolumeRight(InputAction.CallbackContext context)
    {
        if (context.started || context.performed)
        {
            previousPositionRight = rightController.transform.position;
            previousRotationRight = rightController.transform.rotation;

            Logging.Log("GripVolumeRight", new Parameters
            {
                bools = new[] { new KV<bool>("pressed", true) },
            });
        }
        if (context.canceled)
        {
            previousPositionRight = null;
            previousRotationRight = null;

            Logging.Log("GripVolumeRight", new Parameters
            {
                bools = new[] { new KV<bool>("pressed", false) },
            });
        }

        //Debug.Log($"GripVolumeRight: {previousPositionRight}" );
    }
    private void PlaceClippingMesh(InputAction.CallbackContext context)
    {
        if (interaction != Interaction.Both && interaction != Interaction.Joshi)
        {
            return;
        }

        PlaceClippingMesh(true);
    }

    public void PlaceClippingMesh(bool changeMeshScale)
    {
        ClippingMesh placedCarving = Instantiate(selectedMesh.gameObject).GetComponent<ClippingMesh>();
        placedCarving.transform.position = selectedMesh.transform.position;
        placedCarving.transform.rotation = selectedMesh.transform.rotation;
        placedCarving.transform.parent = selectedVolume.transform;

        Logging.Log("PlaceClippingMesh", new Parameters
        {
            vector3s = new[] { new KV<Vector3>("placedCarving.transform.position", placedCarving.transform.position), new KV<Vector3>("placedCarving.transform.localScale", placedCarving.transform.localScale) },
            quaternions = new[] { new KV<Quaternion>("placedCarving.transform.rotation", placedCarving.transform.rotation) },
            texture2ds = new[] { new KV<Texture2DSerializable>("selectedVolume.segmentation.segmentClipMask", new Texture2DSerializable(selectedVolume.segmentation.segmentClipMask)) },
        });

        selectedVolume.segmentation.newCarvingMaskConfig = Segmentation.NewCarvingMaskConfig.CopyFirstElseCarveAll;
        selectedVolume.segmentation.carving.clippingMeshes = selectedVolume.segmentation.carving.clippingMeshes.Concat(new ClippingMesh[] { placedCarving }).ToArray();

        //foreach (Volume volume in volumeRendering.volumes)
        //{
        //    volume.segmentation.newCarvingMaskConfig = Segmentation.NewCarvingMaskConfig.CopyFirstElseCarveAll;
        //    volume.segmentation.carving.clippingMeshes = volume.segmentation.carving.clippingMeshes.Concat(new ClippingMesh[] { placedCarving }).ToArray();
        //    //// Copy mask to new carving mesh
        //    //Texture2D segmentClipMask = volume.segmentation.segmentClipMask;
        //    //for (int i = 0; i < volume.segmentation.segmentClipMask.width; i++)
        //    //{
        //    //    Color mask = segmentClipMask.GetPixel(i, 0);
        //    //    segmentClipMask.SetPixel(i, segmentClipMask.height - 1, mask);
        //    //}
        //    //segmentClipMask.Apply();
        //}

        if (changeMeshScale)
        {
            meshScalePower += nextMeshScaleChange;
            meshScalePower = Mathf.Max(meshScalePower, MIN_MESH_SCALE_POWER);
        }
        
        UpdateMeshScale();
    }

    private void RemoveClippingMesh(InputAction.CallbackContext context)
    {
        if (interaction != Interaction.Both && interaction != Interaction.Joshi)
        {
            return;
        }

        SegmentationCarving carving = selectedVolume.segmentation.carving;

        if (carving.clippingMeshes.Length <= 1)
        {
            return;
        }

        ClippingMesh lastMesh = carving.clippingMeshes.Last();

        DestroyImmediate(lastMesh);

        carving.clippingMeshes = carving.clippingMeshes.SkipLast(1).ToArray();

        Logging.Log("RemoveClippingMesh", new Parameters
        {
            texture2ds = new[] { new KV<Texture2DSerializable>("selectedVolume.segmentation.segmentClipMask", new Texture2DSerializable(selectedVolume.segmentation.segmentClipMask)) },
        });
    }

    private void Update()
    {
        // Clipping mesh rescale
        if (leftTriggerOffset.HasValue)
        {
            meshScalePower += leftTriggerOffset.Value * MESH_RESIZE_SPEED * Time.deltaTime;
            meshScalePower = Mathf.Max(meshScalePower, MIN_MESH_SCALE_POWER);
            UpdateMeshScale();
        }

        // Only one hand
        if (previousPositionLeft.HasValue != previousPositionRight.HasValue)
        {
            Vector3 movement;
            Quaternion rotation;
            if (previousPositionLeft.HasValue)
            {
                movement = leftController.transform.position - previousPositionLeft.Value;
                rotation = leftController.transform.rotation * Quaternion.Inverse(previousRotationLeft.Value);
            }
            else
            {
                movement = rightController.transform.position - previousPositionRight.Value;
                rotation = rightController.transform.rotation * Quaternion.Inverse(previousRotationRight.Value);
            }

            // Translation
            selectedVolume.transform.position += movement;

            // Rotation
            //if (rotateAroundItself)
            //{
            //    selectedVolume.transform.rotation = rotation * selectedVolume.transform.rotation;
            //}
            //else
            //{

            Vector3 axis;
            float angle;
            rotation.ToAngleAxis(out angle, out axis);

            if (previousPositionLeft.HasValue)
            {
                selectedVolume.transform.RotateAround(leftController.transform.position, axis, angle);
            }
            else
            {
                selectedVolume.transform.RotateAround(rightController.transform.position, axis, angle);
            }

            //}            
        }

        

        // Both hands
        if (previousPositionLeft.HasValue && previousPositionRight.HasValue)
        {
            // Movement
            Vector3 currentPosition = 0.5f * (leftController.transform.position + rightController.transform.position);
            Vector3 previousPosition = 0.5f * (previousPositionLeft.Value + previousPositionRight.Value);
            Vector3 movement = currentPosition - previousPosition;
            selectedVolume.transform.position += movement;

            // Rotation
            Vector3 currentLine = leftController.transform.position - rightController.transform.position;
            Vector3 previousLine = previousPositionLeft.Value - previousPositionRight.Value;
            Vector3 axis = Vector3.Cross(previousLine, currentLine);
            if (axis.magnitude > Mathf.Epsilon)
            {
                float angle = Vector3.Angle(currentLine, previousLine);
                selectedVolume.transform.RotateAround(selectedVolume.transform.position, axis, angle);
            }

            // Scale
            float scaleChange = currentLine.magnitude / previousLine.magnitude;
            selectedVolume.transform.localScale *= scaleChange;
        }

        // Update positions of controllers
        if (previousPositionLeft.HasValue)
        {
            previousPositionLeft = leftController.transform.position;
            previousRotationLeft = leftController.transform.rotation;
        }
        if (previousPositionRight.HasValue)
        {
            previousPositionRight = rightController.transform.position;
            previousRotationRight = rightController.transform.rotation;
        }

        //// Volume rotation
        //if (rightTriggerOffset.HasValue)
        //{
        //    Vector3 rotation = new Vector3(rightTriggerOffset.Value.y, -rightTriggerOffset.Value.x, 0) * VOLUME_ROTATE_SPEED * Time.deltaTime;
        //    rotation = Camera.main.transform.rotation * rotation;
        //    selectedVolume.transform.rotation = Quaternion.Euler(rotation) * selectedVolume.transform.rotation;
        //}

        //// Volume movement
        //if (previousHandPosition.HasValue)
        //{
        //    Vector3 movement = rightController.transform.position - previousHandPosition.Value;
        //    //Quaternion fromR = previousHandRotation.Value;
        //    //Quaternion toR = rightController.transform.rotation;
        //    //Quaternion rotation =  Quaternion.Inverse(fromR) * toR;
        //    selectedVolume.transform.position += movement;
        //    //selectedVolume.transform.rotation *= rotation;
        //    previousHandPosition = rightController.transform.position;
        //    //previousHandRotation = rightController.transform.rotation;
        //}
    }

    private void UpdateMeshScale()
    {
        float scale = Mathf.Exp(meshScalePower);
        selectedMesh.transform.localScale = new Vector3(scale, scale, scale);

        Logging.Log("UpdateMeshScale", new Parameters
        {
            floats = new[] { new KV<float>("selectedMesh.transform.localScale", scale) }
        });
    }

    //private void OnSegmentClipMaskChanged(Segmentation segmentation)
    //{
    //    if (segmentation.carving.Length > 1)
    //    {
    //        return;
    //    }

    //    Texture2D segmentClipMask = segmentation.segmentClipMask;

    //    for (int i = 0; i < segmentClipMask.width; i++)
    //    {
    //        for (int j = 0; j < segmentClipMask.height; j++)
    //        {
    //            //// Clip with checkboard pattern
    //            //segmentation.SetClipMask(i, j, (i + j) % 2 == 1);

    //            // Clip All
    //            segmentation.SetClipMask(i, j, true);

    //            //// Clip None
    //            //segmentation.SetClipMask(i, j, false);

    //            //// TODO: just for testing
    //            //segmentClipMask.SetPixel(i, j, new Color((i + j) % 2 == 1 ? 1.0f : 0, 0, 0, 1.0f));
    //        }
    //    }
    //}

    private void OnClippingMeshChanged(ClippingMesh clippingMesh)
    {
        StartCoroutine(ResetClipMaskNextFrame(selectedVolume));
        selectedMesh = clippingMesh;
        meshScalePower = 0f;
    }

    private void ResetClipMask(InputAction.CallbackContext context)
    {
        if (interaction != Interaction.Both)
        {
            return;
        }

        Segmentation s = selectedVolume.segmentation;

        // Only invert clip mask if action was performed by the controller
        if (context.action != null)
        {
            clipAll = !clipAll;
        }        

        Texture2D segmentClipMask = selectedVolume.segmentation.segmentClipMask;

        foreach (var segment in selectedVolume.segmentation.segmentsByLabel)
        {
            s.SetClipMask(segment.Key, 0, clipAll);            
        }

        if (selectedLabel != 0)
        {
            selectedVolume.segmentation.SetColor(selectedLabel, clipAll ? SegmentChooser.SELECTION_CLIPPABLE_COLOR : SegmentChooser.SELECTION_COLOR);
        }

        //segmentList.UpdateLabelMasks();

        Logging.Log("ResetClipMask", new Parameters
        {
            bools = new[] { new KV<bool>("clipAll", clipAll) }
        });
    }

    private void UnhideSegment(InputAction.CallbackContext context)
    {
        if (interaction != Interaction.SegmentHiding)
        {
            return;            
        }

        if (segmentHidingStack.Count >= 1)
        {
            InvertClipMaskValue(segmentHidingStack.Last(), false);
        }
    }

    private void OnDestroy()
    {
        ClearActionMap();
    }

    private void ClearActionMap()
    {
        InputActionMap actionRight = config.laserPointerRightHand ? inputs.FindActionMap("Point Right Hand") : inputs.FindActionMap("Point Left Hand");
        actionRight.FindAction("Toggle Carving").started -= ToggleCarving;
        actionRight.FindAction("Resize Clipping Mesh").started -= ResizeClippingMesh;
        actionRight.FindAction("Resize Clipping Mesh").performed -= ResizeClippingMesh;
        actionRight.FindAction("Resize Clipping Mesh").canceled -= ResizeClippingMesh;
        actionRight.FindAction("Place Clipping Mesh").performed -= PlaceClippingMesh;
        actionRight.FindAction("Remove Clipping Mesh").performed -= RemoveClippingMesh;
        actionRight.FindAction("Reset Clip Mask").performed -= ResetClipMask;

        InputActionMap actionAmbidexter = inputs.FindActionMap("Ambidexter");
        actionAmbidexter.FindAction("Grip Volume Left").started -= GripVolumeLeft;
        actionAmbidexter.FindAction("Grip Volume Left").canceled -= GripVolumeLeft;
        actionAmbidexter.FindAction("Grip Volume Right").started -= GripVolumeRight;
        actionAmbidexter.FindAction("Grip Volume Right").canceled -= GripVolumeRight;
    }

    public class Config
    {
        public string user;
        public bool laserPointerRightHand;
    }
}
