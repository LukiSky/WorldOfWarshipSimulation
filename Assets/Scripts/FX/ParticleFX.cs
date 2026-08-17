using System.Collections.Generic;
using UnityEngine;

namespace Naval
{
    /// <summary>
    /// Lightweight CPU particle system. Everything is batched into two dynamic meshes
    /// (alpha blended + additive) so thousands of splashes, smoke puffs and wake foam
    /// cost two draw calls instead of hundreds of ParticleSystems.
    /// </summary>
    public class ParticleFX : MonoBehaviour
    {
        public static ParticleFX I { get; private set; }

        public const int MaxParticles = 2600;

        struct P
        {
            public Vector2 pos, vel;
            public float life, maxLife;
            public float size0, size1;
            public float rot, rotSpd, drag;
            public Color c0, c1;
            public int tex;
            public bool additive;
        }

        P[] _parts = new P[MaxParticles];
        int _count;

        class Batch
        {
            public Mesh mesh;
            public MeshRenderer mr;
            public List<Vector3> verts = new List<Vector3>(MaxParticles * 4);
            public List<Color> cols = new List<Color>(MaxParticles * 4);
            public List<Vector2> uvs = new List<Vector2>(MaxParticles * 4);
            public List<int> tris = new List<int>(MaxParticles * 6);
        }

        Batch _alpha, _add;
        Texture2D _atlas;

        public static ParticleFX Create(Transform parent)
        {
            var go = new GameObject("ParticleFX");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<ParticleFX>();
            fx.Init();
            return fx;
        }

        void Init()
        {
            I = this;
            _atlas = BuildAtlas();
            _alpha = MakeBatch("FX_Alpha", 60, UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _add = MakeBatch("FX_Additive", 61, UnityEngine.Rendering.BlendMode.One, UnityEngine.Rendering.BlendMode.One);
        }

        Batch MakeBatch(string name, int order, UnityEngine.Rendering.BlendMode src, UnityEngine.Rendering.BlendMode dst)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var b = new Batch();
            b.mesh = new Mesh { name = name };
            b.mesh.MarkDynamic();
            b.mesh.bounds = new Bounds(Vector3.zero, new Vector3(GameConfig.WorldSize * 2f, GameConfig.WorldSize * 2f, 10f));
            go.AddComponent<MeshFilter>().sharedMesh = b.mesh;
            b.mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Naval/Particle"));
            mat.mainTexture = _atlas;
            mat.SetFloat("_SrcBlend", (float)src);
            mat.SetFloat("_DstBlend", (float)dst);
            b.mr.sharedMaterial = mat;
            b.mr.sortingOrder = order;
            b.mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b.mr.receiveShadows = false;
            return b;
        }

        // ------------------------------------------------------------------ spawning

        public static void Spawn(Vector2 pos, Vector2 vel, float life, float size0, float size1,
                                 Color c0, Color c1, int tex = 0, bool additive = false,
                                 float drag = 1.2f, float rotSpd = 0f)
        {
            if (I == null) return;
            I.SpawnInternal(pos, vel, life, size0, size1, c0, c1, tex, additive, drag, rotSpd);
        }

        void SpawnInternal(Vector2 pos, Vector2 vel, float life, float size0, float size1,
                           Color c0, Color c1, int tex, bool additive, float drag, float rotSpd)
        {
            int idx;
            if (_count < MaxParticles) idx = _count++;
            else idx = Random.Range(0, MaxParticles);   // overflow: recycle a random particle

            _parts[idx] = new P
            {
                pos = pos,
                vel = vel,
                life = life,
                maxLife = life,
                size0 = size0,
                size1 = size1,
                rot = Random.Range(0f, 360f),
                rotSpd = rotSpd,
                drag = drag,
                c0 = c0,
                c1 = c1,
                tex = tex,
                additive = additive
            };
        }

        // ------------------------------------------------------------------ presets

        public static void Explosion(Vector2 pos, float scale, bool underwater = false)
        {
            if (I == null) return;
            int n = Mathf.RoundToInt(Mathf.Lerp(8, 22, Mathf.Clamp01(scale / 3f)));
            for (int i = 0; i < n; i++)
            {
                Vector2 v = Random.insideUnitCircle.normalized * Random.Range(4f, 16f) * scale;
                Spawn(pos + Random.insideUnitCircle * scale, v, Random.Range(0.5f, 1.1f),
                    scale * Random.Range(1.4f, 2.6f), scale * Random.Range(3.5f, 6f),
                    underwater ? new Color(0.75f, 0.9f, 1f, 0.9f) : new Color(1f, 0.85f, 0.45f, 1f),
                    underwater ? new Color(0.6f, 0.8f, 0.95f, 0f) : new Color(0.9f, 0.25f, 0.06f, 0f),
                    underwater ? 0 : 1, !underwater, 1.8f, Random.Range(-90f, 90f));
            }
            for (int i = 0; i < n / 2; i++)
            {
                Spawn(pos, Random.insideUnitCircle * 5f * scale, Random.Range(1.6f, 3.4f),
                    scale * 2.4f, scale * 8f,
                    new Color(0.28f, 0.28f, 0.30f, 0.85f), new Color(0.5f, 0.5f, 0.52f, 0f),
                    1, false, 0.7f, Random.Range(-25f, 25f));
            }
        }

        public static void Splash(Vector2 pos, float scale)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(scale * 5f), 4, 16);
            for (int i = 0; i < n; i++)
            {
                Vector2 v = Random.insideUnitCircle.normalized * Random.Range(3f, 11f) * scale;
                Spawn(pos, v, Random.Range(0.45f, 0.95f), scale * 0.9f, scale * 2.6f,
                    new Color(0.86f, 0.94f, 0.98f, 0.95f), new Color(0.7f, 0.85f, 0.95f, 0f),
                    0, false, 2.6f);
            }
            Spawn(pos, Vector2.zero, 1.1f, scale * 1.6f, scale * 5f,
                new Color(0.9f, 0.96f, 1f, 0.55f), new Color(0.8f, 0.9f, 1f, 0f), 3, false, 1f);
        }

        public static void MuzzleFlash(Vector2 pos, float headingDeg, float scale)
        {
            Vector2 dir = NavalMath.HeadingToVector(headingDeg);
            Spawn(pos + dir * scale, dir * 14f * scale, 0.16f, scale * 2.2f, scale * 0.6f,
                new Color(1f, 0.92f, 0.6f, 1f), new Color(1f, 0.5f, 0.1f, 0f), 0, true, 4f);
            for (int i = 0; i < 4; i++)
                Spawn(pos + dir * scale, dir * Random.Range(4f, 10f) * scale + Random.insideUnitCircle * 2f,
                    Random.Range(0.8f, 1.6f), scale * 1.1f, scale * 3.4f,
                    new Color(0.55f, 0.55f, 0.55f, 0.5f), new Color(0.6f, 0.6f, 0.6f, 0f), 1, false, 1.6f);
        }

        public static void Smoke(Vector2 pos, float scale, Color tint, float life = 3.5f, Vector2 drift = default)
        {
            Spawn(pos + Random.insideUnitCircle * scale * 0.4f, drift + Random.insideUnitCircle * 1.2f,
                life * Random.Range(0.8f, 1.2f), scale, scale * Random.Range(2.2f, 3.4f),
                tint, new Color(tint.r, tint.g, tint.b, 0f), 1, false, 0.5f, Random.Range(-18f, 18f));
        }

        public static void Fire(Vector2 pos, float scale)
        {
            Spawn(pos + Random.insideUnitCircle * scale, new Vector2(0f, 0f) + Random.insideUnitCircle * 1.5f,
                Random.Range(0.35f, 0.7f), scale * 1.4f, scale * 0.4f,
                new Color(1f, 0.75f, 0.25f, 1f), new Color(1f, 0.25f, 0.05f, 0f), 0, true, 1.5f);
            if (Random.value < 0.5f)
                Smoke(pos, scale * 1.2f, new Color(0.16f, 0.15f, 0.14f, 0.75f), 4.5f, new Vector2(0f, 1.5f));
        }

        public static void Wake(Vector2 pos, Vector2 vel, float width, float strength)
        {
            Spawn(pos, vel * -0.12f + Random.insideUnitCircle * 0.35f, Random.Range(2.2f, 4.2f),
                width * 0.9f, width * 2.4f,
                new Color(0.92f, 0.97f, 1f, 0.32f * strength), new Color(0.85f, 0.93f, 1f, 0f),
                3, false, 0.9f, Random.Range(-8f, 8f));
        }

        public static void Debris(Vector2 pos, float scale)
        {
            for (int i = 0; i < 7; i++)
                Spawn(pos, Random.insideUnitCircle.normalized * Random.Range(2f, 9f), Random.Range(2f, 5f),
                    scale * 0.5f, scale * 0.25f,
                    new Color(0.18f, 0.17f, 0.16f, 0.9f), new Color(0.2f, 0.2f, 0.2f, 0f), 2, false, 1.4f,
                    Random.Range(-180f, 180f));
        }

        public static void SonarPing(Vector2 pos, float radius)
        {
            Spawn(pos, Vector2.zero, 1.4f, 2f, radius * 2f,
                new Color(0.4f, 1f, 0.85f, 0.5f), new Color(0.4f, 1f, 0.85f, 0f), 4, true, 0f);
        }

        public static void Rain(Vector2 pos, float scale, float alpha)
        {
            Spawn(pos, new Vector2(-6f, -22f) * scale, 0.45f, scale * 0.35f, scale * 0.35f,
                new Color(0.75f, 0.85f, 0.95f, alpha), new Color(0.75f, 0.85f, 0.95f, 0f), 2, false, 0.1f);
        }

        // ------------------------------------------------------------------ update

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) { Build(); return; }

            for (int i = 0; i < _count; i++)
            {
                _parts[i].life -= dt;
                if (_parts[i].life <= 0f)
                {
                    _parts[i] = _parts[_count - 1];
                    _count--; i--;
                    continue;
                }
                _parts[i].pos += _parts[i].vel * dt;
                _parts[i].vel *= Mathf.Max(0f, 1f - _parts[i].drag * dt);
                _parts[i].rot += _parts[i].rotSpd * dt;
            }
            Build();
        }

        void Build()
        {
            BeginBatch(_alpha);
            BeginBatch(_add);

            for (int i = 0; i < _count; i++)
            {
                ref P p = ref _parts[i];
                float t = 1f - Mathf.Clamp01(p.life / Mathf.Max(0.0001f, p.maxLife));
                float size = Mathf.Lerp(p.size0, p.size1, t);
                Color col = Color.Lerp(p.c0, p.c1, t);
                if (col.a <= 0.002f || size <= 0.001f) continue;
                AddQuad(p.additive ? _add : _alpha, p.pos, size, p.rot, col, p.tex);
            }

            EndBatch(_alpha);
            EndBatch(_add);
        }

        static void BeginBatch(Batch b)
        {
            b.verts.Clear(); b.cols.Clear(); b.uvs.Clear(); b.tris.Clear();
        }

        static void EndBatch(Batch b)
        {
            b.mesh.Clear();
            if (b.verts.Count == 0) return;
            b.mesh.SetVertices(b.verts);
            b.mesh.SetColors(b.cols);
            b.mesh.SetUVs(0, b.uvs);
            b.mesh.SetTriangles(b.tris, 0, false);
            b.mesh.bounds = new Bounds(Vector3.zero, new Vector3(GameConfig.WorldSize * 2f, GameConfig.WorldSize * 2f, 10f));
        }

        static void AddQuad(Batch b, Vector2 pos, float size, float rot, Color col, int tex)
        {
            float h = size * 0.5f;
            float r = rot * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            Vector2 ax = new Vector2(c, s) * h;
            Vector2 ay = new Vector2(-s, c) * h;

            int v0 = b.verts.Count;
            b.verts.Add(new Vector3(pos.x - ax.x - ay.x, pos.y - ax.y - ay.y, 0f));
            b.verts.Add(new Vector3(pos.x + ax.x - ay.x, pos.y + ax.y - ay.y, 0f));
            b.verts.Add(new Vector3(pos.x + ax.x + ay.x, pos.y + ax.y + ay.y, 0f));
            b.verts.Add(new Vector3(pos.x - ax.x + ay.x, pos.y - ax.y + ay.y, 0f));

            for (int i = 0; i < 4; i++) b.cols.Add(col);

            // 3x2 atlas layout (6 tiles)
            const int cols = 3, rows = 2;
            int tx = tex % cols, ty = tex / cols;
            float u0 = tx / (float)cols, u1 = (tx + 1f) / cols;
            float w0 = 1f - (ty + 1f) / rows, w1 = 1f - ty / (float)rows;
            const float pad = 0.002f;
            b.uvs.Add(new Vector2(u0 + pad, w0 + pad));
            b.uvs.Add(new Vector2(u1 - pad, w0 + pad));
            b.uvs.Add(new Vector2(u1 - pad, w1 - pad));
            b.uvs.Add(new Vector2(u0 + pad, w1 - pad));

            b.tris.Add(v0); b.tris.Add(v0 + 1); b.tris.Add(v0 + 2);
            b.tris.Add(v0); b.tris.Add(v0 + 2); b.tris.Add(v0 + 3);
        }

        // ------------------------------------------------------------------ atlas

        static Texture2D BuildAtlas()
        {
            const int cols = 3, rows = 2, tile = 128;
            int w = cols * tile, h = rows * tile;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = "fx_atlas",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var px = new Color[w * h];

            for (int ty = 0; ty < rows; ty++)
                for (int tx = 0; tx < cols; tx++)
                {
                    int index = ty * cols + tx;
                    for (int y = 0; y < tile; y++)
                        for (int x = 0; x < tile; x++)
                        {
                            float u = (x + 0.5f) / tile - 0.5f;
                            float v = (y + 0.5f) / tile - 0.5f;
                            float d = Mathf.Sqrt(u * u + v * v) * 2f;
                            float a = 0f;
                            switch (index)
                            {
                                case 0: // soft round blob
                                    a = Mathf.Clamp01(1f - d);
                                    a = a * a;
                                    break;
                                case 1: // billowing smoke puff
                                    {
                                        float n = NavalMath.FBM((x + index * 91) * 0.06f, (y + index * 57) * 0.06f, 3);
                                        float rr = d * (0.78f + n * 0.5f);
                                        a = Mathf.Clamp01(1f - rr);
                                        a = Mathf.SmoothStep(0f, 1f, a) * (0.55f + n * 0.6f);
                                    }
                                    break;
                                case 2: // debris / rain streak
                                    {
                                        float ax = Mathf.Abs(u) * 6f, ay = Mathf.Abs(v) * 1.6f;
                                        a = Mathf.Clamp01(1f - Mathf.Max(ax, ay) * 1.2f);
                                    }
                                    break;
                                case 3: // foam speckle
                                    {
                                        float n = NavalMath.FBM(x * 0.11f + 33f, y * 0.11f + 71f, 3);
                                        a = Mathf.Clamp01(1f - d) * Mathf.Clamp01((n - 0.35f) * 2.6f);
                                        a = Mathf.SmoothStep(0f, 1f, a);
                                    }
                                    break;
                                case 4: // hollow ring (sonar)
                                    {
                                        float ring = 1f - Mathf.Abs(d - 0.86f) * 9f;
                                        a = Mathf.Clamp01(ring);
                                        a *= a;
                                    }
                                    break;
                                default: // soft square glow
                                    a = Mathf.Clamp01(1f - Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) * 2.1f);
                                    break;
                            }
                            px[(ty * tile + y) * w + (tx * tile + x)] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                        }
                }

            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        public void ClearAll() { _count = 0; }
    }
}
