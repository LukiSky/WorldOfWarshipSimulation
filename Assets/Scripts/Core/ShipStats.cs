using UnityEngine;

namespace Naval
{
    /// <summary>Data driven gun battery description.</summary>
    [System.Serializable]
    public class GunData
    {
        public int turrets = 3;
        public int barrelsPerTurret = 3;
        public float damage = 300f;
        public float penetration = 100f;      // "mm" of armour defeated at mid range
        public float reloadTime = 8f;
        public float range = 700f;
        public float shellSpeed = 260f;       // world units / second
        public float dispersion = 22f;        // radius (units) of the fall-of-shot pattern at max range
        public float traverseSpeed = 25f;     // degrees / second
        public float fireChance = 0.09f;
        public float minRange = 0f;
        public bool surfaceOnly = false;      // deck guns cannot fire submerged
        public float frontalArcBlock = 20f;   // degrees around the bow/stern where rear/front turrets cannot fire

        public GunData Clone() => (GunData)MemberwiseClone();
    }

    [System.Serializable]
    public class TorpedoData
    {
        public int launchers = 2;
        public int tubesPerLauncher = 4;
        public float damage = 4000f;
        public float reloadTime = 50f;
        public float range = 320f;
        public float speed = 20f;
        public float spread = 6f;             // degrees
        public float detectRange = 55f;       // how far away an alert ship notices the wake
        public float floodChance = 0.55f;
        public float armTime = 1.5f;
        public float launchArc = 140f;        // usable degrees off the beam

        public TorpedoData Clone() => (TorpedoData)MemberwiseClone();
    }

    [System.Serializable]
    public class ASWData
    {
        public float damage = 1500f;
        public float radius = 26f;
        public float reloadTime = 18f;
        public float range = 90f;             // projector throw range (0 = roll off the stern)
        public float sinkTime = 3.5f;
        public DepthState minDepth = DepthState.Periscope;

        public ASWData Clone() => (ASWData)MemberwiseClone();
    }

    [System.Serializable]
    public class SmokeData
    {
        public float duration = 24f;
        public float radius = 42f;
        public float cooldown = 95f;
        public float emitTime = 8f;

        public SmokeData Clone() => (SmokeData)MemberwiseClone();
    }

    /// <summary>Carrier air group: how many squadrons, how far they reach and what they do on arrival.</summary>
    [System.Serializable]
    public class AirWingData
    {
        public int squadrons = 3;            // how many can be aloft at once
        public int aircraftPerSquadron = 6;
        public float launchInterval = 22f;   // seconds between launches
        public float strikeRange = 1500f;    // far beyond any gun
        public float cruiseSpeed = 34f;      // much faster than any hull
        public float damagePerAircraft = 480f;
        public float torpedoChance = 0.5f;   // otherwise bombs: less damage, starts fires
        public float floodChance = 0.45f;
        public float fireChance = 0.35f;
        public float rearmTime = 30f;
        public float aircraftHealth = 100f;

        public AirWingData Clone() => (AirWingData)MemberwiseClone();
    }

    [System.Serializable]
    public class SubmarineData
    {
        public float batteryCapacity = 100f;
        public float drainPeriscope = 0.7f;
        public float drainSubmerged = 1.3f;
        public float drainDeep = 2.4f;
        public float rechargeRate = 5.5f;
        public float diveTime = 3.5f;
        public float speedMulPeriscope = 0.8f;
        public float speedMulSubmerged = 0.65f;
        public float speedMulDeep = 0.5f;
        public float visibilityMulPeriscope = 0.45f;
        public float visibilityMulSubmerged = 0.2f;
        public float visibilityMulDeep = 0.1f;

        public SubmarineData Clone() => (SubmarineData)MemberwiseClone();
    }

    /// <summary>Full statistics block for a ship class. Cloned per ship so damage can modify it.</summary>
    [System.Serializable]
    public class ShipStats
    {
        public ShipClassType classType = ShipClassType.Destroyer;
        public string className = "Destroyer";

        // Hull geometry
        public float length = 12f;
        public float beam = 1.6f;
        public float draft = 0.5f;            // required water depth (normalised 0..1)

        // Movement
        public float maxSpeed = 6f;
        public float reverseSpeed = 2f;
        public float acceleration = 1.1f;
        public float deceleration = 1.4f;
        public float turnRate = 20f;          // degrees/second at optimum speed
        public float rudderShift = 2.5f;      // how fast the rudder reaches full deflection

        // Survivability
        public float maxHealth = 1600f;
        public float armor = 15f;
        public float citadelArmor = 20f;
        public float floodRate = 9f;          // hp/sec per flooding stack
        public float fireRate = 5f;           // hp/sec per fire stack

        // Sensors
        public float baseDetectability = 190f;
        public float spotRange = 620f;
        public float sonarRange = 0f;         // active sonar, detects submerged contacts
        public float hydroRange = 90f;        // detects torpedoes / close contacts through smoke

        // Logistics
        public float fuelCapacity = 100f;
        public float fuelBurn = 0.32f;        // per second at full throttle
        public float repairRate = 1.6f;       // system integrity %/sec
        public float damageControlCooldown = 60f;
        public float damageControlHeal = 0.12f; // fraction of max hp restored by a DC party

        // Armament
        public GunData mainBattery;
        public GunData secondaryBattery;
        public TorpedoData torpedoes;
        public ASWData asw;
        public SmokeData smoke;
        public SubmarineData submarine;
        public AirWingData airWing;
        public float aaRating = 0f;

        // Ammunition
        public int ammoMain = 300;
        public int ammoSecondary = 400;
        public int ammoTorpedo = 12;
        public int ammoASW = 0;

        // Presentation
        public Color hullColor = new Color(0.32f, 0.36f, 0.42f);
        public Color deckColor = new Color(0.24f, 0.27f, 0.31f);
        public int fleetPointCost = 2;

        public bool IsSubmarine => classType == ShipClassType.Submarine;

        public ShipStats Clone()
        {
            var s = (ShipStats)MemberwiseClone();
            s.mainBattery = mainBattery != null ? mainBattery.Clone() : null;
            s.secondaryBattery = secondaryBattery != null ? secondaryBattery.Clone() : null;
            s.torpedoes = torpedoes != null ? torpedoes.Clone() : null;
            s.asw = asw != null ? asw.Clone() : null;
            s.smoke = smoke != null ? smoke.Clone() : null;
            s.submarine = submarine != null ? submarine.Clone() : null;
            s.airWing = airWing != null ? airWing.Clone() : null;
            return s;
        }
    }
}
