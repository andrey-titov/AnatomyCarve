using AnatomyCarve.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using TMPro;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit;

public class SegmentChooser : ComputeRoutine, IObservableScript
{
    public GameObject segmentPanel;
    public bool rightSide;
    public bool asyncGpuRead { get; set; }

    XRInteractorLineVisual lineVisual;
    LineRenderer lineRenderer;
    float savedLineLength;
    float savedMinLineLength;
    ClippingMesh selectedMesh;

    Volume previousVolume;
    int previousLabel;
    Color previousLabelColor;
    ComputeBuffer raycastHitBuffer;
    float[] raycastHitLabelArray = new float[RAYCAST_HIT_BUFFER_SIZE];
    public const int RAYCAST_HIT_BUFFER_SIZE = 4;
    public static readonly Color SELECTION_COLOR = Color.red;
    public static readonly Color SELECTION_CLIPPABLE_COLOR = Color.green;
    public const float LABEL_RAY_RATIO = 0.5f;

    public delegate void SegmentChosen(Volume volume, int label, Vector3 rayCastHitPos);
    public static event SegmentChosen OnSegmentChosen = delegate { };
    public static event SegmentChosen OnVolumePointed = delegate { };


    private void Awake()
    {
        InitializeShader();
        lineVisual = GetComponent<XRInteractorLineVisual>();     
        lineRenderer = GetComponent<LineRenderer>();

        
    }

    // Start is called before the first frame update
    void Start()
    {
        ClippingMeshManager.OnLoaded += OnClippingMeshChanged;
        VolumeRendering.AddObservableScript(this);

        savedLineLength = lineVisual.lineLength;
        savedMinLineLength = lineVisual.minLineLength;

        raycastHitBuffer = new ComputeBuffer(RAYCAST_HIT_BUFFER_SIZE, sizeof(float), ComputeBufferType.Default);
        float[] initialData = new float[RAYCAST_HIT_BUFFER_SIZE];
        raycastHitBuffer.SetData(initialData);


        segmentPanel.SetActive(false);
    }

    private void OnClippingMeshChanged(ClippingMesh clippingMesh)
    {
        selectedMesh = clippingMesh;
    }

    // Update is called once per frame
    void Update()
    {
        //lineVisual.autoAdjustLineLength = true;
        //lineVisual.minLineLength = Time.timeSinceLevelLoad * 0.1f;
        float newRayLength;
        int raycastHitLabel;
        Volume volume;
        Vector3 rayCastHitPos;
        if (!RayHitsSegment(out newRayLength, out raycastHitLabel, out volume, out rayCastHitPos)) 
        {
            lineVisual.autoAdjustLineLength = true;
            lineVisual.lineLength = savedLineLength;
            lineVisual.minLineLength = savedMinLineLength;
            segmentPanel.SetActive(false);

            if (previousVolume)
            {
                previousVolume.segmentation.SetColor(previousLabel, previousLabelColor);

                bool previousLabelZero = previousLabel == 0;

                previousLabel = 0;

                if (!previousLabelZero)
                {
                    OnVolumePointed.Invoke(previousVolume, previousLabel, rayCastHitPos);
                    OnSegmentChosen.Invoke(previousVolume, previousLabel, rayCastHitPos);
                }                
            }
            
            return;
        }

        if (volume.segmentation.segmentClipMask == null)
        {
            return;
        }

        lineVisual.autoAdjustLineLength = false;
        lineVisual.lineLength = newRayLength;
        lineVisual.minLineLength = newRayLength;
        ApplyLabel(volume, raycastHitLabel);

        OnVolumePointed.Invoke(previousVolume, raycastHitLabel, rayCastHitPos);

        // Nothing changed from last time
        if (raycastHitLabel == previousLabel 
            || volume.segmentation.carvingMap == null 
            || !volume.segmentation.carving.SegmentCarvingEnabled()
            )
        {
            return;
        }

        previousVolume = volume;
        volume.segmentation.SetColor(previousLabel, previousLabelColor);
        previousLabel = raycastHitLabel;
        previousLabelColor = volume.segmentation.GetColor(raycastHitLabel);

        bool mask = volume.segmentation.GetClipMask(raycastHitLabel, selectedMesh);
        volume.segmentation.SetColor(raycastHitLabel, mask ? SELECTION_CLIPPABLE_COLOR : SELECTION_COLOR);

        segmentPanel.SetActive(true);
        

        OnSegmentChosen.Invoke(volume, raycastHitLabel, rayCastHitPos);
    }

    bool RayHitsSegment(out float newRayLength, out int raycastHitLabel, out Volume volume, out Vector3 rayCastHitPos)
    {
        newRayLength = 0;
        raycastHitLabel = 0;
        volume = null;
        rayCastHitPos = Vector3.zero;

        int positionCount = lineRenderer.positionCount;

        if (positionCount < 2) 
        {            
            return false;
        }

        Vector3 origin = lineRenderer.GetPosition(0);
        Vector3 end = lineRenderer.GetPosition(positionCount - 1);
        Vector3 direction = end - origin;

        Vector3 originAdj = origin - direction.normalized;

        int mask = LayerMask.GetMask("Volume Boundaries");
        RaycastHit raycastHit;
        if (!Physics.Raycast(originAdj, direction, out raycastHit, Mathf.Infinity, mask))
        {
            // Ray cast didn't hit the volume
            return false;
        }

        volume = raycastHit.collider.GetComponentInParent<Volume>();

        Matrix4x4 localToWorld = volume.raycastedVolume.transform.GetComponent<Renderer>().localToWorldMatrix;
        Matrix4x4 worldToLocal = localToWorld.inverse;
        Vector4 positionW = raycastHit.point;
        positionW.w = 1;

        Vector4 rayHitL = worldToLocal * positionW;
        rayHitL /= rayHitL.w;
        Vector3 colorRayHit = ((Vector3)rayHitL) + new Vector3(0.5f, 0.5f, 0.5f);

        Vector4 originL = worldToLocal * new Vector4(origin.x, origin.y, origin.z, 1f);
        originL /= originL.w;
        Vector3 colorOrigin = ((Vector3)originL) + new Vector3(0.5f, 0.5f, 0.5f);

        Vector3 colorPosition = colorRayHit;

        float smallValue = 0.0001f;
        float smallerThan1 = 1f - smallValue;

        if (colorOrigin.x > smallValue && colorOrigin.y > smallValue && colorOrigin.z > smallValue
            && colorOrigin.x < smallerThan1 && colorOrigin.y < smallerThan1 && colorOrigin.z < smallerThan1)
        {
            colorPosition = colorOrigin;
        }

        Vector4 directionL = worldToLocal * direction;
        //directionL /= directionL.w;
        directionL.Normalize();

        float sampleStepSize = volume.raySamplingStep * volume.sampleStepSize;
        int numberOfSamples = (int)(Mathf.Sqrt(3) / sampleStepSize);

        //Matrix4x4[] carvingMatrices = volume.segmentation.GetCarvingMatrices();

        if (!CastRay(volume, colorPosition, (Vector3)directionL * sampleStepSize, numberOfSamples,  localToWorld, out rayCastHitPos, out raycastHitLabel))
        {
            return false;
        }

        //Debug.Log("rayCastHitW: " + rayCastHitW);

        Vector3 difference = rayCastHitPos - origin;
        newRayLength = difference.magnitude;// - 1f;

        if (newRayLength < smallValue) //Vector3.Dot(difference, direction) < 0f)
        {
            newRayLength = 0.001f;

            // Would make the pointer not select if it is inside a segment
            //return false; 
        }

        //Adjust segment label
        Vector3 newRayEnd = origin + direction.normalized * newRayLength;        
        Vector3 segmentLabelPos = new Vector3();
        segmentLabelPos.x = Mathf.Lerp(origin.x, newRayEnd.x, LABEL_RAY_RATIO);
        segmentLabelPos.y = Mathf.Lerp(origin.y, newRayEnd.y, LABEL_RAY_RATIO);
        segmentLabelPos.z = Mathf.Lerp(origin.z, newRayEnd.z, LABEL_RAY_RATIO);
        segmentPanel.transform.position = segmentLabelPos;
        //Debug.Log(segmentLabelPos);

        return true;
    }

    bool CastRay(Volume volume, Vector3 colorPosition, Vector3 directionStep, int numberOfSamples, Matrix4x4 localToWorld, out Vector3 rayCastHitPos, out int raycastHitLabel)
    {

        shader.SetTexture(kernel, "LabelMap", volume.segmentation.labelMap);
        shader.SetTexture(kernel, "SegmentOpacities", volume.segmentation.segmentOpacities);

        if (volume.segmentation.carvingMap != null && volume.segmentation.carving.SegmentCarvingEnabled()) //&& clipPerSegment) 
        {
            shader.EnableKeyword("SEGMENT_CARVING");
            shader.SetTexture(kernel, "CarvingMap", volume.segmentation.carvingMap);
            //Debug.Log("1");
        }
        else
        {
            shader.DisableKeyword("SEGMENT_CARVING");
            //Debug.Log("2");
        }


        shader.SetBuffer(kernel, "RaycastHit", raycastHitBuffer);

        shader.SetInt("StepsCount", numberOfSamples);
        shader.SetVector("StartPosition", colorPosition);
        shader.SetVector("DirectionStep", directionStep);

        ExecuteShader(1, 1, 1);

        // Read chosen segment from GPU
        if (asyncGpuRead)
        {
            AsyncGPUReadback.Request(raycastHitBuffer, GetOnCompleteReadback);
        }
        else 
        {
            raycastHitBuffer.GetData(raycastHitLabelArray);
        }
        
        //raycastHitBuffer.Release();
        raycastHitLabel = (int)raycastHitLabelArray[3];

        Vector4 rayCastHitW = localToWorld * new Vector4(raycastHitLabelArray[0], raycastHitLabelArray[1], raycastHitLabelArray[2], 1f);
        rayCastHitW /= rayCastHitW.w;
        rayCastHitPos = rayCastHitW;

        //Debug.Log($"{raycastHitLabelArray[3]}: ({raycastHitLabelArray[0]}, {raycastHitLabelArray[1]}, {raycastHitLabelArray[2]})");

        return raycastHitLabel != 0;
    }

    private void GetOnCompleteReadback(AsyncGPUReadbackRequest request)
    {
        var data = request.GetData<float>();
        data.CopyTo(raycastHitLabelArray);
    }

    void ApplyLabel(Volume volume, int label)
    {
        Transform colorTransform = segmentPanel.transform.Find("Color");
        colorTransform.GetComponent<UnityEngine.UI.Image>().color = volume.segmentation.segmentsByLabel[label].color;
        Transform nameTransform = segmentPanel.transform.Find("Name");
        nameTransform.GetComponent<TextMeshProUGUI>().text = volume.segmentation.segmentsByLabel[label].name;

        bool mask = volume.segmentation.GetClipMask(label, selectedMesh);

        Transform checkboxTransform = segmentPanel.transform.Find("Checkbox");
        checkboxTransform.Find("Check Mark").gameObject.SetActive(mask);
        checkboxTransform.Find("X Mark").gameObject.SetActive(!mask);

        // Adjust length
        RectTransform nameRect = nameTransform.GetComponent<RectTransform>();
        RectTransform checkboxRect = checkboxTransform.GetComponent<RectTransform>();

        float parentLength = nameRect.anchoredPosition.x * 2f + Mathf.Abs(checkboxRect.anchoredPosition.x); // * 3f + checkboxRect.rect.width;
        parentLength += nameTransform.GetComponent<TextMeshProUGUI>().GetRenderedValues().x;
        Rect parentRect = segmentPanel.GetComponent<RectTransform>().rect;
        parentRect.width = parentLength;
        segmentPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(parentRect.width, parentRect.height);
    }

    private void OnDestroy()
    {
        ClearEvents();

        if (raycastHitBuffer != null)
        {
            raycastHitBuffer.Release();
        }
    }

    public void ClearEvents()
    {
        OnSegmentChosen = delegate { };
    }
}
