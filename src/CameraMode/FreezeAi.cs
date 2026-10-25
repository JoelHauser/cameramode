using System.Collections.Generic;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace CameraMode
{
    /// <summary>
    /// Freezes every bot in place: by the Freeze AI key, or while Camera Mode is on with
    /// "Freeze AI in place".
    ///
    /// From the game's own code, a bot runs in two places, and both are stopped:
    /// - Its body and senses: BotOwner.UpdateManual (called for each bot by BotsList) and
    ///   BotOwner.FixedUpdate: looking, Mover (walking), Steering (turning), ShootData, BotTalk,
    ///   Medecine. The <see cref="Patches"/> skip both while this is engaged.
    /// - Its brain: BotsController.AICoreController runs every bot's decision agent, including
    ///   the layers AI mods add, and has its own Stop/Activate, which the game uses at raid end.
    ///   It is stopped here too. Without that (0.3.0 to 0.5.0), brains kept issuing orders to
    ///   frozen bodies, and ORBIT, whose whole tick is a postfix on AICoreController.Update that
    ///   returns when it is stopped, kept steering them, sliding its "ghost" bots along and
    ///   teleporting bots its stuck detection thought were stuck. That looked like superspeed.
    ///   It is started again only if this stopped it, and never during raid teardown.
    ///
    /// What the bot already started is stopped once on entry: BotOwner.StopMove, Player.Move(zero)
    /// (Player.Move stores the direction until the next call, which would otherwise never come),
    /// and the trigger released through IFirearmHandsController.SetTriggerPressed(false).
    ///
    /// "Freeze AI poses" also sets each bot's body and arms animator (Player.BodyAnimatorCommon,
    /// ArmsAnimatorCommon) to speed 0, so they hold their pose mid-step. The game only changes
    /// animator speed in a couple of one-off places (sprint-to-idle, hands operations), so it is
    /// set again on every refresh, and the speed each animator had is put back.
    /// </summary>
    internal static class FreezeAi
    {
        private static readonly HashSet<BotOwner> Frozen = new HashSet<BotOwner>();
        private static readonly Dictionary<IAnimator, float> SavedSpeeds = new Dictionary<IAnimator, float>();

        private static bool _freezePoses;

        /// <summary>The bot brain runner this freeze stopped, to start again on unfreeze.</summary>
        private static AICoreController _pausedBrains;

        /// <summary>True while bots are frozen; the BotOwner patches check this.</summary>
        internal static bool Engaged { get; private set; }

        internal static void Enter()
        {
            Frozen.Clear();
            SavedSpeeds.Clear();
            _freezePoses = Settings.FreezePoses.Value;
            Engaged = true;

            BotsController bots = Singleton<IBotGame>.Instantiated ? Singleton<IBotGame>.Instance.BotsController : null;
            AICoreController brains = bots?.AICoreController;
            if (brains != null && brains._enable)
            {
                brains.Stop();
                _pausedBrains = brains;
            }

            Refresh();
            CameraModePlugin.Log.LogInfo(
                $"Freeze: {Frozen.Count} bots frozen{(_freezePoses ? " with their poses" : "")}, brains {(_pausedBrains != null ? "paused" : "were already stopped")}");
        }

        /// <summary>Picks up bots that spawned since, and holds the animators at speed 0.</summary>
        internal static void Refresh()
        {
            if (!Engaged)
            {
                return;
            }
            BotsController bots = Singleton<IBotGame>.Instantiated ? Singleton<IBotGame>.Instance.BotsController : null;
            if (bots == null)
            {
                return;
            }
            foreach (BotOwner bot in bots.Bots.BotOwners)
            {
                if (bot == null || bot.GetPlayer == null)
                {
                    continue;
                }
                if (Frozen.Add(bot))
                {
                    Stop(bot);
                }
                if (_freezePoses)
                {
                    Hold(bot.GetPlayer.BodyAnimatorCommon);
                    Hold(bot.GetPlayer.ArmsAnimatorCommon);
                }
            }
        }

        /// <param name="raidEnding">
        /// True on raid teardown: the game stops the brains itself then, so they are left stopped.
        /// </param>
        internal static void Exit(bool raidEnding = false)
        {
            if (!Engaged)
            {
                return;
            }
            Engaged = false;

            AICoreController brains = _pausedBrains;
            _pausedBrains = null;
            if (brains != null && !raidEnding && !brains._enable)
            {
                brains.Activate();
            }
            foreach (KeyValuePair<IAnimator, float> entry in SavedSpeeds)
            {
                // IAnimator wraps a Unity Animator; a destroyed bot's wrapper throws or reads null.
                try
                {
                    if (entry.Key != null)
                    {
                        entry.Key.speed = entry.Value;
                    }
                }
                catch (System.Exception)
                {
                }
            }
            SavedSpeeds.Clear();
            Frozen.Clear();
        }

        private static void Stop(BotOwner bot)
        {
            try
            {
                bot.StopMove();
                Player player = bot.GetPlayer;
                player.Move(Vector2.zero);
                if (player.HandsController is IFirearmHandsController firearm)
                {
                    firearm.SetTriggerPressed(false);
                }
            }
            catch (System.Exception e)
            {
                CameraModePlugin.Log.LogWarning($"Freeze: could not stop a bot: {e.Message}");
            }
        }

        private static void Hold(IAnimator animator)
        {
            if (animator == null)
            {
                return;
            }
            if (!SavedSpeeds.ContainsKey(animator))
            {
                SavedSpeeds[animator] = animator.speed;
            }
            if (animator.speed != 0f)
            {
                animator.speed = 0f;
            }
        }
    }
}
