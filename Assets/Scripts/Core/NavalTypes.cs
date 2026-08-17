using UnityEngine;

namespace Naval
{
    public enum ShipClassType { Destroyer, Cruiser, Battleship, Submarine, Transport }

    public enum Team { Player = 0, Enemy = 1, Neutral = 2 }

    public enum ShipSystem { Hull = 0, Engine = 1, Steering = 2, MainGuns = 3, SecondaryGuns = 4, Sensors = 5, Propulsion = 6 }

    public enum DepthState { Surface = 0, Periscope = 1, Submerged = 2, Deep = 3 }

    public enum AIState { Idle, Patrolling, Moving, Searching, Tracking, Attacking, Retreating, Evading, Repairing, Disabled, Sinking }

    public enum OrderType { None, Move, AttackMove, Attack, Patrol, Follow, Stop, Reverse, Retreat, HoldPosition, ReturnToPort }

    public enum FormationType { None, LineAhead, LineAbreast, Wedge, Circle, DefensiveScreen }

    public enum WeatherType { Clear, Fog, Rain, Storm }

    public enum GameMode { Domination, Skirmish, FleetBattle, CaptureAndControl, Escort }

    public enum GamePhase { Menu, Deployment, Battle, Victory, Defeat }

    public enum ContactState { Confirmed, Unknown, LastKnown }

    public enum HitResult { Miss, Shatter, Ricochet, Overpenetration, Penetration, Citadel }

    public static class Teams
    {
        public static Team Opponent(Team t) => t == Team.Player ? Team.Enemy : Team.Player;

        public static Color Color(Team t)
        {
            switch (t)
            {
                case Team.Player: return new Color(0.35f, 0.78f, 1f);
                case Team.Enemy: return new Color(1f, 0.36f, 0.34f);
                default: return new Color(0.85f, 0.85f, 0.6f);
            }
        }
    }

    /// <summary>Global tuning constants. 1 world unit ~ 10 meters.</summary>
    public static class GameConfig
    {
        public const float WorldSize = 4000f;          // square map, world spans [-2000, 2000]
        public const float HeightmapResolution = 512f; // terrain sample grid
        public const float NavCellSize = 16f;          // pathfinding cell size
        public const float SeaLevel = 0f;              // heights above this are land

        public const float ShallowDepth = 0.35f;       // normalized depth below which water is shallow

        public static float Half => WorldSize * 0.5f;

        // Simulation rates (Hz) for time-sliced systems
        public const float AIUpdateRate = 5f;
        public const float DetectionUpdateRate = 8f;
        public const float PathThrottlePerFrame = 3;
    }
}
