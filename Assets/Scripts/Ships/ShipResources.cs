using UnityEngine;

namespace Naval
{
    /// <summary>Fuel and magazines. Running dry cripples a ship until it reaches a friendly port.</summary>
    public class ShipResources
    {
        readonly Ship _s;

        public float Fuel { get; private set; }
        public float FuelCapacity => _s.Stats.fuelCapacity;
        public float FuelFraction => Mathf.Clamp01(Fuel / Mathf.Max(1f, FuelCapacity));

        public int MainAmmo { get; private set; }
        public int SecondaryAmmo { get; private set; }
        public int TorpedoAmmo { get; private set; }
        public int ASWAmmo { get; private set; }

        public int MainAmmoMax => _s.Stats.ammoMain;
        public int TorpedoAmmoMax => _s.Stats.ammoTorpedo;
        public int ASWAmmoMax => _s.Stats.ammoASW;

        bool _lowFuelWarned;
        bool _noFuelWarned;
        float _rearmCarry;

        public ShipResources(Ship s)
        {
            _s = s;
            Fuel = s.Stats.fuelCapacity;
            MainAmmo = s.Stats.ammoMain;
            SecondaryAmmo = s.Stats.ammoSecondary;
            TorpedoAmmo = s.Stats.ammoTorpedo;
            ASWAmmo = s.Stats.ammoASW;
        }

        public void Tick(float dt)
        {
            float load = Mathf.Abs(_s.Movement != null ? _s.Movement.Throttle : 0f);
            // idling still burns a trickle for auxiliaries
            float burn = _s.Stats.fuelBurn * (0.12f + 0.88f * load * load) * dt;
            if (_s.Submarine != null && _s.Submarine.Depth != DepthState.Surface) burn *= 0.35f;   // running on batteries
            Fuel = Mathf.Max(0f, Fuel - burn);

            if (_s.team == Team.Player)
            {
                if (!_lowFuelWarned && FuelFraction < 0.2f)
                {
                    _lowFuelWarned = true;
                    GameEvents.RaiseMessage(_s.shipName + ": fuel state low", Team.Player);
                }
                if (!_noFuelWarned && FuelFraction <= 0.001f)
                {
                    _noFuelWarned = true;
                    GameEvents.RaiseMessage(_s.shipName + " is out of fuel", Team.Player);
                }
                if (FuelFraction > 0.35f) { _lowFuelWarned = false; _noFuelWarned = false; }
            }
        }

        public bool ConsumeMain(int rounds)
        {
            if (MainAmmoMax <= 0) return true;      // classes with no magazine limit modelled
            if (MainAmmo < rounds) return false;
            MainAmmo -= rounds;
            return true;
        }

        public bool ConsumeSecondary(int rounds)
        {
            if (_s.Stats.ammoSecondary <= 0) return true;
            if (SecondaryAmmo < rounds) return false;
            SecondaryAmmo -= rounds;
            return true;
        }

        public bool ConsumeTorpedoes(int n)
        {
            if (TorpedoAmmo < n) return false;
            TorpedoAmmo -= n;
            return true;
        }

        public bool ConsumeASW(int n)
        {
            if (ASWAmmo < n) return false;
            ASWAmmo -= n;
            return true;
        }

        public void Refuel(float amount) => Fuel = Mathf.Min(FuelCapacity, Fuel + amount);

        /// <summary>Rearm at a fraction of maximum load per second.</summary>
        public void Rearm(float fraction)
        {
            _rearmCarry += fraction;
            if (_rearmCarry < 0.02f) return;
            float f = _rearmCarry;
            _rearmCarry = 0f;
            MainAmmo = Mathf.Min(_s.Stats.ammoMain, MainAmmo + Mathf.CeilToInt(_s.Stats.ammoMain * f));
            SecondaryAmmo = Mathf.Min(_s.Stats.ammoSecondary, SecondaryAmmo + Mathf.CeilToInt(_s.Stats.ammoSecondary * f));
            TorpedoAmmo = Mathf.Min(_s.Stats.ammoTorpedo, TorpedoAmmo + Mathf.CeilToInt(_s.Stats.ammoTorpedo * f));
            ASWAmmo = Mathf.Min(_s.Stats.ammoASW, ASWAmmo + Mathf.CeilToInt(_s.Stats.ammoASW * f));
        }

        public bool NeedsResupply => FuelFraction < 0.18f ||
                                     (MainAmmoMax > 0 && MainAmmo < MainAmmoMax * 0.12f) ||
                                     (TorpedoAmmoMax > 0 && TorpedoAmmo == 0 && MainAmmoMax == 0);
    }
}
