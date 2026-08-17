using System.Collections.Generic;
using UnityEngine;

namespace Naval
{
    /// <summary>A drifting smoke cloud. Blocks line of sight for everyone, friend and foe alike.</summary>
    public class SmokeCloud
    {
        public Vector2 position;
        public float radius;
        public float life;
        public float maxLife;
        public Team owner;

        public float Density => Mathf.Clamp01(life / Mathf.Max(0.1f, maxLife) * 2.2f);
    }

    public class SmokeSystem : MonoBehaviour
    {
        public static SmokeSystem I { get; private set; }
        public readonly List<SmokeCloud> Clouds = new List<SmokeCloud>();

        float _fxTimer;

        public static SmokeSystem Create(Transform parent)
        {
            var go = new GameObject("SmokeSystem");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<SmokeSystem>();
            I = s;
            return s;
        }

        public void Deploy(Vector2 pos, float radius, float duration, Team owner)
        {
            Clouds.Add(new SmokeCloud
            {
                position = pos,
                radius = radius,
                life = duration,
                maxLife = duration,
                owner = owner
            });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector2 wind = WeatherSystem.I != null ? WeatherSystem.I.WindDirection : Vector2.right;
            float windSpeed = WeatherSystem.I != null ? WeatherSystem.I.SeaState * 0.9f : 0.5f;

            for (int i = Clouds.Count - 1; i >= 0; i--)
            {
                var c = Clouds[i];
                c.life -= dt;
                c.position += wind * windSpeed * dt;
                if (c.life <= 0f) { Clouds.RemoveAt(i); continue; }
            }

            _fxTimer -= dt;
            if (_fxTimer <= 0f)
            {
                _fxTimer = 0.12f;
                for (int i = 0; i < Clouds.Count; i++)
                {
                    var c = Clouds[i];
                    if (c.Density < 0.2f) continue;
                    Vector2 p = c.position + Random.insideUnitCircle * c.radius;
                    ParticleFX.Smoke(p, c.radius * 0.55f, new Color(0.82f, 0.84f, 0.86f, 0.5f * c.Density), 3.2f, wind * windSpeed);
                }
            }
        }

        /// <summary>True when a sight line passes through any cloud (observer inside its own cloud still sees out).</summary>
        public bool BlocksLineOfSight(Vector2 from, Vector2 to)
        {
            for (int i = 0; i < Clouds.Count; i++)
            {
                var c = Clouds[i];
                if (c.Density < 0.25f) continue;
                if ((from - c.position).sqrMagnitude < c.radius * c.radius) continue;   // we are in it
                if (NavalMath.DistanceToSegment(c.position, from, to) < c.radius) return true;
            }
            return false;
        }

        public bool IsInsideSmoke(Vector2 p)
        {
            for (int i = 0; i < Clouds.Count; i++)
            {
                var c = Clouds[i];
                if (c.Density < 0.25f) continue;
                if ((p - c.position).sqrMagnitude < c.radius * c.radius) return true;
            }
            return false;
        }

        public void Clear() => Clouds.Clear();
    }
}
