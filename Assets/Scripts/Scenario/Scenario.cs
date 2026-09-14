using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Naval
{
    /// <summary>One hull placed by hand: class, side, where it sits and which way it faces.</summary>
    [System.Serializable]
    public class ScenarioShip
    {
        public ShipClassType cls = ShipClassType.Destroyer;
        public Team team = Team.Player;
        public float x, y, heading;
    }

    /// <summary>A capture circle, including who already holds it when the battle opens.</summary>
    [System.Serializable]
    public class ScenarioZone
    {
        public string name = "A";
        public float x, y;
        public float radius = 150f;
        public Team owner = Team.Neutral;
    }

    [System.Serializable]
    public class ScenarioIsland
    {
        public float x, y;
        public float radius = 120f;
        public bool isRock;
    }

    /// <summary>
    /// A hand-authored battle: the map, the objectives and every ship on it.
    ///
    /// This exists so a training setup is reproducible. The procedural match is fine for play, but
    /// an agent needs the identical situation over and over to learn anything from it, and needs to
    /// be able to come back to it next week.
    /// </summary>
    [System.Serializable]
    public class Scenario
    {
        public string scenarioName = "New Scenario";
        public int seed = 12345;

        public MapPreset preset = MapPreset.OceanArchipelago;
        public WeatherType weather = WeatherType.Clear;
        public IslandDensity density = IslandDensity.Medium;

        public float timeLimit = 1200f;
        public float scoreToWin = 1000f;
        public AIDifficulty aiDifficulty = AIDifficulty.Elite;
        public bool fogOfWar = true;

        /// <summary>When false the terrain stays procedural for this seed and the island list is ignored.</summary>
        public bool useCustomIslands = false;

        public List<ScenarioShip> ships = new List<ScenarioShip>();
        public List<ScenarioZone> zones = new List<ScenarioZone>();
        public List<ScenarioIsland> islands = new List<ScenarioIsland>();

        public bool IsEmpty => ships.Count == 0 && zones.Count == 0;

        public int CountOf(Team t)
        {
            int n = 0;
            for (int i = 0; i < ships.Count; i++) if (ships[i].team == t) n++;
            return n;
        }

        // ------------------------------------------------------------------ storage

        public static string Directory
        {
            get
            {
                string dir = Path.Combine(Application.persistentDataPath, "Scenarios");
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>Strips anything that cannot go in a filename, so a typed name is always safe.</summary>
        public static string SafeFileName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "scenario";
            var sb = new System.Text.StringBuilder();
            foreach (char c in raw)
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' ' ? c : '_');
            string s = sb.ToString().Trim();
            return s.Length == 0 ? "scenario" : s;
        }

        public string Path_ => Path.Combine(Directory, SafeFileName(scenarioName) + ".json");

        public bool Save(out string error)
        {
            error = null;
            try
            {
                File.WriteAllText(Path_, JsonUtility.ToJson(this, true));
                return true;
            }
            catch (System.Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        public static Scenario Load(string fileName, out string error)
        {
            error = null;
            try
            {
                string path = Path.Combine(Directory, SafeFileName(fileName) + ".json");
                if (!File.Exists(path)) { error = "No scenario named " + fileName; return null; }
                var s = JsonUtility.FromJson<Scenario>(File.ReadAllText(path));
                if (s == null) { error = "Could not read " + fileName; return null; }
                // JsonUtility leaves null lists when the file omits them
                if (s.ships == null) s.ships = new List<ScenarioShip>();
                if (s.zones == null) s.zones = new List<ScenarioZone>();
                if (s.islands == null) s.islands = new List<ScenarioIsland>();
                return s;
            }
            catch (System.Exception e)
            {
                error = e.Message;
                return null;
            }
        }

        /// <summary>Every saved scenario name, newest first.</summary>
        public static List<string> ListSaved()
        {
            var names = new List<string>();
            try
            {
                var files = System.IO.Directory.GetFiles(Directory, "*.json");
                System.Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                foreach (var f in files) names.Add(Path.GetFileNameWithoutExtension(f));
            }
            catch (System.Exception) { }
            return names;
        }

        public bool Delete()
        {
            try { if (File.Exists(Path_)) File.Delete(Path_); return true; }
            catch (System.Exception) { return false; }
        }

        public Scenario Clone() => JsonUtility.FromJson<Scenario>(JsonUtility.ToJson(this));

        // ------------------------------------------------------------------ authoring helpers

        /// <summary>
        /// A sensible starting point rather than an empty ocean: the standard three-point layout
        /// with a small balanced fleet on each side, which can then be dragged into shape.
        /// </summary>
        public static Scenario Default()
        {
            var s = new Scenario();
            float half = GameConfig.Half;
            float line = half * 0.375f;
            float spread = half * 0.52f;

            s.zones.Add(new ScenarioZone { name = "A", x = -spread, y = 0f, radius = 150f });
            s.zones.Add(new ScenarioZone { name = "B", x = 0f, y = 0f, radius = 150f });
            s.zones.Add(new ScenarioZone { name = "C", x = spread, y = 0f, radius = 150f });

            var comp = new[] { ShipClassType.Battleship, ShipClassType.Cruiser, ShipClassType.Destroyer };
            for (int i = 0; i < comp.Length; i++)
            {
                float x = (i - 1) * 120f;
                s.ships.Add(new ScenarioShip { cls = comp[i], team = Team.Player, x = x, y = -line, heading = 0f });
                s.ships.Add(new ScenarioShip { cls = comp[i], team = Team.Enemy,  x = x, y =  line, heading = 180f });
            }
            return s;
        }
    }
}
