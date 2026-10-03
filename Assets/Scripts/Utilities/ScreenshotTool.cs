using System.IO;
using UnityEngine;

namespace Swat
{
    // Press F12 (remappable) to save a screenshot; Shift+F12 takes the whole
    // screenshot tour (ScreenshotTour). In the editor they go to the project's
    // Screenshots folder; in a built game, to the save folder.
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
            Capture("SWAT_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + label, false);
        }

        // Saves <name>.png; quiet skips the "saved" message (the screenshot tour takes many in a row).
        public static void Capture(string name, bool quiet)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string file = name + ".png";
                string path = Path.Combine(Folder, file);
                ScreenCapture.CaptureScreenshot(path);
                if (!quiet) UIManager.Notify("Screenshot saved: " + file, false, 0.3f);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("SWAT: screenshot failed. " + e.Message);
            }
        }
    }
}
