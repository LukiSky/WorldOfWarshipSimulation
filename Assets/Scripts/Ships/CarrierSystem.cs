using UnityEngine;

namespace Naval
{
    /// <summary>
    /// Flight deck operations for one carrier: how many squadrons are ready, how long until the next
    /// launch, and rearming recovered flights. The squadrons themselves are flown by AirWingSystem.
    /// </summary>
    public class CarrierSystem
    {
        readonly Ship _s;
        readonly AirWingData _d;

        public int ReadySquadrons { get; private set; }
        public float LaunchCooldown { get; private set; }
        public float RearmProgress { get; private set; }

        int _rearming;

        public CarrierSystem(Ship s)
        {
            _s = s;
            _d = s.Stats.airWing ?? new AirWingData();
            ReadySquadrons = _d.squadrons;
        }

        public int MaxSquadrons => _d.squadrons;
        public bool CanLaunch => ReadySquadrons > 0 && LaunchCooldown <= 0f && !_s.Damage.IsSinking;
        public int Aloft => AirWingSystem.I != null ? AirWingSystem.I.SquadronsAloft(_s) : 0;

        /// <summary>Strike range is the carrier's real weapon range - far beyond any gun.</summary>
        public float StrikeRange => _d.strikeRange;

        public bool Launch(Ship target)
        {
            if (!CanLaunch || target == null) return false;
            if (Vector2.Distance(_s.Position, target.Position) > _d.strikeRange) return false;
            // a wrecked flight deck cannot spot aircraft
            if (_s.Damage.SystemIntegrity(ShipSystem.MainGuns) < 0.25f) return false;

            if (AirWingSystem.I == null || AirWingSystem.I.Launch(_s, target) == null) return false;

            ReadySquadrons--;
            LaunchCooldown = _d.launchInterval;
            _s.Detection.NotifyGunFire();      // flight operations are visible for miles
            return true;
        }

        /// <summary>A squadron that made it home goes into the hangar to rearm.</summary>
        public void RecoverSquadron() => _rearming++;

        public void Tick(float dt)
        {
            if (LaunchCooldown > 0f) LaunchCooldown -= dt;

            if (_rearming > 0)
            {
                // deck damage slows the turnaround
                float rate = Mathf.Lerp(0.35f, 1f, _s.Damage.SystemIntegrity(ShipSystem.MainGuns));
                RearmProgress += dt * rate / Mathf.Max(1f, _d.rearmTime);
                if (RearmProgress >= 1f)
                {
                    RearmProgress = 0f;
                    _rearming--;
                    ReadySquadrons = Mathf.Min(_d.squadrons, ReadySquadrons + 1);
                }
            }
            else RearmProgress = 0f;
        }

        public string StatusLine()
        {
            if (ReadySquadrons > 0 && LaunchCooldown <= 0f) return "FLIGHT READY " + ReadySquadrons + "/" + _d.squadrons;
            if (ReadySquadrons > 0) return "SPOTTING " + Mathf.CeilToInt(LaunchCooldown) + "s";
            if (_rearming > 0) return "REARMING " + Mathf.RoundToInt(RearmProgress * 100f) + "%";
            return Aloft > 0 ? "STRIKE AIRBORNE" : "NO AIRCRAFT";
        }
    }
}
