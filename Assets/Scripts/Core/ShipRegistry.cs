using System.Collections.Generic;
using UnityEngine;

namespace Naval
{
    /// <summary>Central list of live ships plus the cheap spatial queries every system needs.</summary>
    public static class ShipRegistry
    {
        public static readonly List<Ship> All = new List<Ship>();
        static readonly List<Ship> _player = new List<Ship>();
        static readonly List<Ship> _enemy = new List<Ship>();
        static readonly List<Ship> _scratch = new List<Ship>();

        public static void Register(Ship s)
        {
            if (!All.Contains(s)) All.Add(s);
            var list = s.team == Team.Player ? _player : _enemy;
            if (!list.Contains(s)) list.Add(s);
        }

        public static void Unregister(Ship s)
        {
            All.Remove(s);
            _player.Remove(s);
            _enemy.Remove(s);
        }

        public static void Clear()
        {
            All.Clear(); _player.Clear(); _enemy.Clear();
        }

        public static List<Ship> OfTeam(Team t) => t == Team.Player ? _player : _enemy;

        public static int AliveCount(Team t)
        {
            var l = OfTeam(t);
            int n = 0;
            for (int i = 0; i < l.Count; i++) if (l[i] != null && !l[i].IsDead) n++;
            return n;
        }

        public static int AliveCount(Team t, ShipClassType cls)
        {
            var l = OfTeam(t);
            int n = 0;
            for (int i = 0; i < l.Count; i++)
                if (l[i] != null && !l[i].IsDead && l[i].Stats.classType == cls) n++;
            return n;
        }

        /// <summary>Nearest live ship of a team within range (scratch list free).</summary>
        public static Ship Nearest(Vector2 pos, Team team, float maxRange, Ship ignore = null)
        {
            var l = OfTeam(team);
            Ship best = null;
            float bd = maxRange * maxRange;
            for (int i = 0; i < l.Count; i++)
            {
                var s = l[i];
                if (s == null || s.IsDead || s == ignore) continue;
                float d = (s.Position - pos).sqrMagnitude;
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        /// <summary>Ships of a team inside a radius. The returned list is reused - copy it if you keep it.</summary>
        public static List<Ship> InRadius(Vector2 pos, float radius, Team team)
        {
            _scratch.Clear();
            var l = OfTeam(team);
            float r2 = radius * radius;
            for (int i = 0; i < l.Count; i++)
            {
                var s = l[i];
                if (s == null || s.IsDead) continue;
                if ((s.Position - pos).sqrMagnitude <= r2) _scratch.Add(s);
            }
            return _scratch;
        }

        public static List<Ship> AllInRadius(Vector2 pos, float radius, Ship ignore = null)
        {
            _scratch.Clear();
            float r2 = radius * radius;
            for (int i = 0; i < All.Count; i++)
            {
                var s = All[i];
                if (s == null || s.IsDead || s == ignore) continue;
                if ((s.Position - pos).sqrMagnitude <= r2) _scratch.Add(s);
            }
            return _scratch;
        }
    }
}
