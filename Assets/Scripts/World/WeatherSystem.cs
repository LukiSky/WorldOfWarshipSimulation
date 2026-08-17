using UnityEngine;

namespace Naval
{
    /// <summary>
    /// Drives sea state and visibility. Weather changes the detection ranges of every ship,
    /// gun dispersion, wave choppiness and the look of the sea.
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem I { get; private set; }

        public WeatherType Current { get; private set; } = WeatherType.Clear;
        public WeatherType Next { get; private set; } = WeatherType.Clear;
        public float TimeToChange { get; private set; }

        public float VisibilityMultiplier { get; private set; } = 1f;
        public float DispersionMultiplier { get; private set; } = 1f;
        public float SeaState { get; private set; } = 1f;      // wave height 0..2
        public Vector2 WindDirection { get; private set; } = new Vector2(0.7f, 0.7f);

        float _blend = 1f;
        float _rainAccum;
        Camera _cam;

        // per weather targets
        struct Profile
        {
            public float visibility, dispersion, sea, fogAlpha;
            public Color tint, fogColor;
        }

        static Profile ProfileFor(WeatherType t)
        {
            switch (t)
            {
                case WeatherType.Fog:
                    return new Profile
                    {
                        visibility = 0.42f, dispersion = 1.25f, sea = 0.55f, fogAlpha = 0.24f,
                        tint = new Color(0.80f, 0.85f, 0.90f), fogColor = new Color(0.66f, 0.71f, 0.76f)
                    };
                case WeatherType.Rain:
                    return new Profile
                    {
                        visibility = 0.74f, dispersion = 1.15f, sea = 1.35f, fogAlpha = 0.12f,
                        tint = new Color(0.76f, 0.81f, 0.86f), fogColor = new Color(0.42f, 0.48f, 0.56f)
                    };
                case WeatherType.Storm:
                    return new Profile
                    {
                        visibility = 0.55f, dispersion = 1.45f, sea = 2.1f, fogAlpha = 0.18f,
                        tint = new Color(0.62f, 0.68f, 0.75f), fogColor = new Color(0.28f, 0.33f, 0.42f)
                    };
                default:
                    return new Profile
                    {
                        visibility = 1f, dispersion = 1f, sea = 1f, fogAlpha = 0f,
                        tint = Color.white, fogColor = new Color(0.5f, 0.6f, 0.7f)
                    };
            }
        }

        public static WeatherSystem Create(Transform parent, WeatherType initial)
        {
            var go = new GameObject("Weather");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<WeatherSystem>();
            I = w;
            w.Current = initial;
            w.Next = initial;
            w.TimeToChange = Random.Range(70f, 150f);
            w._blend = 1f;
            w.Apply(1f);
            return w;
        }

        public void ForceWeather(WeatherType t)
        {
            Current = t; Next = t; _blend = 1f;
            TimeToChange = Random.Range(70f, 150f);
            Apply(1f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_blend < 1f)
            {
                _blend = Mathf.Min(1f, _blend + dt / 18f);
                Apply(_blend);
                if (_blend >= 1f) Current = Next;
            }
            else
            {
                TimeToChange -= dt;
                if (TimeToChange <= 0f)
                {
                    Next = RollWeather(Current);
                    TimeToChange = Random.Range(80f, 170f);
                    if (Next != Current) { _blend = 0f; }
                    else Apply(1f);
                    GameEvents.RaiseMessage("Weather shifting to " + Next.ToString().ToUpper(), Team.Neutral);
                }
            }

            WindDirection = NavalMath.Rotate(WindDirection, dt * 1.5f * Mathf.Sin(Time.time * 0.05f));
            SpawnPrecipitation(dt);
        }

        static WeatherType RollWeather(WeatherType cur)
        {
            float r = Random.value;
            switch (cur)
            {
                case WeatherType.Clear: return r < 0.45f ? WeatherType.Clear : r < 0.7f ? WeatherType.Fog : r < 0.92f ? WeatherType.Rain : WeatherType.Storm;
                case WeatherType.Fog: return r < 0.5f ? WeatherType.Clear : r < 0.8f ? WeatherType.Fog : WeatherType.Rain;
                case WeatherType.Rain: return r < 0.35f ? WeatherType.Clear : r < 0.6f ? WeatherType.Rain : r < 0.85f ? WeatherType.Storm : WeatherType.Fog;
                default: return r < 0.45f ? WeatherType.Rain : r < 0.75f ? WeatherType.Storm : WeatherType.Clear;
            }
        }

        void Apply(float t)
        {
            var a = ProfileFor(Current);
            var b = ProfileFor(Next);

            VisibilityMultiplier = Mathf.Lerp(a.visibility, b.visibility, t);
            DispersionMultiplier = Mathf.Lerp(a.dispersion, b.dispersion, t);
            SeaState = Mathf.Lerp(a.sea, b.sea, t);
            float fogAlpha = Mathf.Lerp(a.fogAlpha, b.fogAlpha, t);
            Color tint = Color.Lerp(a.tint, b.tint, t);
            Color fogCol = Color.Lerp(a.fogColor, b.fogColor, t);

            if (OceanRenderer.I != null)
                OceanRenderer.I.SetWeather(WindDirection, SeaState, Mathf.Lerp(1f, 1.5f, Mathf.InverseLerp(1f, 2.1f, SeaState)), tint);
            if (FogOfWarRenderer.I != null)
                FogOfWarRenderer.I.SetWeather(fogAlpha, fogCol);
        }

        void SpawnPrecipitation(float dt)
        {
            var type = _blend < 0.5f ? Current : Next;
            if (type != WeatherType.Rain && type != WeatherType.Storm) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null || ParticleFX.I == null) return;

            float rate = type == WeatherType.Storm ? 150f : 85f;
            _rainAccum += rate * Time.unscaledDeltaTime;      // rainfall does not speed up with the sim
            int count = Mathf.FloorToInt(_rainAccum);
            _rainAccum -= count;
            count = Mathf.Min(count, 12);

            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;
            Vector2 c = _cam.transform.position;
            // keep drops a constant size on screen, and stop drawing them at strategic zoom
            float pixel = RTSCamera.I != null ? RTSCamera.I.PixelScale : halfH * 0.002f;
            if (halfH > 620f) return;
            float scale = pixel * 4f;

            for (int i = 0; i < count; i++)
            {
                Vector2 p = c + new Vector2(Random.Range(-halfW, halfW) * 1.1f, Random.Range(-halfH, halfH) * 1.1f);
                ParticleFX.Rain(p, scale, type == WeatherType.Storm ? 0.5f : 0.32f);
            }
        }

        public string Describe()
        {
            if (_blend < 1f) return Current + " -> " + Next;
            return Current.ToString();
        }
    }
}
