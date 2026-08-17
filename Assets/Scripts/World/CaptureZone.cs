using System.Collections.Generic;
using UnityEngine;

namespace Naval
{
    /// <summary>
    /// A domination objective. Occupancy is driven by a CircleCollider2D trigger; holding it alone
    /// fills the capture meter, both fleets present freezes it ("contested"), and a held zone
    /// pours points into its team's score.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class CaptureZone : MonoBehaviour
    {
        public string zoneName = "A";
        public float radius = 150f;

        /// <summary>Seconds a single ship needs to flip the zone from neutral.</summary>
        public const float BaseCaptureTime = 40f;

        /// <summary>-1 fully enemy, 0 neutral, +1 fully player.</summary>
        public float Progress { get; private set; }
        public Team Owner { get; private set; } = Team.Neutral;
        public bool Contested { get; private set; }
        public int PlayerShips { get; private set; }
        public int EnemyShips { get; private set; }

        public Vector2 Position => new Vector2(transform.position.x, transform.position.y);

        readonly HashSet<Ship> _inside = new HashSet<Ship>();
        readonly List<Ship> _stale = new List<Ship>();
        float _tickTimer;

        public static CaptureZone Create(Transform parent, Vector2 pos, float radius, string name)
        {
            var go = new GameObject("Zone_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = radius;

            var z = go.AddComponent<CaptureZone>();
            z.radius = radius;
            z.zoneName = name;
            return z;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var s = other.GetComponentInParent<Ship>();
            if (s != null) _inside.Add(s);
        }

        void OnTriggerExit2D(Collider2D other)
        {
            var s = other.GetComponentInParent<Ship>();
            if (s != null) _inside.Remove(s);
        }

        void Update()
        {
            _tickTimer -= Time.deltaTime;
            if (_tickTimer > 0f) return;
            const float tick = 0.25f;
            _tickTimer = tick;
            Evaluate(tick);
        }

        void Evaluate(float dt)
        {
            PlayerShips = 0; EnemyShips = 0;
            _stale.Clear();

            foreach (var s in _inside)
            {
                if (s == null || s.IsDead || s.Damage.IsSinking) { _stale.Add(s); continue; }
                if (s.Stats.classType == ShipClassType.Transport) continue;
                // a boat that is not at least at periscope depth cannot hold ground
                if (s.Submarine != null && s.Submarine.Depth != DepthState.Surface && s.Submarine.Depth != DepthState.Periscope) continue;

                if (s.team == Team.Player) PlayerShips++;
                else if (s.team == Team.Enemy) EnemyShips++;
            }
            for (int i = 0; i < _stale.Count; i++) _inside.Remove(_stale[i]);

            Contested = PlayerShips > 0 && EnemyShips > 0;
            int net = PlayerShips - EnemyShips;

            if (Contested)
            {
                // both fleets present: the meter freezes exactly where it is
            }
            else if (net != 0)
            {
                // extra hulls speed the capture up, but with diminishing returns
                float rate = Mathf.Sqrt(Mathf.Abs(net)) / BaseCaptureTime;
                Progress = Mathf.Clamp(Progress + Mathf.Sign(net) * rate * dt, -1f, 1f);
            }
            else if (Owner == Team.Neutral)
            {
                // an abandoned, uncaptured zone slowly bleeds back to neutral
                Progress = Mathf.MoveTowards(Progress, 0f, dt / (BaseCaptureTime * 3f));
            }

            Team newOwner = Owner;
            if (Progress >= 1f) newOwner = Team.Player;
            else if (Progress <= -1f) newOwner = Team.Enemy;
            else if (Mathf.Abs(Progress) < 0.35f) newOwner = Team.Neutral;

            if (newOwner != Owner)
            {
                Owner = newOwner;
                string who = Owner == Team.Player ? "captured by our forces"
                           : Owner == Team.Enemy ? "lost to the enemy" : "is neutral again";
                GameEvents.RaiseMessage("Zone " + zoneName + " " + who, Owner);
                AudioManager.PlayUI(Owner == Team.Player ? SoundId.Detected : SoundId.Alarm, 0.6f);
            }
        }

        /// <summary>Capture meter for the owning side, 0..1, used by the HUD ring.</summary>
        public float CaptureFraction => Mathf.Abs(Progress);

        public Color DisplayColor
        {
            get
            {
                if (Contested) return new Color(1f, 0.82f, 0.25f);
                if (Owner == Team.Player) return Teams.Color(Team.Player);
                if (Owner == Team.Enemy) return Teams.Color(Team.Enemy);
                return new Color(0.75f, 0.78f, 0.8f);
            }
        }
    }
}
