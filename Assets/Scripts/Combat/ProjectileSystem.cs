using System.Collections.Generic;
using UnityEngine;

namespace Naval
{
    /// <summary>
    /// All ordnance in flight. Shells arc to a scattered aim point, torpedoes run through the water
    /// leaving a wake that alert ships can spot, depth charges sink to a set depth before detonating.
    /// Everything lives in plain arrays and renders through the batched line/particle drawers.
    /// </summary>
    public class ProjectileSystem : MonoBehaviour
    {
        public static ProjectileSystem I { get; private set; }

        // ---------------------------------------------------------------- data

        public class Shell
        {
            public Vector2 start, aim, pos;
            public float totalTime, timeLeft;
            public float damage, penetration, fireChance;
            public Ship owner;
            public Team team;
            public DamageSource source;
            public float arcHeight;
            public bool big;
            public bool isAP;            // armour piercing, otherwise high explosive
        }

        public class Torpedo
        {
            public Vector2 pos;
            public float heading;
            public float speed;
            public float rangeLeft;
            public float damage;
            public float floodChance;
            public float armTimer;
            public Ship owner;
            public Team team;
            public float wakeTimer;
            public bool spotted;         // has the defending team noticed it
            public Ship homing;          // acoustic homing target (submarine torpedoes)
            public float homingTurnRate; // degrees per second
        }

        public class DepthCharge
        {
            public Vector2 pos;
            public float sinkTimer;
            public float damage, radius;
            public Ship owner;
            public Team team;
        }

        readonly List<Shell> _shells = new List<Shell>(256);
        readonly List<Torpedo> _torps = new List<Torpedo>(128);
        readonly List<DepthCharge> _charges = new List<DepthCharge>(64);

        public int ShellCount => _shells.Count;
        public int TorpedoCount => _torps.Count;
        public IReadOnlyList<Torpedo> Torpedoes => _torps;
        public IReadOnlyList<Shell> Shells => _shells;

        public static ProjectileSystem Create(Transform parent)
        {
            var go = new GameObject("ProjectileSystem");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<ProjectileSystem>();
            I = p;
            return p;
        }

        public void ClearAll()
        {
            _shells.Clear(); _torps.Clear(); _charges.Clear();
        }

        // ---------------------------------------------------------------- spawning

        public void FireShell(Ship owner, Vector2 from, Vector2 aimPoint, float shellSpeed, float damage,
                              float penetration, float fireChance, DamageSource source, bool big, bool isAP = true)
        {
            float dist = Vector2.Distance(from, aimPoint);
            float tof = Mathf.Max(0.1f, dist / Mathf.Max(20f, shellSpeed));
            _shells.Add(new Shell
            {
                start = from,
                aim = aimPoint,
                pos = from,
                totalTime = tof,
                timeLeft = tof,
                damage = damage,
                penetration = penetration,
                fireChance = fireChance,
                owner = owner,
                team = owner != null ? owner.team : Team.Neutral,
                source = source,
                arcHeight = Mathf.Min(dist * 0.16f, 60f),
                big = big,
                isAP = isAP
            });
        }

        public void LaunchTorpedo(Ship owner, Vector2 from, float heading, TorpedoData data, Ship homingTarget = null)
        {
            _torps.Add(new Torpedo
            {
                pos = from,
                heading = heading,
                speed = data.speed,
                rangeLeft = data.range,
                damage = data.damage,
                floodChance = data.floodChance,
                armTimer = data.armTime,
                owner = owner,
                team = owner != null ? owner.team : Team.Neutral,
                homing = homingTarget,
                homingTurnRate = homingTarget != null ? 22f : 0f
            });
        }

        public void DropDepthCharge(Ship owner, Vector2 pos, ASWData data)
        {
            _charges.Add(new DepthCharge
            {
                pos = pos,
                sinkTimer = data.sinkTime,
                damage = data.damage,
                radius = data.radius,
                owner = owner,
                team = owner != null ? owner.team : Team.Neutral
            });
        }

        // ---------------------------------------------------------------- update

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) { Render(); return; }
            UpdateShells(dt);
            UpdateTorpedoes(dt);
            UpdateCharges(dt);
            Render();
        }

        void UpdateShells(float dt)
        {
            for (int i = _shells.Count - 1; i >= 0; i--)
            {
                var s = _shells[i];
                s.timeLeft -= dt;
                float t = 1f - Mathf.Clamp01(s.timeLeft / s.totalTime);
                s.pos = Vector2.Lerp(s.start, s.aim, t);

                if (s.timeLeft > 0f) continue;

                _shells.RemoveAt(i);
                ResolveShellImpact(s);
            }
        }

        void ResolveShellImpact(Shell s)
        {
            // Did the scattered fall of shot land on a hull?
            var candidates = ShipRegistry.AllInRadius(s.aim, 40f);
            Ship hit = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (c.team == s.team) continue;
                if (c.Submarine != null && c.Submarine.IsSubmerged) continue;   // shells cannot reach a dived boat
                if (NavalMath.InsideHull(s.aim, c.Position, c.Heading, c.Stats.length, c.Stats.beam * 1.35f))
                {
                    hit = c;
                    break;
                }
            }

            if (hit != null)
            {
                float impactHeading = NavalMath.VectorToHeading(s.aim - s.start);
                var result = hit.Damage.ApplyShellHit(s.damage, s.penetration, s.aim, impactHeading, s.owner, s.source, s.fireChance, s.isAP);
                ParticleFX.Explosion(s.aim, s.big ? 2.2f : 1.1f);
                AudioManager.PlayAt(SoundId.Impact, s.aim, s.big ? 0.9f : 0.5f);
                if (result == HitResult.Citadel)
                {
                    ParticleFX.Explosion(s.aim, 3.4f);
                    if (s.owner != null && s.owner.team == Team.Player)
                        GameEvents.RaiseMessage("CITADEL hit on " + hit.shipName, Team.Player);
                }
                HitMarkers.Add(s.aim, result);
            }
            else
            {
                // near miss: a wall of water
                ParticleFX.Splash(s.aim, s.big ? 3.2f : 1.6f);
                AudioManager.PlayAt(SoundId.Splash, s.aim, 0.35f);

                // shells can also land on a port
                var map = WorldMap.I;
                if (map != null)
                    for (int i = 0; i < map.Ports.Count; i++)
                    {
                        var p = map.Ports[i];
                        if (p == null || p.team == s.team) continue;
                        if ((p.Position - s.aim).sqrMagnitude < 45f * 45f)
                        {
                            p.TakeDamage(s.damage * 0.5f);
                            ParticleFX.Explosion(s.aim, 2f);
                        }
                    }
            }
        }

        void UpdateTorpedoes(float dt)
        {
            for (int i = _torps.Count - 1; i >= 0; i--)
            {
                var t = _torps[i];

                // acoustic homing: steer onto the locked target while it stays detectable
                if (t.homing != null)
                {
                    if (t.homing.IsDead || !t.homing.Detection.SonarLocked) t.homing = null;
                    else
                    {
                        float want = NavalMath.VectorToHeading(t.homing.Position - t.pos);
                        t.heading = Mathf.MoveTowardsAngle(t.heading, want, t.homingTurnRate * dt);
                    }
                }

                Vector2 dir = NavalMath.HeadingToVector(t.heading);
                float step = t.speed * dt;
                t.pos += dir * step;
                t.rangeLeft -= step;
                t.armTimer -= dt;

                // wake
                t.wakeTimer -= dt;
                if (t.wakeTimer <= 0f)
                {
                    t.wakeTimer = 0.08f;
                    ParticleFX.Wake(t.pos, dir * t.speed * 0.2f, 1.6f, 0.5f);
                }

                bool remove = false;

                if (t.rangeLeft <= 0f) remove = true;
                else if (WorldMap.I != null && WorldMap.I.SampleDepth(t.pos) < 0.02f)
                {
                    ParticleFX.Explosion(t.pos, 1.6f);
                    remove = true;
                }
                else if (t.armTimer <= 0f)
                {
                    var near = ShipRegistry.AllInRadius(t.pos, 34f);
                    for (int j = 0; j < near.Count; j++)
                    {
                        var s = near[j];
                        if (s.team == t.team) continue;
                        if (s.Submarine != null && s.Submarine.Depth == DepthState.Deep) continue;
                        if (!NavalMath.InsideHull(t.pos, s.Position, s.Heading, s.Stats.length, s.Stats.beam * 1.5f)) continue;

                        s.Damage.ApplyTorpedoHit(t.damage, t.pos, t.owner, t.floodChance);
                        ParticleFX.Explosion(t.pos, 3.2f, true);
                        ParticleFX.Splash(t.pos, 4f);
                        AudioManager.PlayAt(SoundId.Explosion, t.pos, 1f);
                        HitMarkers.Add(t.pos, HitResult.Penetration);
                        if (t.owner != null && t.owner.team == Team.Player)
                            GameEvents.RaiseMessage("Torpedo hit on " + s.shipName, Team.Player);
                        remove = true;
                        break;
                    }
                }

                if (remove) _torps.RemoveAt(i);
            }
        }

        void UpdateCharges(float dt)
        {
            for (int i = _charges.Count - 1; i >= 0; i--)
            {
                var c = _charges[i];
                c.sinkTimer -= dt;
                if (c.sinkTimer > 0f)
                {
                    if (Random.value < dt * 6f) ParticleFX.Spawn(c.pos, Vector2.zero, 0.6f, 1.2f, 2.4f,
                        new Color(0.7f, 0.85f, 0.95f, 0.4f), new Color(0.7f, 0.85f, 0.95f, 0f), 0, false, 1f);
                    continue;
                }

                _charges.RemoveAt(i);
                ParticleFX.Explosion(c.pos, 2.6f, true);
                ParticleFX.Splash(c.pos, 5f);
                AudioManager.PlayAt(SoundId.Explosion, c.pos, 0.8f);

                var near = ShipRegistry.AllInRadius(c.pos, c.radius);
                for (int j = 0; j < near.Count; j++)
                {
                    var s = near[j];
                    if (s.team == c.team) continue;
                    float dist = Vector2.Distance(s.Position, c.pos);
                    float falloff = 1f - Mathf.Clamp01(dist / c.radius);
                    float vuln = s.Submarine != null ? s.Submarine.DepthChargeVulnerability : 0.25f;
                    float dmg = c.damage * falloff * vuln;
                    if (dmg <= 1f) continue;
                    s.Damage.ApplyDamage(dmg, c.owner, DamageSource.DepthCharge, c.pos);
                    if (s.Submarine != null)
                    {
                        s.Damage.DamageSystem(ShipSystem.Hull, 0.1f * falloff);
                        if (Random.value < 0.35f * falloff) s.Damage.StartFlooding();
                        if (c.owner != null && c.owner.team == Team.Player)
                            GameEvents.RaiseMessage("Depth charge hit on submarine", Team.Player);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- rendering

        void Render()
        {
            var playerTeam = Team.Player;

            for (int i = 0; i < _shells.Count; i++)
            {
                var s = _shells[i];
                float t = 1f - Mathf.Clamp01(s.timeLeft / s.totalTime);
                float height = Mathf.Sin(t * Mathf.PI) * s.arcHeight;
                Vector2 dir = (s.aim - s.start).normalized;
                Vector2 render = s.pos + Vector2.up * height;

                Color col = s.team == playerTeam ? new Color(1f, 0.93f, 0.7f, 0.95f) : new Color(1f, 0.75f, 0.6f, 0.95f);
                float len = s.big ? 7f : 4f;
                LineDrawer.Line(render - dir * len, render + dir * len, s.big ? 1.7f : 1.1f, col);
                // shadow on the water so the arc reads
                LineDrawer.Line(s.pos - dir * len * 0.5f, s.pos + dir * len * 0.5f, s.big ? 1.2f : 0.8f, new Color(0f, 0f, 0f, 0.18f));
            }

            for (int i = 0; i < _torps.Count; i++)
            {
                var t = _torps[i];
                // torpedoes are only drawn once the defending side has spotted them, or if they are ours
                bool visible = t.team == playerTeam || t.spotted || DebugOverlay.ShowAll;
                if (!visible) continue;
                Vector2 dir = NavalMath.HeadingToVector(t.heading);
                Color col = t.team == playerTeam ? new Color(0.75f, 0.95f, 1f, 0.9f) : new Color(1f, 0.6f, 0.55f, 0.95f);
                LineDrawer.Line(t.pos - dir * 2.6f, t.pos + dir * 2.6f, 1.5f, col);
            }

            for (int i = 0; i < _charges.Count; i++)
            {
                var c = _charges[i];
                LineDrawer.Circle(c.pos, 3f, 0.8f, new Color(0.9f, 0.9f, 0.5f, 0.5f), 10);
            }
        }

        /// <summary>Marks a torpedo as noticed so it becomes visible to the defender.</summary>
        public void MarkTorpedoSpotted(Torpedo t) { t.spotted = true; }
    }

    /// <summary>Short lived floating hit markers, drawn by the world overlay.</summary>
    public static class HitMarkers
    {
        public struct Marker { public Vector2 pos; public float time; public HitResult result; }
        public static readonly List<Marker> All = new List<Marker>();

        public static void Add(Vector2 pos, HitResult r)
        {
            All.Add(new Marker { pos = pos, time = Time.time, result = r });
            if (All.Count > 40) All.RemoveAt(0);
        }

        public static void Prune()
        {
            for (int i = All.Count - 1; i >= 0; i--)
                if (Time.time - All[i].time > 1.6f) All.RemoveAt(i);
        }
    }
}
