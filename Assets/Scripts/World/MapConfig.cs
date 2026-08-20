using UnityEngine;

namespace Naval
{
    public enum MapPreset { OceanArchipelago, OpenSea, StraitClash }
    public enum FlagLayout { ThreePoint, KingOfTheHill, TwoFlagAssault }
    public enum IslandDensity { Low, Medium, High, Procedural }

    /// <summary>
    /// Everything that defines the battlefield, chosen before the match. Presets set sensible
    /// defaults for terrain, flag layout and how far apart the fleets start; the individual
    /// parameters can then be overridden.
    /// </summary>
    public class MapConfig
    {
        public MapPreset preset = MapPreset.OceanArchipelago;
        public FlagLayout flagLayout = FlagLayout.ThreePoint;
        public IslandDensity islandDensity = IslandDensity.Medium;
        public WeatherType weather = WeatherType.Clear;

        /// <summary>Capture circle radius in world units. 1 unit is about 10 m, so 50-200 units is 500-2000 m.</summary>
        public float captureRadius = 150f;

        /// <summary>
        /// How far each fleet starts from the centre line, as a fraction of the map half width.
        /// Ships run at real speeds - a Yamato makes 27 knots, or 1.39 units/second - so these are
        /// set so the two lines start just outside mutual battleship spotting range (14.1 km) and
        /// a destroyer reaches the nearest cap in about five minutes.
        /// </summary>
        public float spawnDistance = 0.375f;

        public const float MinCaptureRadius = 50f;
        public const float MaxCaptureRadius = 200f;

        public static MapConfig ForPreset(MapPreset p)
        {
            var c = new MapConfig { preset = p };
            switch (p)
            {
                case MapPreset.OpenSea:
                    // no cover at all, so the fleets start closer or the approach is a long empty sail
                    c.islandDensity = IslandDensity.Low;
                    c.flagLayout = FlagLayout.KingOfTheHill;
                    c.spawnDistance = 0.36f;
                    c.captureRadius = 190f;
                    break;

                case MapPreset.StraitClash:
                    // two landmasses squeeze everything through the middle
                    c.islandDensity = IslandDensity.High;
                    c.flagLayout = FlagLayout.TwoFlagAssault;
                    c.spawnDistance = 0.55f;
                    c.captureRadius = 130f;
                    break;

                default:
                    c.islandDensity = IslandDensity.Medium;
                    c.flagLayout = FlagLayout.ThreePoint;
                    c.spawnDistance = 0.375f;
                    c.captureRadius = 150f;
                    break;
            }
            return c;
        }

        /// <summary>Island count multiplier for the chosen density.</summary>
        public float DensityScale
        {
            get
            {
                switch (islandDensity)
                {
                    case IslandDensity.Low: return 0.35f;
                    case IslandDensity.High: return 1.7f;
                    case IslandDensity.Procedural: return Random.Range(0.3f, 1.8f);
                    default: return 1f;
                }
            }
        }

        public int ZoneCount
        {
            get
            {
                switch (flagLayout)
                {
                    case FlagLayout.KingOfTheHill: return 1;
                    case FlagLayout.TwoFlagAssault: return 2;
                    default: return 3;
                }
            }
        }

        public string PresetName
        {
            get
            {
                switch (preset)
                {
                    case MapPreset.OpenSea: return "Open Sea";
                    case MapPreset.StraitClash: return "Strait Clash";
                    default: return "Ocean Archipelago";
                }
            }
        }

        public string LayoutName
        {
            get
            {
                switch (flagLayout)
                {
                    case FlagLayout.KingOfTheHill: return "King of the Hill";
                    case FlagLayout.TwoFlagAssault: return "Two Flag Assault";
                    default: return "Three Point Domination";
                }
            }
        }

        public MapConfig Clone() => (MapConfig)MemberwiseClone();
    }
}
