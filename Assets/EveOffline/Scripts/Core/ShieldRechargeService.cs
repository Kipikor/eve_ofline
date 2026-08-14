using System;

namespace EveOffline
{
    public static class ShieldRechargeService
    {
        public static void Tick(GameSave save, float dt)
        {
            if (save?.Ships == null || dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;

            foreach (var ship in save.Ships)
            {
                if (ship == null || ship.StructureHp <= 0) continue;
                var hull = Catalog.GetShip(ship.HullId);
                if (hull == null || hull.ShieldRechargeSeconds <= 0) continue;

                CharacterSave pilot = null;
                if (save.Characters != null)
                {
                    pilot = save.Characters.Find(candidate => candidate != null &&
                        string.Equals(candidate.AssignedShipUid, ship.Uid, StringComparison.Ordinal));
                }

                var maximum = PreparedPackageService.MaxShieldHp(ship, pilot);
                if (maximum <= 0 || float.IsNaN(maximum) || float.IsInfinity(maximum)) continue;

                var current = ship.ShieldHp;
                if (float.IsNaN(current) || float.IsInfinity(current)) continue;
                current = Math.Clamp(current, 0f, maximum);
                if (current >= maximum)
                {
                    ship.ShieldHp = maximum;
                    continue;
                }

                var shieldOperationLevel = Math.Clamp(SkillService.GetLevel(pilot, "shield-operation"), 0, 5);
                var rechargeSeconds = hull.ShieldRechargeSeconds * (1d - .05d * shieldOperationLevel);
                if (rechargeSeconds <= 0 || double.IsNaN(rechargeSeconds) || double.IsInfinity(rechargeSeconds)) continue;

                // EVE-style passive recharge has dP/dt = (10/T)(sqrt(P)-P),
                // where P is the current shield fraction. Integrating the
                // square-root form exactly makes one long offline step agree
                // with any partition of that same interval and cannot overshoot.
                var fraction = Math.Clamp((double)current / maximum, 0d, 1d);
                var root = 1d - (1d - Math.Sqrt(fraction)) * Math.Exp(-5d * dt / rechargeSeconds);
                var recharged = maximum * root * root;
                ship.ShieldHp = (float)Math.Clamp(recharged, current, maximum);
            }
        }
    }
}
