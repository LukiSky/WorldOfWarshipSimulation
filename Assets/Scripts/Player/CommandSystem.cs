using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Naval
{
    public enum PendingCommand { None, AttackMove, Patrol, Follow, FocusTarget }

    /// <summary>Translates player input into fleet orders.</summary>
    public class CommandSystem : MonoBehaviour
    {
        public static CommandSystem I { get; private set; }

        public PendingCommand Pending { get; private set; }
        public FormationType CurrentFormation { get; private set; } = FormationType.None;
        public bool WantsEnemyClick => Pending == PendingCommand.FocusTarget;

        public static CommandSystem Create(Transform parent)
        {
            var go = new GameObject("CommandSystem");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<CommandSystem>();
            I = c;
            return c;
        }

        List<Ship> Sel => SelectionManager.I != null ? SelectionManager.I.Selected : null;

        /// <summary>Arms a click-target command (used by the command panel buttons).</summary>
        public void SetPending(PendingCommand c)
        {
            Pending = c;
            switch (c)
            {
                case PendingCommand.AttackMove: Toast("Attack move: pick a destination"); break;
                case PendingCommand.Patrol: Toast("Patrol: pick a patrol point"); break;
                case PendingCommand.Follow: Toast("Escort: pick a ship to follow"); break;
                case PendingCommand.FocusTarget: Toast("Focus fire: pick an enemy"); break;
            }
        }

        void Update()
        {
            if (GameManager.I != null && GameManager.I.Phase != GamePhase.Battle && GameManager.I.Phase != GamePhase.Deployment) return;
            // while the player is conning a ship, the keyboard and mouse belong to that ship
            if (ControlModeManager.I != null && ControlModeManager.I.IsDirect) return;
            HandleHotkeys();
            HandleRightClick();
        }

        // ------------------------------------------------------------------ input

        void HandleHotkeys()
        {
            if (InputHub.KeyDown(Key.Escape)) Pending = PendingCommand.None;

            if (InputHub.KeyDown(Key.C)) { Pending = PendingCommand.AttackMove; Toast("Attack move: pick a destination"); }
            if (InputHub.KeyDown(Key.V)) { Pending = PendingCommand.Patrol; Toast("Patrol: pick a patrol point"); }
            if (InputHub.KeyDown(Key.B)) { Pending = PendingCommand.Follow; Toast("Follow: pick a ship to escort"); }
            if (InputHub.KeyDown(Key.T)) { Pending = PendingCommand.FocusTarget; Toast("Focus fire: pick an enemy"); }

            if (InputHub.KeyDown(Key.Space)) Stop();
            if (InputHub.KeyDown(Key.H)) Hold();
            if (InputHub.KeyDown(Key.R)) Reverse();
            if (InputHub.KeyDown(Key.G)) Retreat();
            if (InputHub.KeyDown(Key.Q)) Smoke();
            if (InputHub.KeyDown(Key.Z)) Dive();
            if (InputHub.KeyDown(Key.X)) Surface();
            if (InputHub.KeyDown(Key.E)) DamageControl();
            if (InputHub.KeyDown(Key.Y)) ReturnToPort();

            // Tab belongs to the direct/fleet control toggle, so select-all is Ctrl+A
            if (InputHub.Ctrl && InputHub.KeyDown(Key.A)) SelectionManager.I.SelectAll();
            if (InputHub.KeyDown(Key.F) && SelectionManager.I.Primary != null)
            {
                if (RTSCamera.I.IsFollowing) RTSCamera.I.StopFollowing();
                else RTSCamera.I.Follow(SelectionManager.I.Primary);
            }
            if (InputHub.KeyDown(Key.Home) || InputHub.KeyDown(Key.Backquote)) RTSCamera.I.FleetOverview();

            if (InputHub.KeyDown(Key.F5)) SetFormation(FormationType.LineAhead);
            if (InputHub.KeyDown(Key.F6)) SetFormation(FormationType.LineAbreast);
            if (InputHub.KeyDown(Key.F7)) SetFormation(FormationType.Wedge);
            if (InputHub.KeyDown(Key.F8)) SetFormation(FormationType.Circle);
            if (InputHub.KeyDown(Key.F9)) SetFormation(FormationType.DefensiveScreen);
            if (InputHub.KeyDown(Key.F10)) { CurrentFormation = FormationType.None; FormationManager.Break(Sel); Toast("Formation broken"); }
        }

        void HandleRightClick()
        {
            if (!InputHub.RightDown) return;
            if (UIManager.IsPointerOverUI(InputHub.MousePosition)) return;
            if (Sel == null || Sel.Count == 0) return;

            Pending = PendingCommand.None;
            Vector2 world = RTSCamera.I.ScreenToWorld(InputHub.MousePosition);
            var ship = SelectionManager.I.ShipAtScreen(InputHub.MousePosition, true);

            if (ship != null && ship.team != Team.Player)
            {
                AttackTarget(ship);
                return;
            }

            // right clicking a friendly port sends the ships home to repair
            var map = WorldMap.I;
            if (map != null)
                for (int i = 0; i < map.Ports.Count; i++)
                {
                    var p = map.Ports[i];
                    if (p != null && p.team == Team.Player && Vector2.Distance(p.Position, world) < p.serviceRadius)
                    {
                        ReturnToPort();
                        return;
                    }
                }

            Move(world, InputHub.Shift);
        }

        /// <summary>Called by the selection manager: returns true when a pending command ate the click.</summary>
        public bool ConsumeClick(Vector2 world, Ship clicked)
        {
            switch (Pending)
            {
                case PendingCommand.AttackMove:
                    Pending = PendingCommand.None;
                    Move(world, InputHub.Shift, OrderType.AttackMove);
                    return true;

                case PendingCommand.Patrol:
                    Pending = PendingCommand.None;
                    Patrol(world);
                    return true;

                case PendingCommand.Follow:
                    if (clicked == null) return true;
                    Pending = PendingCommand.None;
                    Follow(clicked);
                    return true;

                case PendingCommand.FocusTarget:
                    if (clicked == null || clicked.team == Team.Player) return true;
                    Pending = PendingCommand.None;
                    AttackTarget(clicked);
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ orders

        public void Move(Vector2 point, bool queue, OrderType type = OrderType.Move)
        {
            if (Sel == null || Sel.Count == 0) return;

            // spread the group so they do not all sail for the same pixel
            FormationType shape = CurrentFormation == FormationType.None ? FormationType.Wedge : CurrentFormation;
            var offsets = FormationManager.Offsets(shape, Sel.Count, Mathf.Max(45f, 34f + Sel.Count * 4f));
            Vector2 axis = Sel.Count > 0 ? (point - AverageSelectionPos()).normalized : Vector2.up;
            if (axis.sqrMagnitude < 0.01f) axis = Vector2.up;
            float rot = NavalMath.VectorToHeading(axis);

            for (int i = 0; i < Sel.Count; i++)
            {
                Vector2 offset = NavalMath.Rotate(offsets[i], -rot);
                Vector2 dest = point + offset;
                if (NavGrid.I != null) dest = NavGrid.I.NearestNavigable(dest, Sel[i].Stats.draft);
                Sel[i].Navigation.OrderMove(dest, queue, type);
                if (!queue) Sel[i].AI.ManualTarget = type == OrderType.AttackMove ? Sel[i].AI.ManualTarget : null;
            }

            AudioManager.PlayUI(SoundId.OrderConfirm);
            OrderMarkers.Add(point, type == OrderType.AttackMove ? new Color(1f, 0.6f, 0.3f) : new Color(0.4f, 1f, 0.6f));
        }

        public void AttackTarget(Ship target)
        {
            if (Sel == null || target == null) return;
            for (int i = 0; i < Sel.Count; i++)
            {
                Sel[i].AI.ManualTarget = target;
                Sel[i].Navigation.OrderAttack(target);
                Sel[i].Weapons.HoldFire = false;
            }
            AudioManager.PlayUI(SoundId.OrderConfirm);
            OrderMarkers.Add(target.Position, new Color(1f, 0.35f, 0.3f));
            Toast("Engaging " + target.shipName);
        }

        public void Patrol(Vector2 point)
        {
            if (Sel == null) return;
            for (int i = 0; i < Sel.Count; i++)
            {
                var pts = new List<Vector2> { Sel[i].Position, point };
                Sel[i].Navigation.OrderPatrol(pts);
            }
            AudioManager.PlayUI(SoundId.OrderConfirm);
            OrderMarkers.Add(point, new Color(0.5f, 0.8f, 1f));
            Toast("Patrolling");
        }

        public void Follow(Ship leader)
        {
            if (Sel == null || leader == null) return;
            int k = 0;
            for (int i = 0; i < Sel.Count; i++)
            {
                if (Sel[i] == leader) continue;
                float spacing = Mathf.Max(45f, leader.Stats.length * 3f);
                Vector2 offset = new Vector2((k % 2 == 0 ? 1f : -1f) * spacing * (1 + k / 2), -spacing * 0.8f * (1 + k / 2));
                Sel[i].Navigation.OrderFollow(leader, offset);
                k++;
            }
            Toast("Escorting " + leader.shipName);
        }

        public void Stop()
        {
            if (Sel == null) return;
            for (int i = 0; i < Sel.Count; i++) Sel[i].Navigation.OrderStop();
            Toast("All stop");
        }

        public void Hold()
        {
            if (Sel == null) return;
            for (int i = 0; i < Sel.Count; i++) Sel[i].Navigation.OrderHold();
            Toast("Holding position");
        }

        public void Reverse()
        {
            if (Sel == null) return;
            for (int i = 0; i < Sel.Count; i++) Sel[i].Navigation.OrderReverse();
            Toast("Engines astern");
        }

        public void Retreat()
        {
            if (Sel == null) return;
            var map = WorldMap.I;
            for (int i = 0; i < Sel.Count; i++)
            {
                var port = map != null ? map.NearestPort(Sel[i].Position, Team.Player) : null;
                Vector2 goal = port != null ? port.Position : map != null ? map.PlayerDeployCenter : Vector2.zero;
                Sel[i].Navigation.OrderRetreat(goal);
                if (Sel[i].Abilities != null) Sel[i].Abilities.Use(AbilityId.SmokeScreen);
            }
            Toast("Withdrawing");
        }

        public void ReturnToPort()
        {
            if (Sel == null) return;
            var map = WorldMap.I;
            for (int i = 0; i < Sel.Count; i++)
            {
                var port = map != null ? map.NearestPort(Sel[i].Position, Team.Player) : null;
                if (port != null) Sel[i].Navigation.OrderReturnToPort(port.Position);
            }
            Toast("Returning to port");
        }

        public void Smoke()
        {
            if (Sel == null) return;
            int n = 0;
            for (int i = 0; i < Sel.Count; i++) if (Sel[i].Abilities != null && Sel[i].Abilities.Use(AbilityId.SmokeScreen)) n++;
            if (n == 0) Toast("No smoke generators available");
        }

        public void Dive()
        {
            if (Sel == null) return;
            int n = 0;
            for (int i = 0; i < Sel.Count; i++) if (Sel[i].Submarine != null) { Sel[i].Submarine.Dive(); n++; }
            if (n == 0) Toast("No submarines selected");
        }

        public void Surface()
        {
            if (Sel == null) return;
            for (int i = 0; i < Sel.Count; i++) if (Sel[i].Submarine != null) Sel[i].Submarine.Surface();
        }

        public void DamageControl()
        {
            if (Sel == null) return;
            int n = 0;
            for (int i = 0; i < Sel.Count; i++) if (Sel[i].Damage.UseDamageControl()) n++;
            if (n == 0) Toast("Damage control party not ready");
        }

        public void SetFormation(FormationType t)
        {
            CurrentFormation = t;
            FormationManager.Apply(Sel, t);
        }

        public void ToggleHoldFire()
        {
            if (Sel == null) return;
            bool anyFiring = false;
            for (int i = 0; i < Sel.Count; i++) if (!Sel[i].Weapons.HoldFire) anyFiring = true;
            for (int i = 0; i < Sel.Count; i++) Sel[i].Weapons.HoldFire = anyFiring;
            Toast(anyFiring ? "Weapons tight" : "Weapons free");
        }

        Vector2 AverageSelectionPos()
        {
            if (Sel == null || Sel.Count == 0) return Vector2.zero;
            Vector2 p = Vector2.zero;
            for (int i = 0; i < Sel.Count; i++) p += Sel[i].Position;
            return p / Sel.Count;
        }

        void Toast(string msg) => GameEvents.RaiseMessage(msg, Team.Player);
    }

    /// <summary>Short lived click feedback markers drawn by the world overlay.</summary>
    public static class OrderMarkers
    {
        public struct Marker { public Vector2 pos; public float time; public Color color; }
        public static readonly List<Marker> All = new List<Marker>();

        public static void Add(Vector2 pos, Color c)
        {
            All.Add(new Marker { pos = pos, time = Time.unscaledTime, color = c });
            if (All.Count > 24) All.RemoveAt(0);
        }

        public static void Prune()
        {
            for (int i = All.Count - 1; i >= 0; i--)
                if (Time.unscaledTime - All[i].time > 1.2f) All.RemoveAt(i);
        }
    }
}
