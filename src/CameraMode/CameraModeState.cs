using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace CameraMode
{
    /// <summary>
    /// On/off state and the order things are applied and undone in.
    ///
    /// Each part (<see cref="HudHider"/>, <see cref="ModelHider"/>, <see cref="ScreenEffects"/>,
    /// <see cref="AiStealth"/>, <see cref="GodMode"/>, <see cref="Vitals"/>, <see cref="FreezeAi"/>)
    /// is applied only if its settings ask for it, inside its own try/catch, so one part throwing
    /// never leaves the others half-applied. Changing a setting while Camera Mode is on undoes and
    /// reapplies just that part. <see cref="CameraFov"/>, <see cref="CameraRoll"/>,
    /// <see cref="PhotoEffects"/> and <see cref="Overlay"/> read their settings every frame and
    /// need neither.
    /// </summary>
    internal static class CameraModeState
    {
        /// <summary>
        /// How often the hidden HUD and model are checked again while Camera Mode is on: a weapon
        /// swap or a newly enabled panel shows up within this many seconds.
        /// </summary>
        private const float RefreshInterval = 0.25f;

        private static float _nextRefresh;

        /// <summary>Camera Mode was turned on by holding the key, so letting go turns it off.</summary>
        private static bool _onByHold;

        internal static bool Active { get; private set; }

        /// <summary>The player Camera Mode was turned on for. Null while it is off.</summary>
        internal static Player Player { get; private set; }

        /// <summary>True when there is a living player to turn Camera Mode on for.</summary>
        internal static bool CanTurnOn
        {
            get
            {
                Player player = MainPlayer();
                return player != null && player.HealthController != null && player.HealthController.IsAlive;
            }
        }

        internal static void Toggle()
        {
            if (Active)
            {
                Exit("toggled off", raidEnding: false);
            }
            else
            {
                TryEnter();
            }
        }

        /// <summary>Every frame from the plugin's Update.</summary>
        internal static void Tick()
        {
            ReadKey();

            if (!Active)
            {
                return;
            }

            Player player = Player;
            if (player == null || MainPlayer() != player)
            {
                Exit("raid ended", raidEnding: true);
                return;
            }
            if (player.HealthController == null || !player.HealthController.IsAlive)
            {
                Exit("player died", raidEnding: false);
                return;
            }

            Run("vitals", () => Vitals.Tick(player));

            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshInterval;
                Run("HUD", HudHider.Refresh);
                Run("model", () => ModelHider.Refresh(player));
            }
        }

        /// <summary>Every frame from the plugin's LateUpdate, after the game's own Update.</summary>
        internal static void LateTick()
        {
            if (Active)
            {
                Run("screen effects", ScreenEffects.LateTick);
            }
        }

        /// <summary>
        /// Called from the GameWorld.Dispose patch, while the player and UI still exist, so the
        /// HUD canvases are back on before the next raid reuses them.
        /// </summary>
        internal static void OnRaidEnding()
        {
            Exit("raid ended", raidEnding: true);
            FreezeByKey = false;
            SyncFreeze(raidEnding: true);
        }

        /// <summary>Bots were frozen with the Freeze AI key (rather than by Camera Mode turning on).</summary>
        internal static bool FreezeByKey { get; private set; }

        /// <summary>
        /// The Freeze AI key: freezes every bot, or unfreezes them. Unfreezing also overrides Camera
        /// Mode's "Freeze AI in place" until Camera Mode next turns on.
        /// </summary>
        internal static void ToggleFreeze()
        {
            if (FreezeAi.Engaged)
            {
                FreezeByKey = false;
                _cameraFreezeOverridden = Active;
            }
            else
            {
                if (!CanTurnOn)
                {
                    Toast.Show("Freezing AI works only in raid", force: true);
                    return;
                }
                FreezeByKey = true;
            }
            SyncFreeze();
            Toast.Show(FreezeAi.Engaged ? "AI frozen" : "AI unfrozen");
        }

        /// <summary>The Freeze AI key turned bots back on while Camera Mode wanted them frozen.</summary>
        private static bool _cameraFreezeOverridden;

        /// <summary>Freezes or unfreezes bots to match the Freeze AI key and Camera Mode together.</summary>
        /// <param name="raidEnding">True on raid teardown: the bot brains are left stopped.</param>
        internal static void SyncFreeze(bool raidEnding = false)
        {
            bool want = !raidEnding && (FreezeByKey || (Active && Settings.FreezeAi.Value && !_cameraFreezeOverridden));
            if (want && !FreezeAi.Engaged)
            {
                Run("freeze", FreezeAi.Enter);
            }
            else if (!want && FreezeAi.Engaged)
            {
                Run("freeze", () => FreezeAi.Exit(raidEnding));
            }
        }

        private static float _nextSharedRefresh;

        /// <summary>
        /// Every frame from the plugin's Update, whichever mode is on: getting out of the BTR sets
        /// the damage coefficient back to 1, so godmode puts it back to 0; frozen bots are held, and
        /// a freeze left on after the raid has gone is let go.
        /// </summary>
        internal static void SharedTick()
        {
            Run("godmode", GodMode.Tick);

            if (FreezeAi.Engaged && Time.unscaledTime >= _nextSharedRefresh)
            {
                _nextSharedRefresh = Time.unscaledTime + RefreshInterval;
                if (MainPlayer() == null)
                {
                    FreezeByKey = false;
                    SyncFreeze(raidEnding: true);
                }
                else
                {
                    Run("freeze", FreezeAi.Refresh);
                }
            }
        }

        /// <summary>
        /// Turns AI stealth and godmode on or off to match what Camera Mode and UFO mode want
        /// together: each is on while either mode asks for it. Called whenever either mode turns
        /// on or off, or one of the four settings involved changes.
        /// </summary>
        /// <param name="raidEnding">True on raid teardown: bots are not given the player back.</param>
        internal static void SyncShared(bool raidEnding)
        {
            Player player = Active ? Player : UfoMode.Player;
            bool wantAi = (Active && Settings.AiIgnore.Value) || (UfoMode.Active && Settings.UfoInvisible.Value);
            bool wantGod = (Active && Settings.NoDamage.Value) || (UfoMode.Active && Settings.UfoNoDamage.Value);

            if (AiStealth.Engaged && !wantAi)
            {
                Run("AI", () => AiStealth.Exit(restore: !raidEnding));
            }
            else if (!AiStealth.Engaged && wantAi && player != null)
            {
                Run("AI", () => AiStealth.Enter(player));
            }

            if (GodMode.Engaged && !wantGod)
            {
                Run("godmode", GodMode.Exit);
            }
            else if (!GodMode.Engaged && wantGod && player != null)
            {
                Run("godmode", () => GodMode.Enter(player));
            }
        }

        /// <summary>From ConfigFile.SettingChanged: redo the one part the setting belongs to.</summary>
        internal static void OnSettingChanged(ConfigEntryBase entry)
        {
            Part part = Settings.PartOf(entry);
            if (part == Part.Shared)
            {
                SyncShared(raidEnding: false);
                return;
            }
            if (part == Part.Freeze)
            {
                // Pose freezing is chosen on entry, so a change re-freezes; the switch itself just syncs.
                if (entry == Settings.FreezePoses && FreezeAi.Engaged)
                {
                    Run("freeze", () => FreezeAi.Exit());
                }
                SyncFreeze();
                return;
            }
            if (!Active)
            {
                return;
            }
            Player player = Player;
            switch (part)
            {
                case Part.Hud:
                    Run("HUD", () => { HudHider.Show(); HudHider.Hide(); });
                    break;
                case Part.Model:
                    Run("model", () => { ModelHider.Show(); ModelHider.Hide(player); });
                    break;
                case Part.Screen:
                    Run("screen effects", () => { ScreenEffects.Exit(); ScreenEffects.Enter(); });
                    break;
                case Part.Vitals:
                    Run("vitals", () => { Vitals.Exit(player); Vitals.Enter(player); });
                    break;
            }
        }

        private static void ReadKey()
        {
            if (MenuWindow.IsCapturingKey)
            {
                return;
            }
            KeyboardShortcut key = Settings.ToggleKey.Value;
            if (Settings.HoldToUse.Value)
            {
                bool held = key.MainKey != KeyCode.None && key.IsPressed();
                if (held && !Active && !_onByHold)
                {
                    _onByHold = TryEnter();
                }
                else if (!held && _onByHold)
                {
                    _onByHold = false;
                    Exit("key released", raidEnding: false);
                }
            }
            else
            {
                _onByHold = false;
                if (key.IsDown())
                {
                    Toggle();
                }
            }
        }

        private static Player MainPlayer()
        {
            return Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance.MainPlayer : null;
        }

        private static bool TryEnter()
        {
            if (!CanTurnOn)
            {
                CameraModePlugin.Log.LogInfo("Camera Mode only works with a living player in a raid.");
                Toast.Show("Camera Mode works only in raid");
                return false;
            }
            Enter(MainPlayer());
            return true;
        }

        private static void Enter(Player player)
        {
            Player = player;
            Active = true;

            Run("HUD", HudHider.Hide);
            Run("model", () => ModelHider.Hide(player));
            Run("screen effects", ScreenEffects.Enter);
            SyncShared(raidEnding: false);
            Run("vitals", () => Vitals.Enter(player));
            _cameraFreezeOverridden = false;
            SyncFreeze();

            _nextRefresh = Time.unscaledTime + RefreshInterval;
            CameraModePlugin.Log.LogInfo("Camera Mode on");
            Toast.Show("Camera Mode on");
        }

        /// <param name="raidEnding">
        /// True when the raid is being torn down: bots are not given the player back as an
        /// enemy, since the bot game is going away with the raid.
        /// </param>
        private static void Exit(string reason, bool raidEnding)
        {
            if (!Active)
            {
                return;
            }

            Player player = Player;
            Active = false;
            _onByHold = false;

            // A freeze from the Freeze AI key outlives Camera Mode; Camera Mode's own one does not.
            _cameraFreezeOverridden = false;
            SyncFreeze(raidEnding);
            Run("vitals", () => Vitals.Exit(player));
            // With UFO mode still on, its own AI and damage settings decide what stays.
            SyncShared(raidEnding);
            Run("screen effects", ScreenEffects.Exit);
            Run("model", ModelHider.Show);
            Run("HUD", HudHider.Show);

            Player = null;
            CameraModePlugin.Log.LogInfo($"Camera Mode off ({reason})");
            if (!raidEnding)
            {
                Toast.Show("Camera Mode off");
            }
        }

        private static void Run(string part, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                CameraModePlugin.Log.LogError($"Camera Mode {part}: {e}");
            }
        }
    }
}
