using EFT;
using EFT.HealthSystem;
using HarmonyLib;

namespace CameraMode
{
    /// <summary>
    /// No damage while Camera Mode is on, through the game's own switch:
    /// ActiveHealthController.SetDamageCoeff(0). That is what the BTR uses for its passengers
    /// (LocalPlayer.IgnoreDamage). With the coefficient at 0, ApplyDamage, bleeds, fractures,
    /// contusion, stun and side effects all return early, and every body part's
    /// HealthValue.DownMult is 0, so effects already running cannot lower health either.
    ///
    /// ActiveHealthController.Kill does not check the coefficient (lethal toxin calls it
    /// directly), so <see cref="Patches"/> blocks it for the player too.
    ///
    /// Hunger, thirst and stamina are not affected.
    /// </summary>
    internal static class GodMode
    {
        /// <summary>
        /// IgnoreDamage(true) sets this when the player is already invincible, after which leaving
        /// the BTR no longer restores damage. Saved and put back so Camera Mode cannot leave a
        /// later BTR ride invincible.
        /// </summary>
        private static readonly AccessTools.FieldRef<LocalPlayer, bool> WasInvincibleBeforeRef =
            AccessTools.FieldRefAccess<LocalPlayer, bool>("_wasInvincibleBefore");

        private static bool _entered;
        private static float _savedCoeff;
        private static bool _savedWasInvincibleBefore;

        /// <summary>True while damage is off; the Kill patch checks this.</summary>
        internal static bool Engaged => _entered;

        /// <summary>The protected player. Camera Mode and UFO mode share this part.</summary>
        internal static Player Player { get; private set; }

        internal static void Enter(Player player)
        {
            ActiveHealthController health = player.ActiveHealthController;
            if (health == null)
            {
                CameraModePlugin.Log.LogWarning("Godmode: player has no ActiveHealthController");
                return;
            }
            _savedCoeff = health.DamageCoeff;
            if (player is LocalPlayer local)
            {
                _savedWasInvincibleBefore = WasInvincibleBeforeRef(local);
            }
            health.SetDamageCoeff(0f);
            Player = player;
            _entered = true;
            CameraModePlugin.Log.LogInfo($"Godmode: on (damage coefficient was {_savedCoeff})");
        }

        /// <summary>Getting out of the BTR sets the coefficient back to 1; this puts it back to 0.</summary>
        internal static void Tick()
        {
            if (!_entered || Player == null)
            {
                return;
            }
            ActiveHealthController health = Player.ActiveHealthController;
            if (health != null && health.DamageCoeff != 0f)
            {
                health.SetDamageCoeff(0f);
            }
        }

        internal static void Exit()
        {
            if (!_entered)
            {
                return;
            }
            _entered = false;
            Player player = Player;
            Player = null;
            if (player == null)
            {
                return;
            }
            ActiveHealthController health = player.ActiveHealthController;
            if (health != null)
            {
                health.SetDamageCoeff(_savedCoeff);
            }
            if (player is LocalPlayer local)
            {
                WasInvincibleBeforeRef(local) = _savedWasInvincibleBefore;
            }
        }
    }
}
