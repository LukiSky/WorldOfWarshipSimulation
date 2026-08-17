using System.Collections.Generic;
using UnityEngine;

namespace Naval
{
    /// <summary>Fleet formations. Offsets are in formation space: +Y is the direction of advance.</summary>
    public static class FormationManager
    {
        public static Vector2[] Offsets(FormationType type, int count, float spacing)
        {
            var result = new Vector2[Mathf.Max(0, count)];
            if (count <= 0) return result;

            switch (type)
            {
                case FormationType.LineAhead:
                    for (int i = 0; i < count; i++) result[i] = new Vector2(0f, -spacing * i);
                    break;

                case FormationType.LineAbreast:
                    for (int i = 0; i < count; i++)
                    {
                        int side = (i % 2 == 0) ? 1 : -1;
                        int rank = (i + 1) / 2;
                        result[i] = new Vector2(side * spacing * rank, 0f);
                    }
                    break;

                case FormationType.Wedge:
                    for (int i = 0; i < count; i++)
                    {
                        int side = (i % 2 == 0) ? 1 : -1;
                        int rank = (i + 1) / 2;
                        result[i] = new Vector2(side * spacing * rank * 0.85f, -spacing * rank * 0.8f);
                    }
                    break;

                case FormationType.Circle:
                    for (int i = 0; i < count; i++)
                    {
                        float a = i / (float)count * Mathf.PI * 2f;
                        float r = spacing * Mathf.Max(1f, count * 0.22f);
                        result[i] = new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r);
                    }
                    break;

                case FormationType.DefensiveScreen:
                    // heavies in the middle, escorts on a ring around them (ordering is done by the caller)
                    for (int i = 0; i < count; i++)
                    {
                        if (i == 0) { result[i] = Vector2.zero; continue; }
                        float a = (i - 1) / Mathf.Max(1f, count - 1f) * Mathf.PI * 2f;
                        result[i] = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * spacing * 1.9f;
                    }
                    break;

                default:
                    for (int i = 0; i < count; i++)
                    {
                        int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
                        int cx = i % cols, cy = i / cols;
                        result[i] = new Vector2((cx - (cols - 1) * 0.5f) * spacing, -cy * spacing);
                    }
                    break;
            }
            return result;
        }

        /// <summary>Puts the selection into a formation around its heaviest unit.</summary>
        public static void Apply(List<Ship> ships, FormationType type)
        {
            if (ships == null || ships.Count == 0) return;

            var ordered = new List<Ship>(ships);
            // formation order: heaviest first so the flagship leads (or sits in the middle of a screen)
            ordered.Sort((a, b) => ClassWeight(b.Stats.classType).CompareTo(ClassWeight(a.Stats.classType)));

            var leader = ordered[0];
            float spacing = Mathf.Max(45f, leader.Stats.length * 3.2f);
            var offsets = Offsets(type, ordered.Count, spacing);

            for (int i = 1; i < ordered.Count; i++)
            {
                var s = ordered[i];
                if (s == leader) continue;
                s.Navigation.OrderFollow(leader, offsets[i]);
            }

            GameEvents.RaiseMessage("Formation: " + Pretty(type) + " on " + leader.shipName, Team.Player);
        }

        public static void Break(List<Ship> ships)
        {
            if (ships == null) return;
            for (int i = 0; i < ships.Count; i++)
                if (ships[i].Navigation.Order == OrderType.Follow) ships[i].Navigation.OrderStop();
        }

        static int ClassWeight(ShipClassType c)
        {
            switch (c)
            {
                case ShipClassType.Battleship: return 4;
                case ShipClassType.Cruiser: return 3;
                case ShipClassType.Destroyer: return 2;
                case ShipClassType.Submarine: return 1;
                default: return 0;
            }
        }

        public static string Pretty(FormationType t)
        {
            switch (t)
            {
                case FormationType.LineAhead: return "Line Ahead";
                case FormationType.LineAbreast: return "Line Abreast";
                case FormationType.Wedge: return "Wedge";
                case FormationType.Circle: return "Circle";
                case FormationType.DefensiveScreen: return "Defensive Screen";
                default: return "None";
            }
        }
    }
}
