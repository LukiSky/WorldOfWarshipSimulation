using UnityEngine;
using UnityEngine.InputSystem;

namespace Naval
{
    public enum ControlMode { RTS, Direct }

    /// <summary>
    /// Owns the hybrid control scheme. Tab swaps between commanding the fleet from the tactical map
    /// and personally conning one ship. Taking the helm suspends that ship's autopilot and gunnery
    /// AI; handing it back restores them.
    /// </summary>
    public class ControlModeManager : MonoBehaviour
    {
        public static ControlModeManager I { get; private set; }

        public ControlMode Mode { get; private set; } = ControlMode.RTS;
        public Ship Controlled { get; private set; }
        public bool IsDirect => Mode == ControlMode.Direct && Controlled != null && !Controlled.IsDead;

        float _rtsZoom = 420f;

        public static ControlModeManager Create(Transform parent)
        {
            var go = new GameObject("ControlModeManager");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<ControlModeManager>();
            I = c;
            return c;
        }

        void Update()
        {
            if (GameManager.I != null && GameManager.I.Phase != GamePhase.Battle && GameManager.I.Phase != GamePhase.Deployment)
                return;

            if (InputHub.KeyDown(Key.Tab)) Toggle();

            // if our ship is lost we fall back to fleet command
            if (Mode == ControlMode.Direct && (Controlled == null || Controlled.IsDead))
            {
                GameEvents.RaiseMessage("Ship lost - returning to fleet command", Team.Player);
                EnterRTS();
            }
        }

        public void Toggle()
        {
            if (Mode == ControlMode.RTS) EnterDirect(PickShip());
            else EnterRTS();
        }

        Ship PickShip()
        {
            // whatever is selected, else the ship we last conned, else the flagship
            if (SelectionManager.I != null && SelectionManager.I.Primary != null) return SelectionManager.I.Primary;
            if (Controlled != null && !Controlled.IsDead) return Controlled;

            var ships = ShipRegistry.OfTeam(Team.Player);
            Ship best = null;
            for (int i = 0; i < ships.Count; i++)
            {
                if (ships[i] == null || ships[i].IsDead) continue;
                if (best == null || ships[i].Stats.fleetPointCost > best.Stats.fleetPointCost) best = ships[i];
            }
            return best;
        }

        public void EnterDirect(Ship ship)
        {
            if (ship == null || ship.IsDead)
            {
                GameEvents.RaiseMessage("No ship available to take the helm", Team.Player);
                return;
            }

            if (Controlled != null && Controlled != ship) ReleaseShip(Controlled);

            if (RTSCamera.I != null) _rtsZoom = RTSCamera.I.Zoom;

            Controlled = ship;
            Mode = ControlMode.Direct;
            TakeShip(ship);

            if (SelectionManager.I != null) SelectionManager.I.SelectOnly(ship);
            if (RTSCamera.I != null)
            {
                RTSCamera.I.DirectMode = true;
                RTSCamera.I.Follow(ship);
                RTSCamera.I.SetZoom(Mathf.Min(RTSCamera.I.Zoom, 150f));
            }

            GameEvents.RaiseMessage("Direct control: " + ship.shipName + " (" + ship.ClassTag + ")  -  Tab for fleet command", Team.Player);
            AudioManager.PlayUI(SoundId.OrderConfirm, 0.6f);
        }

        public void EnterRTS()
        {
            if (Controlled != null) ReleaseShip(Controlled);
            Mode = ControlMode.RTS;

            if (RTSCamera.I != null)
            {
                RTSCamera.I.DirectMode = false;
                RTSCamera.I.StopFollowing();
                RTSCamera.I.SetZoom(Mathf.Max(_rtsZoom, 300f));
            }
            GameEvents.RaiseMessage("Fleet command  -  Tab to take the helm", Team.Player);
            AudioManager.PlayUI(SoundId.OrderConfirm, 0.6f);
        }

        /// <summary>Suspend the autopilot: the player is the captain now.</summary>
        void TakeShip(Ship s)
        {
            s.IsDirectlyControlled = true;
            s.Weapons.ManualControl = true;
            s.Weapons.HoldFire = false;
            s.AI.AutoEngage = false;
            s.AI.AutoEvade = false;          // dodging torpedoes is the player's job
            s.AI.AutoDamageControl = false;  // damage control is on the action bar
            s.CurrentTarget = null;
            s.Navigation.OrderStop();
            s.Movement.SetThrottle(0f);
        }

        void ReleaseShip(Ship s)
        {
            if (s == null) return;
            s.IsDirectlyControlled = false;
            s.Weapons.ManualControl = false;
            s.AI.AutoEngage = true;
            s.AI.AutoEvade = true;
            s.AI.AutoDamageControl = true;
            // hold the course it was left on until the player gives it an order
            s.Navigation.OrderStop();
        }
    }
}
