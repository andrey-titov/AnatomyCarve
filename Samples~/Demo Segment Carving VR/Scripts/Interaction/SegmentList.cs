using AnatomyCarve.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using static ClippingMeshManager;
using static AnatomyCarve.Runtime.Segmentation;
using static SegmentChooser;

#if AC_USER_STUDY
using static CarvingConfigLoader;
#endif

public class SegmentList : MonoBehaviour
{
    public SegmentCarvingVR controller;
    public GameObject sampleItem;

    public Dictionary<int, Item> items;
    Volume volume;
    GameObject panel;

    public static readonly Color NORMAL_COLOR_GREEN = Color.HSVToRGB(0.333f, 0.15f, 1f);
    public static readonly Color HIGHLIGHT_COLOR_GREEN = Color.HSVToRGB(0.333f, 0.15f, 0.94f);
    public static readonly Color PRESSED_COLOR_GREEN = Color.HSVToRGB(0.333f, 0.3f, 0.78f);

    public static readonly Color NORMAL_COLOR_RED = Color.HSVToRGB(0f, 0.15f, 1f);
    public static readonly Color HIGHLIGHT_COLOR_RED = Color.HSVToRGB(0f, 0.15f, 0.94f);
    public static readonly Color PRESSED_COLOR_RED = Color.HSVToRGB(0f, 0.3f, 0.78f);

    private void Awake()
    {
        //sampleItem = transform.GetChild(0);
        sampleItem.gameObject.SetActive(false);
        panel = gameObject.transform.Find("Panel").gameObject;

        Volume.OnVolumeLoaded += OnVolumeLoaded;
        Segmentation.OnClipMaskGenerated += OnClipMaskGenerated;
        Segmentation.OnClipMaskChanged += OnClipMaskChanged;
        Segmentation.OnOpacityChanged += OnOpacityChanged;
        Volume.OnVolumeEnabled += Volume_OnVolumeEnabled;
        Volume.OnVolumeDisabled += Volume_OnVolumeDisabled;

#if AC_USER_STUDY
        CarvingConfigLoader.OnLoaded += OnCarvingConfigLoaded;
#endif
    }



    private void Volume_OnVolumeEnabled(Volume volume)
    {
        if (volume.segmentation.segmentsByLabel != null)
        {
            StartCoroutine(OnVolumeLoadedNextFrame(volume));
        }        
    }

    private IEnumerator OnVolumeLoadedNextFrame(Volume volume)
    {
        yield return null;
        OnVolumeLoaded(volume);
        UpdateLabelMasks();
    }

    private void Volume_OnVolumeDisabled(Volume volume)
    {
        try
        {
            panel.SetActive(false);
            if (items != null)
            {
                items.Values.ToList().ForEach(i => i.Dispose());
                items = null;
            }
        }
        catch
        { 
            
        }
    }

    private void OnVolumeLoaded(Volume volume)
    {
        panel.SetActive(true);

        // Create an item for each segment
        this.volume = volume;

        if (items != null)
        {
            items.Values.ToList().ForEach(i => i.Dispose());
        }

        items = volume.segmentation.segmentsByLabel
            .Select((s, index) => new Item(s.Value, this, index))
            .ToDictionary(i => i.segment.labelValue);

        StartCoroutine(AdjustSize());

        //// Adjust size
        //RectTransform nameRect = nameText.GetComponent<RectTransform>();
        //RectTransform checkboxRect = checkMarkText.GetComponent<RectTransform>();
        //float parentLength = nameRect.anchoredPosition.x * 2f + Mathf.Abs(checkboxRect.anchoredPosition.x); // * 3f + checkboxRect.rect.width;
        //parentLength += nameText.GetComponent<TextMeshProUGUI>().GetRenderedValues().x;
        //Rect parentRect = transform.GetComponent<RectTransform>().rect;
        //parentRect.width = parentLength;
        //transform.GetComponent<RectTransform>().sizeDelta = new Vector2(parentRect.width, parentRect.height);
    }

    IEnumerator AdjustSize()
    {
        yield return null;

        float maxWidth = 0;

        foreach (var item in items.Values) 
        {
            RectTransform nameRect = item.nameText.GetComponent<RectTransform>();
            RectTransform checkboxRect = item.checkMarkText.GetComponent<RectTransform>();
            float currentLength = nameRect.anchoredPosition.x * 2f + Mathf.Abs(checkboxRect.anchoredPosition.x); // * 3f + checkboxRect.rect.width;
            currentLength += item.nameText.GetComponent<TextMeshProUGUI>().GetRenderedValues().x;
            maxWidth = Mathf.Max(maxWidth, currentLength);
            //Rect parentRect = transform.GetComponent<RectTransform>().rect;
            //parentRect.width = parentLength;
            //transform.GetComponent<RectTransform>().sizeDelta = new Vector2(parentRect.width, parentRect.height);
        }

        float height = sampleItem.GetComponent<RectTransform>().rect.height * items.Count;

        foreach (var item in items) 
        {
            item.Value.rectTransform.sizeDelta = new Vector2(maxWidth, item.Value.rectTransform.rect.height);
        }

        RectTransform rectTransform = GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(maxWidth, height);
    }

    void OnClipMaskGenerated(Segmentation segmentation)
    {
        UpdateLabelMasks();
    }

#if AC_USER_STUDY
    void OnCarvingConfigLoaded(CarvingConfigLoader configLoader)
    {
        UpdateLabelMasks();
    }
#endif

    public void OnClipMaskChanged(int label, int carvingIndex, bool visibility)
    {
        items[label].UpdateLabelMask(visibility);
    }

    private void OnOpacityChanged(int label, float visibility)
    {
        items[label].UpdateLabelMask(visibility > 0);
    }

    public void UpdateLabelMasks()
    {
        foreach (var item in items)
        {
            bool mask = volume.segmentation.GetClipMask(item.Key, 0);
            item.Value.UpdateLabelMask(mask);
        }
    }

    public class Item : IDisposable
    {
        public Segment segment { get; private set; }
        public Transform transform { get; private set; }
        public RectTransform rectTransform { get; private set; }
        public Image colorImage { get; private set; }
        public TextMeshProUGUI nameText { get; private set; }
        public GameObject checkMarkText { get; private set; }
        public GameObject xMarkText { get; private set; }
        public Button button { get; private set; }
        public float itemLength { get; set; }

        public Item(Segment segment, SegmentList list, int index)
        {
            // Instantiate item
            this.segment = segment;
            transform = Instantiate(list.sampleItem.transform);
            transform.gameObject.SetActive(true);
            transform.SetParent(list.transform, false);
            colorImage = transform.Find("Color").GetComponent<Image>();
            nameText = transform.Find("Name").GetComponent<TextMeshProUGUI>();
            checkMarkText = transform.Find("Checkbox/Check Mark").gameObject;
            xMarkText = transform.Find("Checkbox/X Mark").gameObject;
            button = transform.GetComponent<Button>();
            var buttonClicked = button.onClick;
            buttonClicked.AddListener(() => list.controller.InvertClipMaskValue(segment.labelValue, true));

            // Set values of the item
            colorImage.color = segment.color;
            nameText.text = segment.name;
            //UpdateLabelMask(volume);

            // Adjust position
            rectTransform = transform.GetComponent<RectTransform>();
            rectTransform.anchoredPosition3D = new Vector3(0, -rectTransform.rect.height * index, 0);
            //rectTransform.
        }

        public void UpdateLabelMask(bool mask)
        {
            //bool mask = volume.segmentation.GetClipMask(segment.labelValue, 0);
            checkMarkText.SetActive(mask);
            xMarkText.SetActive(!mask);

            ColorBlock colorBlock = button.colors;
            colorBlock.normalColor = mask ? NORMAL_COLOR_GREEN : NORMAL_COLOR_RED;
            colorBlock.highlightedColor = mask ? HIGHLIGHT_COLOR_GREEN : HIGHLIGHT_COLOR_RED;
            colorBlock.pressedColor = mask ? PRESSED_COLOR_GREEN : PRESSED_COLOR_RED;
            colorBlock.selectedColor = colorBlock.highlightedColor;
            button.colors = colorBlock;
        }

        public void Dispose()
        {
            Destroy(transform.gameObject);
        }
    }
}
