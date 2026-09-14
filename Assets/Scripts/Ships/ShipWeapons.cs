using UnityEngine;

namespace Naval
{
    /// <summary>
    /// Gunnery, torpedoes, anti submarine ordnance and smoke. Turrets traverse at a finite rate and
    /// respect their firing arcs, so a battleship has to present its broadside before it can shoot.
    /// </summary>
    public class ShipWeapons
    {
        readonly Ship _s;

        public float MainReload { get; private set; }
        public float SecondaryReload { get; private set; }
        public float TorpedoReload { get; private set; }
        public float ASWReload { get; private set; }
        public float SmokeCooldown { get; private set; }

        public float[] TurretAngles;          // world heading of each main turret
        public float[] TurretTargetAngles;

        public bool MainReady => MainReload <= 0f && GunsOperational;
        public bool TorpedoesReady => _s.Stats.torpedoes != null && TorpedoReload <= 0f && _s.Resources.TorpedoAmmo > 0;
        public bool SmokeReady => _s.Stats.smoke != null && SmokeCooldown <= 0f;
        public bool GunsOperational => _s.Stats.mainBattery != null && _s.Damage.SystemIntegrity(ShipSystem.MainGuns) > 0.15f;

        public bool HoldFire { get; set; }
        public Ship LastTarget { get; private set; }

        /// <summary>Where the guns are pointed. The AI derives it from its target, direct control from the mouse.</summary>
        public Vector2 AimPoint { get; set; }
        /// <summary>In manual mode the battery only fires when the player pulls the trigger.</summary>
        public bool ManualControl { get; set; }
        /// <summary>True while every bearing turret is lined up on the aim point.</summary>
        public bool OnTarget { get; private set; }
        public float MainReloadFraction => _s.Stats.mainBattery == null ? 0f :
            1f - Mathf.Clamp01(MainReload / Mathf.Max(0.01f, MainReloadTime));
        public float TorpedoReloadFraction => _s.Stats.torpedoes == null ? 0f :
            1f - Mathf.Clamp01(TorpedoReload / Mathf.Max(0.01f, _s.Stats.torpedoes.reloadTime));

        float _smokeEmitTimer;
        float _smokeSpawnTimer;

        public ShipWeapons(Ship s)
        {
            _s = s;
            var mb = s.Stats.mainBattery;
            int turrets = mb != null ? Mathf.Max(1, mb.turrets) : 0;
            TurretAngles = new float[turrets];
            TurretTargetAngles = new float[turrets];
            for (int i = 0; i < turrets; i++) { TurretAngles[i] = s.Heading; TurretTargetAngles[i] = s.Heading; }
            if (s.Stats.torpedoes != null) TorpedoReload = 12f;   // tubes are not loaded at the start of the action
            AimPoint = s.Position + NavalMath.HeadingToVector(s.Heading) * 200f;
        }

        /// <summary>HE or AP, resolved from the ship's loaded ammunition.</summary>
        public bool UsingAP
        {
            get
            {
                if (_s.Abilities == null) return true;
                return _s.Abilities.Has(AbilityId.ShellAP) && _s.Abilities.ShellType == AbilityId.ShellAP;
            }
        }

        void ShellProfile(GunData gun, out float damage, out float penetration, out float fireChance, out bool isAP)
        {
            if (UsingAP)
            {
                damage = gun.damage;
                penetration = gun.penetration;
                fireChance = gun.fireChance * 0.15f;
                isAP = true;
            }
            else if (gun.heDamage > 0f)
            {
                // the gun carries a real HE round of its own
                damage = gun.heDamage;
                penetration = gun.hePenetration;
                fireChance = gun.heFireChance;
                isAP = false;
            }
            else
            {
                // no HE data: derive one. HE trades penetration and raw damage for reliable fires
                damage = gun.damage * 0.62f;
                penetration = gun.penetration * 0.28f + 12f;
                fireChance = gun.fireChance * 2.4f;
                isAP = false;
            }
        }

        /// <summary>True while the guns are loaded and trained but holding for a friendly in the lane.</summary>
        public bool CheckingFire { get; private set; }

        /// <summary>
        /// Is a friendly hull sitting in the corridor between us and where we are about to shoot?
        ///
        /// With friendly fire live this is what stops a battle line from shooting its own screen to
        /// pieces. It deliberately does not apply to a ship the player is conning: if you want to
        /// fire through your own destroyer that is your decision, and the log will tell you about it.
        /// </summary>
        public bool FriendlyInLineOfFire(Vector2 aim, float corridor)
        {
            if (_s.IsDirectlyControlled) return false;

            var mates = ShipRegistry.OfTeam(_s.team);
            Vector2 from = _s.Position;
            float shotLength = Vector2.Distance(from, aim);
            if (shotLength < 1f) return false;

            for (int i = 0; i < mates.Count; i++)
            {
                var m = mates[i];
                if (m == null || m == _s || m.IsDead) continue;
                // a boat under the surface is not in anybody's way
                if (m.Submarine != null && m.Submarine.IsSubmerged) continue;

                // only care about mates actually between us and the aim point
                float along = Vector2.Dot(m.Position - from, (aim - from) / shotLength);
                if (along <= 0f || along >= shotLength) continue;

                float clearance = corridor + m.Stats.length * 0.5f;
                if (NavalMath.DistanceToSegment(m.Position, from, aim) < clearance) return true;
            }
            return false;
        }

        /// <summary>Main battery reach, including any spotter aircraft currently up.</summary>
        public float MainRange
        {
            get
            {
                var mb = _s.Stats.mainBattery;
                if (mb == null) return 0f;
                return mb.range * (_s.Abilities != null ? _s.Abilities.GunRangeMultiplier : 1f);
            }
        }

        /// <summary>Muzzle velocity for the round currently loaded.</summary>
        float ShellSpeed(GunData gun) =>
            !UsingAP && gun.heShellSpeed > 0f ? gun.heShellSpeed : gun.shellSpeed;

        public float MainReloadTime
        {
            get
            {
                var mb = _s.Stats.mainBattery;
                if (mb == null) return 1f;
                float integrity = Mathf.Max(0.2f, _s.Damage.SystemIntegrity(ShipSystem.MainGuns));
                return mb.reloadTime / integrity;
            }
        }

        // ------------------------------------------------------------------ tick

        public void Tick(float dt)
        {
            if (MainReload > 0f) MainReload -= dt;
            if (SecondaryReload > 0f) SecondaryReload -= dt;
            if (TorpedoReload > 0f) TorpedoReload -= dt;
            if (ASWReload > 0f) ASWReload -= dt;
            if (SmokeCooldown > 0f) SmokeCooldown -= dt;

            TickSmokeEmission(dt);
            TickTurrets(dt);

            if (HoldFire) return;

            TryMainBattery(dt);
            TrySecondaries(dt);
            TryASW(dt);
        }

        void TickTurrets(float dt)
        {
            var mb = _s.Stats.mainBattery;
            if (mb == null || TurretAngles == null) return;
            float speed = mb.traverseSpeed * Mathf.Lerp(0.35f, 1f, _s.Damage.SystemIntegrity(ShipSystem.MainGuns));
            for (int i = 0; i < TurretAngles.Length; i++)
                TurretAngles[i] = Mathf.MoveTowardsAngle(TurretAngles[i], TurretTargetAngles[i], speed * dt);
        }

        /// <summary>
        /// The mount description for turret i, falling back to a fore/aft split when the gun has no
        /// explicit mount data.
        /// </summary>
        public static TurretMount MountFor(GunData gun, int index, int turretCount)
        {
            if (gun != null && gun.mounts != null && index < gun.mounts.Length && gun.mounts[index] != null)
                return gun.mounts[index];

            // legacy fallback: first half forward, remainder aft, sharing one blind sector
            int fore = Mathf.CeilToInt(turretCount / 2f);
            bool forward = index < fore;
            float block = gun != null ? gun.frontalArcBlock : 20f;
            return new TurretMount
            {
                position = forward ? 0.30f : -0.30f,
                restHeading = forward ? 0f : 180f,
                arcHalfWidth = Mathf.Clamp(180f - block, 30f, 179f)
            };
        }

        public TurretMount Mount(int index) =>
            MountFor(_s.Stats.mainBattery, index, TurretAngles != null ? TurretAngles.Length : 0);

        /// <summary>
        /// Can turret i bear on this heading? Each mount trains within its own arc either side of
        /// where it rests, so a bow-on ship loses its after turrets and a stern chase loses its
        /// forward ones. This is the cost side of angling the armour.
        /// </summary>
        public bool TurretCanBear(int index, float worldHeading)
        {
            var mb = _s.Stats.mainBattery;
            if (mb == null) return false;
            var m = Mount(index);
            float relative = Mathf.DeltaAngle(_s.Heading, worldHeading);      // 0 = dead ahead
            return Mathf.Abs(Mathf.DeltaAngle(relative, m.restHeading)) <= m.arcHalfWidth;
        }

        /// <summary>
        /// The world heading turret i will actually train to. Inside its arc that is the bearing
        /// itself; outside it, the nearest arc limit, so the mount sits pressed against the stop
        /// and is already there the moment the hull turns far enough.
        /// </summary>
        public float TurretTrainTarget(int index, float worldHeading)
        {
            var m = Mount(index);
            float relative = Mathf.DeltaAngle(_s.Heading, worldHeading);
            float off = Mathf.DeltaAngle(relative, m.restHeading);
            if (Mathf.Abs(off) <= m.arcHalfWidth) return worldHeading;
            float limit = m.restHeading + Mathf.Sign(off) * m.arcHalfWidth;
            return NavalMath.Wrap360(_s.Heading + limit);
        }

        // ------------------------------------------------------------------ main battery

        void TryMainBattery(float dt)
        {
            var mb = _s.Stats.mainBattery;
            if (mb == null || !GunsOperational) return;
            if (mb.surfaceOnly && _s.Submarine != null && !_s.Submarine.CanUseDeckGun) return;

            if (ManualControl)
            {
                // the player aims; the trigger is pulled by DirectShipController
                TrainTurrets(AimPoint, out _);
                return;
            }

            var target = _s.CurrentTarget;
            if (!ValidGunTarget(target)) { OnTarget = false; return; }

            float dist = Vector2.Distance(_s.Position, target.Position);
            if (dist > MainRange || dist < mb.minRange) { OnTarget = false; return; }

            // aim: lead the target for the shell's time of flight
            // lead with the velocity of the round actually loaded, not the AP one
            if (!NavalMath.Intercept(_s.Position, target.Position, target.Velocity, ShellSpeed(mb), out Vector2 aim, out float tof))
                aim = target.Position;
            AimPoint = aim;

            int barrels = TrainTurrets(aim, out float bearing);
            if (MainReload > 0f || barrels == 0) return;

            // Check fire: a squadron mate has drifted into the line. Keep the turrets trained and
            // wait for the lane to clear rather than shooting through them.
            if (GameConfig.FriendlyFire && FriendlyInLineOfFire(aim, mb.dispersion * 0.9f))
            {
                CheckingFire = true;
                return;
            }
            CheckingFire = false;

            if (!_s.Resources.ConsumeMain(barrels)) return;

            ShellProfile(mb, out float dmg, out float pen, out float fire, out bool isAP);
            FireSalvo(target, aim, bearing, barrels, mb, DamageSource.Shell, true, dmg, pen, fire, isAP);
            MainReload = MainReloadTime;
            LastTarget = target;
            _s.Detection.NotifyGunFire();
            AudioManager.PlayAt(_s.Stats.classType == ShipClassType.Battleship ? SoundId.MainGunHeavy : SoundId.MainGun, _s.Position, 0.9f);
        }

        /// <summary>
        /// Points every turret that can bear at a world position and reports how many barrels are
        /// currently lined up (within 6 degrees), which is what a salvo actually fires.
        /// </summary>
        int TrainTurrets(Vector2 point, out float bearing)
        {
            var mb = _s.Stats.mainBattery;
            bearing = NavalMath.VectorToHeading(point - _s.Position);
            if (mb == null || TurretTargetAngles == null) { OnTarget = false; return 0; }

            int barrels = 0;
            for (int i = 0; i < TurretTargetAngles.Length; i++)
            {
                bool canBear = TurretCanBear(i, bearing);
                TurretTargetAngles[i] = TurretTrainTarget(i, bearing);
                if (!canBear) continue;
                if (Mathf.Abs(Mathf.DeltaAngle(TurretAngles[i], bearing)) > 6f) continue;
                barrels += mb.barrelsPerTurret;
            }
            OnTarget = barrels > 0;
            return barrels;
        }

        /// <summary>Player trigger pull in direct control. Returns true if a salvo left the barrels.</summary>
        public bool RequestMainFire()
        {
            var mb = _s.Stats.mainBattery;
            if (mb == null || !GunsOperational || MainReload > 0f) return false;
            if (mb.surfaceOnly && _s.Submarine != null && !_s.Submarine.CanUseDeckGun) return false;

            float dist = Vector2.Distance(_s.Position, AimPoint);
            Vector2 aim = AimPoint;
            if (dist > MainRange)
                aim = _s.Position + (AimPoint - _s.Position).normalized * MainRange;   // shoot as far as we can

            int barrels = TrainTurrets(aim, out float bearing);
            if (barrels == 0) return false;
            if (!_s.Resources.ConsumeMain(barrels)) return false;

            ShellProfile(mb, out float dmg, out float pen, out float fire, out bool isAP);
            FireSalvo(null, aim, bearing, barrels, mb, DamageSource.Shell, true, dmg, pen, fire, isAP);
            MainReload = MainReloadTime;
            _s.Detection.NotifyGunFire();
            AudioManager.PlayAt(_s.Stats.classType == ShipClassType.Battleship ? SoundId.MainGunHeavy : SoundId.MainGun, _s.Position, 0.9f);
            return true;
        }

        void FireSalvo(Ship target, Vector2 aim, float bearing, int barrels, GunData gun, DamageSource source, bool big,
                       float damage, float penetration, float fireChance, bool isAP)
        {
            float dist = Vector2.Distance(_s.Position, aim);
            float rangeFrac = Mathf.Clamp01(dist / Mathf.Max(1f, gun.range));

            // dispersion grows with range, weather, our own manoeuvring and battle damage
            float sigma = gun.dispersion * Mathf.Lerp(0.35f, 1f, rangeFrac);
            if (WeatherSystem.I != null) sigma *= WeatherSystem.I.DispersionMultiplier;
            sigma *= 1f + Mathf.Abs(_s.Movement.Rudder) * 0.45f;
            sigma *= Mathf.Lerp(1.8f, 1f, _s.Damage.SystemIntegrity(ShipSystem.MainGuns));
            sigma *= Mathf.Lerp(1.5f, 1f, _s.Damage.SystemIntegrity(ShipSystem.Sensors));
            // shooting at a target that is manoeuvring hard is harder
            if (target != null) sigma *= 1f + Mathf.Abs(target.Movement.Rudder) * 0.25f;

            Vector2 los = (aim - _s.Position).normalized;
            Vector2 perp = new Vector2(-los.y, los.x);

            for (int b = 0; b < barrels; b++)
            {
                // naval patterns are longer along the line of fire than across it
                Vector2 scatter = NavalMath.EllipticalScatter(sigma * 0.55f, sigma * 1.5f, gun.sigma);
                Vector2 point = aim + perp * scatter.x + los * scatter.y;
                Vector2 muzzle = _s.Position + NavalMath.HeadingToVector(bearing) * _s.Stats.length * 0.3f;
                ProjectileSystem.I.FireShell(_s, muzzle, point, ShellSpeed(gun), damage, penetration,
                    fireChance, source, big, isAP,
                    isAP ? gun.overmatchThreshold : 0f, gun.ricochetStart, gun.ricochetAlways);
            }

            ParticleFX.MuzzleFlash(_s.Position + NavalMath.HeadingToVector(bearing) * _s.Stats.length * 0.28f,
                bearing, big ? 3.2f : 1.6f);
        }

        bool ValidGunTarget(Ship t)
        {
            if (t == null || t.IsDead || t.Damage.IsSinking) return false;
            if (t.Submarine != null && t.Submarine.IsSubmerged) return false;
            if (DetectionSystem.I != null && !DetectionSystem.I.IsVisible(t, _s.team)) return false;
            if (!DetectionSystem.HasLineOfSight(_s.Position, t.Position, true)) return false;
            return true;
        }

        // ------------------------------------------------------------------ secondaries

        void TrySecondaries(float dt)
        {
            var sb = _s.Stats.secondaryBattery;
            if (sb == null || SecondaryReload > 0f) return;
            if (_s.Damage.SystemIntegrity(ShipSystem.SecondaryGuns) < 0.15f) return;

            var enemies = ShipRegistry.OfTeam(Teams.Opponent(_s.team));
            Ship best = null; float bestD = sb.range;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (!ValidGunTarget(e)) continue;
                float d = Vector2.Distance(_s.Position, e.Position);
                if (d < bestD) { bestD = d; best = e; }
            }
            if (best == null) return;

            if (!NavalMath.Intercept(_s.Position, best.Position, best.Velocity, sb.shellSpeed, out Vector2 aim, out float tof))
                aim = best.Position;
            float bearing = NavalMath.VectorToHeading(aim - _s.Position);

            int barrels = Mathf.Max(1, sb.turrets * sb.barrelsPerTurret / 2);   // only half the battery bears
            if (!_s.Resources.ConsumeSecondary(barrels)) return;

            // secondaries always fire HE
            FireSalvo(best, aim, bearing, barrels, sb, DamageSource.Secondary, false,
                sb.damage, sb.penetration, sb.fireChance, false);
            SecondaryReload = sb.reloadTime / Mathf.Max(0.2f, _s.Damage.SystemIntegrity(ShipSystem.SecondaryGuns));
            _s.Detection.NotifyGunFire();
            AudioManager.PlayAt(SoundId.SecondaryGun, _s.Position, 0.35f);
        }

        // ------------------------------------------------------------------ torpedoes

        public bool CanLaunchTorpedoesAt(Vector2 point, out float launchHeading)
        {
            launchHeading = NavalMath.VectorToHeading(point - _s.Position);
            var td = _s.Stats.torpedoes;
            if (td == null || !TorpedoesReady) return false;
            if (_s.Submarine != null && !_s.Submarine.CanFireTorpedoes) return false;
            if (Vector2.Distance(_s.Position, point) > td.range) return false;

            float rel = Mathf.Abs(Mathf.DeltaAngle(_s.Heading, launchHeading));
            float half = td.launchArc * 0.5f;
            return rel >= Mathf.Max(0f, 90f - half) && rel <= Mathf.Min(180f, 90f + half);
        }

        /// <summary>Fires a spread at a moving target, leading it with the torpedo run time.</summary>
        public bool LaunchTorpedoesAtTarget(Ship target)
        {
            var td = _s.Stats.torpedoes;
            if (td == null || target == null) return false;
            if (!NavalMath.Intercept(_s.Position, target.Position, target.Velocity, td.speed, out Vector2 aim, out float tof))
                aim = target.Position;
            return LaunchTorpedoesAt(aim);
        }

        public bool LaunchTorpedoesAt(Vector2 point)
        {
            var td = _s.Stats.torpedoes;
            if (!CanLaunchTorpedoesAt(point, out float heading)) return false;

            // Torpedoes run for kilometres and do not care whose hull they meet. Give the spread a
            // wide berth around friendlies - wider than for guns, because the fish keep going.
            if (GameConfig.FriendlyFire && td != null &&
                FriendlyInLineOfFire(_s.Position + NavalMath.HeadingToVector(heading) * td.range,
                                     Mathf.Max(30f, td.range * Mathf.Sin(td.spread * Mathf.Deg2Rad) + 25f)))
                return false;

            int tubes = Mathf.Min(td.launchers * td.tubesPerLauncher, _s.Resources.TorpedoAmmo);
            if (tubes <= 0) return false;
            _s.Resources.ConsumeTorpedoes(tubes);

            // submarine fish home on whatever the sonar ping is holding
            Ship homing = null;
            if (_s.Abilities != null && _s.Abilities.Has(AbilityId.HomingTorpedoes))
                homing = FindSonarLockedTarget(td.range);

            for (int i = 0; i < tubes; i++)
            {
                float offset = tubes == 1 ? 0f : Mathf.Lerp(-td.spread, td.spread, i / (float)(tubes - 1));
                Vector2 from = _s.Position + NavalMath.HeadingToVector(heading) * _s.Stats.length * 0.25f;
                ProjectileSystem.I.LaunchTorpedo(_s, from, NavalMath.Wrap360(heading + offset), td, homing);
            }

            TorpedoReload = td.reloadTime;
            _s.Detection.NotifyTorpedoLaunch();
            AudioManager.PlayAt(SoundId.TorpedoLaunch, _s.Position, 0.7f);
            if (_s.team == Team.Player)
                GameEvents.RaiseMessage(_s.shipName + ": torpedoes away", Team.Player);
            return true;
        }

        Ship FindSonarLockedTarget(float range)
        {
            var enemies = ShipRegistry.OfTeam(Teams.Opponent(_s.team));
            Ship best = null;
            float bd = range * 1.3f;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || e.IsDead || !e.Detection.SonarLocked) continue;
                float d = _s.DistanceTo(e);
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        // ------------------------------------------------------------------ ASW

        void TryASW(float dt)
        {
            var asw = _s.Stats.asw;
            if (asw == null || ASWReload > 0f || _s.Resources.ASWAmmo <= 0) return;

            var enemies = ShipRegistry.OfTeam(Teams.Opponent(_s.team));
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.Submarine == null || e.IsDead) continue;
                if (!e.Submarine.IsSubmerged) continue;
                if (DetectionSystem.I != null && !DetectionSystem.I.IsVisible(e, _s.team)) continue;

                float d = Vector2.Distance(_s.Position, e.Position);
                if (d > asw.range) continue;

                // lead the boat a little: charges take time to sink
                Vector2 drop = e.Position + e.Velocity * asw.sinkTime * 0.6f + Random.insideUnitCircle * 8f;
                if (!_s.Resources.ConsumeASW(1)) return;
                ProjectileSystem.I.DropDepthCharge(_s, drop, asw);
                ASWReload = asw.reloadTime;
                AudioManager.PlayAt(SoundId.DepthCharge, _s.Position, 0.6f);
                if (_s.team == Team.Player)
                    GameEvents.RaiseMessage(_s.shipName + ": depth charges away", Team.Player);
                return;
            }
        }

        // ------------------------------------------------------------------ smoke

        /// <summary>
        /// Starts the smoke generator. Availability is owned by the consumable slot in ShipAbilities,
        /// which is the only caller, so this does not gate on its own cooldown as well.
        /// </summary>
        public bool DeploySmoke()
        {
            var sd = _s.Stats.smoke;
            if (sd == null) return false;
            SmokeCooldown = sd.cooldown;
            _smokeEmitTimer = sd.emitTime;
            _smokeSpawnTimer = 0f;
            AudioManager.PlayAt(SoundId.Smoke, _s.Position, 0.6f);
            if (_s.team == Team.Player)
                GameEvents.RaiseMessage(_s.shipName + ": making smoke", Team.Player);
            return true;
        }

        void TickSmokeEmission(float dt)
        {
            if (_smokeEmitTimer <= 0f) return;
            var sd = _s.Stats.smoke;
            _smokeEmitTimer -= dt;
            _smokeSpawnTimer -= dt;
            if (_smokeSpawnTimer > 0f) return;
            _smokeSpawnTimer = 0.8f;
            SmokeSystem.I?.Deploy(_s.Position, sd.radius, sd.duration, _s.team);
        }
    }
}
