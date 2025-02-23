using AnatomyCarve.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

#if AC_USER_STUDY
using static ReplicateStudy;
#endif

public class Logging : MonoBehaviour
{
    public GameObject head;
    public GameObject leftHand;
    public GameObject rightHand;    
    
    Volume volume;
    ClippingMesh clippingMesh;

    StringBuilder logData;

    SegmentCarvingVR segmentCarvingVR;

    public const string LOGGING_FOLDER = "Logging";

    static Logging instance;

    private void Awake()
    {
        if (!enabled)
        {
            return;
        }

        instance = this;
        
        
        Volume.OnVolumeLoaded += OnVolumeLoaded;
        ClippingMesh.OnCarvingCreated += OnCarvingCreated;

#if AC_USER_STUDY
        ReplicateStudy.OnTrialFinished += OnTrialFinished;
#endif

        logData = new StringBuilder();
        segmentCarvingVR = GetComponent<SegmentCarvingVR>();
    }

    private void Start()
    {
        Directory.CreateDirectory(Path.Combine(LOGGING_FOLDER, segmentCarvingVR.config.user));
    }

    private void OnTrialFinished()
    {
        string fileName = volume.name + " " + DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
        string fileNameCorrect = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));

        string filePath = Path.Combine(LOGGING_FOLDER, segmentCarvingVR.config.user, fileNameCorrect + ".js");
        StreamWriter stream = new StreamWriter(filePath);
        stream.Write(logData.ToString());
        stream.Close();
    }

    //private void Read()
    //{
    //    List<SerializedLine> lines = new List<SerializedLine>();

    //    using (StreamReader sr = new StreamReader(Path.Combine(LOGGING_FOLDER, "Cas pratique 2024-07-22T16_42_18.0743700-04_00.txt")))
    //    {
    //        string line;
    //        while ((line = sr.ReadLine()) != null)
    //        {
    //            lines.Add(JsonUtility.FromJson<SerializedLine>(line));
    //        }
    //    }
    //}

    private void OnVolumeLoaded(Volume volume)
    {
        this.volume = volume;
    }

    private void OnCarvingCreated(ClippingMesh clippingMesh)
    {
        this.clippingMesh = clippingMesh;
    }

    // Update is called once per frame
    void Update()
    {
        logData.AppendLine(CreateFrameLine());    
    }

    string CreateFrameLine()
    {
        SerializedLine frame = new SerializedLine
        {
            frame = Time.frameCount,
            time = Time.realtimeSinceStartupAsDouble,
            action = "Update",
            parameters = new Parameters{
                vector3s= new[] { 
                    new KV<Vector3>("head.transform.position", head.transform.position),
                    new KV<Vector3>("leftHand.transform.position", leftHand.transform.position),
                    new KV<Vector3>("rightHand.transform.position", rightHand.transform.position),
                    new KV<Vector3>("volume.transform.position", volume != null? volume.transform.position : Vector3.zero),
                    new KV<Vector3>("volume.transform.localScale", volume != null? volume.transform.localScale : Vector3.zero),
                    new KV<Vector3>("clippingMesh.transform.position", clippingMesh != null? clippingMesh.transform.position : Vector3.zero),
                    new KV<Vector3>("clippingMesh.transform.localScale", clippingMesh != null? clippingMesh.transform.localScale : Vector3.zero),
                },
                quaternions = new[] {
                    new KV<Quaternion>("head.transform.rotation", head.transform.rotation),
                    new KV<Quaternion>("leftHand.transform.rotation", leftHand.transform.rotation),
                    new KV<Quaternion>("rightHand.transform.rotation", rightHand.transform.rotation),
                    new KV<Quaternion>("volume.transform.rotation", volume != null? volume.transform.rotation : Quaternion.identity),
                    new KV<Quaternion>("clippingMesh.transform.rotation", clippingMesh != null? clippingMesh.transform.rotation : Quaternion.identity),
                },
                bools = new[] {
                    new KV<bool>("volumeNotNull", volume != null),
                    new KV<bool>("clippingMeshNotNull", clippingMesh != null),
                }
            }
        };

        //string a = JsonUtility.ToJson(frame);
        //SerializedLine b= JsonUtility.FromJson<SerializedLine>(a);

        return JsonUtility.ToJson(frame);
    }

    //public static void Write(string action, params KV<int>[] parameters)
    //{
    //    Write(action, new Parameters { ints = parameters });
    //}

    public static void Log(string action, Parameters parameters)
    {
        if (instance == null || instance.logData == null)
        {
            return;
        }

        SerializedLine serializedAction = new SerializedLine
        {
            frame = Time.frameCount,
            time = Time.realtimeSinceStartupAsDouble,
            action = action,
            parameters = parameters,
        };

        string line = JsonUtility.ToJson(serializedAction);

        //Debug.Log(line);
        instance.logData.AppendLine(line);
        //instance.stream.Flush();
    }

    private void OnDestroy()
    {
        if (instance == null)
        {
            return;
        }
        
        //stream.Close();
        //stream.Dispose();
        instance = null;
    }

    [System.Serializable]
    public class SerializedLine
    {
        public int frame;
        public double time;
        public string action;
        public Parameters parameters;
    }

    //[System.Serializable]
    //public class SerializedFrame : SerializedLine
    //{
    //    public Vector3 headPoistion;
    //    public Quaternion headRotation;
    //    public Vector3 leftHandPoistion;
    //    public Quaternion leftHandRotation;
    //    public Vector3 rightHandPoistion;
    //    public Quaternion rightHandRotation;
    //}

    //[System.Serializable]
    //public class SerializedAction : SerializedLine
    //{
    //    public string action;
    //    public KV[] parameters;
    //}

    [System.Serializable]
    public class Parameters
    {
        public KV<bool>[] bools;
        public KV<int>[] ints;
        public KV<float>[] floats;
        public KV<string>[] strings;
        public KV<Vector3>[] vector3s;
        public KV<Quaternion>[] quaternions;
        public KV<Texture2DSerializable>[] texture2ds;

        public T Get<T>(string key)
        {
            Type type = typeof(T);

            if (type == typeof(bool))
            {
                return (T)(object)bools.Where(kv=> kv.k == key).Single().v;
            }
            else if (type == typeof(int))
            {
                return (T)(object)ints.Where(kv => kv.k == key).Single().v;
            }
            else if (type == typeof(float))
            {
                return (T)(object)floats.Where(kv => kv.k == key).Single().v;
            }
            else if (type == typeof(string))
            {
                return (T)(object)strings.Where(kv => kv.k == key).Single().v;
            }
            else if (type == typeof(Vector3))
            {
                return (T)(object)vector3s.Where(kv => kv.k == key).Single().v;
            }
            else if (type == typeof(Quaternion))
            {
                return (T)(object)quaternions.Where(kv => kv.k == key).Single().v;
            }
            else if (type == typeof(Texture2DSerializable))
            {
                return (T)(object)texture2ds.Where(kv => kv.k == key).Single().v;
            }
            else
            {
                return default;
            }
        }
    }

    [System.Serializable]
    public class KV<T>
    {
        public KV(string key, T value)
        {
            k = key;
            v = value;            
        }

        public string k;
        public T v;
    }

    [Serializable]
    public class Texture2DSerializable
    {
        public int width;
        public int height;
        public string format;
        public byte[] pixels;

        public Texture2DSerializable(Texture2D texture)
        {
            width = texture.width;
            height = texture.height;
            format = texture.format.ToString();
            pixels = texture.GetRawTextureData();

            //return JsonUtility.ToJson(textureData);
        }

        public Texture2D GetTexture2D()
        { 
            return new Texture2D(width, height, (TextureFormat)Enum.Parse(typeof(TextureFormat), format), false);
        }
    }

    //[System.Serializable]
    //public class KV : KV<string>
    //{
    //    public KV(string key, string value) : base (key, value) { }
    //}
}


