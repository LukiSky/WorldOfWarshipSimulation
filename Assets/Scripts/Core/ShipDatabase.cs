using System.Collections.Generic;
using UnityEngine;

namespace Naval
{
    /// <summary>Data driven ship class definitions. Everything gameplay related lives here.</summary>
    public static class ShipDatabase
    {
        static Dictionary<ShipClassType, ShipStats> _cache;

        public static ShipStats Get(ShipClassType type)
        {
            if (_cache == null) Build();
            return _cache[type].Clone();
        }

        public static IEnumerable<ShipClassType> CombatClasses
        {
            get
            {
                yield return ShipClassType.Destroyer;
                yield return ShipClassType.Cruiser;
                yield return ShipClassType.Battleship;
                yield return ShipClassType.Submarine;
            }
        }

        // ------------------------------------------------------------------ scale
        // The battlefield runs at 1 world unit = 10 m, so:
        //   kilometres x 100  -> units          metres / 10   -> units
        //   knots / 19.4      -> units/second   m/s   / 10    -> units/second
        // Every figure below is a real Tier 10 statistic put through those conversions.
        public const float KmToUnits = 100f;
        public const float KnotsToUnits = 1f / 19.4f;
        public const float MpsToUnits = 0.1f;

        static float Km(float km) => km * KmToUnits;
        static float Kn(float knots) => knots * KnotsToUnits;
        static float Mps(float mps) => mps * MpsToUnits;
        static float Metres(float m) => m * 0.1f;

        /// <summary>
        /// Steady turn rate implied by a turning circle radius at full speed: omega = v / r.
        /// Real warships are genuinely this sluggish - a Yamato needs the better part of seven
        /// minutes for a full circle - which is why committing a turn is a real decision.
        /// </summary>
        static float TurnRateFor(float knots, float circleRadiusMetres)
            => Kn(knots) / Metres(circleRadiusMetres) * Mathf.Rad2Deg;

        /// <summary>Rudder shift time in seconds becomes a per-second rate of rudder travel.</summary>
        static float RudderFor(float seconds) => 1f / Mathf.Max(0.1f, seconds);

        // Bunkerage and magazines are sized to outlast the match by half again. Real warships have
        // days of endurance, so fuel and ammunition should never be what decides a twenty minute
        // action - a ship that runs dry on the way to its first objective is a bug, not a tactic.

        /// <summary>
        /// Every surface ship is acquired inside 2 km however good its concealment is - the
        /// guaranteed acquisition range.
        /// </summary>
        const float AssuredAcquisition = 200f;

        /// <summary>
        /// Spotting is limited by the target's concealment, not by how far the lookouts can see, so
        /// this sits above every concealment value on the map and lets stealth do the work.
        /// </summary>
        const float LookoutRange = 2000f;

        static void Build()
        {
            _cache = new Dictionary<ShipClassType, ShipStats>();

            // ============================================================ DESTROYER
            // Shimakaze - Japan, Tier 10. Stealth, speed and the heaviest torpedo salvo afloat.
            var dd = new ShipStats
            {
                classType = ShipClassType.Destroyer,
                className = "Shimakaze",
                length = Metres(129.5f), beam = Metres(11.2f), draft = 0.30f,
                maxSpeed = Kn(39.0f), reverseSpeed = Kn(11f),
                acceleration = 0.42f, deceleration = 0.54f,
                turnRate = TurnRateFor(39.0f, 690f), rudderShift = RudderFor(3.0f),

                maxHealth = 17900f,
                armor = 19f,                    // hull plating
                citadelArmor = 19f,
                hasCitadel = false,             // destroyers have no citadel to hit
                torpedoProtection = 0f,
                floodRate = 84f, fireRate = 47f,

                baseDetectability = Km(5.6f),   // the best concealment on the map
                spotRange = LookoutRange,
                sonarRange = 200f,              // depth-charge search set, not in the source data
                hydroRange = 120f,
                assuredDetectionRange = AssuredAcquisition,

                fuelCapacity = 760f, fuelBurn = 0.42f, repairRate = 2.4f,
                damageControlCooldown = 40f, damageControlHeal = 0.03f,
                aaRating = 81f,
                ammoMain = 1300, ammoSecondary = 0, ammoTorpedo = 120, ammoASW = 40,
                hullColor = new Color(0.33f, 0.38f, 0.44f), deckColor = new Color(0.23f, 0.27f, 0.32f),
                fleetPointCost = 2,

                // 127mm/50: only HE is carried, so the AP columns mirror it
                mainBattery = new GunData
                {
                    turrets = 3, barrelsPerTurret = 2,
                    damage = 2150f, penetration = 21f,
                    reloadTime = 5.7f, range = Km(11.4f), shellSpeed = Mps(915f),
                    dispersion = Metres(104f), traverseSpeed = 7.9f,
                    fireChance = 0.09f, sigma = 2.0f,
                    overmatchThreshold = 127f / 14.3f,
                    heDamage = 2150f, hePenetration = 21f, heFireChance = 0.09f, heShellSpeed = Mps(915f),
                    frontalArcBlock = 22f
                },
                // Type 93 mod 3 "Long Lance"
                torpedoes = new TorpedoData
                {
                    launchers = 3, tubesPerLauncher = 5,
                    damage = 23767f, reloadTime = 153f,
                    range = Km(12.0f), speed = Kn(67f), spread = 7f,
                    detectRange = Km(1.7f),         // long reaction time for the target
                    floodChance = 0.6f, launchArc = 150f
                },
                asw = new ASWData { damage = 4200f, radius = 28f, reloadTime = 16f, range = 70f, sinkTime = 3.2f },
                // smoke generator: 20 s emission, 97 s lifetime, 450 m radius
                smoke = new SmokeData { duration = 97f, radius = Metres(450f), cooldown = 160f, emitTime = 20f }
            };
            _cache[ShipClassType.Destroyer] = dd;

            // ============================================================== CRUISER
            // Des Moines - USA, Tier 10. Auto-loaders, radar, super-heavy AP, exposed citadel.
            var ca = new ShipStats
            {
                classType = ShipClassType.Cruiser,
                className = "Des Moines",
                length = Metres(218f), beam = Metres(23.0f), draft = 0.52f,
                maxSpeed = Kn(33.0f), reverseSpeed = Kn(9f),
                acceleration = 0.26f, deceleration = 0.34f,
                turnRate = TurnRateFor(33.0f, 770f), rudderShift = RudderFor(8.6f),

                maxHealth = 50600f,
                armor = 27f,                    // bow plating: overmatched by 406mm and up
                citadelArmor = 152f,            // belt sits above the waterline
                hasCitadel = true,
                torpedoProtection = 0.07f,
                floodRate = 155f, fireRate = 98f,

                baseDetectability = Km(10.9f),
                spotRange = LookoutRange,
                sonarRange = 200f,
                hydroRange = 95f,
                assuredDetectionRange = AssuredAcquisition,

                fuelCapacity = 600f, fuelBurn = 0.33f, repairRate = 1.8f,
                damageControlCooldown = 60f, damageControlHeal = 0.04f,
                aaRating = 585f,
                ammoMain = 2000, ammoSecondary = 1800, ammoTorpedo = 0, ammoASW = 24,
                hullColor = new Color(0.36f, 0.40f, 0.46f), deckColor = new Color(0.26f, 0.29f, 0.34f),
                fleetPointCost = 3,

                // 203mm/55 auto-loaders. Super-heavy AP: improved ricochet angles, poor at range.
                mainBattery = new GunData
                {
                    turrets = 3, barrelsPerTurret = 3,
                    damage = 5000f, penetration = 450f,
                    reloadTime = 5.5f, range = Km(15.8f), shellSpeed = Mps(762f),
                    dispersion = Metres(143f), traverseSpeed = 30f,
                    fireChance = 0.14f, sigma = 2.05f,
                    overmatchThreshold = 203f / 14.3f,
                    ricochetStart = 60f, ricochetAlways = 75f,
                    heDamage = 2800f, hePenetration = 34f, heFireChance = 0.14f, heShellSpeed = Mps(823f),
                    frontalArcBlock = 26f
                },
                secondaryBattery = new GunData
                {
                    turrets = 4, barrelsPerTurret = 2,
                    damage = 1300f, penetration = 30f,
                    reloadTime = 4f, range = Km(5.0f), shellSpeed = Mps(792f),
                    dispersion = Metres(60f), traverseSpeed = 45f,
                    fireChance = 0.06f, frontalArcBlock = 12f
                },
                asw = new ASWData { damage = 3000f, radius = 22f, reloadTime = 26f, range = 55f, sinkTime = 3.6f },
                smoke = null
            };
            _cache[ShipClassType.Cruiser] = ca;

            // =========================================================== BATTLESHIP
            // Yamato - Japan, Tier 10. The 460mm rifles overmatch 32mm plating, so angling does not
            // save a cruiser from them. Sluggish enough that every course change is a commitment.
            var bb = new ShipStats
            {
                classType = ShipClassType.Battleship,
                className = "Yamato",
                length = Metres(263f), beam = Metres(38.9f), draft = 0.72f,
                maxSpeed = Kn(27.0f), reverseSpeed = Kn(7.5f),
                acceleration = 0.12f, deceleration = 0.15f,
                turnRate = TurnRateFor(27.0f, 900f), rudderShift = RudderFor(22.1f),

                maxHealth = 97200f,
                armor = 32f,                    // bow
                citadelArmor = 410f,            // belt: nothing on the map cuts it bow-on
                hasCitadel = true,
                torpedoProtection = 0.55f,
                floodRate = 210f, fireRate = 158f,

                baseDetectability = Km(14.1f),  // seen from far outside its own spotting need
                spotRange = LookoutRange,
                sonarRange = 0f,
                hydroRange = 70f,
                assuredDetectionRange = AssuredAcquisition,

                fuelCapacity = 540f, fuelBurn = 0.30f, repairRate = 1.1f,
                damageControlCooldown = 80f, damageControlHeal = 0.05f,
                aaRating = 445f,
                ammoMain = 400, ammoSecondary = 2000, ammoTorpedo = 0, ammoASW = 0,
                hullColor = new Color(0.38f, 0.41f, 0.47f), deckColor = new Color(0.27f, 0.30f, 0.35f),
                fleetPointCost = 5,

                // 460mm/45. HE is not in the source data and uses the historical Yamato round.
                mainBattery = new GunData
                {
                    turrets = 3, barrelsPerTurret = 3,
                    damage = 14800f, penetration = 850f,
                    reloadTime = 30f, range = Km(26.6f), shellSpeed = Mps(780f),
                    dispersion = Metres(275f), traverseSpeed = 3.0f,
                    fireChance = 0.36f, sigma = 2.1f,
                    overmatchThreshold = 32f,   // the defining Yamato mechanic
                    heDamage = 7300f, hePenetration = 76f, heFireChance = 0.36f, heShellSpeed = Mps(780f),
                    frontalArcBlock = 30f
                },
                // 155mm/60 wing mounts
                secondaryBattery = new GunData
                {
                    turrets = 2, barrelsPerTurret = 3,
                    damage = 2500f, penetration = 26f,
                    reloadTime = 12f, range = Km(7.3f), shellSpeed = Mps(920f),
                    dispersion = Metres(90f), traverseSpeed = 40f,
                    fireChance = 0.10f, frontalArcBlock = 10f
                }
            };
            _cache[ShipClassType.Battleship] = bb;

            // ============================================================ SUBMARINE
            // Balao - USA, Tier 10. Fights by sonar ping and acoustic homing torpedoes; the dive
            // capacity is the real resource, not the hull.
            var ss = new ShipStats
            {
                classType = ShipClassType.Submarine,
                className = "Balao",
                length = Metres(95f), beam = Metres(8.3f), draft = 0.28f,
                maxSpeed = Kn(30.0f), reverseSpeed = Kn(8f),
                acceleration = 0.30f, deceleration = 0.38f,
                turnRate = TurnRateFor(30.0f, 590f), rudderShift = RudderFor(5.8f),

                maxHealth = 20200f,
                armor = 19f,
                citadelArmor = 19f,
                hasCitadel = false,
                torpedoProtection = 0f,
                floodRate = 226f, fireRate = 65f,

                baseDetectability = Km(5.9f),   // surfaced
                spotRange = Km(8.0f),           // a periscope sees far less than a spotting top
                sonarRange = 0f,
                hydroRange = Km(4.2f),
                assuredDetectionRange = Km(2.0f),

                fuelCapacity = 400f, fuelBurn = 0.22f, repairRate = 1.4f,
                damageControlCooldown = 40f, damageControlHeal = 0.03f,
                ammoMain = 280, ammoTorpedo = 120,
                hullColor = new Color(0.22f, 0.25f, 0.29f), deckColor = new Color(0.17f, 0.19f, 0.23f),
                fleetPointCost = 3,

                // deck gun, surfaced only - not in the source data, but a surfaced boat with no
                // weapon at all cannot defend itself while the tubes reload
                mainBattery = new GunData
                {
                    turrets = 1, barrelsPerTurret = 1,
                    damage = 1600f, penetration = 24f,
                    reloadTime = 4.5f, range = Km(4.0f), shellSpeed = Mps(800f),
                    dispersion = Metres(90f), traverseSpeed = 30f,
                    fireChance = 0.05f, surfaceOnly = true, frontalArcBlock = 0f,
                    heDamage = 1600f, hePenetration = 24f, heFireChance = 0.05f
                },
                // Bow six and stern four are modelled as two groups of five acoustic homing fish.
                // The alternative heavy torpedo is not carried: one profile per boat.
                torpedoes = new TorpedoData
                {
                    launchers = 2, tubesPerLauncher = 5,
                    damage = 7833f, reloadTime = 48f,
                    range = Km(14.0f), speed = Kn(89f), spread = 4f,
                    detectRange = Km(2.1f),
                    floodChance = 0.7f, launchArc = 200f
                },
                // dive capacity: 240 s submerged, draining and recharging at 1/s
                submarine = new SubmarineData
                {
                    batteryCapacity = 240f,
                    drainPeriscope = 1f, drainSubmerged = 1f, drainDeep = 1f,
                    rechargeRate = 1f,
                    diveTime = 4f,
                    speedMulPeriscope = 1f,          // 30 kn at periscope depth
                    speedMulSubmerged = 0.6f,        // 18 kn deep
                    speedMulDeep = 0.6f,
                    visibilityMulPeriscope = 0.39f,  // 2.3 km against 5.9 km surfaced
                    visibilityMulSubmerged = 0.05f,
                    visibilityMulDeep = 0.02f        // found by hydrophone or surveillance only
                }
            };
            _cache[ShipClassType.Submarine] = ss;

            // ============================================================ TRANSPORT
            var tr = new ShipStats
            {
                classType = ShipClassType.Transport,
                className = "Transport",
                length = Metres(180f), beam = Metres(24f), draft = 0.6f,
                maxSpeed = Kn(15f), reverseSpeed = Kn(4f),
                acceleration = 0.10f, deceleration = 0.14f,
                turnRate = TurnRateFor(15f, 820f), rudderShift = RudderFor(18f),
                maxHealth = 30000f, armor = 16f, citadelArmor = 20f,
                hasCitadel = true, torpedoProtection = 0f,
                floodRate = 150f, fireRate = 90f,
                baseDetectability = Km(15f), spotRange = Km(6f), hydroRange = 50f,
                assuredDetectionRange = AssuredAcquisition,
                fuelCapacity = 380f, fuelBurn = 0.2f, repairRate = 0.6f,
                damageControlCooldown = 120f, damageControlHeal = 0.03f,
                ammoMain = 0, ammoSecondary = 0,
                hullColor = new Color(0.42f, 0.36f, 0.28f), deckColor = new Color(0.30f, 0.26f, 0.21f),
                fleetPointCost = 1
            };
            _cache[ShipClassType.Transport] = tr;
        }

        // ------------------------------------------------------------------ names
        static readonly string[] PlayerNames = {
            "Valiant","Dauntless","Vanguard","Intrepid","Resolute","Defiant","Sentinel","Tempest",
            "Aurora","Cormorant","Lancer","Harbinger","Warden","Sovereign","Talon","Marauder",
            "Kestrel","Bulwark","Falchion","Aegis"
        };
        static readonly string[] EnemyNames = {
            "Nachtwolf","Eisenfaust","Roter Stern","Sturmvogel","Kaiserin","Drachen","Wespe","Nordwind",
            "Skarhold","Vindr","Kraken","Basilisk","Nemesis","Wyvern","Grimnir","Fenrir",
            "Onyx","Obsidian","Revenant","Warlock"
        };

        static int _playerIdx, _enemyIdx;

        public static void ResetNames() { _playerIdx = 0; _enemyIdx = 0; }

        public static string NextName(Team team, ShipClassType cls)
        {
            string prefix = team == Team.Player ? "HMS " : "KMS ";
            if (cls == ShipClassType.Transport) prefix = team == Team.Player ? "MV " : "MV ";
            var arr = team == Team.Player ? PlayerNames : EnemyNames;
            int i = team == Team.Player ? _playerIdx++ : _enemyIdx++;
            string n = arr[i % arr.Length];
            if (i >= arr.Length) n += " " + Roman((i / arr.Length) + 1);
            return prefix + n;
        }

        static string Roman(int n)
        {
            switch (n)
            {
                case 2: return "II";
                case 3: return "III";
                case 4: return "IV";
                case 5: return "V";
                default: return n.ToString();
            }
        }

        public static string ShortTag(ShipClassType c)
        {
            switch (c)
            {
                case ShipClassType.Destroyer: return "DD";
                case ShipClassType.Cruiser: return "CA";
                case ShipClassType.Battleship: return "BB";
                case ShipClassType.Submarine: return "SS";
                default: return "TR";
            }
        }
    }
}
