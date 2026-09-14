using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Naval
{
    public enum EditorTool { Select, PlaceShip, PlaceZone, PlaceIsland }
    public enum EditorPick { None, Ship, Zone, Island }

    /// <summary>
    /// The scenario editor's hands: picking, dragging, placing and deleting the things a battle is
    /// made of. It edits the <see cref="Scenario"/> data directly and draws it in the world, so what
    /// you see on screen is exactly what gets saved.
    ///
    /// Nothing here spawns real ships. Hulls are drawn as markers until the scenario is played,
    /// which keeps the editor cheap and stops the simulation running while you are still arranging.
    /// </summary>
    public class ScenarioEditor : MonoBehaviour
    {
        public static ScenarioEditor I { get; private set; }

        public Scenario Current = Scenario.Default();

        public EditorTool Tool = EditorTool.Select;
        public ShipClassType PaletteClass = ShipClassType.Destroyer;
        public Team PaletteTeam = Team.Player;

        public EditorPick PickKind { get; private set; }
        public int PickIndex { get; private set; } = -1;

        public string Status { get; private set; } = "";

        bool _dragging;
        bool _resizing;          // dragging a zone or island rim rather than its middle
        Vector2 _dragOffset;

        public static ScenarioEditor Create(Transform parent)
        {
            var go = new GameObject("ScenarioEditor");
            go.transform.SetParent(parent, false);
            var e = go.AddComponent<ScenarioEditor>();
            I = e;
            return e;
        }

        float PS => RTSCamera.I != null ? RTSCamera.I.PixelScale : 0.15f;

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || gm.Phase != GamePhase.Editor) return;

            HandleInput();
            Draw();
        }

        // ------------------------------------------------------------------ input

        void HandleInput()
        {
            var cam = RTSCamera.I;
            if (cam == null) return;

            bool overUI = UIManager.IsPointerOverUI(InputHub.MousePosition);
            Vector2 world = cam.ScreenToWorld(InputHub.MousePosition);

            if (InputHub.KeyDown(Key.Delete) || InputHub.KeyDown(Key.Backspace)) DeleteSelection();
            if (InputHub.KeyDown(Key.Escape)) { PickKind = EditorPick.None; PickIndex = -1; Tool = EditorTool.Select; }

            if (InputHub.LeftDown && !overUI)
            {
                switch (Tool)
                {
                    case EditorTool.PlaceShip:   PlaceShip(world);   break;
                    case EditorTool.PlaceZone:   PlaceZone(world);   break;
                    case EditorTool.PlaceIsland: PlaceIsland(world); break;
                    default:                     PickAt(world);      break;
                }
            }

            if (!InputHub.LeftHeld) { _dragging = false; _resizing = false; }

            if (_dragging) DragSelection(world);

            // right-drag turns the selected ship
            if (InputHub.RightHeld && !overUI && PickKind == EditorPick.Ship && PickIndex >= 0)
            {
                var sh = Current.ships[PickIndex];
                Vector2 d = world - new Vector2(sh.x, sh.y);
                if (d.sqrMagnitude > 4f) sh.heading = NavalMath.VectorToHeading(d);
            }
        }

        void PlaceShip(Vector2 world)
        {
            var stats = ShipDatabase.Get(PaletteClass);
            if (NavGrid.I != null) world = NavGrid.I.NearestNavigable(world, stats.draft);
            Current.ships.Add(new ScenarioShip
            {
                cls = PaletteClass,
                team = PaletteTeam,
                x = world.x, y = world.y,
                heading = PaletteTeam == Team.Player ? 0f : 180f
            });
            PickKind = EditorPick.Ship;
            PickIndex = Current.ships.Count - 1;
            _dragging = true;
            _dragOffset = Vector2.zero;
            Status = "Placed " + ShipDatabase.ShortTag(PaletteClass) + " (" + PaletteTeam + ")";
        }

        void PlaceZone(Vector2 world)
        {
            string name = ((char)('A' + Mathf.Clamp(Current.zones.Count, 0, 25))).ToString();
            Current.zones.Add(new ScenarioZone { name = name, x = world.x, y = world.y, radius = 150f });
            PickKind = EditorPick.Zone;
            PickIndex = Current.zones.Count - 1;
            _dragging = true; _resizing = true;      // drag straight out to size it
            _dragOffset = Vector2.zero;
            Status = "Placed zone " + name + " - drag to size";
        }

        void PlaceIsland(Vector2 world)
        {
            Current.useCustomIslands = true;
            Current.islands.Add(new ScenarioIsland { x = world.x, y = world.y, radius = 120f });
            PickKind = EditorPick.Island;
            PickIndex = Current.islands.Count - 1;
            _dragging = true; _resizing = true;
            _dragOffset = Vector2.zero;
            Status = "Placed island - drag to size. Terrain rebuilds when you play.";
        }

        /// <summary>Picks whatever is under the cursor, nearest first: ships, then zone rims, then islands.</summary>
        void PickAt(Vector2 world)
        {
            PickKind = EditorPick.None; PickIndex = -1;
            float ps = PS;

            float best = float.MaxValue;
            for (int i = 0; i < Current.ships.Count; i++)
            {
                var sh = Current.ships[i];
                float grab = Mathf.Max(ShipDatabase.Get(sh.cls).length * 0.8f, ps * 14f);
                float d = Vector2.Distance(world, new Vector2(sh.x, sh.y));
                if (d < grab && d < best) { best = d; PickKind = EditorPick.Ship; PickIndex = i; }
            }
            if (PickKind == EditorPick.Ship)
            {
                var sh = Current.ships[PickIndex];
                _dragOffset = new Vector2(sh.x, sh.y) - world;
                _dragging = true; _resizing = false;
                Status = ShipDatabase.ShortTag(sh.cls) + " (" + sh.team + ") selected";
                return;
            }

            for (int i = 0; i < Current.zones.Count; i++)
            {
                var z = Current.zones[i];
                float d = Vector2.Distance(world, new Vector2(z.x, z.y));
                bool onRim = Mathf.Abs(d - z.radius) < Mathf.Max(z.radius * 0.14f, ps * 10f);
                if (d < z.radius || onRim)
                {
                    PickKind = EditorPick.Zone; PickIndex = i;
                    _resizing = onRim;
                    _dragOffset = onRim ? Vector2.zero : new Vector2(z.x, z.y) - world;
                    _dragging = true;
                    Status = "Zone " + z.name + (onRim ? " - resizing" : " selected");
                    return;
                }
            }

            for (int i = 0; i < Current.islands.Count; i++)
            {
                var isl = Current.islands[i];
                float d = Vector2.Distance(world, new Vector2(isl.x, isl.y));
                bool onRim = Mathf.Abs(d - isl.radius) < Mathf.Max(isl.radius * 0.16f, ps * 10f);
                if (d < isl.radius || onRim)
                {
                    PickKind = EditorPick.Island; PickIndex = i;
                    _resizing = onRim;
                    _dragOffset = onRim ? Vector2.zero : new Vector2(isl.x, isl.y) - world;
                    _dragging = true;
                    Status = "Island " + (onRim ? "- resizing" : "selected");
                    return;
                }
            }
            Status = "";
        }

        void DragSelection(Vector2 world)
        {
            var map = WorldMap.I;
            switch (PickKind)
            {
                case EditorPick.Ship:
                    if (PickIndex < 0 || PickIndex >= Current.ships.Count) return;
                    var sh = Current.ships[PickIndex];
                    Vector2 p = world + _dragOffset;
                    if (map != null) p = map.Clamp(p);
                    sh.x = p.x; sh.y = p.y;
                    break;

                case EditorPick.Zone:
                    if (PickIndex < 0 || PickIndex >= Current.zones.Count) return;
                    var z = Current.zones[PickIndex];
                    if (_resizing)
                        z.radius = Mathf.Clamp(Vector2.Distance(world, new Vector2(z.x, z.y)),
                                               MapConfig.MinCaptureRadius, MapConfig.MaxCaptureRadius);
                    else
                    {
                        Vector2 zp = world + _dragOffset;
                        if (map != null) zp = map.Clamp(zp);
                        z.x = zp.x; z.y = zp.y;
                    }
                    break;

                case EditorPick.Island:
                    if (PickIndex < 0 || PickIndex >= Current.islands.Count) return;
                    var isl = Current.islands[PickIndex];
                    if (_resizing)
                        isl.radius = Mathf.Clamp(Vector2.Distance(world, new Vector2(isl.x, isl.y)), 14f, 320f);
                    else
                    {
                        Vector2 ip = world + _dragOffset;
                        if (map != null) ip = map.Clamp(ip);
                        isl.x = ip.x; isl.y = ip.y;
                    }
                    break;
            }
        }

        public void DeleteSelection()
        {
            switch (PickKind)
            {
                case EditorPick.Ship:
                    if (PickIndex >= 0 && PickIndex < Current.ships.Count) Current.ships.RemoveAt(PickIndex);
                    Status = "Ship removed";
                    break;
                case EditorPick.Zone:
                    if (PickIndex >= 0 && PickIndex < Current.zones.Count) Current.zones.RemoveAt(PickIndex);
                    Status = "Zone removed";
                    break;
                case EditorPick.Island:
                    if (PickIndex >= 0 && PickIndex < Current.islands.Count) Current.islands.RemoveAt(PickIndex);
                    Status = "Island removed";
                    break;
            }
            PickKind = EditorPick.None; PickIndex = -1;
        }

        public void ClearAll()
        {
            Current.ships.Clear();
            Current.zones.Clear();
            Current.islands.Clear();
            PickKind = EditorPick.None; PickIndex = -1;
            Status = "Cleared";
        }

        // ------------------------------------------------------------------ drawing

        void Draw()
        {
            float ps = PS;

            for (int i = 0; i < Current.islands.Count; i++)
            {
                var isl = Current.islands[i];
                bool sel = PickKind == EditorPick.Island && PickIndex == i;
                Vector2 c = new Vector2(isl.x, isl.y);
                var col = sel ? new Color(1f, 0.9f, 0.4f, 0.9f) : new Color(0.55f, 0.45f, 0.32f, 0.75f);
                LineDrawer.Circle(c, isl.radius, (sel ? 2f : 1.3f) * ps, col, 40);
                LineDrawer.Cross(c, 6f * ps, 1.2f * ps, col);
            }

            for (int i = 0; i < Current.zones.Count; i++)
            {
                var z = Current.zones[i];
                bool sel = PickKind == EditorPick.Zone && PickIndex == i;
                Vector2 c = new Vector2(z.x, z.y);
                Color col = z.owner == Team.Neutral ? new Color(0.8f, 0.82f, 0.85f, 0.85f) : Teams.Color(z.owner);
                if (sel) col = Color.Lerp(col, Color.white, 0.5f);
                LineDrawer.Circle(c, z.radius, (sel ? 2.4f : 1.5f) * ps, col, 72);
                LineDrawer.DashedCircle(c, z.radius * 0.55f, 1f * ps, new Color(col.r, col.g, col.b, 0.4f), 48);
                LineDrawer.Cross(c, 9f * ps, 1.4f * ps, col);
            }

            for (int i = 0; i < Current.ships.Count; i++)
            {
                var sh = Current.ships[i];
                bool sel = PickKind == EditorPick.Ship && PickIndex == i;
                Vector2 c = new Vector2(sh.x, sh.y);
                var stats = ShipDatabase.Get(sh.cls);
                Color col = Teams.Color(sh.team);
                if (sel) col = Color.Lerp(col, Color.white, 0.55f);

                // a hull-shaped marker: length along the heading, beam across it
                Vector2 fwd = NavalMath.HeadingToVector(sh.heading);
                Vector2 side = new Vector2(fwd.y, -fwd.x);
                float len = Mathf.Max(stats.length * 0.6f, ps * 9f);
                float beam = Mathf.Max(stats.beam * 1.6f, ps * 3.5f);

                LineDrawer.Line(c - fwd * len, c + fwd * len, (sel ? 2.6f : 1.8f) * ps, col);
                LineDrawer.Line(c - side * beam, c + side * beam, (sel ? 2.2f : 1.5f) * ps, col);
                LineDrawer.Arrow(c, c + fwd * len * 1.7f, 1.4f * ps, col, 5f * ps);
                if (sel) LineDrawer.Circle(c, len * 1.5f, 1.4f * ps, new Color(1f, 1f, 1f, 0.6f), 24);
            }
        }
    }
}
