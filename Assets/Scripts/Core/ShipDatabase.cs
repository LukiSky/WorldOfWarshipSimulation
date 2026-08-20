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

        static void Build()
        {
            _cache = new Dictionary<ShipClassType, ShipStats>();

            // ---------------------------------------------------------- DESTROYER
            var dd = new ShipStats
            {
                classType = ShipClassType.Destroyer,
                className = "Destroyer",
                length = 11f, beam = 1.25f, draft = 0.30f,
                maxSpeed = 6.4f, reverseSpeed = 2.2f, acceleration = 1.35f, deceleration = 1.7f,
                turnRate = 26f, rudderShift = 3.4f,
                maxHealth = 1700f, armor = 13f, citadelArmor = 16f,
                floodRate = 8f, fireRate = 4.5f,
                baseDetectability = 175f, spotRange = 640f, sonarRange = 175f, hydroRange = 120f,
                fuelCapacity = 100f, fuelBurn = 0.42f, repairRate = 2.4f,
                damageControlCooldown = 45f, damageControlHeal = 0.10f,
                aaRating = 38f,
                ammoMain = 420, ammoSecondary = 0, ammoTorpedo = 15, ammoASW = 10,
                hullColor = new Color(0.33f, 0.38f, 0.44f), deckColor = new Color(0.23f, 0.27f, 0.32f),
                fleetPointCost = 2,
                mainBattery = new GunData
                {
                    turrets = 3, barrelsPerTurret = 1, damage = 230f, penetration = 34f,
                    reloadTime = 3.2f, range = 540f, shellSpeed = 230f, dispersion = 16f,
                    traverseSpeed = 40f, fireChance = 0.07f, frontalArcBlock = 22f
                },
                torpedoes = new TorpedoData
                {
                    launchers = 2, tubesPerLauncher = 4, damage = 4300f, reloadTime = 46f,
                    range = 360f, speed = 21f, spread = 7f, detectRange = 62f,
                    floodChance = 0.6f, launchArc = 150f
                },
                asw = new ASWData { damage = 1750f, radius = 28f, reloadTime = 16f, range = 70f, sinkTime = 3.2f },
                smoke = new SmokeData { duration = 26f, radius = 44f, cooldown = 90f, emitTime = 9f }
            };
            _cache[ShipClassType.Destroyer] = dd;

            // ------------------------------------------------------------ CRUISER
            var ca = new ShipStats
            {
                classType = ShipClassType.Cruiser,
                className = "Cruiser",
                length = 17f, beam = 2.1f, draft = 0.52f,
                maxSpeed = 5.3f, reverseSpeed = 1.8f, acceleration = 0.8f, deceleration = 1.05f,
                turnRate = 15f, rudderShift = 2.4f,
                maxHealth = 3600f, armor = 32f, citadelArmor = 60f,
                floodRate = 11f, fireRate = 7f,
                baseDetectability = 300f, spotRange = 570f, sonarRange = 140f, hydroRange = 95f,
                fuelCapacity = 140f, fuelBurn = 0.33f, repairRate = 1.8f,
                damageControlCooldown = 55f, damageControlHeal = 0.11f,
                aaRating = 72f,
                ammoMain = 360, ammoSecondary = 500, ammoTorpedo = 8, ammoASW = 6,
                hullColor = new Color(0.36f, 0.40f, 0.46f), deckColor = new Color(0.26f, 0.29f, 0.34f),
                fleetPointCost = 3,
                mainBattery = new GunData
                {
                    turrets = 3, barrelsPerTurret = 3, damage = 340f, penetration = 95f,
                    reloadTime = 9.5f, range = 730f, shellSpeed = 255f, dispersion = 24f,
                    traverseSpeed = 22f, fireChance = 0.10f, frontalArcBlock = 26f
                },
                secondaryBattery = new GunData
                {
                    turrets = 4, barrelsPerTurret = 2, damage = 90f, penetration = 22f,
                    reloadTime = 4f, range = 200f, shellSpeed = 200f, dispersion = 14f,
                    traverseSpeed = 45f, fireChance = 0.05f, frontalArcBlock = 12f
                },
                torpedoes = new TorpedoData
                {
                    launchers = 2, tubesPerLauncher = 3, damage = 3600f, reloadTime = 70f,
                    range = 240f, speed = 19f, spread = 5f, detectRange = 58f,
                    floodChance = 0.5f, launchArc = 120f
                },
                asw = new ASWData { damage = 1200f, radius = 22f, reloadTime = 26f, range = 55f, sinkTime = 3.6f },
                smoke = null
            };
            _cache[ShipClassType.Cruiser] = ca;

            // --------------------------------------------------------- BATTLESHIP
            var bb = new ShipStats
            {
                classType = ShipClassType.Battleship,
                className = "Battleship",
                length = 25f, beam = 3.4f, draft = 0.72f,
                maxSpeed = 3.9f, reverseSpeed = 1.1f, acceleration = 0.34f, deceleration = 0.42f,
                turnRate = 7.5f, rudderShift = 1.3f,
                maxHealth = 7400f, armor = 95f, citadelArmor = 190f,
                floodRate = 16f, fireRate = 12f,
                baseDetectability = 520f, spotRange = 500f, sonarRange = 0f, hydroRange = 70f,
                fuelCapacity = 220f, fuelBurn = 0.30f, repairRate = 1.1f,
                damageControlCooldown = 80f, damageControlHeal = 0.14f,
                aaRating = 95f,
                ammoMain = 180, ammoSecondary = 700, ammoTorpedo = 0, ammoASW = 0,
                hullColor = new Color(0.38f, 0.41f, 0.47f), deckColor = new Color(0.27f, 0.30f, 0.35f),
                fleetPointCost = 5,
                mainBattery = new GunData
                {
                    turrets = 3, barrelsPerTurret = 3, damage = 1150f, penetration = 320f,
                    reloadTime = 27f, range = 960f, shellSpeed = 300f, dispersion = 34f,
                    traverseSpeed = 5f, fireChance = 0.24f, frontalArcBlock = 30f
                },
                secondaryBattery = new GunData
                {
                    turrets = 6, barrelsPerTurret = 2, damage = 130f, penetration = 30f,
                    reloadTime = 3.5f, range = 240f, shellSpeed = 210f, dispersion = 16f,
                    traverseSpeed = 40f, fireChance = 0.08f, frontalArcBlock = 10f
                }
            };
            _cache[ShipClassType.Battleship] = bb;

            // ---------------------------------------------------------- SUBMARINE
            var ss = new ShipStats
            {
                classType = ShipClassType.Submarine,
                className = "Submarine",
                length = 9.5f, beam = 1.1f, draft = 0.28f,
                maxSpeed = 3.4f, reverseSpeed = 1.1f, acceleration = 0.45f, deceleration = 0.55f,
                turnRate = 12f, rudderShift = 1.8f,
                maxHealth = 1250f, armor = 10f, citadelArmor = 12f,
                floodRate = 14f, fireRate = 4f,
                baseDetectability = 250f, spotRange = 330f, sonarRange = 0f, hydroRange = 420f,
                fuelCapacity = 90f, fuelBurn = 0.22f, repairRate = 1.4f,
                damageControlCooldown = 70f, damageControlHeal = 0.08f,
                ammoMain = 60, ammoTorpedo = 14,
                hullColor = new Color(0.22f, 0.25f, 0.29f), deckColor = new Color(0.17f, 0.19f, 0.23f),
                fleetPointCost = 3,
                mainBattery = new GunData
                {
                    turrets = 1, barrelsPerTurret = 1, damage = 150f, penetration = 24f,
                    reloadTime = 4.5f, range = 230f, shellSpeed = 200f, dispersion = 18f,
                    traverseSpeed = 30f, fireChance = 0.05f, surfaceOnly = true, frontalArcBlock = 0f
                },
                torpedoes = new TorpedoData
                {
                    launchers = 2, tubesPerLauncher = 3, damage = 3900f, reloadTime = 42f,
                    range = 300f, speed = 17f, spread = 4f, detectRange = 50f,
                    floodChance = 0.7f, launchArc = 200f
                },
                submarine = new SubmarineData()
            };
            _cache[ShipClassType.Submarine] = ss;

            // ---------------------------------------------------------- TRANSPORT
            var tr = new ShipStats
            {
                classType = ShipClassType.Transport,
                className = "Transport",
                length = 19f, beam = 3.0f, draft = 0.6f,
                maxSpeed = 2.6f, reverseSpeed = 0.8f, acceleration = 0.3f, deceleration = 0.4f,
                turnRate = 6f, rudderShift = 1.1f,
                maxHealth = 3000f, armor = 8f, citadelArmor = 10f,
                floodRate = 18f, fireRate = 8f,
                baseDetectability = 560f, spotRange = 360f, hydroRange = 50f,
                fuelCapacity = 300f, fuelBurn = 0.2f, repairRate = 0.6f,
                damageControlCooldown = 120f, damageControlHeal = 0.06f,
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
