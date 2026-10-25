using EFT;
using EFT.HealthSystem;
using HarmonyLib;

namespace CameraMode
{
    /// <summary>
    /// The Harmony patches. Each one does nothing unless its part of Camera Mode is on. All but the
    /// two freeze patches touch only the Camera Mode player. Targets are written with nameof so a rename in a
    /// game update breaks the build instead of silently not patching.
    /// </summary>
    [HarmonyPatch]
    internal static class Patches
    {
        /// <summary>Bots may not make the player an enemy; see <see cref="AiStealth"/>.</summary>
        [HarmonyPatch(typeof(BotsGroup), nameof(BotsGroup.AddEnemy))]
        [HarmonyPrefix]
        private static bool AddEnemyPrefix(BotsGroup __instance, IPlayer person, EBotEnemyCause cause, ref bool __result)
        {
            if (!AiStealth.Engaged || !AiStealth.IsHiddenPlayer(person))
            {
                return true;
            }
            AiStealth.NoteRefusedEnemy(__instance, cause);
            __result = false;
            return false;
        }

        /// <summary>
        /// Nor a neutral: bots still react to gunshots from neutrals (LastSoundsController).
        /// </summary>
        [HarmonyPatch(typeof(BotsGroup), nameof(BotsGroup.AddNeutral))]
        [HarmonyPrefix]
        private static bool AddNeutralPrefix(BotsGroup __instance, IPlayer player)
        {
            if (!AiStealth.Engaged || !AiStealth.IsHiddenPlayer(player))
            {
                return true;
            }
            AiStealth.NoteRefusedNeutral(__instance);
            return false;
        }

        /// <summary>The one way to die that ignores the damage coefficient; see <see cref="GodMode"/>.</summary>
        [HarmonyPatch(typeof(ActiveHealthController), nameof(ActiveHealthController.Kill))]
        [HarmonyPrefix]
        private static bool KillPrefix(ActiveHealthController __instance)
        {
            return !GodMode.Engaged || __instance.Player == null || __instance.Player != GodMode.Player;
        }

        /// <summary>A bot's whole brain, once a frame (from BotsList); see <see cref="FreezeAi"/>.</summary>
        [HarmonyPatch(typeof(BotOwner), nameof(BotOwner.UpdateManual))]
        [HarmonyPrefix]
        private static bool BotUpdatePrefix()
        {
            return !FreezeAi.Engaged;
        }

        /// <summary>The bot's walking and turning on the physics tick; see <see cref="FreezeAi"/>.</summary>
        [HarmonyPatch(typeof(BotOwner), nameof(BotOwner.FixedUpdate))]
        [HarmonyPrefix]
        private static bool BotFixedUpdatePrefix()
        {
            return !FreezeAi.Engaged;
        }

        /// <summary>
        /// Every movement state's motion for the frame, on its way into the character controller.
        /// While UFO mode is on, your player's motion is replaced by flight; see <see cref="UfoMode"/>.
        /// </summary>
        [HarmonyPatch(typeof(MovementContext), nameof(MovementContext.DirectApplyMotion))]
        [HarmonyPrefix]
        private static bool DirectApplyMotionPrefix(MovementContext __instance, float deltaTime)
        {
            if (!UfoMode.IsFlying(__instance))
            {
                return true;
            }
            UfoMode.Fly(__instance, deltaTime);
            return false;
        }

        /// <summary>
        /// Raid teardown (ClientGameWorld.Dispose calls this base first). Both modes are turned off
        /// here, while the UI still exists, so the next raid starts with its HUD on.
        /// </summary>
        [HarmonyPatch(typeof(GameWorld), nameof(GameWorld.Dispose))]
        [HarmonyPrefix]
        private static void GameWorldDisposePrefix()
        {
            UfoMode.OnRaidEnding();
            CameraModeState.OnRaidEnding();
        }
    }
}
