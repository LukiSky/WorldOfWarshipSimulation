using System.Collections.Generic;
using UnityEngine;

namespace Naval
{
    /// <summary>
    /// Tactical fog of war. A low resolution vision mask is stamped from the player's sensors and
    /// sampled by a full-map overlay: currently observed water is clear, previously explored water is
    /// dimmed, never-visited water is dark. Weather adds its own veil on top.
    /// </summary>
    public class FogOfWarRenderer : MonoBehaviour
    {
        public static FogOfWarRenderer I { get; private set; }

        const int Res = 160;
        const float UpdateInterval = 0.15f;

        Texture2D _vision;
        Color32[] _px;
        Material _mat;
        float _timer;
        float _weatherAlpha;
        Color _weatherColor = new Color(0.55f, 0.6f, 0.66f);

        public bool Enabled { get; set; } = true;

        public static FogOfWarRenderer Create(Transform parent, WorldMap map)
        {
            var go = new GameObject("FogOfWar");
            go.transform.SetParent(parent, false);
            var f = go.AddComponent<FogOfWarRenderer>();
            f.Init(map);
            return f;
        }

        void Init(WorldMap map)
        {
            I = this;
            _vision = new Texture2D(Res, Res, TextureFormat.RGBA32, false)
            {
                name = "vision",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            _px = new Color32[Res * Res];
            for (int i = 0; i < _px.Length; i++) _px[i] = new Color32(0, 0, 0, 255);
            _vision.SetPixels32(_px);
            _vision.Apply(false);

            float half = map.Half + 400f;
            var mesh = new Mesh { name = "fog_quad" };
            mesh.vertices = new[]
            {
                new Vector3(-half, -half, 0f), new Vector3(half, -half, 0f),
                new Vector3(half, half, 0f), new Vector3(-half, half, 0f)
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            _mat = new Material(Shader.Find("Naval/FogOfWar"));
            _mat.SetTexture("_VisionTex", _vision);
            _mat.SetVector("_WorldMin", new Vector4(-map.Half, -map.Half, 0, 0));
            _mat.SetVector("_WorldSize", new Vector4(map.Size, map.Size, 0, 0));
            mr.sharedMaterial = _mat;
            mr.sortingOrder = -500;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            transform.position = new Vector3(0f, 0f, 0.5f);
        }

        public void SetWeather(float alpha, Color color)
        {
            _weatherAlpha = alpha;
            _weatherColor = color;
        }

        void LateUpdate()
        {
            if (_mat != null)
            {
                _mat.SetFloat("_WeatherAlpha", _weatherAlpha);
                _mat.SetColor("_WeatherColor", _weatherColor);
                _mat.SetFloat("_Enabled", Enabled ? 1f : 0f);
            }

            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = UpdateInterval;
            Stamp();
        }

        void Stamp()
        {
            // decay current visibility, keep the explored channel
            for (int i = 0; i < _px.Length; i++)
            {
                byte r = _px[i].r;
                _px[i].r = (byte)(r > 26 ? r - 26 : 0);
            }

            float half = GameConfig.WorldSize * 0.5f;
            float unitsPerTexel = GameConfig.WorldSize / Res;

            var ships = ShipRegistry.OfTeam(Team.Player);
            for (int s = 0; s < ships.Count; s++)
            {
                var ship = ships[s];
                if (ship == null || ship.IsDead) continue;
                float radius = ship.Detection != null ? ship.Detection.EffectiveSpotRange : ship.Stats.spotRange;
                StampCircle(ship.Position, radius, half, unitsPerTexel);
            }

            var ports = WorldMap.I != null ? WorldMap.I.Ports : null;
            if (ports != null)
                for (int i = 0; i < ports.Count; i++)
                    if (ports[i] != null && ports[i].team == Team.Player)
                        StampCircle(ports[i].Position, 320f, half, unitsPerTexel);

            _vision.SetPixels32(_px);
            _vision.Apply(false);
        }

        void StampCircle(Vector2 pos, float radius, float half, float unitsPerTexel)
        {
            int cx = Mathf.RoundToInt((pos.x + half) / unitsPerTexel);
            int cy = Mathf.RoundToInt((pos.y + half) / unitsPerTexel);
            int rad = Mathf.CeilToInt(radius / unitsPerTexel);
            int x0 = Mathf.Max(0, cx - rad), x1 = Mathf.Min(Res - 1, cx + rad);
            int y0 = Mathf.Max(0, cy - rad), y1 = Mathf.Min(Res - 1, cy + rad);
            float r2 = rad * rad;

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float d2 = dx * dx + dy * dy;
                    if (d2 > r2) continue;
                    float f = 1f - Mathf.Sqrt(d2 / Mathf.Max(1f, r2));
                    byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(f * 3f) * 255f), 0, 255);
                    int i = y * Res + x;
                    if (_px[i].r < v) _px[i].r = v;
                    if (_px[i].g < v) _px[i].g = v;
                }
        }

        public void RevealAll()
        {
            for (int i = 0; i < _px.Length; i++) _px[i] = new Color32(255, 255, 0, 255);
            _vision.SetPixels32(_px);
            _vision.Apply(false);
        }
    }
}
