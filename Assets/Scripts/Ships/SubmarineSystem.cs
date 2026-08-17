using UnityEngine;

namespace Naval
{
    /// <summary>
    /// Depth keeping and battery management. Depth decides how visible the boat is, how fast it can
    /// run, whether it can shoot, and which enemy weapons can reach it.
    /// </summary>
    public class SubmarineSystem
    {
        readonly Ship _s;
        readonly SubmarineData _d;

        public DepthState Depth { get; private set; } = DepthState.Surface;
        public DepthState TargetDepth { get; private set; } = DepthState.Surface;
        public bool InTransit => Depth != TargetDepth;
        public float TransitProgress { get; private set; }

        public float Battery { get; private set; }
        public float BatteryFraction => Mathf.Clamp01(Battery / Mathf.Max(1f, _d.batteryCapacity));
        public bool SilentRunning { get; set; }

        bool _forcedSurface;
        float _pingTimer;

        public SubmarineSystem(Ship s)
        {
            _s = s;
            _d = s.Stats.submarine ?? new SubmarineData();
            Battery = _d.batteryCapacity;
        }

        public float SpeedMultiplier
        {
            get
            {
                float m;
                switch (Depth)
                {
                    case DepthState.Periscope: m = _d.speedMulPeriscope; break;
                    case DepthState.Submerged: m = _d.speedMulSubmerged; break;
                    case DepthState.Deep: m = _d.speedMulDeep; break;
                    default: m = 1f; break;
                }
                if (SilentRunning && Depth != DepthState.Surface) m *= 0.55f;
                if (BatteryFraction <= 0.001f && Depth != DepthState.Surface) m *= 0.4f;
                return m;
            }
        }

        public float VisibilityMultiplier
        {
            get
            {
                float m;
                switch (Depth)
                {
                    case DepthState.Periscope: m = _d.visibilityMulPeriscope; break;
                    case DepthState.Submerged: m = _d.visibilityMulSubmerged; break;
                    case DepthState.Deep: m = _d.visibilityMulDeep; break;
                    default: m = 1f; break;
                }
                if (SilentRunning) m *= 0.7f;
                if (InTransit) m *= 1.35f;   // blowing or flooding tanks makes noise
                return m;
            }
        }

        public bool CanUseDeckGun => Depth == DepthState.Surface;
        public bool CanFireTorpedoes => Depth != DepthState.Deep;
        public bool IsSubmerged => Depth != DepthState.Surface;

        /// <summary>How exposed the boat is to depth charges at its current depth (0 = safe).</summary>
        public float DepthChargeVulnerability
        {
            get
            {
                switch (Depth)
                {
                    case DepthState.Surface: return 0.35f;
                    case DepthState.Periscope: return 1f;
                    case DepthState.Submerged: return 1f;
                    default: return 0.55f;
                }
            }
        }

        // ------------------------------------------------------------------ orders

        public void SetDepth(DepthState d)
        {
            if (_forcedSurface && d != DepthState.Surface) return;
            if (d == TargetDepth) return;
            TargetDepth = d;
            TransitProgress = 0f;
            AudioManager.PlayAt(SoundId.Dive, _s.Position, 0.5f);
            if (_s.team == Team.Player)
                GameEvents.RaiseMessage(_s.shipName + ": making depth " + d.ToString().ToUpper(), Team.Player);
        }

        public void Dive()
        {
            switch (Depth)
            {
                case DepthState.Surface: SetDepth(DepthState.Periscope); break;
                case DepthState.Periscope: SetDepth(DepthState.Submerged); break;
                default: SetDepth(DepthState.Deep); break;
            }
        }

        public void Surface()
        {
            switch (Depth)
            {
                case DepthState.Deep: SetDepth(DepthState.Submerged); break;
                case DepthState.Submerged: SetDepth(DepthState.Periscope); break;
                default: SetDepth(DepthState.Surface); break;
            }
        }

        // ------------------------------------------------------------------ tick

        public void Tick(float dt)
        {
            // depth transition -------------------------------------------------
            if (Depth != TargetDepth)
            {
                float rate = 1f / Mathf.Max(0.2f, _d.diveTime);
                TransitProgress += rate * dt;
                if (TransitProgress >= 1f)
                {
                    TransitProgress = 0f;
                    int cur = (int)Depth, want = (int)TargetDepth;
                    Depth = (DepthState)(cur + (want > cur ? 1 : -1));
                    if (Depth == DepthState.Surface)
                    {
                        ParticleFX.Splash(_s.Position, _s.Stats.length * 0.25f);
                        _forcedSurface = false;
                    }
                    else if ((int)Depth == 1 && want > cur)
                    {
                        ParticleFX.Splash(_s.Position, _s.Stats.length * 0.2f);
                    }
                }
            }

            // battery ----------------------------------------------------------
            if (Depth == DepthState.Surface)
            {
                Battery = Mathf.Min(_d.batteryCapacity, Battery + _d.rechargeRate * dt);
            }
            else
            {
                float drain = Depth == DepthState.Periscope ? _d.drainPeriscope
                            : Depth == DepthState.Submerged ? _d.drainSubmerged : _d.drainDeep;
                drain *= SilentRunning ? 0.6f : 1f;
                drain *= 0.5f + Mathf.Abs(_s.Movement.Throttle);
                Battery = Mathf.Max(0f, Battery - drain * dt);

                if (Battery <= 0f && !_forcedSurface)
                {
                    _forcedSurface = true;
                    TargetDepth = DepthState.Surface;
                    TransitProgress = 0f;
                    GameEvents.RaiseMessage(_s.shipName + ": batteries exhausted, forced to surface", _s.team);
                }
            }

            // periscope wake so a sharp eyed player can spot a shallow boat -------
            if (Depth == DepthState.Periscope && Mathf.Abs(_s.Speed) > 0.4f)
            {
                _pingTimer -= dt;
                if (_pingTimer <= 0f)
                {
                    _pingTimer = 0.35f;
                    ParticleFX.Wake(_s.Position, _s.Velocity, _s.Stats.beam * 0.6f, 0.35f);
                }
            }
        }

        public string DepthLabel()
        {
            switch (Depth)
            {
                case DepthState.Surface: return "SURFACED";
                case DepthState.Periscope: return "PERISCOPE";
                case DepthState.Submerged: return "SUBMERGED";
                default: return "DEEP";
            }
        }
    }
}
