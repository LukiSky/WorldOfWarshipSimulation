using System.Collections.Generic;
using UnityEngine;

namespace Naval
{
    public enum SquadronState { Outbound, AttackRun, Returning }

    /// <summary>
    /// A flight of aircraft in the air. Squadrons are not ships - they ignore terrain and draft,
    /// fly far faster than any hull, and are worn down by anti-aircraft fire on the way in.
    /// </summary>
    public class Squadron
    {
        public Ship carrier;
        public Team team;
        public Vector2 position;
        public float heading;
        public int aircraft;
        public Ship target;
        public SquadronState state = SquadronState.Outbound;
        public float attritionCarry;     // fractional aircraft losses, applied when they reach 1
        public float runTimer;
        public bool torpedoes;           // otherwise bombs
        public float fuel;               // seconds aloft before they must return

        public bool Alive => aircraft > 0;
    }

    /// <summary>
    /// Carrier air operations. Squadrons fly out to a target, make an attack run, and return to be
    /// rearmed. Anti-aircraft fire from every ship they pass over thins them out, which is what
    /// finally makes the AA rating on every hull matter.
    /// </summary>
    public class AirWingSystem : MonoBehaviour
    {
        public static AirWingSystem I { get; private set; }

        readonly List<Squadron> _squadrons = new List<Squadron>();
        public IReadOnlyList<Squadron> Squadrons => _squadrons;

        public static AirWingSystem Create(Transform parent)
        {
            var go = new GameObject("AirWingSystem");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AirWingSystem>();
            I = a;
            return a;
        }

        public void ClearAll() => _squadrons.Clear();

        public int SquadronsAloft(Ship carrier)
        {
            int n = 0;
            for (int i = 0; i < _squadrons.Count; i++)
                if (_squadrons[i].carrier == carrier && _squadrons[i].Alive) n++;
            return n;
        }

        public Squadron Launch(Ship carrier, Ship target)
        {
            var wing = carrier.Stats.airWing;
            if (wing == null || target == null) return null;

            var sq = new Squadron
            {
                carrier = carrier,
                team = carrier.team,
                position = carrier.Position + carrier.Forward * carrier.Stats.length * 0.5f,
                heading = NavalMath.VectorToHeading(target.Position - carrier.Position),
                aircraft = wing.aircraftPerSquadron,
                target = target,
                torpedoes = Random.value < wing.torpedoChance,
                fuel = wing.strikeRange / Mathf.Max(1f, wing.cruiseSpeed) * 2.4f
            };
            _squadrons.Add(sq);

            AudioManager.PlayAt(SoundId.Smoke, carrier.Position, 0.4f);
            if (carrier.team == Team.Player)
                GameEvents.RaiseMessage(carrier.shipName + ": strike launched on " + target.shipName, Team.Player);
            else if (DetectionSystem.I != null && DetectionSystem.I.IsVisible(carrier, Team.Player))
                GameEvents.RaiseMessage("Enemy aircraft launching", Team.Enemy);

            return sq;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) { Render(); return; }

            for (int i = _squadrons.Count - 1; i >= 0; i--)
            {
                var sq = _squadrons[i];

                if (!sq.Alive || sq.carrier == null || sq.carrier.IsDead)
                {
                    // a squadron whose deck is gone has nowhere to land
                    _squadrons.RemoveAt(i);
                    continue;
                }

                var wing = sq.carrier.Stats.airWing;
                sq.fuel -= dt;

                if (sq.state != SquadronState.Returning &&
                    (sq.fuel <= 0f || sq.target == null || sq.target.IsDead || sq.target.Damage.IsSinking))
                    sq.state = SquadronState.Returning;

                Vector2 goal = sq.state == SquadronState.Returning ? sq.carrier.Position : sq.target.Position;
                Vector2 to = goal - sq.position;
                float dist = to.magnitude;

                // aircraft turn quickly but not instantly
                float want = NavalMath.VectorToHeading(to);
                sq.heading = Mathf.MoveTowardsAngle(sq.heading, want, 160f * dt);
                sq.position += NavalMath.HeadingToVector(sq.heading) * wing.cruiseSpeed * dt;

                ApplyAntiAircraftFire(sq, wing, dt);
                if (!sq.Alive) { _squadrons.RemoveAt(i); continue; }

                if (sq.state == SquadronState.Returning)
                {
                    if (dist < sq.carrier.Stats.length * 0.9f)
                    {
                        // recovered: the carrier can rearm and send them out again
                        sq.carrier.Carrier?.RecoverSquadron();
                        _squadrons.RemoveAt(i);
                    }
                    continue;
                }

                if (dist < 40f)
                {
                    sq.state = SquadronState.AttackRun;
                    sq.runTimer += dt;
                    if (sq.runTimer >= 0.6f)
                    {
                        DeliverAttack(sq, wing);
                        sq.state = SquadronState.Returning;
                    }
                }
            }

            Render();
        }

        /// <summary>Every ship with an AA battery near the flight path thins it out.</summary>
        void ApplyAntiAircraftFire(Squadron sq, AirWingData wing, float dt)
        {
            var foes = ShipRegistry.OfTeam(Teams.Opponent(sq.team));
            float dps = 0f;
            for (int i = 0; i < foes.Count; i++)
            {
                var s = foes[i];
                if (s == null || s.IsDead || s.Stats.aaRating <= 0f) continue;
                if (s.Submarine != null && s.Submarine.IsSubmerged) continue;

                float d = Vector2.Distance(s.Position, sq.position);
                const float aaRange = 130f;
                if (d > aaRange) continue;

                // fire is heaviest directly overhead
                float falloff = 1f - d / aaRange;
                dps += s.Stats.aaRating * falloff;
            }
            if (dps <= 0f) return;

            // aaRating is tuned so a strong AA ship strips a squadron in a handful of seconds
            sq.attritionCarry += dps * dt / Mathf.Max(1f, wing.aircraftHealth) * 0.5f;
            while (sq.attritionCarry >= 1f && sq.aircraft > 0)
            {
                sq.attritionCarry -= 1f;
                sq.aircraft--;
                ParticleFX.Explosion(sq.position + Random.insideUnitCircle * 8f, 1.1f);
            }
        }

        void DeliverAttack(Squadron sq, AirWingData wing)
        {
            var target = sq.target;
            if (target == null || target.IsDead) return;

            float damage = wing.damagePerAircraft * sq.aircraft;

            if (sq.torpedoes)
            {
                target.Damage.ApplyTorpedoHit(damage, target.Position, sq.carrier, wing.floodChance);
                ParticleFX.Explosion(target.Position, 3.4f, true);
                ParticleFX.Splash(target.Position, 4f);
            }
            else
            {
                // bombs: less raw damage but reliable fires
                target.Damage.ApplyDamage(damage * 0.7f, sq.carrier, DamageSource.Shell, target.Position);
                if (Random.value < wing.fireChance) target.Damage.StartFire();
                if (Random.value < wing.fireChance * 0.6f) target.Damage.StartFire();
                ParticleFX.Explosion(target.Position, 2.6f);
            }

            AudioManager.PlayAt(SoundId.Explosion, target.Position, 0.9f);
            HitMarkers.Add(target.Position, HitResult.Penetration);

            if (sq.team == Team.Player)
                GameEvents.RaiseMessage("Air strike hit " + target.shipName, Team.Player);
            else if (target.team == Team.Player)
                GameEvents.RaiseMessage(target.shipName + " under air attack!", Team.Enemy);
        }

        // ------------------------------------------------------------------ rendering

        void Render()
        {
            float ps = RTSCamera.I != null ? RTSCamera.I.PixelScale : 0.15f;

            for (int i = 0; i < _squadrons.Count; i++)
            {
                var sq = _squadrons[i];

                // aircraft obey the same fog of war as ships
                bool visible = sq.team == Team.Player || DebugOverlay.ShowAll;
                if (!visible && DetectionSystem.I != null)
                {
                    // spotted if anything of ours is close enough to see them
                    var mine = ShipRegistry.OfTeam(Team.Player);
                    for (int k = 0; k < mine.Count && !visible; k++)
                        if (mine[k] != null && !mine[k].IsDead &&
                            Vector2.Distance(mine[k].Position, sq.position) < mine[k].Detection.EffectiveSpotRange)
                            visible = true;
                }
                if (!visible) continue;

                Color col = sq.team == Team.Player ? Teams.Friendly : Teams.Hostile;
                col.a = sq.state == SquadronState.Returning ? 0.55f : 0.95f;

                Vector2 dir = NavalMath.HeadingToVector(sq.heading);
                Vector2 side = new Vector2(dir.y, -dir.x);
                float size = Mathf.Max(6f, ps * 9f);

                // a small formation of chevrons, one per surviving aircraft (capped so it stays readable)
                int shown = Mathf.Min(sq.aircraft, 6);
                for (int k = 0; k < shown; k++)
                {
                    int row = k / 2, col2 = (k % 2 == 0) ? -1 : 1;
                    Vector2 p = sq.position - dir * (row * size * 1.5f) + side * (col2 * size * 0.9f);
                    LineDrawer.Line(p - side * size * 0.5f, p + dir * size * 0.7f, 1.5f * ps, col);
                    LineDrawer.Line(p + side * size * 0.5f, p + dir * size * 0.7f, 1.5f * ps, col);
                }

                // strike line to the target so the player can read the threat
                if (sq.state != SquadronState.Returning && sq.target != null && !sq.target.IsDead)
                    LineDrawer.Dashed(sq.position, sq.target.Position, 1f * ps,
                        new Color(col.r, col.g, col.b, 0.28f), 14f * ps, 10f * ps, Time.time * 30f * ps);
            }
        }
    }
}
