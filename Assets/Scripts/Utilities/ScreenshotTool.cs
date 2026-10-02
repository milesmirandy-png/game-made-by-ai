using System.IO;
using UnityEngine;

namespace Swat
{
    // Press F12 (remappable) to save a screenshot. In the editor they go to
    // the project's Screenshots folder; in a built game, to the save folder.
    public static class ScreenshotTool
    {
        public static string Folder
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
#else
                return Path.Combine(Application.persistentDataPath, "Screenshots");
#endif
            }
        }

        public static void Capture(string label)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string name = "SWAT_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + label + ".png";
                string path = Path.Combine(Folder, name);
                ScreenCapture.CaptureScreenshot(path);
                UIManager.Notify("Screenshot saved: " + name, false, 0.3f);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("SWAT: screenshot failed. " + e.Message);
            }
        }
    }
}
