using UnityEngine;

namespace Naval
{
    /// <summary>
    /// One gun mount. Real warships do not have a single shared blind sector: each turret sits at a
    /// point along the hull and can train only so far before the superstructure is in the way, which
    /// is why turning to angle your armour costs you the guns that can no longer bear.
    /// </summary>
    [System.Serializable]
    public class TurretMount
    {
        /// <summary>Signed fraction of hull length: +0.34 is near the bow, -0.34 near the stern.</summary>
        public float position = 0.3f;
        /// <summary>Where the mount points when idle. 0 trains forward, 180 trains aft.</summary>
        public float restHeading = 0f;
        /// <summary>How far either side of rest it can train, in degrees.</summary>
        public float arcHalfWidth = 150f;
        /// <summary>A raised mount firing over the one in front of it, so it sees slightly further round.</summary>
        public bool superfiring = false;

        public TurretMount Clone() => (TurretMount)MemberwiseClone();
    }

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

        /// <summary>
        /// Dispersion tightness. Higher values cluster the salvo at the aim point instead of
        /// spreading it evenly through the pattern; 2.0 is a typical heavy cruiser or battleship.
        /// </summary>
        public float sigma = 1.8f;

        /// <summary>
        /// Plating at or below this thickness (mm) is defeated regardless of impact angle - the
        /// shell is simply too heavy to be turned. 0 disables overmatch. This is what lets a
        /// battleship punch through the bow of an angled cruiser that would otherwise bounce it.
        /// </summary>
        public float overmatchThreshold = 0f;

        /// <summary>Impact angle from the plate normal (degrees) where ricochets become possible.</summary>
        public float ricochetStart = 45f;
        /// <summary>Impact angle from the plate normal (degrees) beyond which the shell always bounces.</summary>
        public float ricochetAlways = 60f;

        /// <summary>
        /// Per-mount geometry and firing arcs. Left null, arcs fall back to a simple fore/aft split
        /// derived from <see cref="turrets"/>, which keeps older data working.
        /// </summary>
        public TurretMount[] mounts;

        // Explicit high-explosive ballistics. Where these are left at 0 the HE round is derived
        // from the AP one, which is what the secondary batteries rely on.
        public float heDamage = 0f;
        public float hePenetration = 0f;
        public float heFireChance = 0f;
        public float heShellSpeed = 0f;

        public GunData Clone()
        {
            var g = (GunData)MemberwiseClone();
            if (mounts != null)
            {
                g.mounts = new TurretMount[mounts.Length];
                for (int i = 0; i < mounts.Length; i++)
                    g.mounts[i] = mounts[i] != null ? mounts[i].Clone() : null;
            }
            return g;
        }
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
        public float armor = 15f;             // hull/bow plating thickness in mm
        public float citadelArmor = 20f;      // citadel belt thickness in mm
        /// <summary>Destroyers and submarines have no citadel: they can never take a citadel hit.</summary>
        public bool hasCitadel = true;
        /// <summary>Fraction of torpedo damage absorbed by the anti-torpedo bulge, 0..1.</summary>
        public float torpedoProtection = 0f;
        public float floodRate = 9f;          // hp/sec per flooding stack
        public float fireRate = 5f;           // hp/sec per fire stack

        // Sensors
        public float baseDetectability = 190f;
        public float spotRange = 620f;
        public float sonarRange = 0f;         // active sonar, detects submerged contacts
        public float hydroRange = 90f;        // detects torpedoes / close contacts through smoke
        /// <summary>Inside this range the ship is spotted no matter how good its concealment is.</summary>
        public float assuredDetectionRange = 0f;

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
            return s;
        }
    }
}
