using UnityEngine;

namespace Naval
{
    /// <summary>
    /// Everything drawn in the world that is not a sprite: selection rings, health and status bars,
    /// order routes, contact markers for the fog of war, capture zones, ports and click feedback.
    /// All of it goes through the batched LineDrawer.
    /// </summary>
    public class WorldOverlay : MonoBehaviour
    {
        public static WorldOverlay I { get; private set; }

        public bool ShowRangeRings = true;

        public static WorldOverlay Create(Transform parent)
        {
            var go = new GameObject("WorldOverlay");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<WorldOverlay>();
            I = w;
            return w;
        }

        float PS => RTSCamera.I != null ? RTSCamera.I.PixelScale : 0.15f;

        void Update()
        {
            OrderMarkers.Prune();
            HitMarkers.Prune();

            DrawZones();
            DrawPorts();
            DrawShips();
            DrawContacts();
            DrawOrders();
            DrawMarkers();
            DrawSelectionBox();
        }

        // ------------------------------------------------------------------ ships

        void DrawShips()
        {
            var all = ShipRegistry.All;
            var sel = SelectionManager.I;
            float ps = PS;

            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s == null || s.IsDead) continue;
                bool friendly = s.team == Team.Player;
                if (!friendly && (s.Visual == null || !s.Visual.VisibleToPlayer)) continue;

                Color teamCol = Teams.Color(s.team);

                // selection / hover rings
                if (s.Selected)
                {
                    float r = s.Stats.length * 0.75f + 6f * ps;
                    LineDrawer.Circle(s.Position, r, Mathf.Max(0.6f, 1.6f * ps), new Color(0.45f, 1f, 0.65f, 0.95f), 40);
                    // heading pointer
                    LineDrawer.Line(s.Position + s.Forward * r, s.Position + s.Forward * (r + 9f * ps), 1.6f * ps, new Color(0.45f, 1f, 0.65f, 0.8f));
                }
                else if (sel != null && sel.Hovered == s)
                {
                    LineDrawer.Circle(s.Position, s.Stats.length * 0.72f, Mathf.Max(0.5f, 1.2f * ps), new Color(1f, 1f, 1f, 0.45f), 32);
                }

                DrawStatusBar(s, teamCol, ps, friendly);

                // enemy target line for the selected ship
                if (s.Selected && s.CurrentTarget != null && !s.CurrentTarget.IsDead)
                    LineDrawer.Dashed(s.Position, s.CurrentTarget.Position, 1.1f * ps,
                        new Color(1f, 0.4f, 0.35f, 0.5f), 10f * ps, 8f * ps, Time.time * 25f * ps);
            }

            // range rings for the primary selection
            if (ShowRangeRings && sel != null && sel.Primary != null)
            {
                var p = sel.Primary;
                if (p.Stats.mainBattery != null)
                    LineDrawer.DashedCircle(p.Position, p.Weapons.MainRange, 1.1f * ps, new Color(1f, 0.72f, 0.35f, 0.28f), 96);
                if (p.Stats.torpedoes != null)
                    LineDrawer.DashedCircle(p.Position, p.Stats.torpedoes.range, 1f * ps, new Color(0.4f, 1f, 0.8f, 0.22f), 80);
                LineDrawer.DashedCircle(p.Position, p.Detectability, 1f * ps, new Color(0.6f, 0.8f, 1f, 0.2f), 72);
                if (p.Stats.sonarRange > 0f && p.Detection != null)
                    LineDrawer.DashedCircle(p.Position, p.Detection.EffectiveSonarRange, 1f * ps, new Color(0.4f, 1f, 0.9f, 0.18f), 64);
            }
        }

        void DrawStatusBar(Ship s, Color teamCol, float ps, bool friendly)
        {
            float w = (friendly ? 46f : 38f) * ps;
            float h = 4.5f * ps;
            float y = s.Position.y + s.Stats.length * 0.62f + 10f * ps;
            Vector2 c = new Vector2(s.Position.x, y);

            Vector2 min = new Vector2(c.x - w * 0.5f, c.y);
            Vector2 max = new Vector2(c.x + w * 0.5f, c.y + h);

            LineDrawer.FilledRect(min - new Vector2(ps, ps), max + new Vector2(ps, ps), new Color(0f, 0f, 0f, 0.55f));

            float frac = Mathf.Clamp01(s.HealthFraction);
            Color hp = frac > 0.6f ? new Color(0.35f, 0.9f, 0.45f) : frac > 0.3f ? new Color(1f, 0.8f, 0.25f) : new Color(1f, 0.35f, 0.3f);
            if (!friendly) hp = Color.Lerp(hp, teamCol, 0.35f);
            LineDrawer.FilledRect(min, new Vector2(Mathf.Lerp(min.x, max.x, frac), max.y), hp);

            // team stripe under the bar
            LineDrawer.FilledRect(new Vector2(min.x, min.y - 2.2f * ps), new Vector2(max.x, min.y - 0.7f * ps),
                new Color(teamCol.r, teamCol.g, teamCol.b, 0.85f));

            // status pips: fires, flooding, reload
            float px = min.x;
            float pipY = max.y + 1.4f * ps;
            float pip = 3.4f * ps;
            for (int i = 0; i < s.Damage.FireStacks; i++)
            {
                LineDrawer.FilledRect(new Vector2(px, pipY), new Vector2(px + pip, pipY + pip), new Color(1f, 0.55f, 0.15f));
                px += pip + 1.2f * ps;
            }
            for (int i = 0; i < s.Damage.FloodingStacks; i++)
            {
                LineDrawer.FilledRect(new Vector2(px, pipY), new Vector2(px + pip, pipY + pip), new Color(0.35f, 0.7f, 1f));
                px += pip + 1.2f * ps;
            }

            if (friendly && s.Stats.mainBattery != null)
            {
                float rl = s.Weapons.MainReloadFraction;
                Vector2 rmin = new Vector2(min.x, min.y - 4.6f * ps);
                Vector2 rmax = new Vector2(Mathf.Lerp(min.x, max.x, rl), min.y - 3.1f * ps);
                LineDrawer.FilledRect(new Vector2(min.x, rmin.y), new Vector2(max.x, rmax.y), new Color(0f, 0f, 0f, 0.4f));
                LineDrawer.FilledRect(rmin, rmax, new Color(0.9f, 0.85f, 0.4f, 0.9f));
            }

            if (s.Submarine != null)
            {
                // depth ladder on the left of the bar
                float bx = min.x - 4f * ps;
                for (int d = 0; d < 4; d++)
                {
                    bool on = (int)s.Submarine.Depth >= d;
                    LineDrawer.FilledRect(new Vector2(bx - 2.4f * ps, min.y - d * 2.2f * ps),
                        new Vector2(bx, min.y - d * 2.2f * ps + 1.6f * ps),
                        on ? new Color(0.5f, 0.9f, 1f, 0.9f) : new Color(0.3f, 0.4f, 0.5f, 0.5f));
                }
            }
        }

        // ------------------------------------------------------------------ contacts

        void DrawContacts()
        {
            var ds = DetectionSystem.I;
            if (ds == null) return;
            float ps = PS;

            foreach (var c in ds.Contacts(Team.Player))
            {
                if (c.ship == null) continue;

                if (c.state == ContactState.Unknown)
                {
                    // sonar contact: we know something is there but not what
                    float pulse = 0.55f + Mathf.Sin(Time.time * 3.4f) * 0.25f;
                    LineDrawer.DashedCircle(c.lastKnownPosition, 18f * ps + 8f, 1.4f * ps, new Color(0.4f, 1f, 0.85f, pulse), 24);
                    LineDrawer.Cross(c.lastKnownPosition, 7f * ps, 1.3f * ps, new Color(0.4f, 1f, 0.85f, pulse));
                }
                else if (c.state == ContactState.LastKnown)
                {
                    float age = Mathf.Clamp01(c.Age / DetectionSystem.MemoryDuration);
                    float a = (1f - age) * 0.65f;
                    if (a <= 0.03f) continue;
                    Color col = new Color(1f, 0.55f, 0.5f, a);
                    Vector2 p = c.lastKnownPosition;
                    float r = 11f * ps + 5f;
                    // ghost diamond marks the last known position
                    LineDrawer.Line(p + new Vector2(0, r), p + new Vector2(r, 0), 1.2f * ps, col);
                    LineDrawer.Line(p + new Vector2(r, 0), p + new Vector2(0, -r), 1.2f * ps, col);
                    LineDrawer.Line(p + new Vector2(0, -r), p + new Vector2(-r, 0), 1.2f * ps, col);
                    LineDrawer.Line(p + new Vector2(-r, 0), p + new Vector2(0, r), 1.2f * ps, col);
                    // last known course
                    Vector2 dir = NavalMath.HeadingToVector(c.lastKnownHeading);
                    LineDrawer.Dashed(p, p + dir * 34f, 1f * ps, col, 6f, 5f);
                }
            }
        }

        // ------------------------------------------------------------------ orders

        void DrawOrders()
        {
            var sel = SelectionManager.I;
            if (sel == null) return;
            float ps = PS;
            Color routeCol = new Color(0.45f, 1f, 0.7f, 0.55f);

            for (int i = 0; i < sel.Selected.Count; i++)
            {
                var s = sel.Selected[i];
                if (s == null || s.IsDead) continue;
                var nav = s.Navigation;

                Vector2 from = s.Position;

                if (nav.Path != null && nav.Path.Count > 0)
                {
                    for (int p = 0; p < nav.Path.Count; p++)
                    {
                        LineDrawer.Dashed(from, nav.Path[p], 1.2f * ps, routeCol, 9f * ps, 7f * ps, Time.time * 18f * ps);
                        from = nav.Path[p];
                    }
                }

                for (int w = 0; w < nav.Waypoints.Count; w++)
                {
                    LineDrawer.Dashed(from, nav.Waypoints[w], 1.2f * ps, routeCol, 9f * ps, 7f * ps, Time.time * 18f * ps);
                    LineDrawer.Circle(nav.Waypoints[w], 5f * ps, 1.2f * ps, routeCol, 14);
                    from = nav.Waypoints[w];
                }

                if (nav.Order == OrderType.Patrol && nav.PatrolPoints.Count > 1)
                {
                    for (int p = 0; p < nav.PatrolPoints.Count; p++)
                    {
                        Vector2 a = nav.PatrolPoints[p];
                        Vector2 b = nav.PatrolPoints[(p + 1) % nav.PatrolPoints.Count];
                        LineDrawer.Dashed(a, b, 1.1f * ps, new Color(0.4f, 0.75f, 1f, 0.45f), 10f * ps, 8f * ps);
                        LineDrawer.Circle(a, 5f * ps, 1.1f * ps, new Color(0.4f, 0.75f, 1f, 0.7f), 12);
                    }
                }

                if (nav.FormationLeader != null && !nav.FormationLeader.IsDead)
                    LineDrawer.Dashed(s.Position, nav.FormationLeader.Position, 0.9f * ps,
                        new Color(0.7f, 0.8f, 1f, 0.3f), 7f * ps, 9f * ps);
            }
        }

        void DrawMarkers()
        {
            float ps = PS;

            for (int i = 0; i < OrderMarkers.All.Count; i++)
            {
                var m = OrderMarkers.All[i];
                float t = Mathf.Clamp01((Time.unscaledTime - m.time) / 1.2f);
                float r = Mathf.Lerp(4f, 20f, t) * ps + 3f;
                Color c = m.color;
                c.a = 1f - t;
                LineDrawer.Circle(m.pos, r, 1.6f * ps, c, 24);
            }

            for (int i = 0; i < HitMarkers.All.Count; i++)
            {
                var m = HitMarkers.All[i];
                float t = Mathf.Clamp01((Time.time - m.time) / 1.6f);
                Color c;
                switch (m.result)
                {
                    case HitResult.Citadel: c = new Color(1f, 0.35f, 0.9f); break;
                    case HitResult.Penetration: c = new Color(1f, 0.75f, 0.3f); break;
                    case HitResult.Overpenetration: c = new Color(0.8f, 0.85f, 0.9f); break;
                    case HitResult.Ricochet: c = new Color(0.6f, 0.75f, 1f); break;
                    default: c = new Color(0.7f, 0.7f, 0.7f); break;
                }
                c.a = 1f - t;
                float size = (m.result == HitResult.Citadel ? 12f : 7f) * ps;
                Vector2 p = m.pos + Vector2.up * t * 22f * ps;
                LineDrawer.Cross(p, size, 1.6f * ps, c);
            }
        }

        void DrawSelectionBox()
        {
            var sel = SelectionManager.I;
            if (sel == null || !sel.IsBoxing || RTSCamera.I == null) return;
            Rect r = sel.BoxScreenRect;
            Vector2 a = RTSCamera.I.ScreenToWorld(new Vector2(r.xMin, r.yMin));
            Vector2 b = RTSCamera.I.ScreenToWorld(new Vector2(r.xMax, r.yMax));
            LineDrawer.FilledRect(Vector2.Min(a, b), Vector2.Max(a, b), new Color(0.4f, 1f, 0.65f, 0.08f));
            LineDrawer.Rect(Vector2.Min(a, b), Vector2.Max(a, b), 1.4f * PS, new Color(0.45f, 1f, 0.7f, 0.85f));
        }

        // ------------------------------------------------------------------ world features

        void DrawZones()
        {
            var map = WorldMap.I;
            if (map == null) return;
            float ps = PS;

            for (int i = 0; i < map.Zones.Count; i++)
            {
                var z = map.Zones[i];
                if (z == null) continue;
                Color c = z.DisplayColor;

                LineDrawer.Circle(z.Position, z.radius, 2f * ps, new Color(c.r, c.g, c.b, 0.55f), 64);
                LineDrawer.DashedCircle(z.Position, z.radius * 0.94f, 1.2f * ps, new Color(c.r, c.g, c.b, 0.3f), 48);

                // capture progress as an arc from the top
                float p = Mathf.Abs(z.Progress);
                if (p > 0.01f)
                {
                    int segs = Mathf.Max(2, Mathf.RoundToInt(64 * p));
                    Color pc = z.Progress > 0f ? Teams.Color(Team.Player) : Teams.Color(Team.Enemy);
                    Vector2 prev = z.Position + new Vector2(0f, z.radius + 6f * ps);
                    for (int k = 1; k <= segs; k++)
                    {
                        float a = k / 64f * Mathf.PI * 2f;
                        Vector2 pt = z.Position + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (z.radius + 6f * ps);
                        LineDrawer.Line(prev, pt, 3f * ps, pc);
                        prev = pt;
                    }
                }

                if (z.Contested)
                {
                    float pulse = 0.4f + Mathf.Sin(Time.time * 5f) * 0.25f;
                    LineDrawer.Circle(z.Position, z.radius * (0.55f + Mathf.Sin(Time.time * 2f) * 0.05f), 1.6f * ps,
                        new Color(1f, 0.85f, 0.3f, pulse), 40);
                }
            }
        }

        void DrawPorts()
        {
            var map = WorldMap.I;
            if (map == null) return;
            float ps = PS;

            for (int i = 0; i < map.Ports.Count; i++)
            {
                var p = map.Ports[i];
                if (p == null) continue;
                Color c = Teams.Color(p.team);
                float a = p.IsDestroyed ? 0.2f : 0.5f;
                LineDrawer.DashedCircle(p.Position, p.serviceRadius, 1.4f * ps, new Color(c.r, c.g, c.b, a), 48);

                if (!p.IsDestroyed && p.health < p.maxHealth)
                {
                    float w = 60f * ps;
                    Vector2 min = new Vector2(p.Position.x - w * 0.5f, p.Position.y + 40f * ps);
                    Vector2 max = new Vector2(p.Position.x + w * 0.5f, p.Position.y + 45f * ps);
                    LineDrawer.FilledRect(min, max, new Color(0f, 0f, 0f, 0.5f));
                    LineDrawer.FilledRect(min, new Vector2(Mathf.Lerp(min.x, max.x, p.health / p.maxHealth), max.y), c);
                }
            }
        }
    }
}
