using UnityEngine;
using UnityEngine.InputSystem;

namespace Naval
{
    /// <summary>
    /// Developer view for watching a battle: hulls stay legible at strategic zoom, every turret shows
    /// the arc it can actually train through, and the selected ship's sensor and weapon envelopes are
    /// drawn as rings. Toggled with F6.
    ///
    /// This is the visual proof of the firing-arc model: turn the bow toward a target and the after
    /// turret's wedge goes red as it runs out of arc.
    /// </summary>
    public class DevOverlay : MonoBehaviour
    {
        public static DevOverlay I { get; private set; }
        public static bool Enabled { get; private set; }

        /// <summary>
        /// Ceiling on the zoom-compensated hull scale. Normal play keeps ships close to true size;
        /// dev mode lets them grow until they are readable from the strategic view.
        /// </summary>
        public static float HullScaleCap => Enabled ? 9f : 3.2f;

        public static DevOverlay Create(Transform parent)
        {
            var go = new GameObject("DevOverlay");
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<DevOverlay>();
            I = d;
            return d;
        }

        float PS => RTSCamera.I != null ? RTSCamera.I.PixelScale : 0.15f;

        static readonly Color ArcOpen    = new Color(0.45f, 1f, 0.65f, 0.55f);
        static readonly Color ArcBlocked = new Color(1f, 0.35f, 0.30f, 0.45f);
        static readonly Color ArcBlind   = new Color(1f, 0.35f, 0.30f, 0.14f);

        void Update()
        {
            if (InputHub.KeyDown(Key.F6))
            {
                Enabled = !Enabled;
                // the AI/objective layer is part of the same view, so it comes along
                DebugOverlay.SetEnabled(Enabled);
                GameEvents.RaiseMessage("Dev view " + (Enabled ? "ON" : "OFF") +
                    (Enabled ? " - turret arcs, sensor rings, enlarged hulls" : ""), Team.Neutral);
            }
            if (!Enabled) return;

            var all = ShipRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s == null || s.IsDead) continue;
                if (!IsVisibleToPlayer(s)) continue;
                DrawTurretArcs(s);
            }

            DrawEnvelopes();
        }

        /// <summary>Dev view still respects fog of war unless the map has been revealed with F2.</summary>
        static bool IsVisibleToPlayer(Ship s)
        {
            if (s.team == Team.Player || DebugOverlay.ShowAll) return true;
            return DetectionSystem.I != null && DetectionSystem.I.IsVisible(s, Team.Player);
        }

        // ------------------------------------------------------------------ turret arcs

        void DrawTurretArcs(Ship s)
        {
            var mb = s.Stats.mainBattery;
            var w = s.Weapons;
            if (mb == null || w == null || w.TurretAngles == null) return;

            float ps = PS;
            // long enough to read at any zoom, but tied to the hull so it still looks like the ship's
            float reach = Mathf.Max(s.Stats.length * 1.6f, ps * 46f);
            float bearingToTarget = s.CurrentTarget != null
                ? NavalMath.VectorToHeading(s.CurrentTarget.Position - s.Position)
                : NavalMath.VectorToHeading(w.AimPoint - s.Position);

            for (int t = 0; t < w.TurretAngles.Length; t++)
            {
                var m = ShipWeapons.MountFor(mb, t, w.TurretAngles.Length);
                Vector2 mount = s.Position + NavalMath.HeadingToVector(s.Heading) * (m.position * s.Stats.length);

                bool bears = w.TurretCanBear(t, bearingToTarget);
                Color edge = bears ? ArcOpen : ArcBlocked;

                // the two arc limits, in world headings
                float lo = s.Heading + m.restHeading - m.arcHalfWidth;
                float hi = s.Heading + m.restHeading + m.arcHalfWidth;

                LineDrawer.Line(mount, mount + NavalMath.HeadingToVector(lo) * reach, 0.9f * ps, edge);
                LineDrawer.Line(mount, mount + NavalMath.HeadingToVector(hi) * reach, 0.9f * ps, edge);
                ArcPolyline(mount, reach, lo, hi, 0.8f * ps, edge);

                // the blind sector behind the mount, drawn faintly so the dead zone is obvious
                ArcPolyline(mount, reach * 0.55f, hi, lo + 360f, 0.7f * ps, ArcBlind);

                // where this turret is actually pointing right now
                LineDrawer.Line(mount, mount + NavalMath.HeadingToVector(w.TurretAngles[t]) * reach * 1.05f,
                                1.5f * ps, bears ? Teams.Color(s.team) : ArcBlocked);
                LineDrawer.Circle(mount, ps * 2.4f, 0.8f * ps, edge, 10);
            }
        }

        /// <summary>Draws the arc from one heading to another the short way round the circle.</summary>
        static void ArcPolyline(Vector2 centre, float radius, float fromHeading, float toHeading,
                                float width, Color c)
        {
            float sweep = toHeading - fromHeading;
            int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(sweep) / 8f), 2, 64);
            Vector2 prev = centre + NavalMath.HeadingToVector(fromHeading) * radius;
            for (int i = 1; i <= steps; i++)
            {
                float h = fromHeading + sweep * (i / (float)steps);
                Vector2 p = centre + NavalMath.HeadingToVector(h) * radius;
                LineDrawer.Line(prev, p, width, c);
                prev = p;
            }
        }

        // ------------------------------------------------------------------ sensor and weapon rings

        void DrawEnvelopes()
        {
            var ship = SelectedShip();
            if (ship == null) return;

            float ps = PS;
            Vector2 p = ship.Position;

            // how far this ship can be seen from, and how far it can see
            LineDrawer.DashedCircle(p, ship.Detectability, 1.1f * ps, new Color(1f, 0.55f, 0.25f, 0.5f), 96);
            LineDrawer.DashedCircle(p, ship.Detection.EffectiveSpotRange, 1f * ps, new Color(0.4f, 0.85f, 1f, 0.35f), 96);

            if (ship.Stats.mainBattery != null)
                LineDrawer.DashedCircle(p, ship.Weapons.MainRange, 1.1f * ps, new Color(1f, 0.8f, 0.35f, 0.4f), 96);
            if (ship.Stats.torpedoes != null)
                LineDrawer.DashedCircle(p, ship.Stats.torpedoes.range, 1f * ps, new Color(0.6f, 1f, 0.7f, 0.35f), 96);

            var abil = ship.Abilities;
            if (abil != null && abil.AssuredDetectionRange > 0f)
                LineDrawer.Circle(p, abil.AssuredDetectionRange, 1.3f * ps, new Color(1f, 0.3f, 0.9f, 0.55f), 96);

            // who currently holds this ship on their plot
            var hostiles = ShipRegistry.OfTeam(Teams.Opponent(ship.team));
            for (int i = 0; i < hostiles.Count; i++)
            {
                var e = hostiles[i];
                if (e == null || e.IsDead) continue;
                if (DetectionSystem.I == null || !DetectionSystem.I.IsVisible(ship, e.team)) continue;
                LineDrawer.Dashed(e.Position, p, 0.8f * ps, new Color(1f, 0.35f, 0.3f, 0.4f), 14f, 12f);
            }
        }

        static Ship SelectedShip()
        {
            if (ControlModeManager.I != null && ControlModeManager.I.Controlled != null)
                return ControlModeManager.I.Controlled;
            return SelectionManager.I != null ? SelectionManager.I.Primary : null;
        }
    }
}
