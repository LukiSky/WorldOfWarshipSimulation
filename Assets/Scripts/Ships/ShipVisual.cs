using UnityEngine;

namespace Naval
{
    /// <summary>
    /// Builds and animates a ship's sprites: hull, traversing turrets, wake, foam, battle damage
    /// effects, dive state and the sinking sequence. Also enforces visual fog of war.
    /// </summary>
    public class ShipVisual
    {
        readonly Ship _s;

        GameObject _root;
        SpriteRenderer _hull;
        SpriteRenderer _glow;
        Transform _glowT;
        Transform _hullT;
        SpriteRenderer[] _turrets;
        Transform[] _turretPivots;
        float[] _turretY;               // fraction of hull length, so turrets follow the zoom scale

        float _wakeTimer;
        float _fireTimer;
        bool _visible = true;

        public bool VisibleToPlayer => _visible;

        public ShipVisual(Ship s)
        {
            _s = s;
            Build();
        }

        void Build()
        {
            var st = _s.Stats;

            _root = new GameObject("Visual");
            _root.transform.SetParent(_s.transform, false);

            // team glow behind the hull so ships read at strategic zoom
            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(_root.transform, false);
            _glow = glowGo.AddComponent<SpriteRenderer>();
            _glow.sprite = SpriteFactory.SoftCircle(0.05f);
            _glow.color = new Color(Teams.Color(_s.team).r, Teams.Color(_s.team).g, Teams.Color(_s.team).b, 0.30f);
            _glow.sortingOrder = -2;
            _glowT = glowGo.transform;
            _glowT.localScale = Vector3.one * st.length * 1.5f;

            var hullGo = new GameObject("Hull");
            hullGo.transform.SetParent(_root.transform, false);
            _hull = hullGo.AddComponent<SpriteRenderer>();
            _hull.sprite = SpriteFactory.Ship(st.classType, st);
            _hull.sortingOrder = 4;
            _hullT = hullGo.transform;

            // turrets
            var mb = st.mainBattery;
            int n = mb != null ? Mathf.Max(0, mb.turrets) : 0;
            if (st.classType == ShipClassType.Submarine) n = Mathf.Min(n, 1);
            _turrets = new SpriteRenderer[n];
            _turretPivots = new Transform[n];
            _turretY = new float[n];
            bool big = st.classType == ShipClassType.Battleship || st.classType == ShipClassType.Cruiser;
            int fore = Mathf.CeilToInt(n / 2f);

            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Turret" + i);
                go.transform.SetParent(_root.transform, false);
                float y;
                if (i < fore)
                    y = fore == 1 ? 0.24f : Mathf.Lerp(0.34f, 0.15f, i / Mathf.Max(1f, fore - 1f));
                else
                {
                    int k = i - fore, aft = n - fore;
                    y = aft == 1 ? -0.28f : Mathf.Lerp(-0.16f, -0.34f, k / Mathf.Max(1f, aft - 1f));
                }
                if (st.classType == ShipClassType.Submarine) y = 0.08f;
                _turretY[i] = y;
                go.transform.localPosition = new Vector3(0f, y * st.length, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Turret(big);
                sr.color = Color.Lerp(st.hullColor, Color.white, 0.15f);
                sr.sortingOrder = 6;
                float scale = st.length / (big ? 26f : 16f);
                go.transform.localScale = Vector3.one * Mathf.Clamp(scale, 0.45f, 1.5f);
                _turrets[i] = sr;
                _turretPivots[i] = go.transform;
            }
        }

        // ------------------------------------------------------------------ tick

        public void Tick(float dt)
        {
            UpdateVisibility();
            if (!_visible) return;

            UpdateZoomScale();
            UpdateTurrets();
            UpdateWake(dt);
            UpdateDamageFx(dt);
            UpdateDepthAndSinking();
        }

        /// <summary>
        /// Keeps ships legible at strategic zoom: the hull is gently enlarged and the team glow is
        /// held at a minimum on-screen size, the way a tactical plot shows contact markers.
        /// </summary>
        void UpdateZoomScale()
        {
            if (RTSCamera.I == null) return;
            float ps = RTSCamera.I.PixelScale;                 // world units per screen pixel
            float st = _s.Stats.length;

            float hullScale = Mathf.Clamp(ps * 15f / Mathf.Max(1f, st), 1f, 3.2f);
            if (_hullT != null && !_s.Damage.IsSinking)
                _hullT.localScale = new Vector3(hullScale, hullScale, 1f);

            if (_glowT != null)
            {
                float glow = Mathf.Max(st * 1.5f * hullScale, ps * 26f);
                _glowT.localScale = new Vector3(glow, glow, 1f);
            }

            if (_turretPivots != null)
            {
                float turretScale = Mathf.Clamp(st / (_s.Stats.classType == ShipClassType.Battleship ||
                                                      _s.Stats.classType == ShipClassType.Cruiser ? 26f : 16f), 0.45f, 1.5f) * hullScale;
                for (int i = 0; i < _turretPivots.Length; i++)
                {
                    var t = _turretPivots[i];
                    t.localScale = Vector3.one * turretScale;
                    t.localPosition = new Vector3(t.localPosition.x, _turretY[i] * st * hullScale, 0f);
                }
            }
        }

        void UpdateVisibility()
        {
            bool vis = _s.team == Team.Player;
            if (!vis)
            {
                if (DebugOverlay.ShowAll) vis = true;
                else if (DetectionSystem.I != null)
                {
                    var c = DetectionSystem.I.GetContact(_s, Team.Player);
                    vis = c != null && c.state == ContactState.Confirmed;
                }
            }

            if (vis != _visible)
            {
                _visible = vis;
                if (_root != null) _root.SetActive(vis);
            }
        }

        void UpdateTurrets()
        {
            if (_turretPivots == null || _s.Weapons == null || _s.Weapons.TurretAngles == null) return;
            int n = Mathf.Min(_turretPivots.Length, _s.Weapons.TurretAngles.Length);
            for (int i = 0; i < n; i++)
            {
                // turret angles are world headings; the parent already carries the hull rotation
                float local = Mathf.DeltaAngle(_s.Heading, _s.Weapons.TurretAngles[i]);
                _turretPivots[i].localRotation = Quaternion.Euler(0f, 0f, -local);
            }
        }

        void UpdateWake(float dt)
        {
            float speed = Mathf.Abs(_s.Speed);
            if (speed < 0.25f) return;
            if (_s.Submarine != null && _s.Submarine.Depth != DepthState.Surface && _s.Submarine.Depth != DepthState.Periscope) return;

            // With 36 hulls under way the wake is the heaviest particle source, so only ships that
            // are actually on screen make foam, and it thins out at strategic zoom.
            float zoomThin = 1f;
            if (RTSCamera.I != null)
            {
                Rect view = RTSCamera.I.WorldViewRect();
                view.xMin -= 60f; view.xMax += 60f; view.yMin -= 60f; view.yMax += 60f;
                if (!view.Contains(_s.Position)) return;
                if (RTSCamera.I.Zoom > 600f) zoomThin = 2.5f;
                else if (RTSCamera.I.Zoom > 350f) zoomThin = 1.6f;
            }

            _wakeTimer -= dt;
            float interval = Mathf.Lerp(0.14f, 0.05f, Mathf.Clamp01(speed / Mathf.Max(0.1f, _s.Stats.maxSpeed))) * zoomThin;
            if (_wakeTimer > 0f) return;
            _wakeTimer = interval;

            var st = _s.Stats;
            Vector2 aft = _s.Position - _s.Forward * st.length * 0.45f;
            Vector2 side = new Vector2(_s.Forward.y, -_s.Forward.x);
            float strength = Mathf.Clamp01(speed / Mathf.Max(0.1f, st.maxSpeed));

            // stern wash
            ParticleFX.Wake(aft, _s.Velocity, st.beam * 1.1f, strength);
            // bow foam spreading to both quarters
            Vector2 bow = _s.Position + _s.Forward * st.length * 0.42f;
            ParticleFX.Wake(bow + side * st.beam * 0.4f, _s.Velocity * 0.3f + side * 2f, st.beam * 0.8f, strength * 0.8f);
            ParticleFX.Wake(bow - side * st.beam * 0.4f, _s.Velocity * 0.3f - side * 2f, st.beam * 0.8f, strength * 0.8f);
        }

        void UpdateDamageFx(float dt)
        {
            var dmg = _s.Damage;
            if (dmg == null) return;

            if (dmg.FireStacks > 0)
            {
                _fireTimer -= dt;
                if (_fireTimer <= 0f)
                {
                    _fireTimer = 0.12f / dmg.FireStacks;
                    Vector2 p = _s.Position + Random.insideUnitCircle * _s.Stats.length * 0.35f;
                    ParticleFX.Fire(p, _s.Stats.length * 0.18f);
                }
            }

            if (dmg.FloodingStacks > 0 && Random.value < dt * 3f)
            {
                Vector2 p = _s.Position + Random.insideUnitCircle * _s.Stats.length * 0.4f;
                ParticleFX.Splash(p, 1.1f);
            }

            // battered hulls darken and trail smoke
            if (_hull != null)
            {
                float h = dmg.HealthFraction;
                _hull.color = Color.Lerp(new Color(0.55f, 0.5f, 0.48f), Color.white, Mathf.Clamp01(h * 1.4f));
            }
        }

        void UpdateDepthAndSinking()
        {
            if (_hull == null) return;
            float alpha = 1f;
            Color tint = Color.white;

            if (_s.Submarine != null)
            {
                switch (_s.Submarine.Depth)
                {
                    case DepthState.Periscope: alpha = 0.62f; tint = new Color(0.7f, 0.85f, 1f); break;
                    case DepthState.Submerged: alpha = 0.34f; tint = new Color(0.55f, 0.75f, 1f); break;
                    case DepthState.Deep: alpha = 0.18f; tint = new Color(0.4f, 0.6f, 0.95f); break;
                }
            }

            if (_s.Damage != null && _s.Damage.IsSinking)
            {
                float p = Mathf.Clamp01(_s.Movement.SinkProgress);
                alpha *= 1f - p * 0.85f;
                float shrink = 1f - p * 0.35f;
                _root.transform.localScale = new Vector3(shrink, shrink, 1f);
                _root.transform.localRotation = Quaternion.Euler(0f, 0f, _s.Movement.ListAngle * 0.35f);
            }

            var c = _hull.color;
            _hull.color = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, alpha);
            if (_glow != null)
            {
                var tc = Teams.Color(_s.team);
                _glow.color = new Color(tc.r, tc.g, tc.b, 0.3f * alpha);
            }
            if (_turrets != null)
                for (int i = 0; i < _turrets.Length; i++)
                    if (_turrets[i] != null)
                    {
                        var tc = _turrets[i].color;
                        _turrets[i].color = new Color(tc.r, tc.g, tc.b, alpha);
                    }
        }

        public void OnDestroyed()
        {
            if (_root != null) _root.SetActive(false);
        }
    }
}
