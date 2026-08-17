using UnityEngine;

namespace Naval
{
    /// <summary>
    /// Shared maths helpers. Heading convention: degrees, 0 = north (+Y), clockwise positive.
    /// Sprites are authored pointing up, so transform z-rotation = -heading.
    /// </summary>
    public static class NavalMath
    {
        public static Vector2 HeadingToVector(float headingDeg)
        {
            float r = headingDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
        }

        public static float VectorToHeading(Vector2 v)
        {
            if (v.sqrMagnitude < 1e-8f) return 0f;
            return Mathf.Repeat(Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg, 360f);
        }

        /// <summary>Signed shortest delta from a to b in degrees, range [-180, 180].</summary>
        public static float DeltaAngle(float a, float b) => Mathf.DeltaAngle(a, b);

        public static float Wrap360(float a) => Mathf.Repeat(a, 360f);

        public static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public static Vector3 V3(Vector2 v, float z = 0f) => new Vector3(v.x, v.y, z);
        public static Vector2 V2(Vector3 v) => new Vector2(v.x, v.y);

        /// <summary>Box-Muller gaussian, mean 0 stddev 1, clamped to +-3.</summary>
        public static float Gaussian()
        {
            float u1 = Mathf.Max(1e-6f, Random.value);
            float u2 = Random.value;
            float g = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
            return Mathf.Clamp(g, -3f, 3f);
        }

        /// <summary>Random point inside a unit circle biased toward the centre (naval shell pattern).</summary>
        public static Vector2 EllipticalScatter(float halfWidth, float halfLength)
        {
            float a = Random.value * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Random.value);
            r = Mathf.Lerp(r, r * r, 0.35f);
            return new Vector2(Mathf.Cos(a) * r * halfWidth, Mathf.Sin(a) * r * halfLength);
        }

        /// <summary>
        /// First-order intercept solution. Returns false if the projectile is too slow to catch the target.
        /// </summary>
        public static bool Intercept(Vector2 shooter, Vector2 target, Vector2 targetVel, float projSpeed, out Vector2 aimPoint, out float timeOfFlight)
        {
            aimPoint = target;
            timeOfFlight = 0f;
            Vector2 rel = target - shooter;
            float a = Vector2.Dot(targetVel, targetVel) - projSpeed * projSpeed;
            float b = 2f * Vector2.Dot(rel, targetVel);
            float c = Vector2.Dot(rel, rel);

            float t;
            if (Mathf.Abs(a) < 1e-4f)
            {
                if (Mathf.Abs(b) < 1e-4f) return false;
                t = -c / b;
            }
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc < 0f) return false;
                float sq = Mathf.Sqrt(disc);
                float t1 = (-b + sq) / (2f * a);
                float t2 = (-b - sq) / (2f * a);
                t = Mathf.Min(t1, t2);
                if (t < 0f) t = Mathf.Max(t1, t2);
            }
            if (t < 0f) return false;
            timeOfFlight = t;
            aimPoint = target + targetVel * t;
            return true;
        }

        /// <summary>Distance from point p to segment ab.</summary>
        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-6f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Point/oriented-ellipse test used for shell and torpedo hits on a hull.</summary>
        public static bool InsideHull(Vector2 p, Vector2 center, float headingDeg, float length, float beam)
        {
            Vector2 local = Rotate(p - center, headingDeg);   // rotate into hull space (heading -> +Y)
            float a = beam * 0.5f, b = length * 0.5f;
            if (a < 1e-4f || b < 1e-4f) return false;
            return (local.x * local.x) / (a * a) + (local.y * local.y) / (b * b) <= 1f;
        }

        public static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        public static float Remap(float v, float a, float b, float x, float y)
            => Mathf.Lerp(x, y, Mathf.InverseLerp(a, b, v));

        // ------------------------------------------------------------- value noise
        static int _seed = 1337;
        public static void SetNoiseSeed(int s) { _seed = s; }

        static float Hash(int x, int y)
        {
            int n = x * 374761393 + y * 668265263 + _seed * 1442695040;
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n ^ (n >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }

        public static float ValueNoise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = SmoothStep01(xf), v = SmoothStep01(yf);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        public static float FBM(float x, float y, int octaves = 4, float lacunarity = 2.1f, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += ValueNoise(x * freq, y * freq) * amp;
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return norm > 0f ? sum / norm : 0f;
        }
    }
}
