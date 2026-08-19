using System.Collections.Generic;
using UnityEngine;

namespace Naval
{
    public struct IslandInfo
    {
        public Vector2 center;
        public float radius;      // land radius
        public float hazard;      // radius of the shallow shelf around it
        public bool isRock;
    }

    /// <summary>
    /// Procedural ocean battlefield: a height field where h &gt; 0 is land and h &lt; 0 is water
    /// (-1 abyss .. 0 shoreline). The same field drives the water shader, navigation and AI.
    /// </summary>
    public class WorldMap : MonoBehaviour
    {
        public static WorldMap I { get; private set; }

        public const float DraftToDepth = 0.36f;   // converts a ship draft stat into required normalised depth

        public int Resolution { get; private set; }
        public float Size => GameConfig.WorldSize;
        public float Half => GameConfig.WorldSize * 0.5f;

        float[] _height;
        public Texture2D HeightTexture { get; private set; }

        public readonly List<IslandInfo> Islands = new List<IslandInfo>();
        public readonly List<NavalPort> Ports = new List<NavalPort>();
        public readonly List<CaptureZone> Zones = new List<CaptureZone>();

        /// <summary>Left, centre and right squadron spawns for each side (classic domination layout).</summary>
        public Vector2[] PlayerDeployCenters { get; private set; } = new Vector2[3];
        public Vector2[] EnemyDeployCenters { get; private set; } = new Vector2[3];

        public Vector2 PlayerDeployCenter => PlayerDeployCenters[1];
        public Vector2 EnemyDeployCenter => EnemyDeployCenters[1];
        public float DeployRadius { get; private set; } = 300f;

        /// <summary>
        /// Sizes the deployment areas to the fleets that will spawn in them. Called before generation
        /// so island placement leaves the right amount of sea room - a 30 ship fleet needs far more
        /// than a 6 ship one, and squeezing them into a fixed circle means colliding on the start line.
        /// </summary>
        public void ConfigureDeployment(int largestFleet)
        {
            int perGroup = Mathf.CeilToInt(Mathf.Max(1, largestFleet) / 3f);
            DeployRadius = Mathf.Clamp(150f + perGroup * 34f, 200f, 560f);
        }
        public Vector2 EscortDestination { get; private set; }

        public static readonly string[] GroupNames = { "LEFT", "CENTRE", "RIGHT" };

        /// <summary>The battlefield configuration this map was generated from.</summary>
        public MapConfig Config { get; private set; } = MapConfig.ForPreset(MapPreset.OceanArchipelago);

        /// <summary>Nearest friendly squadron spawn to a point, used to clamp deployment dragging.</summary>
        public Vector2 NearestDeployCenter(Team team, Vector2 p)
        {
            var arr = team == Team.Player ? PlayerDeployCenters : EnemyDeployCenters;
            Vector2 best = arr[0];
            float bd = float.MaxValue;
            for (int i = 0; i < arr.Length; i++)
            {
                float d = (arr[i] - p).sqrMagnitude;
                if (d < bd) { bd = d; best = arr[i]; }
            }
            return best;
        }

        public static WorldMap Create(Transform parent)
        {
            var go = new GameObject("WorldMap");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<WorldMap>();
            I = m;
            return m;
        }

        // ------------------------------------------------------------------ generation

        public void Generate(int seed, GameMode mode, int resolution = 512, MapConfig config = null)
        {
            I = this;
            if (config != null) Config = config;
            Resolution = resolution;
            Random.InitState(seed);
            NavalMath.SetNoiseSeed(seed);

            Islands.Clear();
            BuildIslands(mode);
            BuildHeightField();
            BuildTexture();
            PlaceStrategicPoints(mode);
        }

        void BuildIslands(GameMode mode)
        {
            float density = Config.DensityScale;
            int bigCount = Mathf.RoundToInt((mode == GameMode.Escort ? 7 : 6) * density);
            int smallCount = Mathf.RoundToInt(9 * density);
            int rockCount = Mathf.RoundToInt(14 * density);

            // Open Sea is exactly that: no cover, so gunnery and angling decide everything
            if (Config.preset == MapPreset.OpenSea) { bigCount = 0; smallCount = 0; rockCount = Mathf.Min(rockCount, 4); }

            // Classic domination layout: the two fleets face each other across the middle of the
            // map, each deploying as three squadrons - left flank, centre, right flank - with the
            // caps strung along the centre line between them.
            // Baseline distance comes from the map preset: far enough that the approach is a real
            // phase of the battle, close enough that a battleship is not steaming for six minutes
            // before it can shoot.
            float baseLine = Half * Config.spawnDistance;
            // Strait Clash puts land where the flanks would normally be, so the squadrons have to
            // deploy inside the channel or they spawn hard aground.
            float flank = Config.preset == MapPreset.StraitClash ? Half * 0.11f : Half * 0.55f;

            PlayerDeployCenters = new[]
            {
                new Vector2(-flank, -baseLine),
                new Vector2(0f,     -baseLine),
                new Vector2( flank, -baseLine)
            };
            EnemyDeployCenters = new[]
            {
                new Vector2(-flank, baseLine),
                new Vector2(0f,     baseLine),
                new Vector2( flank, baseLine)
            };

            if (mode == GameMode.Escort)
            {
                // the convoy runs west to east instead, so the spawns rotate onto that axis
                PlayerDeployCenters = new[]
                {
                    new Vector2(-Half * 0.82f, -Half * 0.42f),
                    new Vector2(-Half * 0.82f, -Half * 0.10f),
                    new Vector2(-Half * 0.82f,  Half * 0.22f)
                };
                EnemyDeployCenters = new[]
                {
                    new Vector2(Half * 0.30f, Half * 0.62f),
                    new Vector2(Half * 0.45f, Half * 0.40f),
                    new Vector2(Half * 0.60f, Half * 0.16f)
                };
                EscortDestination = new Vector2(Half * 0.80f, Half * 0.10f);
            }

            // Strait Clash: two big landmasses on the flanks squeeze the whole battle through a
            // narrow channel down the middle, with shallow water on either side of it.
            if (Config.preset == MapPreset.StraitClash)
            {
                // the channel has to stay wide enough for a deployed squadron to manoeuvre in
                float straitHalfWidth = Mathf.Max(Half * 0.30f, DeployRadius + Half * 0.11f + 90f);
                float massRadius = Half * 0.46f;
                Islands.Add(new IslandInfo
                {
                    center = new Vector2(-straitHalfWidth - massRadius * 0.75f, 0f),
                    radius = massRadius,
                    hazard = massRadius + 120f,
                    isRock = false
                });
                Islands.Add(new IslandInfo
                {
                    center = new Vector2(straitHalfWidth + massRadius * 0.75f, 0f),
                    radius = massRadius,
                    hazard = massRadius + 120f,
                    isRock = false
                });
                // the scattered stuff is thinned right down so the channel stays navigable
                bigCount = 0;
                smallCount = Mathf.Min(smallCount, 3);
            }

            for (int i = 0; i < bigCount + smallCount + rockCount; i++)
            {
                bool rock = i >= bigCount + smallCount;
                bool small = !rock && i >= bigCount;
                float radius = rock ? Random.Range(14f, 34f)
                             : small ? Random.Range(55f, 105f)
                                     : Random.Range(120f, 235f);

                Vector2 c = Vector2.zero;
                bool ok = false;
                for (int attempt = 0; attempt < 60 && !ok; attempt++)
                {
                    c = new Vector2(Random.Range(-Half * 0.92f, Half * 0.92f), Random.Range(-Half * 0.92f, Half * 0.92f));
                    ok = true;
                    float clearance = radius + 150f;

                    // every squadron spawn needs sea room, and the caps must stay open water
                    for (int k = 0; k < 3 && ok; k++)
                    {
                        if (Vector2.Distance(c, PlayerDeployCenters[k]) < DeployRadius + clearance) ok = false;
                        if (Vector2.Distance(c, EnemyDeployCenters[k]) < DeployRadius + clearance) ok = false;
                    }
                    var caps = CapSpots(mode);
                    for (int k = 0; k < caps.Length && ok; k++)
                        if (Vector2.Distance(c, caps[k]) < 190f + radius) ok = false;

                    if (mode == GameMode.Escort && NavalMath.DistanceToSegment(c, PlayerDeployCenter, EscortDestination) < radius + 150f) ok = false;
                    for (int j = 0; j < Islands.Count && ok; j++)
                    {
                        float need = Islands[j].radius + radius + (rock ? 40f : 130f);
                        if (Vector2.Distance(c, Islands[j].center) < need) ok = false;
                    }
                }
                if (!ok) continue;

                Islands.Add(new IslandInfo
                {
                    center = c,
                    radius = radius,
                    hazard = radius + (rock ? 45f : 110f),
                    isRock = rock
                });
            }
        }

        void BuildHeightField()
        {
            int n = Resolution;
            _height = new float[n * n];
            float step = Size / n;
            float origin = -Half + step * 0.5f;

            for (int y = 0; y < n; y++)
            {
                float wy = origin + y * step;
                for (int x = 0; x < n; x++)
                {
                    float wx = origin + x * step;
                    _height[y * n + x] = ComputeHeight(new Vector2(wx, wy));
                }
            }
        }

        float ComputeHeight(Vector2 w)
        {
            // island contribution -----------------------------------------------------
            float land = -999f;
            float nearestShore = 99999f;     // distance from the nearest coastline
            float warp = (NavalMath.FBM(w.x * 0.006f, w.y * 0.006f, 4) - 0.5f);

            for (int i = 0; i < Islands.Count; i++)
            {
                var isl = Islands[i];
                float d = Vector2.Distance(w, isl.center);
                float r = isl.radius * (1f + warp * (isl.isRock ? 0.35f : 0.55f));
                float t = 1f - d / Mathf.Max(1f, r);
                if (t > land) land = t;
                nearestShore = Mathf.Min(nearestShore, d - r);
            }

            if (land > 0f)
            {
                // above water: dome profile with rocky noise on top
                float h = Mathf.Pow(Mathf.Clamp01(land), 0.65f);
                float rough = NavalMath.FBM(w.x * 0.02f, w.y * 0.02f, 4);
                return Mathf.Clamp01(h * (0.75f + rough * 0.5f)) * 0.95f + 0.02f;
            }

            // water: a continental shelf that deepens away from the beach, plus offshore banks.
            // The profile matters for gameplay: a destroyer (draft 0.30) clears the shelf about
            // 17 units off the beach, a battleship (draft 0.72) needs roughly 50.
            float shelf = Mathf.Pow(Mathf.Clamp01(nearestShore / 240f), 0.85f);
            float basin = NavalMath.FBM(w.x * 0.0022f + 11f, w.y * 0.0022f + 7f, 4);
            float banks = NavalMath.FBM(w.x * 0.0065f + 40f, w.y * 0.0065f + 90f, 3);

            float depth = shelf * Mathf.Lerp(0.78f, 1.1f, basin);

            // offshore shoals: broad shallow banks that only light hulls can cross
            float shoal = Mathf.InverseLerp(0.78f, 0.96f, banks);
            depth = Mathf.Lerp(depth, Mathf.Min(depth, 0.17f), shoal);

            // map edges are open ocean
            float edge = Mathf.InverseLerp(Half, Half * 0.86f, Mathf.Max(Mathf.Abs(w.x), Mathf.Abs(w.y)));
            depth = Mathf.Lerp(1f, depth, edge);

            return -Mathf.Clamp(depth, 0.015f, 1f);
        }

        void BuildTexture()
        {
            int n = Resolution;
            // reuse the texture across regenerations so materials keep a valid reference
            if (HeightTexture != null && (HeightTexture.width != n || HeightTexture.height != n))
            {
                Destroy(HeightTexture);
                HeightTexture = null;
            }
            if (HeightTexture == null)
                HeightTexture = new Texture2D(n, n, TextureFormat.RFloat, false, true)
                {
                    name = "heightfield",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave
                };
            // Height is -1..1 but texture uploads clamp to 0..1, so store it biased and
            // let the shader decode it back (see NavalOcean.shader).
            var px = new Color[n * n];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(_height[i] * 0.5f + 0.5f, 0f, 0f, 1f);
            HeightTexture.SetPixels(px);
            HeightTexture.Apply(false);
        }

        /// <summary>
        /// Cap positions. Domination strings A, B and C along the centre line between the fleets,
        /// so every squadron has an equal run to the point in front of it.
        /// </summary>
        Vector2[] CapSpots(GameMode mode)
        {
            if (mode == GameMode.Escort)
                return new[]
                {
                    new Vector2(-Half * 0.30f, -Half * 0.05f),
                    new Vector2( Half * 0.15f,  Half * 0.10f),
                    new Vector2( Half * 0.55f,  Half * 0.05f)
                };

            float spread = Half * 0.52f;
            if (mode == GameMode.CaptureAndControl)
                return new[]
                {
                    new Vector2(-spread, 0f),
                    Vector2.zero,
                    new Vector2(spread, 0f),
                    new Vector2(-spread * 0.5f, Half * 0.34f),
                    new Vector2( spread * 0.5f, -Half * 0.34f)
                };

            switch (Config.flagLayout)
            {
                case FlagLayout.KingOfTheHill:
                    // one big prize dead centre: everything converges
                    return new[] { Vector2.zero };

                case FlagLayout.TwoFlagAssault:
                    // a flag in front of each base - you win by taking theirs, so both sides must
                    // decide how much to commit forward and how much to leave at home
                    float baseLine = Half * Config.spawnDistance;
                    return new[]
                    {
                        new Vector2(0f, -baseLine * 0.55f),   // A, in front of our base
                        new Vector2(0f,  baseLine * 0.55f)    // B, in front of theirs
                    };

                default:
                    return new[]
                    {
                        new Vector2(-spread, 0f),   // A, off the left flank
                        Vector2.zero,               // B, dead centre
                        new Vector2(spread, 0f)     // C, off the right flank
                    };
            }
        }

        void PlaceStrategicPoints(GameMode mode)
        {
            foreach (var p in Ports) if (p != null) Destroy(p.gameObject);
            foreach (var z in Zones) if (z != null) Destroy(z.gameObject);
            Ports.Clear(); Zones.Clear();

            // ------------------------------------------------------------- ports
            Ports.Add(NavalPort.Create(transform, FindHarborSpot(PlayerDeployCenter), Team.Player, "Port Kestrel"));
            Ports.Add(NavalPort.Create(transform, FindHarborSpot(EnemyDeployCenter), Team.Enemy, "Wolfsbucht"));

            // ------------------------------------------------------------- capture zones
            var names = new[] { "A", "B", "C", "D", "E" };
            var spots = CapSpots(mode);

            float capRadius = Mathf.Clamp(Config.captureRadius, MapConfig.MinCaptureRadius, MapConfig.MaxCaptureRadius);
            for (int i = 0; i < spots.Length; i++)
            {
                Vector2 p = FindOpenWater(Clamp(spots[i]), 300f, 0.25f);
                Zones.Add(CaptureZone.Create(transform, p, capRadius, names[i % names.Length]));
            }
        }

        Vector2 FindHarborSpot(Vector2 near)
        {
            // tuck the harbour in behind the deployment line, away from the contested middle
            Vector2 outward = near.sqrMagnitude > 1f ? near.normalized : Vector2.down;
            return FindOpenWater(Clamp(near + outward * 170f), 240f, 0.2f);
        }

        public Vector2 FindOpenWater(Vector2 preferred, float searchRadius, float minDepth)
        {
            if (SampleDepth(preferred) >= minDepth) return preferred;
            for (int ring = 1; ring <= 12; ring++)
            {
                float r = searchRadius * ring / 12f;
                for (int a = 0; a < 16; a++)
                {
                    float ang = (a / 16f) * Mathf.PI * 2f + ring * 0.31f;
                    Vector2 p = preferred + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                    p = Clamp(p);
                    if (SampleDepth(p) >= minDepth) return p;
                }
            }
            return preferred;
        }

        // ------------------------------------------------------------------ sampling

        public float SampleHeight(Vector2 w)
        {
            if (_height == null) return -1f;
            int n = Resolution;
            float u = (w.x + Half) / Size * n - 0.5f;
            float v = (w.y + Half) / Size * n - 0.5f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, n - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(v), 0, n - 1);
            int x1 = Mathf.Min(x0 + 1, n - 1);
            int y1 = Mathf.Min(y0 + 1, n - 1);
            float fx = Mathf.Clamp01(u - x0), fy = Mathf.Clamp01(v - y0);
            float h00 = _height[y0 * n + x0], h10 = _height[y0 * n + x1];
            float h01 = _height[y1 * n + x0], h11 = _height[y1 * n + x1];
            return Mathf.Lerp(Mathf.Lerp(h00, h10, fx), Mathf.Lerp(h01, h11, fx), fy);
        }

        /// <summary>0 at the shoreline, 1 in the deep ocean. Negative over land.</summary>
        public float SampleDepth(Vector2 w) => -SampleHeight(w);

        public bool IsLand(Vector2 w) => SampleHeight(w) > 0f;

        public bool IsNavigable(Vector2 w, float draft) => SampleDepth(w) >= draft * DraftToDepth;

        public bool InBounds(Vector2 w) => Mathf.Abs(w.x) <= Half && Mathf.Abs(w.y) <= Half;

        public Vector2 Clamp(Vector2 w)
        {
            float m = Half - 12f;
            return new Vector2(Mathf.Clamp(w.x, -m, m), Mathf.Clamp(w.y, -m, m));
        }

        /// <summary>Approximate outward gradient of the sea floor, used to steer away from shoals.</summary>
        public Vector2 DepthGradient(Vector2 w, float sample = 22f)
        {
            float dx = SampleDepth(w + Vector2.right * sample) - SampleDepth(w - Vector2.right * sample);
            float dy = SampleDepth(w + Vector2.up * sample) - SampleDepth(w - Vector2.up * sample);
            return new Vector2(dx, dy);
        }

        public NavalPort NearestPort(Vector2 pos, Team team)
        {
            NavalPort best = null; float bd = float.MaxValue;
            foreach (var p in Ports)
            {
                if (p == null || p.team != team) continue;
                float d = Vector2.SqrMagnitude(p.Position - pos);
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }
    }
}
