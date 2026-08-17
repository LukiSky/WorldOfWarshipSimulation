using UnityEngine;
using UnityEngine.UI;

namespace Naval
{
    /// <summary>
    /// Tactical minimap. Terrain is baked once from the height field; ships, contacts, zones and the
    /// camera rectangle are stamped a few times a second. Undetected enemies are never drawn.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        public static Minimap I { get; private set; }

        const int Res = 224;

        Texture2D _tex;
        Color32[] _base;
        Color32[] _px;
        RawImage _image;
        RectTransform _rect;
        float _timer;

        public static Minimap Create(Transform parent, RawImage image, RectTransform rect)
        {
            var go = new GameObject("Minimap");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<Minimap>();
            m._image = image;
            m._rect = rect;
            m.Init();
            I = m;
            return m;
        }

        void Init()
        {
            _tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false)
            {
                name = "minimap",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            _base = new Color32[Res * Res];
            _px = new Color32[Res * Res];
            BakeTerrain();
            if (_image != null) _image.texture = _tex;
        }

        public void BakeTerrain()
        {
            var map = WorldMap.I;
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    Color c;
                    if (map == null) c = new Color(0.05f, 0.1f, 0.16f);
                    else
                    {
                        Vector2 w = TexelToWorld(x, y);
                        float h = map.SampleHeight(w);
                        if (h > 0f)
                            c = Color.Lerp(new Color(0.32f, 0.30f, 0.22f), new Color(0.22f, 0.26f, 0.19f), Mathf.Clamp01(h * 2f));
                        else
                        {
                            float d = Mathf.Clamp01(-h);
                            c = Color.Lerp(new Color(0.10f, 0.28f, 0.34f), new Color(0.03f, 0.07f, 0.14f), d);
                        }
                    }
                    _base[y * Res + x] = c;
                }
            _base.CopyTo(_px, 0);
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        Vector2 TexelToWorld(int x, int y)
        {
            float half = GameConfig.WorldSize * 0.5f;
            return new Vector2(-half + (x + 0.5f) / Res * GameConfig.WorldSize,
                               -half + (y + 0.5f) / Res * GameConfig.WorldSize);
        }

        void WorldToTexel(Vector2 w, out int x, out int y)
        {
            float half = GameConfig.WorldSize * 0.5f;
            x = Mathf.RoundToInt((w.x + half) / GameConfig.WorldSize * Res);
            y = Mathf.RoundToInt((w.y + half) / GameConfig.WorldSize * Res);
        }

        void LateUpdate()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = 0.1f;
            Redraw();
        }

        void Redraw()
        {
            _base.CopyTo(_px, 0);

            var map = WorldMap.I;

            // capture zones
            if (map != null)
            {
                for (int i = 0; i < map.Zones.Count; i++)
                {
                    var z = map.Zones[i];
                    if (z == null) continue;
                    Ring(z.Position, z.radius, z.DisplayColor, 0.55f);
                }
                for (int i = 0; i < map.Ports.Count; i++)
                {
                    var p = map.Ports[i];
                    if (p == null) continue;
                    Box(p.Position, 3, Teams.Color(p.team));
                }
            }

            // last known and sonar contacts
            var ds = DetectionSystem.I;
            if (ds != null)
                foreach (var c in ds.Contacts(Team.Player))
                {
                    if (c.state == ContactState.LastKnown)
                    {
                        float a = 1f - Mathf.Clamp01(c.Age / DetectionSystem.MemoryDuration);
                        if (a < 0.05f) continue;
                        Box(c.lastKnownPosition, 1, new Color(0.7f, 0.25f, 0.22f, a));
                    }
                    else if (c.state == ContactState.Unknown)
                    {
                        Box(c.lastKnownPosition, 2, new Color(0.35f, 0.95f, 0.8f));
                    }
                }

            // ships
            var all = ShipRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s == null || s.IsDead) continue;
                bool friendly = s.team == Team.Player;
                if (!friendly)
                {
                    if (s.Visual == null || !s.Visual.VisibleToPlayer) continue;   // fog of war applies to the minimap too
                }
                int size = s.Stats.classType == ShipClassType.Battleship ? 3
                         : s.Stats.classType == ShipClassType.Cruiser ? 2 : 1;
                Color col = Teams.Color(s.team);
                if (friendly && s.Selected) col = new Color(0.6f, 1f, 0.7f);
                Box(s.Position, size, col);
            }

            // camera viewport
            if (RTSCamera.I != null)
            {
                Rect v = RTSCamera.I.WorldViewRect();
                RectOutline(v, new Color(1f, 1f, 1f, 0.55f));
            }

            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        void Box(Vector2 world, int radius, Color color)
        {
            WorldToTexel(world, out int cx, out int cy);
            for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    if (x < 0 || y < 0 || x >= Res || y >= Res) continue;
                    Blend(y * Res + x, color);
                }
        }

        void Ring(Vector2 world, float radius, Color color, float alpha)
        {
            WorldToTexel(world, out int cx, out int cy);
            int r = Mathf.RoundToInt(radius / GameConfig.WorldSize * Res);
            int segs = Mathf.Max(12, r * 6);
            for (int i = 0; i < segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                int x = cx + Mathf.RoundToInt(Mathf.Cos(a) * r);
                int y = cy + Mathf.RoundToInt(Mathf.Sin(a) * r);
                if (x < 0 || y < 0 || x >= Res || y >= Res) continue;
                Blend(y * Res + x, new Color(color.r, color.g, color.b, alpha));
            }
        }

        void RectOutline(Rect worldRect, Color color)
        {
            WorldToTexel(new Vector2(worldRect.xMin, worldRect.yMin), out int x0, out int y0);
            WorldToTexel(new Vector2(worldRect.xMax, worldRect.yMax), out int x1, out int y1);
            x0 = Mathf.Clamp(x0, 0, Res - 1); x1 = Mathf.Clamp(x1, 0, Res - 1);
            y0 = Mathf.Clamp(y0, 0, Res - 1); y1 = Mathf.Clamp(y1, 0, Res - 1);
            for (int x = x0; x <= x1; x++) { Blend(y0 * Res + x, color); Blend(y1 * Res + x, color); }
            for (int y = y0; y <= y1; y++) { Blend(y * Res + x0, color); Blend(y * Res + x1, color); }
        }

        void Blend(int index, Color c)
        {
            if (index < 0 || index >= _px.Length) return;
            if (c.a >= 0.99f) { _px[index] = c; return; }
            Color dst = _px[index];
            _px[index] = Color.Lerp(dst, c, c.a);
        }

        // ------------------------------------------------------------------ interaction

        /// <summary>Converts a screen point over the minimap into a world position, if it is inside.</summary>
        public bool ScreenToWorld(Vector2 screen, out Vector2 world)
        {
            world = Vector2.zero;
            if (_rect == null) return false;
            if (!RectTransformUtility.RectangleContainsScreenPoint(_rect, screen, null)) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screen, null, out Vector2 local)) return false;

            Rect r = _rect.rect;
            float u = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
            float v = Mathf.InverseLerp(r.yMin, r.yMax, local.y);
            float half = GameConfig.WorldSize * 0.5f;
            world = new Vector2(-half + u * GameConfig.WorldSize, -half + v * GameConfig.WorldSize);
            return true;
        }
    }
}
