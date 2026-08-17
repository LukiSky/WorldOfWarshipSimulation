using System.Collections.Generic;
using UnityEngine;

namespace Naval
{
    /// <summary>
    /// Every visual in the game is generated at runtime - the project ships with no art assets.
    /// Textures are rasterised with 3x3 supersampling so silhouettes stay clean when zoomed in.
    /// </summary>
    public static class SpriteFactory
    {
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public static void Clear()
        {
            _cache.Clear();
        }

        // ------------------------------------------------------------------ public API

        public static Sprite Ship(ShipClassType cls, ShipStats stats)
        {
            string key = "ship_" + cls;
            if (_cache.TryGetValue(key, out var s)) return s;

            int h = 256;
            float ratio = stats.beam / stats.length;
            int w = Mathf.Max(24, Mathf.RoundToInt(h * ratio * 1.6f));   // slight widening so detail is visible
            var tex = NewTex(w, h, key);
            DrawHull(tex, cls, stats);
            Commit(tex);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), h / stats.length, 0,
                SpriteMeshType.FullRect);
            _cache[key] = s;
            return s;
        }

        public static Sprite Turret(bool big)
        {
            string key = big ? "turret_big" : "turret_small";
            if (_cache.TryGetValue(key, out var s)) return s;
            int w = 32, h = 48;
            var tex = NewTex(w, h, key);
            Clear(tex);
            // barbette
            FillEllipse(tex, new Vector2(0.5f, 0.34f), 0.30f, 0.26f, new Color(0.30f, 0.33f, 0.38f));
            // gun house
            FillPolygon(tex, new[]{
                new Vector2(0.26f,0.14f), new Vector2(0.74f,0.14f),
                new Vector2(0.74f,0.52f), new Vector2(0.62f,0.60f),
                new Vector2(0.38f,0.60f), new Vector2(0.26f,0.52f)
            }, new Color(0.42f, 0.45f, 0.50f));
            // barrels
            int barrels = big ? 3 : 2;
            for (int i = 0; i < barrels; i++)
            {
                float x = barrels == 1 ? 0.5f : Mathf.Lerp(0.34f, 0.66f, i / (float)(barrels - 1));
                FillRect(tex, x - 0.045f, 0.55f, x + 0.045f, 0.97f, new Color(0.20f, 0.22f, 0.26f));
            }
            Commit(tex);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.34f), h / (big ? 3.2f : 2.0f), 0, SpriteMeshType.FullRect);
            _cache[key] = s;
            return s;
        }

        /// <summary>Soft radial blob - smoke, explosions, foam, glow.</summary>
        public static Sprite SoftCircle(float hardness = 0.35f)
        {
            string key = "soft_" + hardness.ToString("F2");
            if (_cache.TryGetValue(key, out var s)) return s;
            int n = 128;
            var tex = NewTex(n, n, key);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float a = Mathf.Clamp01(1f - Mathf.InverseLerp(hardness, 1f, d));
                    a = a * a * (3f - 2f * a);
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply(true);
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
            _cache[key] = s;
            return s;
        }

        public static Sprite Ring(float thickness = 0.10f)
        {
            string key = "ring_" + thickness.ToString("F2");
            if (_cache.TryGetValue(key, out var s)) return s;
            int n = 256;
            var tex = NewTex(n, n, key);
            var px = new Color[n * n];
            float outer = 0.5f, inner = 0.5f - thickness;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = 1.2f / n;
                    float a = Mathf.Clamp01((outer - d) / edge) * Mathf.Clamp01((d - inner) / edge);
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply(true);
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
            _cache[key] = s;
            return s;
        }

        public static Sprite Square()
        {
            const string key = "square";
            if (_cache.TryGetValue(key, out var s)) return s;
            int n = 8;
            var tex = NewTex(n, n, key);
            var px = new Color[n * n];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px); tex.Apply(false);
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
            _cache[key] = s;
            return s;
        }

        /// <summary>Chevron used for team markers and course arrows.</summary>
        public static Sprite Arrow()
        {
            const string key = "arrow";
            if (_cache.TryGetValue(key, out var s)) return s;
            int n = 64;
            var tex = NewTex(n, n, key);
            Clear(tex);
            FillPolygon(tex, new[]{
                new Vector2(0.5f,0.96f), new Vector2(0.94f,0.06f),
                new Vector2(0.5f,0.34f), new Vector2(0.06f,0.06f)
            }, Color.white);
            Commit(tex);
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
            _cache[key] = s;
            return s;
        }

        public static Sprite Torpedo()
        {
            const string key = "torp";
            if (_cache.TryGetValue(key, out var s)) return s;
            int w = 16, h = 48;
            var tex = NewTex(w, h, key);
            Clear(tex);
            FillPolygon(tex, new[]{
                new Vector2(0.5f,1.0f), new Vector2(0.78f,0.72f), new Vector2(0.78f,0.12f),
                new Vector2(0.62f,0.0f), new Vector2(0.38f,0.0f), new Vector2(0.22f,0.12f),
                new Vector2(0.22f,0.72f)
            }, new Color(0.75f, 0.78f, 0.82f));
            Commit(tex);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), h / 1.6f, 0, SpriteMeshType.FullRect);
            _cache[key] = s;
            return s;
        }

        /// <summary>Class icon used by the fleet panel and minimap (drawn in a square, pointing up).</summary>
        public static Sprite ClassIcon(ShipClassType cls)
        {
            string key = "icon_" + cls;
            if (_cache.TryGetValue(key, out var s)) return s;
            int n = 64;
            var tex = NewTex(n, n, key);
            Clear(tex);
            switch (cls)
            {
                case ShipClassType.Destroyer:
                    FillPolygon(tex, new[]{ new Vector2(0.5f,0.95f), new Vector2(0.70f,0.10f), new Vector2(0.30f,0.10f) }, Color.white);
                    break;
                case ShipClassType.Cruiser:
                    FillPolygon(tex, new[]{ new Vector2(0.5f,0.95f), new Vector2(0.82f,0.30f), new Vector2(0.5f,0.06f), new Vector2(0.18f,0.30f) }, Color.white);
                    break;
                case ShipClassType.Battleship:
                    FillPolygon(tex, new[]{
                        new Vector2(0.5f,0.96f), new Vector2(0.86f,0.55f), new Vector2(0.78f,0.08f),
                        new Vector2(0.22f,0.08f), new Vector2(0.14f,0.55f) }, Color.white);
                    break;
                case ShipClassType.Submarine:
                    FillEllipse(tex, new Vector2(0.5f, 0.5f), 0.22f, 0.44f, Color.white);
                    FillRect(tex, 0.36f, 0.46f, 0.64f, 0.66f, Color.white);
                    break;
                default:
                    FillRect(tex, 0.24f, 0.12f, 0.76f, 0.88f, Color.white);
                    break;
            }
            Commit(tex);
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
            _cache[key] = s;
            return s;
        }

        // ------------------------------------------------------------------ hull drawing

        static void DrawHull(Texture2D tex, ShipClassType cls, ShipStats st)
        {
            Clear(tex);
            Color hull = st.hullColor;
            Color deck = st.deckColor;
            Color super = Color.Lerp(hull, Color.white, 0.22f);
            Color dark = Color.Lerp(hull, Color.black, 0.45f);

            Vector2[] outline;
            switch (cls)
            {
                case ShipClassType.Destroyer:
                    outline = new[]{
                        new Vector2(0.50f,0.99f), new Vector2(0.66f,0.84f), new Vector2(0.74f,0.60f),
                        new Vector2(0.76f,0.28f), new Vector2(0.72f,0.06f), new Vector2(0.60f,0.01f),
                        new Vector2(0.40f,0.01f), new Vector2(0.28f,0.06f), new Vector2(0.24f,0.28f),
                        new Vector2(0.26f,0.60f), new Vector2(0.34f,0.84f) };
                    break;
                case ShipClassType.Cruiser:
                    outline = new[]{
                        new Vector2(0.50f,0.99f), new Vector2(0.68f,0.86f), new Vector2(0.80f,0.62f),
                        new Vector2(0.82f,0.26f), new Vector2(0.74f,0.04f), new Vector2(0.58f,0.01f),
                        new Vector2(0.42f,0.01f), new Vector2(0.26f,0.04f), new Vector2(0.18f,0.26f),
                        new Vector2(0.20f,0.62f), new Vector2(0.32f,0.86f) };
                    break;
                case ShipClassType.Battleship:
                    outline = new[]{
                        new Vector2(0.50f,0.99f), new Vector2(0.70f,0.88f), new Vector2(0.84f,0.66f),
                        new Vector2(0.86f,0.24f), new Vector2(0.76f,0.03f), new Vector2(0.56f,0.00f),
                        new Vector2(0.44f,0.00f), new Vector2(0.24f,0.03f), new Vector2(0.14f,0.24f),
                        new Vector2(0.16f,0.66f), new Vector2(0.30f,0.88f) };
                    break;
                case ShipClassType.Submarine:
                    outline = new[]{
                        new Vector2(0.50f,0.99f), new Vector2(0.66f,0.86f), new Vector2(0.72f,0.55f),
                        new Vector2(0.72f,0.20f), new Vector2(0.62f,0.02f), new Vector2(0.38f,0.02f),
                        new Vector2(0.28f,0.20f), new Vector2(0.28f,0.55f), new Vector2(0.34f,0.86f) };
                    break;
                default:
                    outline = new[]{
                        new Vector2(0.50f,0.99f), new Vector2(0.72f,0.84f), new Vector2(0.80f,0.60f),
                        new Vector2(0.80f,0.14f), new Vector2(0.70f,0.02f), new Vector2(0.30f,0.02f),
                        new Vector2(0.20f,0.14f), new Vector2(0.20f,0.60f), new Vector2(0.28f,0.84f) };
                    break;
            }

            FillPolygon(tex, outline, hull);
            // deck inset
            var inset = Shrink(outline, 0.86f);
            FillPolygon(tex, inset, deck);

            switch (cls)
            {
                case ShipClassType.Destroyer:
                    FillRect(tex, 0.40f, 0.44f, 0.60f, 0.62f, super);           // bridge
                    FillRect(tex, 0.43f, 0.30f, 0.57f, 0.40f, dark);            // funnel
                    FillRect(tex, 0.43f, 0.16f, 0.57f, 0.24f, dark);            // funnel 2
                    FillRect(tex, 0.34f, 0.08f, 0.66f, 0.14f, super);           // torpedo mount deck
                    break;
                case ShipClassType.Cruiser:
                    FillRect(tex, 0.38f, 0.44f, 0.62f, 0.64f, super);
                    FillRect(tex, 0.41f, 0.30f, 0.59f, 0.42f, dark);
                    FillRect(tex, 0.41f, 0.17f, 0.59f, 0.27f, dark);
                    FillRect(tex, 0.30f, 0.06f, 0.70f, 0.13f, super);
                    break;
                case ShipClassType.Battleship:
                    FillRect(tex, 0.34f, 0.42f, 0.66f, 0.66f, super);           // citadel superstructure
                    FillRect(tex, 0.40f, 0.60f, 0.60f, 0.74f, super);
                    FillRect(tex, 0.39f, 0.26f, 0.61f, 0.40f, dark);            // funnel
                    FillRect(tex, 0.36f, 0.10f, 0.64f, 0.20f, super);           // aft tower
                    break;
                case ShipClassType.Submarine:
                    FillRect(tex, 0.40f, 0.44f, 0.60f, 0.66f, super);           // conning tower
                    FillRect(tex, 0.46f, 0.62f, 0.54f, 0.76f, dark);            // periscope mast
                    FillRect(tex, 0.30f, 0.30f, 0.70f, 0.34f, dark);            // dive planes
                    break;
                default:
                    FillRect(tex, 0.34f, 0.60f, 0.66f, 0.74f, super);
                    FillRect(tex, 0.28f, 0.20f, 0.72f, 0.52f, Color.Lerp(deck, Color.black, 0.2f)); // cargo hold
                    break;
            }

            // waterline shading down both sides
            OutlineDarken(tex, outline, dark);
        }

        static Vector2[] Shrink(Vector2[] poly, float f)
        {
            Vector2 c = Vector2.zero;
            for (int i = 0; i < poly.Length; i++) c += poly[i];
            c /= poly.Length;
            var r = new Vector2[poly.Length];
            for (int i = 0; i < poly.Length; i++) r[i] = c + (poly[i] - c) * f;
            return r;
        }

        // ------------------------------------------------------------------ raster helpers

        // Rasterisation happens in a managed buffer and is uploaded once - per pixel SetPixel calls
        // on a Texture2D are far too slow for the amount of drawing we do at start up.
        static Color[] _buf;
        static int _bw, _bh;

        static Texture2D NewTex(int w, int h, string name)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = "gen_" + name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            return t;
        }

        /// <summary>Starts a drawing session against a texture: allocates and clears the pixel buffer.</summary>
        static void Clear(Texture2D t)
        {
            _bw = t.width; _bh = t.height;
            if (_buf == null || _buf.Length != _bw * _bh) _buf = new Color[_bw * _bh];
            for (int i = 0; i < _buf.Length; i++) _buf[i] = Color.clear;
        }

        /// <summary>Uploads the buffer and applies the texture.</summary>
        static void Commit(Texture2D t)
        {
            t.SetPixels(_buf, 0);
            t.Apply(true);
        }

        static bool PointInPoly(Vector2[] p, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                if ((p[i].y > y) != (p[j].y > y) &&
                    x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                    inside = !inside;
            }
            return inside;
        }

        static void Blend(int x, int y, Color c, float a)
        {
            if (a <= 0f) return;
            int i = y * _bw + x;
            if (i < 0 || i >= _buf.Length) return;
            Color dst = _buf[i];
            float outA = a + dst.a * (1f - a);
            Color rgb = outA > 0f ? (c * a + dst * dst.a * (1f - a)) / outA : Color.clear;
            rgb.a = outA;
            _buf[i] = rgb;
        }

        const int SS = 3;   // supersample factor

        static void FillPolygon(Texture2D t, Vector2[] poly, Color c)
        {
            int w = _bw, h = _bh;
            float minX = 1f, maxX = 0f, minY = 1f, maxY = 0f;
            for (int i = 0; i < poly.Length; i++)
            {
                minX = Mathf.Min(minX, poly[i].x); maxX = Mathf.Max(maxX, poly[i].x);
                minY = Mathf.Min(minY, poly[i].y); maxY = Mathf.Max(maxY, poly[i].y);
            }
            int x0 = Mathf.Max(0, Mathf.FloorToInt(minX * w) - 1), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(maxX * w) + 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY * h) - 1), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(maxY * h) + 1);

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                        {
                            float u = (x + (sx + 0.5f) / SS) / w;
                            float v = (y + (sy + 0.5f) / SS) / h;
                            if (PointInPoly(poly, u, v)) hits++;
                        }
                    if (hits > 0) Blend(x, y, c, hits / (float)(SS * SS));
                }
        }

        static void FillRect(Texture2D t, float x0, float y0, float x1, float y1, Color c)
        {
            FillPolygon(t, new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) }, c);
        }

        static void FillEllipse(Texture2D t, Vector2 center, float rx, float ry, Color c)
        {
            int w = _bw, h = _bh;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((center.x - rx) * w) - 1), x1 = Mathf.Min(w - 1, Mathf.CeilToInt((center.x + rx) * w) + 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt((center.y - ry) * h) - 1), y1 = Mathf.Min(h - 1, Mathf.CeilToInt((center.y + ry) * h) + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                        {
                            float u = (x + (sx + 0.5f) / SS) / w - center.x;
                            float v = (y + (sy + 0.5f) / SS) / h - center.y;
                            if ((u * u) / (rx * rx) + (v * v) / (ry * ry) <= 1f) hits++;
                        }
                    if (hits > 0) Blend(x, y, c, hits / (float)(SS * SS));
                }
        }

        /// <summary>Darkens pixels close to the hull outline so the silhouette reads at low zoom.</summary>
        static void OutlineDarken(Texture2D t, Vector2[] poly, Color dark)
        {
            int w = _bw, h = _bh;
            float thick = 2.2f / Mathf.Min(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color dst = _buf[y * w + x];
                    if (dst.a < 0.05f) continue;
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float best = 999f;
                    for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                        best = Mathf.Min(best, NavalMath.DistanceToSegment(new Vector2(u, v), poly[j], poly[i]));
                    if (best < thick)
                        Blend(x, y, dark, 0.85f * (1f - best / thick));
                }
        }
    }
}
