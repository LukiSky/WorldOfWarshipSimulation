using System.Collections.Generic;
using UnityEngine;

namespace Naval
{
    public enum AbilityId
    {
        None,
        ShellHE, ShellAP,            // ammunition selection (instant toggles, no cooldown)
        Torpedoes, HomingTorpedoes,
        SmokeScreen, EngineBoost,
        HydroacousticSearch, SurveillanceRadar,
        DamageControl, RepairParty,
        SonarPing, Hydrophone,
        Dive
    }

    /// <summary>One consumable slot: charges, cooldown, active duration.</summary>
    public class Ability
    {
        public AbilityId id;
        public string label;
        public string hotkey;
        public float cooldown;
        public float duration;      // 0 = instant effect
        public int maxCharges;      // 0 = unlimited

        public float cooldownLeft;
        public float activeLeft;
        public int chargesLeft;

        public bool IsActive => activeLeft > 0f;
        public bool IsToggle => id == AbilityId.ShellHE || id == AbilityId.ShellAP || id == AbilityId.Dive;
        public bool HasCharges => maxCharges <= 0 || chargesLeft > 0;
        public bool Ready => cooldownLeft <= 0f && !IsActive && HasCharges;
        public float CooldownFraction => cooldown <= 0f ? 1f : 1f - Mathf.Clamp01(cooldownLeft / cooldown);
        public float ActiveFraction => duration <= 0f ? 0f : Mathf.Clamp01(activeLeft / duration);
    }

    /// <summary>
    /// Consumables. Each class carries four slots on keys 1-4 (submarines also use X to dive).
    /// The same code path serves the player's action bar and the AI - the AI simply calls Use().
    /// </summary>
    public class ShipAbilities
    {
        readonly Ship _s;
        public readonly List<Ability> Slots = new List<Ability>();

        /// <summary>Currently loaded shell type. Ships without AP simply always use HE.</summary>
        public AbilityId ShellType { get; private set; } = AbilityId.ShellHE;

        // active effect state read by other systems
        public float SpeedMultiplier { get; private set; } = 1f;
        public float DetectionBonus { get; private set; } = 0f;      // extra spotting range
        public bool SeesThroughSmoke { get; private set; }
        public float RepairPerSecond { get; private set; }
        public bool SonarActive { get; private set; }

        public ShipAbilities(Ship s)
        {
            _s = s;
            Build(s.Stats.classType);
        }

        void Add(AbilityId id, string label, string key, float cooldown, float duration, int charges)
        {
            Slots.Add(new Ability
            {
                id = id,
                label = label,
                hotkey = key,
                cooldown = cooldown,
                duration = duration,
                maxCharges = charges,
                chargesLeft = charges
            });
        }

        void Build(ShipClassType cls)
        {
            switch (cls)
            {
                case ShipClassType.Destroyer:
                    Add(AbilityId.ShellHE, "HE Shells", "1", 0f, 0f, 0);
                    Add(AbilityId.Torpedoes, "Torpedoes", "2", 0f, 0f, 0);
                    Add(AbilityId.SmokeScreen, "Smoke Screen", "3", 95f, 26f, 4);
                    Add(AbilityId.EngineBoost, "Engine Boost", "4", 90f, 20f, 4);
                    break;

                case ShipClassType.Cruiser:
                    Add(AbilityId.ShellHE, "HE Shells", "1", 0f, 0f, 0);
                    Add(AbilityId.ShellAP, "AP Shells", "2", 0f, 0f, 0);
                    Add(AbilityId.HydroacousticSearch, "Hydro Search", "3", 110f, 22f, 3);
                    Add(AbilityId.SurveillanceRadar, "Radar", "4", 140f, 18f, 2);
                    break;

                case ShipClassType.Battleship:
                    Add(AbilityId.ShellHE, "HE Shells", "1", 0f, 0f, 0);
                    Add(AbilityId.ShellAP, "AP Shells", "2", 0f, 0f, 0);
                    Add(AbilityId.DamageControl, "Damage Control", "3", 80f, 0f, 0);
                    Add(AbilityId.RepairParty, "Repair Party", "4", 100f, 22f, 3);
                    ShellType = AbilityId.ShellAP;      // battleships load AP by default
                    break;

                case ShipClassType.Submarine:
                    Add(AbilityId.HomingTorpedoes, "Homing Torps", "1", 0f, 0f, 0);
                    Add(AbilityId.SonarPing, "Sonar Ping", "2", 18f, 30f, 0);
                    Add(AbilityId.Hydrophone, "Hydrophone", "3", 70f, 16f, 4);
                    Add(AbilityId.Dive, "Dive / Surface", "X", 0f, 0f, 0);
                    break;

                default:
                    Add(AbilityId.DamageControl, "Damage Control", "3", 110f, 0f, 0);
                    break;
            }
        }

        public Ability Get(AbilityId id)
        {
            for (int i = 0; i < Slots.Count; i++) if (Slots[i].id == id) return Slots[i];
            return null;
        }

        public Ability GetSlot(int index) => index >= 0 && index < Slots.Count ? Slots[index] : null;

        public bool Has(AbilityId id) => Get(id) != null;

        // ------------------------------------------------------------------ activation

        public bool UseSlot(int index)
        {
            var a = GetSlot(index);
            return a != null && Use(a.id);
        }

        public bool Use(AbilityId id)
        {
            var a = Get(id);
            if (a == null) return false;

            // ammunition and depth toggles are always available
            switch (id)
            {
                case AbilityId.ShellHE:
                case AbilityId.ShellAP:
                    ShellType = id;
                    if (_s.team == Team.Player)
                        GameEvents.RaiseMessage(_s.shipName + ": loading " + (id == AbilityId.ShellHE ? "HE" : "AP"), Team.Player);
                    return true;

                case AbilityId.Torpedoes:
                case AbilityId.HomingTorpedoes:
                    // handled by the weapon system; the slot is a reminder of the key binding
                    return _s.Weapons.LaunchTorpedoesAt(_s.Weapons.AimPoint);

                case AbilityId.Dive:
                    if (_s.Submarine == null) return false;
                    if (_s.Submarine.Depth == DepthState.Surface) _s.Submarine.Dive();
                    else _s.Submarine.Surface();
                    return true;
            }

            if (!a.Ready) return false;

            a.cooldownLeft = a.cooldown;
            if (a.maxCharges > 0) a.chargesLeft--;
            if (a.duration > 0f) a.activeLeft = a.duration;

            switch (id)
            {
                case AbilityId.SmokeScreen:
                    if (!_s.Weapons.DeploySmoke())
                    {
                        a.cooldownLeft = 0f;
                        if (a.maxCharges > 0) a.chargesLeft++;
                        return false;
                    }
                    break;

                case AbilityId.EngineBoost:
                    Announce("engine boost engaged");
                    break;

                case AbilityId.HydroacousticSearch:
                    Announce("hydroacoustic search active");
                    AudioManager.PlayAt(SoundId.Sonar, _s.Position, 0.6f);
                    break;

                case AbilityId.SurveillanceRadar:
                    Announce("radar active");
                    AudioManager.PlayAt(SoundId.Sonar, _s.Position, 0.7f);
                    break;

                case AbilityId.DamageControl:
                    if (!_s.Damage.UseDamageControl())
                    {
                        // refund if the damage control party could not be sent away
                        a.cooldownLeft = 0f;
                        if (a.maxCharges > 0) a.chargesLeft++;
                        return false;
                    }
                    break;

                case AbilityId.RepairParty:
                    Announce("repair party working");
                    break;

                case AbilityId.SonarPing:
                    FireSonarPing();
                    break;

                case AbilityId.Hydrophone:
                    Announce("hydrophone sweep");
                    AudioManager.PlayAt(SoundId.Sonar, _s.Position, 0.5f);
                    break;
            }
            return true;
        }

        void Announce(string what)
        {
            if (_s.team == Team.Player) GameEvents.RaiseMessage(_s.shipName + ": " + what, Team.Player);
        }

        /// <summary>Submarine ping: marks a target so homing torpedoes can track it.</summary>
        void FireSonarPing()
        {
            ParticleFX.SonarPing(_s.Position, 120f);
            AudioManager.PlayAt(SoundId.Sonar, _s.Position, 0.8f);

            var enemies = ShipRegistry.OfTeam(Teams.Opponent(_s.team));
            float best = 420f;
            Ship hit = null;
            Vector2 aim = _s.Weapons.AimPoint;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || e.IsDead) continue;
                float d = Vector2.Distance(_s.Position, e.Position);
                if (d > 420f) continue;
                // prefer whatever the player is aiming at
                float score = d + Vector2.Distance(aim, e.Position) * 0.5f;
                if (score < best) { best = score; hit = e; }
            }

            if (hit != null)
            {
                hit.Detection.ApplySonarLock(24f);
                Announce("sonar lock on " + hit.shipName);
            }
        }

        // ------------------------------------------------------------------ tick

        public void Tick(float dt)
        {
            SpeedMultiplier = 1f;
            DetectionBonus = 0f;
            SeesThroughSmoke = false;
            RepairPerSecond = 0f;
            SonarActive = false;

            for (int i = 0; i < Slots.Count; i++)
            {
                var a = Slots[i];
                if (a.cooldownLeft > 0f) a.cooldownLeft = Mathf.Max(0f, a.cooldownLeft - dt);
                if (a.activeLeft > 0f)
                {
                    a.activeLeft = Mathf.Max(0f, a.activeLeft - dt);
                    ApplyActiveEffect(a, dt);
                    if (a.activeLeft <= 0f && a.id == AbilityId.RepairParty) Announce("repair party finished");
                }
            }
        }

        void ApplyActiveEffect(Ability a, float dt)
        {
            switch (a.id)
            {
                case AbilityId.EngineBoost:
                    SpeedMultiplier = 1.28f;
                    break;

                case AbilityId.HydroacousticSearch:
                    DetectionBonus = Mathf.Max(DetectionBonus, 190f);
                    SeesThroughSmoke = true;
                    SonarActive = true;
                    break;

                case AbilityId.SurveillanceRadar:
                    DetectionBonus = Mathf.Max(DetectionBonus, 520f);
                    SeesThroughSmoke = true;
                    break;

                case AbilityId.Hydrophone:
                    DetectionBonus = Mathf.Max(DetectionBonus, 300f);
                    SonarActive = true;
                    break;

                case AbilityId.RepairParty:
                    // heals a slice of the hull back, the classic battleship heal
                    RepairPerSecond = _s.Stats.maxHealth * 0.012f;
                    _s.Damage.Heal(RepairPerSecond * dt);
                    break;
            }
        }

        public string StatusLine()
        {
            for (int i = 0; i < Slots.Count; i++)
                if (Slots[i].IsActive) return Slots[i].label.ToUpper() + " " + Mathf.CeilToInt(Slots[i].activeLeft) + "s";
            return "";
        }
    }
}
