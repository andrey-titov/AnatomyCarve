using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class ScreenshotSaver : MonoBehaviour
{
    public const string FOLDER = "Screenshots/";

    // Update is called once per frame
    void Update()
    {
#if UNITY_EDITOR

        if (Keyboard.current.f10Key.wasPressedThisFrame)
        {
            Debug.Log("Time restarted");
            Time.timeScale = 1.0f;
        }

        if (Keyboard.current.f11Key.wasPressedThisFrame)
        {
            Debug.Log("Time stopped");
            Time.timeScale = 0.0f;
        }

        if (Keyboard.current.f12Key.wasPressedThisFrame)
        {
            CreateScrenshot(FOLDER, "", true, true);

            //string file = FOLDER + DateTime.Now.ToString().Replace(':', '=').Replace('/', '-') + ".png";
            //Debug.Log("File saved as: " + file);
            //ScreenCapture.CaptureScreenshot(file);
        }
#endif
    }

    public static void CreateScrenshot(string folder, string volumeName, bool addTime, bool printSavedMessagde)
    {
        Directory.CreateDirectory(folder);
        string fileName = volumeName + (addTime ? " " + DateTime.Now.ToString("o", CultureInfo.InvariantCulture) : "");
        string fileNameCorrect = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
        string file = Path.Combine(folder, fileNameCorrect + ".png");        
        ScreenCapture.CaptureScreenshot(file);

        if (printSavedMessagde)
        {
            Debug.Log("File saved as: " + file);
        }
    }
}
