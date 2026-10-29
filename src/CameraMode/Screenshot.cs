using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
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
    ///
    /// With a letterbox shape set, the bars are only a preview: the frame inside them is read from
    /// the back buffer at the end of the frame (ReadPixels) and saved on its own, so a 4:3 shot on a
    /// 3440x1440 screen is a 1920x1440 picture, not a 3440x1440 one with black bars.
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

            float ratio = Overlay.Ratio(Settings.LetterboxBars.Value);
            string path = null;
            try
            {
                Directory.CreateDirectory(Folder);
                path = Path.Combine(Folder, $"CameraMode {DateTime.Now:yyyy-MM-dd HH-mm-ss}.png");
                if (ratio <= 0f)
                {
                    ScreenCapture.CaptureScreenshot(path);
                }
            }
            catch (Exception e)
            {
                CameraModePlugin.Log.LogError($"Screenshot failed: {e}");
                path = null;
            }

            yield return new WaitForEndOfFrame();

            if (path != null && ratio > 0f && !SaveFrame(Overlay.Frame(ratio), path))
            {
                path = null;
            }

            yield return null;
            Capturing = false;

            if (path != null)
            {
                CameraModePlugin.Log.LogInfo($"Screenshot saved: {path}");
                Toast.Show("Screenshot saved", force: true);
            }
        }

        /// <summary>Reads the framed part of the finished frame and writes it as a PNG. Must run
        /// after WaitForEndOfFrame, when the back buffer holds the whole picture.</summary>
        private static bool SaveFrame(Rect frame, string path)
        {
            Texture2D texture = null;
            try
            {
                int width = (int)frame.width;
                int height = (int)frame.height;
                // ReadPixels counts y up from the bottom; the GUI frame counts it down from the top.
                var source = new Rect(frame.x, Screen.height - frame.yMax, width, height);
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(source, 0, 0, false);
                texture.Apply(false);
                byte[] png = texture.EncodeToPNG();
                Task.Run(() =>
                {
                    try
                    {
                        File.WriteAllBytes(path, png);
                    }
                    catch (Exception e)
                    {
                        CameraModePlugin.Log.LogError($"Screenshot failed to write {path}: {e}");
                    }
                });
                CameraModePlugin.Log.LogInfo($"Screenshot cropped to {width}x{height} ({Overlay.Label(Settings.LetterboxBars.Value)})");
                return true;
            }
            catch (Exception e)
            {
                CameraModePlugin.Log.LogError($"Screenshot failed: {e}");
                return false;
            }
            finally
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
        }
    }
}
