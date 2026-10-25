using EFT;
using EFT.HealthSystem;

namespace CameraMode
{
    /// <summary>
    /// Infinite stamina and no hunger or thirst, through switches the game already has.
    ///
    /// - Stamina: Stamina.ForceMode. While it is set, Consume takes nothing and CanAdd always
    ///   allows the action. The game sets it on bots (BotOwner) and for the Berserk buff
    ///   (Physical.BerserkRestorationFactor) on Physical.Stamina, HandsStamina and Oxygen. The
    ///   Berserk buff ending can clear it, so it is set again every frame.
    /// - Hunger and thirst: BaseHealthController.MetabolismDisabled, the flag behind the offline
    ///   raid setting "Disable metabolism". The energy and hydration loops check it.
    ///
    /// The previous values are saved and put back.
    /// </summary>
    internal static class Vitals
    {
        private static bool _stamina;
        private static bool _savedStamina;
        private static bool _savedHands;
        private static bool _savedOxygen;

        private static bool _metabolism;
        private static bool _savedMetabolismDisabled;

        internal static void Enter(Player player)
        {
            PhysicalBase physical = player.Physical;
            if (Settings.InfiniteStamina.Value && physical != null)
            {
                _savedStamina = physical.Stamina.ForceMode;
                _savedHands = physical.HandsStamina.ForceMode;
                _savedOxygen = physical.Oxygen.ForceMode;
                _stamina = true;
                Refill(physical.Stamina);
                Refill(physical.HandsStamina);
                Refill(physical.Oxygen);
                Tick(player);
            }

            ActiveHealthController health = player.ActiveHealthController;
            if (Settings.NoHungerThirst.Value && health != null)
            {
                _savedMetabolismDisabled = health.MetabolismDisabled;
                health.MetabolismDisabled = true;
                _metabolism = true;
            }

            if (_stamina || _metabolism)
            {
                CameraModePlugin.Log.LogInfo($"Vitals: stamina {(_stamina ? "infinite" : "normal")}, metabolism {(_metabolism ? "off" : "normal")}");
            }
        }

        internal static void Tick(Player player)
        {
            if (!_stamina)
            {
                return;
            }
            PhysicalBase physical = player.Physical;
            if (physical == null)
            {
                return;
            }
            if (!physical.Stamina.ForceMode)
            {
                physical.Stamina.ForceMode = true;
            }
            if (!physical.HandsStamina.ForceMode)
            {
                physical.HandsStamina.ForceMode = true;
            }
            if (!physical.Oxygen.ForceMode)
            {
                physical.Oxygen.ForceMode = true;
            }
        }

        internal static void Exit(Player player)
        {
            PhysicalBase physical = player != null ? player.Physical : null;
            if (_stamina && physical != null)
            {
                physical.Stamina.ForceMode = _savedStamina;
                physical.HandsStamina.ForceMode = _savedHands;
                physical.Oxygen.ForceMode = _savedOxygen;
            }
            _stamina = false;

            ActiveHealthController health = player != null ? player.ActiveHealthController : null;
            if (_metabolism && health != null)
            {
                health.MetabolismDisabled = _savedMetabolismDisabled;
            }
            _metabolism = false;
        }

        private static void Refill(Stamina stamina)
        {
            stamina.Current = stamina.TotalCapacity;
            stamina.InvokeChangedAction();
        }
    }
}
