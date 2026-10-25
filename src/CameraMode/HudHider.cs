using System;
using System.Collections.Generic;
using EFT.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CameraMode
{
    /// <summary>
    /// Hides the in-raid HUD by switching off the Canvas components that draw it.
    ///
    /// What is hidden, from the game's own UI singletons, one group per setting:
    /// - Battle HUD: CommonUI.EftBattleUIScreen (health, stance, quick slots, interaction prompts
    ///   (ActionPanel), ammo count, fire mode, azimuth, raid time, gestures) and
    ///   PreloaderUI._malfunctionGlow.
    /// - Raid panels: GameUI (extraction timers and panels, exit trigger, PMC count, BTR seats,
    ///   "using" panel, transit panels, death panel) and PreloaderUI._raidStartIntro.
    /// - Notifications: PreloaderUI.NotifierView.
    /// - Overlays: PreloaderUI watermark, version label, FPS counter, network RTT/loss.
    /// - The GameObjects named in the "Extra objects to hide" setting.
    ///
    /// Why canvases and not SetActive or a CanvasGroup: the screen manager shows and hides these
    /// screens with SetActive and fades them through their own CanvasGroup (UIScreen.SoftShow),
    /// so both would be overwritten. Nothing in EFT's UI code sets Canvas.enabled.
    ///
    /// A target that has no Canvas of its own but sits under one is given a nested Canvas, so it
    /// can be switched off without touching its parent, which may also draw menus. It keeps that
    /// Canvas afterwards; a nested Canvas draws exactly like its parent.
    ///
    /// The Escape menu (CommonUI.MenuScreen), the inventory and settings screens, error screens
    /// and the console are never hidden, even if a target turns out to contain them.
    /// </summary>
    internal static class HudHider
    {
        private static readonly List<GameObject> Roots = new List<GameObject>();

        /// <summary>Only canvases that were on when this mod switched them off.</summary>
        private static readonly List<Canvas> HiddenCanvases = new List<Canvas>();

        /// <summary>Extra objects with no Canvas at all, hidden with SetActive instead.</summary>
        private static readonly List<GameObject> Deactivated = new List<GameObject>();

        private static readonly List<Canvas> Scratch = new List<Canvas>();

        internal static void Hide()
        {
            HiddenCanvases.Clear();
            Deactivated.Clear();
            CollectRoots();

            foreach (GameObject root in Roots)
            {
                HideRoot(root, firstPass: true);
            }
            CameraModePlugin.Log.LogInfo(
                $"HUD: {Roots.Count} objects, {HiddenCanvases.Count} canvases hidden, {Deactivated.Count} deactivated");
        }

        /// <summary>Switches off any canvas under the targets that has come on since.</summary>
        internal static void Refresh()
        {
            foreach (GameObject root in Roots)
            {
                if (root != null)
                {
                    HideRoot(root, firstPass: false);
                }
            }
        }

        internal static void Show()
        {
            foreach (Canvas canvas in HiddenCanvases)
            {
                if (canvas != null)
                {
                    canvas.enabled = true;
                }
            }
            foreach (GameObject go in Deactivated)
            {
                if (go != null)
                {
                    go.SetActive(true);
                }
            }
            HiddenCanvases.Clear();
            Deactivated.Clear();
            Roots.Clear();
        }

        private static void CollectRoots()
        {
            Roots.Clear();
            List<Transform> protectedScreens = ProtectedScreens();

            PreloaderUI preloader = MonoBehaviourSingleton<PreloaderUI>.Instantiated
                ? MonoBehaviourSingleton<PreloaderUI>.Instance
                : null;

            if (Settings.HideBattleHud.Value)
            {
                if (MonoBehaviourSingleton<CommonUI>.Instantiated)
                {
                    AddRoot(MonoBehaviourSingleton<CommonUI>.Instance.EftBattleUIScreen, protectedScreens);
                }
                if (preloader != null)
                {
                    AddRoot(preloader._malfunctionGlow, protectedScreens);
                }
            }
            if (Settings.HideRaidPanels.Value)
            {
                if (MonoBehaviourSingleton<GameUI>.Instantiated)
                {
                    AddRoot(MonoBehaviourSingleton<GameUI>.Instance, protectedScreens);
                }
                if (preloader != null)
                {
                    AddRoot(preloader._raidStartIntro, protectedScreens);
                }
            }
            if (Settings.HideNotifications.Value && preloader != null)
            {
                AddRoot(preloader.NotifierView, protectedScreens);
            }
            if (Settings.HideOverlays.Value && preloader != null)
            {
                AddRoot(preloader.ClientWatermark, protectedScreens);
                AddRoot(preloader._alphaVersionLabel, protectedScreens);
                AddRoot(preloader._fpsCounter, protectedScreens);
                AddRoot(preloader._rttObject, protectedScreens);
                AddRoot(preloader._lossObject, protectedScreens);
            }

            foreach (string name in Settings.ExtraObjects.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = name.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }
                // Only finds active objects. Called once per toggle, never on a timer.
                GameObject go = GameObject.Find(trimmed);
                if (go == null)
                {
                    CameraModePlugin.Log.LogWarning($"HUD: extra object '{trimmed}' not found (or not active)");
                    continue;
                }
                AddRoot(go, protectedScreens);
            }
        }

        private static List<Transform> ProtectedScreens()
        {
            var screens = new List<Transform>();
            if (MonoBehaviourSingleton<CommonUI>.Instantiated)
            {
                CommonUI common = MonoBehaviourSingleton<CommonUI>.Instance;
                AddTransform(screens, common.MenuScreen);
                AddTransform(screens, common.InventoryScreen);
                AddTransform(screens, common.SettingsScreen);
                AddTransform(screens, common.ReconnectionScreen);
            }
            if (MonoBehaviourSingleton<PreloaderUI>.Instantiated)
            {
                PreloaderUI preloader = MonoBehaviourSingleton<PreloaderUI>.Instance;
                AddTransform(screens, preloader.ErrorScreen);
                AddTransform(screens, preloader.Console);
                AddTransform(screens, preloader.MenuTaskBar);
            }
            return screens;
        }

        private static void AddTransform(List<Transform> list, Component component)
        {
            if (component != null)
            {
                list.Add(component.transform);
            }
        }

        private static void AddRoot(Component component, List<Transform> protectedScreens)
        {
            if (component != null)
            {
                AddRoot(component.gameObject, protectedScreens);
            }
        }

        private static void AddRoot(GameObject go, List<Transform> protectedScreens)
        {
            if (go == null || Roots.Contains(go))
            {
                return;
            }
            foreach (Transform screen in protectedScreens)
            {
                if (screen.IsChildOf(go.transform))
                {
                    CameraModePlugin.Log.LogWarning($"HUD: not hiding '{go.name}', it contains '{screen.name}'");
                    return;
                }
            }
            Roots.Add(go);
        }

        private static void HideRoot(GameObject root, bool firstPass)
        {
            if (firstPass && root.GetComponent<Canvas>() == null)
            {
                Canvas parent = root.transform.parent != null
                    ? root.transform.parent.GetComponentInParent<Canvas>(true)
                    : null;
                if (parent != null && root.transform is RectTransform)
                {
                    GiveOwnCanvas(root, parent);
                }
            }

            Scratch.Clear();
            root.GetComponentsInChildren(true, Scratch);
            if (Scratch.Count == 0)
            {
                // Not drawn by a canvas at all; only reachable through "Extra objects to hide".
                if (firstPass && root.activeSelf)
                {
                    root.SetActive(false);
                    Deactivated.Add(root);
                }
                return;
            }

            foreach (Canvas canvas in Scratch)
            {
                if (canvas.enabled)
                {
                    canvas.enabled = false;
                    if (!HiddenCanvases.Contains(canvas))
                    {
                        HiddenCanvases.Add(canvas);
                    }
                }
            }
            Scratch.Clear();
        }

        /// <summary>
        /// A nested Canvas on <paramref name="root"/>, set up like <paramref name="parent"/>, so
        /// that switching it off hides only this branch. Text needs the parent's extra shader
        /// channels, and clicks need a raycaster: a parent's GraphicRaycaster does not see
        /// graphics that belong to a nested canvas.
        /// </summary>
        private static void GiveOwnCanvas(GameObject root, Canvas parent)
        {
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.additionalShaderChannels = parent.additionalShaderChannels;
            if (parent.GetComponent<GraphicRaycaster>() != null && root.GetComponent<GraphicRaycaster>() == null)
            {
                root.AddComponent<GraphicRaycaster>();
            }
        }
    }
}
