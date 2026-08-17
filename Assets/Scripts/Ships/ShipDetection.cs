using UnityEngine;

namespace Naval
{
    /// <summary>Per ship sensor and signature state. The global DetectionSystem consumes this.</summary>
    public class ShipDetection
    {
        readonly Ship _s;

        public float CurrentDetectability { get; private set; }
        public float EffectiveSpotRange { get; private set; }
        public float EffectiveSonarRange { get; private set; }
        public float EffectiveHydroRange { get; private set; }

        public float LastGunFireTime { get; private set; } = -99f;
        public float LastTorpedoTime { get; private set; } = -99f;
        public float LastSonarPing { get; private set; } = -99f;

        public bool SpottedByEnemy { get; set; }
        public bool SonarContactOnly { get; set; }

        float _sonarLockUntil = -99f;
        /// <summary>A submarine ping marks this ship: it lights up and homing torpedoes can track it.</summary>
        public bool SonarLocked => Time.time < _sonarLockUntil;
        public void ApplySonarLock(float duration) => _sonarLockUntil = Mathf.Max(_sonarLockUntil, Time.time + duration);

        public ShipDetection(Ship s)
        {
            _s = s;
            Recalculate();
        }

        public void NotifyGunFire() => LastGunFireTime = Time.time;
        public void NotifyTorpedoLaunch() => LastTorpedoTime = Time.time;
        public void NotifySonarPing() => LastSonarPing = Time.time;

        public bool RecentlyFired => Time.time - LastGunFireTime < 12f;

        public void Recalculate()
        {
            var st = _s.Stats;
            float weather = WeatherSystem.I != null ? WeatherSystem.I.VisibilityMultiplier : 1f;
            float sensors = _s.Damage != null ? Mathf.Lerp(0.4f, 1f, _s.Damage.SystemIntegrity(ShipSystem.Sensors)) : 1f;

            // ---- how far this ship can see ------------------------------------
            float abilityBonus = _s.Abilities != null ? _s.Abilities.DetectionBonus : 0f;
            EffectiveSpotRange = st.spotRange * weather * sensors + abilityBonus;
            EffectiveSonarRange = (st.sonarRange + abilityBonus * 0.6f) * sensors;
            EffectiveHydroRange = st.hydroRange * sensors + abilityBonus;

            // ---- how far away this ship can be seen ---------------------------
            float d = st.baseDetectability;
            d *= Mathf.Lerp(1f, 0.62f, weather < 1f ? 1f - weather : 0f);   // haze hides everyone

            // speed and wake
            float speedFrac = Mathf.Clamp01(Mathf.Abs(_s.Speed) / Mathf.Max(0.1f, st.maxSpeed));
            d *= Mathf.Lerp(0.82f, 1.12f, speedFrac);

            // gun flashes and smoke give the position away
            if (RecentlyFired) d *= 1.55f;

            // burning ships are lit up
            if (_s.Damage != null && _s.Damage.FireStacks > 0) d *= 1.35f + 0.15f * _s.Damage.FireStacks;

            // submarines
            if (_s.Submarine != null) d *= _s.Submarine.VisibilityMultiplier;

            // smoke screens
            if (SmokeSystem.I != null && SmokeSystem.I.IsInsideSmoke(_s.Position))
                d *= RecentlyFired ? 0.55f : 0.12f;   // firing from inside smoke gives you away

            // a ship held by an active sonar ping cannot hide
            if (SonarLocked) d = Mathf.Max(d, 420f);

            CurrentDetectability = Mathf.Max(12f, d);
        }
    }
}
