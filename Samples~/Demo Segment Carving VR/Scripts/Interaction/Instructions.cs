using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Instructions : MonoBehaviour
{
    public GameObject controllerLeft;
    public GameObject controllerRight;
    public bool isLaserPointer;

    GameObject attachedController;
    Vector3 startPosition;

    private void Awake()
    {
        startPosition = transform.localPosition;
    }

    private void Start()
    {
        SegmentCarvingVR sceneController = GameObject.Find("Controller").GetComponent<SegmentCarvingVR>();
        
        if (sceneController.config.laserPointerRightHand) 
        {
            attachedController = isLaserPointer ? controllerRight : controllerLeft;
        }
        else
        {
            attachedController = isLaserPointer ? controllerLeft : controllerRight;

            RectTransform rect = GetComponent<RectTransform>();
            if (isLaserPointer) 
            {
                rect.pivot = new Vector2(1, rect.pivot.y);
            }
            else 
            {
                rect.pivot = new Vector2(0, rect.pivot.y);
            }
            
            startPosition = new Vector3(-startPosition.x, startPosition.y, startPosition.z);
        }
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = attachedController.transform.position;
        transform.localPosition += startPosition;
    }
}
