using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace CameraMode
{
    /// <summary>
    /// Saves a PNG of the screen without the Camera Mode menu, message or grid, to the folder the
    /// game itself uses (PreloaderUI.ScreenshotsFolderPath: Documents\Escape from Tarkov\Screenshots).
    ///
    /// ScreenCapture.CaptureScreenshot writes the frame it is called in once that frame is done,
    /// IMGUI included. So <see cref="Capturing"/> is raised one frame ahead, which keeps the menu
    /// out of that frame, and lowered after it.
    /// </summary>
    internal static class Screenshot
    {
        private static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Escape from Tarkov", "Screenshots");

        /// <summary>True while a capture is in progress; the menu, message and grid are not drawn.</summary>
        internal static bool Capturing { get; private set; }

        internal static void Take(MonoBehaviour host)
        {
            if (!Capturing)
            {
                host.StartCoroutine(Capture());
            }
        }

        private static IEnumerator Capture()
        {
            Capturing = true;
            yield return null;

            string path = null;
            try
            {
                Directory.CreateDirectory(Folder);
                path = Path.Combine(Folder, $"CameraMode {DateTime.Now:yyyy-MM-dd HH-mm-ss}.png");
                ScreenCapture.CaptureScreenshot(path);
            }
            catch (Exception e)
            {
                CameraModePlugin.Log.LogError($"Screenshot failed: {e}");
                path = null;
            }

            yield return new WaitForEndOfFrame();
            yield return null;
            Capturing = false;

            if (path != null)
            {
                CameraModePlugin.Log.LogInfo($"Screenshot saved: {path}");
                Toast.Show("Screenshot saved", force: true);
            }
        }
    }
}
