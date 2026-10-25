using System.Collections.Generic;
using Comfort.Common;
using EFT;

namespace CameraMode
{
    /// <summary>
    /// Makes every bot ignore the player while Camera Mode is on.
    ///
    /// How bots relate to a player, from the game's own code: each BotsGroup keeps the players it
    /// is hostile to in <c>Enemies</c> and the ones it tolerates in <c>Neutrals</c>. Every way a
    /// player becomes an enemy (spawn, being seen, being shot, boss and BTR logic) ends in
    /// BotsGroup.AddEnemy (CheckAndAddEnemy calls it too), and a bot only aims at players its
    /// group holds as enemies. BotHearingSensor.OnSoundPlayedInner drops any sound from a
    /// player who is in neither list, so the player is silent to them too.
    ///
    /// So: on entry the player is removed from every group (BotsGroup.RemoveEnemy also clears
    /// Neutrals and each member's memory of the player), and while Camera Mode is on the
    /// AddEnemy and AddNeutral patches refuse the player and note which group asked, with what
    /// cause. On exit each noted group gets the player back the same way, so bots can see and
    /// fight you again as before.
    /// </summary>
    internal static class AiStealth
    {
        /// <summary>Groups that held, or tried to add, the player as an enemy, with the cause.</summary>
        private static readonly Dictionary<BotsGroup, EBotEnemyCause> EnemyOf = new Dictionary<BotsGroup, EBotEnemyCause>();

        /// <summary>Groups that held, or tried to add, the player as a neutral.</summary>
        private static readonly HashSet<BotsGroup> NeutralOf = new HashSet<BotsGroup>();

        /// <summary>True while bots are kept from seeing the player; the patches check this.</summary>
        internal static bool Engaged { get; private set; }

        /// <summary>The player being hidden. Camera Mode and UFO mode share this part.</summary>
        private static Player _player;

        internal static void Enter(Player player)
        {
            EnemyOf.Clear();
            NeutralOf.Clear();
            _player = player;
            Engaged = true;

            BotsController bots = Singleton<IBotGame>.Instantiated ? Singleton<IBotGame>.Instance.BotsController : null;
            if (bots == null)
            {
                CameraModePlugin.Log.LogInfo("AI: no bot game running");
                return;
            }

            var groups = new HashSet<BotsGroup>();
            foreach (BotOwner bot in bots.Bots.BotOwners)
            {
                if (bot == null)
                {
                    continue;
                }
                if (bot.BotsGroup != null)
                {
                    groups.Add(bot.BotsGroup);
                }
                // Safe when the bot has no record of the player: it checks first.
                bot.Memory?.DeleteInfoAboutEnemy(player);
            }

            foreach (BotsGroup group in groups)
            {
                Forget(group, player);
            }
            CameraModePlugin.Log.LogInfo(
                $"AI: player removed from {EnemyOf.Count} enemy and {NeutralOf.Count} neutral lists ({groups.Count} groups)");
        }

        /// <param name="restore">False when the raid is ending; the bookkeeping is just dropped.</param>
        internal static void Exit(bool restore)
        {
            if (!Engaged)
            {
                return;
            }
            // Off first, so the AddEnemy patch lets the restoring calls below through.
            Engaged = false;
            Player player = _player;
            _player = null;
            if (restore && player != null)
            {
                int restored = 0;
                foreach (KeyValuePair<BotsGroup, EBotEnemyCause> entry in EnemyOf)
                {
                    try
                    {
                        if (entry.Key.AddEnemy(player, entry.Value))
                        {
                            restored++;
                        }
                    }
                    catch (System.Exception e)
                    {
                        CameraModePlugin.Log.LogWarning($"AI: could not give a group its enemy back: {e.Message}");
                    }
                }
                foreach (BotsGroup group in NeutralOf)
                {
                    if (!EnemyOf.ContainsKey(group))
                    {
                        group.AddNeutral(player);
                    }
                }
                CameraModePlugin.Log.LogInfo($"AI: player is an enemy again to {restored} of {EnemyOf.Count} groups");
            }
            EnemyOf.Clear();
            NeutralOf.Clear();
        }

        internal static bool IsHiddenPlayer(IPlayer person)
        {
            Player player = _player;
            return person != null && player != null && person.ProfileId == player.ProfileId;
        }

        /// <summary>From the AddEnemy patch: remembers the first cause a group gave.</summary>
        internal static void NoteRefusedEnemy(BotsGroup group, EBotEnemyCause cause)
        {
            if (!EnemyOf.ContainsKey(group))
            {
                EnemyOf[group] = cause;
            }
        }

        internal static void NoteRefusedNeutral(BotsGroup group)
        {
            NeutralOf.Add(group);
        }

        private static void Forget(BotsGroup group, Player player)
        {
            // Matched by profile, not by reference: callers pass the player as different IPlayer views.
            IPlayer enemyKey = FindKey(group.Enemies, player);
            if (enemyKey != null)
            {
                EnemyOf[group] = group.Enemies[enemyKey].Cause;
            }
            IPlayer neutralKey = FindKey(group.Neutrals, player);
            if (neutralKey != null)
            {
                NeutralOf.Add(group);
            }

            if (enemyKey != null)
            {
                group.RemoveEnemy(enemyKey);
            }
            else if (neutralKey != null)
            {
                group.Neutrals.Remove(neutralKey);
            }
        }

        private static IPlayer FindKey(Dictionary<IPlayer, BotGroupEnemyInfo> map, Player player)
        {
            foreach (IPlayer key in map.Keys)
            {
                if (key != null && key.ProfileId == player.ProfileId)
                {
                    return key;
                }
            }
            return null;
        }
    }
}
