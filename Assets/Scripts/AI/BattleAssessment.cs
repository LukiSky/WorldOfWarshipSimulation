using System.Collections.Generic;
using UnityEngine;

namespace Naval
{
    /// <summary>How a fleet intends to play the current situation.</summary>
    public enum FleetPosture
    {
        LandGrab,    // early and open: take the cheap points fast
        Press,       // losing on projection: force fights and flips
        Hold,        // winning on projection: garrison what we own, trade safely
        CloseOut,    // winning with the clock running down: disengage and stall it out
        Desperate    // losing with the clock running down: everything on one point
    }

    public enum AIDifficulty { Recruit, Veteran, Elite }

    /// <summary>What one team knows and believes about a capture zone.</summary>
    public struct ZoneIntel
    {
        public CaptureZone zone;
        public bool isMine, isEnemy, isNeutral, contested;
        public int myShips, knownEnemyShips;
        public float value;          // how much this team wants it right now
        public bool mustHold;        // losing it flips the projected result
        public Vector2 Position => zone != null ? zone.Position : Vector2.zero;
    }

    /// <summary>A contact with its position estimated forward from when it was last seen.</summary>
    public struct PredictedContact
    {
        public Ship ship;
        public Vector2 position;
        public float uncertainty;    // radius the ship could be anywhere inside
        public bool live;            // currently observed, so position is exact
        public ShipClassType shipClass;
        public float strength;
    }

    /// <summary>
    /// One team's read of the battle, rebuilt a couple of times a second by its FleetCommander and
    /// published for every ShipAI on that team to share. This is where the AI learns what game it is
    /// playing: the mode, the score, the clock, who is winning if nothing changes, and where the
    /// enemy probably is now rather than where it was last seen.
    ///
    /// Everything here is derived strictly from what the team has actually detected - the AI never
    /// reads through the fog of war.
    /// </summary>
    public class BattleAssessment
    {
        static readonly BattleAssessment[] _byTeam = new BattleAssessment[2];

        public static BattleAssessment For(Team t)
        {
            int i = (int)t;
            return i >= 0 && i < 2 ? _byTeam[i] : null;
        }

        public static void Publish(Team t, BattleAssessment a)
        {
            int i = (int)t;
            if (i >= 0 && i < 2) _byTeam[i] = a;
        }

        public static void Clear()
        {
            _byTeam[0] = null;
            _byTeam[1] = null;
        }

        // ------------------------------------------------------------------ state

        public Team team;
        public GameMode mode;
        public AIDifficulty difficulty = AIDifficulty.Elite;

        public bool IsObjectiveMode;      // domination / capture and control - points win it
        public bool IsEscortMode;
        public bool IsAnnihilationMode;   // skirmish / fleet battle - hulls win it

        public float MyScore, TheirScore, ScoreToWin, TimeRemaining, TimeLimit;
        public float MyPointRate, TheirPointRate;
        public float ProjectedMine, ProjectedTheirs;
        public bool ProjectedWin;

        public FleetPosture Posture = FleetPosture.LandGrab;
        public string PostureReason = "";

        public float MyStrength, TheirStrength;
        public float StrengthRatio => TheirStrength > 0.01f ? MyStrength / TheirStrength : 4f;

        public Vector2 FleetCenter, ThreatCentroid;

        public ZoneIntel[] Zones = new ZoneIntel[0];
        public readonly List<PredictedContact> Contacts = new List<PredictedContact>();

        /// <summary>Longest we trust a dead-reckoned position before treating the contact as lost.</summary>
        public const float MaxExtrapolation = 26f;

        // ------------------------------------------------------------------ build

        public void Rebuild(Team myTeam, GameMode gameMode, AIDifficulty diff, List<Ship> myShips)
        {
            team = myTeam;
            mode = gameMode;
            difficulty = diff;

            IsObjectiveMode = mode == GameMode.Domination || mode == GameMode.CaptureAndControl;
            IsEscortMode = mode == GameMode.Escort;
            IsAnnihilationMode = mode == GameMode.Skirmish || mode == GameMode.FleetBattle;

            BuildScores();
            BuildStrength(myShips);
            BuildContacts();
            BuildZones();
            BuildPosture();
        }

        void BuildScores()
        {
            var gm = GameManager.I;
            if (gm == null) return;

            bool player = team == Team.Player;
            MyScore = player ? gm.PlayerScore : gm.EnemyScore;
            TheirScore = player ? gm.EnemyScore : gm.PlayerScore;
            ScoreToWin = GameManager.ScoreToWin;
            TimeRemaining = gm.TimeRemaining;
            TimeLimit = gm.TimeLimit;

            // points per second currently flowing to each side from held zones
            MyPointRate = 0f;
            TheirPointRate = 0f;
            var map = WorldMap.I;
            if (map != null)
                for (int i = 0; i < map.Zones.Count; i++)
                {
                    var z = map.Zones[i];
                    if (z == null || z.Owner == Team.Neutral) continue;
                    if (z.Owner == team) MyPointRate += GameManager.ZonePointsPerSecond;
                    else TheirPointRate += GameManager.ZonePointsPerSecond;
                }

            ProjectedMine = MyScore + MyPointRate * TimeRemaining;
            ProjectedTheirs = TheirScore + TheirPointRate * TimeRemaining;

            // whoever reaches the cap first wins; if neither does, the buzzer decides
            float myRace = MyPointRate > 0.01f ? (ScoreToWin - MyScore) / MyPointRate : float.MaxValue;
            float theirRace = TheirPointRate > 0.01f ? (ScoreToWin - TheirScore) / TheirPointRate : float.MaxValue;

            if (myRace <= TimeRemaining || theirRace <= TimeRemaining)
                ProjectedWin = myRace < theirRace;
            else
                ProjectedWin = ProjectedMine > ProjectedTheirs;
        }

        void BuildStrength(List<Ship> myShips)
        {
            MyStrength = 0f;
            FleetCenter = Vector2.zero;
            int n = 0;
            for (int i = 0; i < myShips.Count; i++)
            {
                var s = myShips[i];
                if (s == null || s.IsDead) continue;
                MyStrength += Value(s);
                FleetCenter += s.Position;
                n++;
            }
            if (n > 0) FleetCenter /= n;
        }

        static float Value(Ship s) => s.Stats.fleetPointCost * Mathf.Max(0.1f, s.HealthFraction);

        /// <summary>
        /// Turns the team's contact list into position estimates. A live contact is exact; a contact
        /// that has gone dark is dead-reckoned along its last known course, with an uncertainty
        /// radius that grows the longer it stays dark.
        /// </summary>
        void BuildContacts()
        {
            Contacts.Clear();
            TheirStrength = 0f;
            ThreatCentroid = Vector2.zero;

            var ds = DetectionSystem.I;
            if (ds == null) return;

            int n = 0;
            foreach (var c in ds.Contacts(team))
            {
                if (c.ship == null || c.ship.IsDead) continue;

                bool live = c.state == ContactState.Confirmed || c.state == ContactState.Unknown;
                Vector2 pos;
                float uncertainty;

                if (live)
                {
                    pos = c.state == ContactState.Confirmed ? c.ship.Position : c.lastKnownPosition;
                    uncertainty = c.state == ContactState.Confirmed ? 0f : 40f;
                }
                else
                {
                    float age = Mathf.Min(c.Age, MaxExtrapolation);
                    if (c.Age > DetectionSystem.MemoryDuration) continue;

                    float speed = EstimatedSpeed(c);
                    pos = c.lastKnownPosition + NavalMath.HeadingToVector(c.lastKnownHeading) * speed * age;
                    // it could also have turned, so the error grows in every direction
                    uncertainty = speed * age * 0.75f + 30f;
                    if (WorldMap.I != null) pos = WorldMap.I.Clamp(pos);
                }

                // a stale contact counts for less when weighing up strength
                float confidence = live ? 1f : Mathf.Clamp01(1f - c.Age / DetectionSystem.MemoryDuration);
                float strength = Value(c.ship) * confidence;

                Contacts.Add(new PredictedContact
                {
                    ship = c.ship,
                    position = pos,
                    uncertainty = uncertainty,
                    live = live,
                    shipClass = c.classIdentified ? c.knownClass : c.ship.Stats.classType,
                    strength = strength
                });

                TheirStrength += strength;
                ThreatCentroid += pos;
                n++;
            }

            if (n > 0) ThreatCentroid /= n;
            else ThreatCentroid = FleetCenter;
        }

        float EstimatedSpeed(Contact c)
        {
            // we only know what class it looked like, so assume a typical cruising speed for it
            var cls = c.classIdentified ? c.knownClass : ShipClassType.Cruiser;
            switch (cls)
            {
                case ShipClassType.Destroyer: return 5.4f;
                case ShipClassType.Cruiser: return 4.4f;
                case ShipClassType.Battleship: return 3.2f;
                case ShipClassType.Submarine: return 2.2f;
                default: return 2.4f;
            }
        }

        void BuildZones()
        {
            var map = WorldMap.I;
            if (map == null || map.Zones.Count == 0) { Zones = new ZoneIntel[0]; return; }

            if (Zones.Length != map.Zones.Count) Zones = new ZoneIntel[map.Zones.Count];
            Team foe = Teams.Opponent(team);

            for (int i = 0; i < map.Zones.Count; i++)
            {
                var z = map.Zones[i];
                var intel = new ZoneIntel { zone = z };
                if (z == null) { Zones[i] = intel; continue; }

                intel.isMine = z.Owner == team;
                intel.isEnemy = z.Owner == foe;
                intel.isNeutral = z.Owner == Team.Neutral;
                intel.contested = z.Contested;
                intel.myShips = team == Team.Player ? z.PlayerShips : z.EnemyShips;

                // enemy presence inside the ring is only what we can actually see
                int seen = 0;
                for (int k = 0; k < Contacts.Count; k++)
                    if ((Contacts[k].position - z.Position).sqrMagnitude < z.radius * z.radius) seen++;
                intel.knownEnemyShips = seen;

                intel.value = ZoneValue(intel);
                Zones[i] = intel;
            }

            // a zone we own is "must hold" when giving it up flips the projection
            for (int i = 0; i < Zones.Length; i++)
            {
                if (!Zones[i].isMine) continue;
                float rateWithout = MyPointRate - GameManager.ZonePointsPerSecond;
                float projWithout = MyScore + rateWithout * TimeRemaining;
                Zones[i].mustHold = ProjectedWin && projWithout <= ProjectedTheirs;
            }
        }

        float ZoneValue(ZoneIntel z)
        {
            float v = 0f;
            if (z.isNeutral) v += 45f;                 // cheapest points on the board
            else if (z.isEnemy) v += 28f;
            else v += 12f;                             // ours already, worth defending not taking

            if (z.contested) v += 26f;                 // a contest is decided in the next few seconds
            if (z.isMine && z.knownEnemyShips > 0) v += 30f;   // being taken off us right now

            v -= Vector2.Distance(z.Position, FleetCenter) * 0.022f;

            // do not send the fleet somewhere we already know is stacked against us
            v -= z.knownEnemyShips * 6f;
            return v;
        }

        void BuildPosture()
        {
            if (IsEscortMode)
            {
                Posture = FleetPosture.Press;
                PostureReason = "hunting the convoy";
                return;
            }

            if (IsAnnihilationMode)
            {
                // no points to play for, so it comes down to whether we can win the trade
                if (StrengthRatio >= 1.1f) { Posture = FleetPosture.Press; PostureReason = "stronger fleet, forcing the fight"; }
                else if (StrengthRatio <= 0.75f) { Posture = FleetPosture.Hold; PostureReason = "outgunned, fighting defensively"; }
                else { Posture = FleetPosture.Press; PostureReason = "even fight"; }
                return;
            }

            bool lowClock = TimeRemaining < Mathf.Min(200f, TimeLimit * 0.22f);
            bool anyNeutral = false;
            for (int i = 0; i < Zones.Length; i++) if (Zones[i].isNeutral) anyNeutral = true;
            bool early = TimeRemaining > TimeLimit * 0.72f;

            if (early && anyNeutral)
            {
                Posture = FleetPosture.LandGrab;
                PostureReason = "open points on the board";
            }
            else if (ProjectedWin && lowClock)
            {
                Posture = FleetPosture.CloseOut;
                PostureReason = "ahead on projection, running the clock";
            }
            else if (ProjectedWin)
            {
                Posture = FleetPosture.Hold;
                PostureReason = "ahead on projection, holding what we own";
            }
            else if (lowClock)
            {
                Posture = FleetPosture.Desperate;
                PostureReason = "behind with the clock gone, all in";
            }
            else
            {
                Posture = FleetPosture.Press;
                PostureReason = "behind on projection, pressing";
            }
        }

        // ------------------------------------------------------------------ queries

        /// <summary>
        /// Friendly value versus known enemy value around a point. Above 1 we are locally stronger.
        /// This is the number the tactical layer uses to decide whether to push or back off.
        /// </summary>
        public float LocalStrengthRatio(Vector2 pos, float radius)
        {
            float mine = 0f, theirs = 0f;
            float r2 = radius * radius;

            var friends = ShipRegistry.OfTeam(team);
            for (int i = 0; i < friends.Count; i++)
            {
                var s = friends[i];
                if (s == null || s.IsDead || s.Damage.IsSinking) continue;
                if ((s.Position - pos).sqrMagnitude <= r2) mine += Value(s);
            }

            for (int i = 0; i < Contacts.Count; i++)
                if ((Contacts[i].position - pos).sqrMagnitude <= r2) theirs += Contacts[i].strength;

            if (theirs < 0.01f) return mine > 0.01f ? 4f : 1f;
            return mine / theirs;
        }

        /// <summary>Is a contact of this class believed to be lurking near a point but not visible?</summary>
        public bool DarkThreatNear(Vector2 pos, float radius, ShipClassType cls)
        {
            for (int i = 0; i < Contacts.Count; i++)
            {
                var c = Contacts[i];
                if (c.live || c.shipClass != cls) continue;
                if ((c.position - pos).magnitude <= radius + c.uncertainty) return true;
            }
            return false;
        }

        /// <summary>Highest value zone this team should be working on.</summary>
        public ZoneIntel? BestZone(bool preferOwned)
        {
            ZoneIntel? best = null;
            float bestV = float.MinValue;
            for (int i = 0; i < Zones.Length; i++)
            {
                if (Zones[i].zone == null) continue;
                if (preferOwned && !Zones[i].isMine && !Zones[i].contested) continue;
                if (Zones[i].value > bestV) { bestV = Zones[i].value; best = Zones[i]; }
            }
            return best;
        }

        /// <summary>Nearest known contact to a point, live or inferred.</summary>
        public bool NearestContact(Vector2 pos, float maxRange, out PredictedContact result)
        {
            result = default;
            float best = maxRange;
            bool found = false;
            for (int i = 0; i < Contacts.Count; i++)
            {
                float d = Vector2.Distance(Contacts[i].position, pos);
                if (d < best) { best = d; result = Contacts[i]; found = true; }
            }
            return found;
        }

        // ------------------------------------------------------------------ difficulty

        public float DecisionInterval => difficulty == AIDifficulty.Elite ? 1.4f
                                       : difficulty == AIDifficulty.Veteran ? 2.4f : 3.6f;

        /// <summary>Elite crews use inference and terrain; green crews just fight what they can see.</summary>
        public bool UsesInference => difficulty != AIDifficulty.Recruit;
        public bool UsesCover => difficulty == AIDifficulty.Elite;
        public bool UsesAngling => difficulty != AIDifficulty.Recruit;

        /// <summary>How lopsided a local fight has to look before a ship disengages.</summary>
        public float DisengageRatio => difficulty == AIDifficulty.Elite ? 0.8f
                                     : difficulty == AIDifficulty.Veteran ? 0.6f : 0.35f;

        public string Describe() => Posture.ToString().ToUpper() + " - " + PostureReason;
    }
}
