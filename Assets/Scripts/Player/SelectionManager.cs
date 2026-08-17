using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Naval
{
    /// <summary>Click, drag box, shift modify and control groups.</summary>
    public class SelectionManager : MonoBehaviour
    {
        public static SelectionManager I { get; private set; }

        public readonly List<Ship> Selected = new List<Ship>();
        public Ship Primary => Selected.Count > 0 ? Selected[0] : null;
        public Ship Hovered { get; private set; }

        readonly Dictionary<int, List<Ship>> _groups = new Dictionary<int, List<Ship>>();

        bool _dragging;
        Vector2 _dragStartScreen;
        float _lastClickTime;
        Ship _lastClicked;

        public bool IsBoxing => _dragging && (InputHub.MousePosition - _dragStartScreen).magnitude > 8f;
        public Rect BoxScreenRect
        {
            get
            {
                Vector2 a = _dragStartScreen, b = InputHub.MousePosition;
                return new Rect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
            }
        }

        public static SelectionManager Create(Transform parent)
        {
            var go = new GameObject("SelectionManager");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<SelectionManager>();
            I = s;
            return s;
        }

        void Update()
        {
            PruneDead();
            // box selection and control groups are fleet-command tools only
            if (ControlModeManager.I != null && ControlModeManager.I.IsDirect) { Hovered = null; _dragging = false; return; }
            UpdateHover();
            HandleMouse();
            HandleGroups();
        }

        void PruneDead()
        {
            for (int i = Selected.Count - 1; i >= 0; i--)
                if (Selected[i] == null || Selected[i].IsDead) { if (Selected[i] != null) Selected[i].Selected = false; Selected.RemoveAt(i); }
            foreach (var kv in _groups)
                kv.Value.RemoveAll(s => s == null || s.IsDead);
        }

        void UpdateHover()
        {
            if (UIManager.IsPointerOverUI(InputHub.MousePosition)) { Hovered = null; return; }
            Hovered = ShipAtScreen(InputHub.MousePosition, true);
        }

        void HandleMouse()
        {
            if (InputHub.LeftDown && !UIManager.IsPointerOverUI(InputHub.MousePosition))
            {
                _dragging = true;
                _dragStartScreen = InputHub.MousePosition;
            }

            if (!InputHub.LeftHeld && _dragging)
            {
                _dragging = false;
                float dist = (InputHub.MousePosition - _dragStartScreen).magnitude;
                if (dist > 8f) BoxSelect();
                else ClickSelect();
            }
        }

        void ClickSelect()
        {
            var ship = ShipAtScreen(InputHub.MousePosition, false);

            // a command mode intercepts the click instead of selecting
            if (CommandSystem.I != null && CommandSystem.I.ConsumeClick(RTSCamera.I.ScreenToWorld(InputHub.MousePosition), ship))
                return;

            if (ship == null)
            {
                if (!InputHub.Shift) Clear();
                return;
            }

            // double click selects every ship of that class on screen
            bool doubleClick = ship == _lastClicked && Time.unscaledTime - _lastClickTime < 0.35f;
            _lastClicked = ship;
            _lastClickTime = Time.unscaledTime;

            if (doubleClick && ship.team == Team.Player)
            {
                SelectClassOnScreen(ship.Stats.classType);
                return;
            }

            if (InputHub.Shift) Toggle(ship);
            else SelectOnly(ship);
        }

        void BoxSelect()
        {
            Rect r = BoxScreenRect;
            if (!InputHub.Shift) Clear();

            var ships = ShipRegistry.OfTeam(Team.Player);
            for (int i = 0; i < ships.Count; i++)
            {
                var s = ships[i];
                if (s == null || s.IsDead) continue;
                Vector2 sp = RTSCamera.I.WorldToScreen(s.Position);
                if (r.Contains(sp)) Add(s);
            }
            GameEvents.RaiseSelectionChanged(Primary);
        }

        void HandleGroups()
        {
            int num = InputHub.NumberRowDown();
            if (num < 0) return;
            if (num == 0) return;

            if (InputHub.Ctrl)
            {
                var list = new List<Ship>(Selected);
                _groups[num] = list;
                for (int i = 0; i < list.Count; i++) list[i].ControlGroup = num;
                GameEvents.RaiseMessage("Control group " + num + " set (" + list.Count + " ships)", Team.Player);
            }
            else if (_groups.TryGetValue(num, out var g) && g.Count > 0)
            {
                Clear();
                for (int i = 0; i < g.Count; i++) Add(g[i]);
                GameEvents.RaiseSelectionChanged(Primary);
                if (InputHub.Shift && Primary != null) RTSCamera.I.FocusOn(Primary.Position);
            }
        }

        // ------------------------------------------------------------------ API

        public Ship ShipAtScreen(Vector2 screen, bool includeEnemies)
        {
            Vector2 world = RTSCamera.I.ScreenToWorld(screen);
            float pad = RTSCamera.I.PixelScale * 14f;

            Ship best = null; float bestD = float.MaxValue;
            var all = ShipRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s == null || s.IsDead) continue;
                if (s.team != Team.Player)
                {
                    if (!includeEnemies && !(CommandSystem.I != null && CommandSystem.I.WantsEnemyClick)) continue;
                    if (s.Visual != null && !s.Visual.VisibleToPlayer) continue;
                }
                float d = Vector2.Distance(world, s.Position);
                float reach = Mathf.Max(s.Stats.length * 0.6f, pad);
                if (d < reach && d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        public void SelectOnly(Ship s)
        {
            Clear();
            Add(s);
            GameEvents.RaiseSelectionChanged(Primary);
        }

        public void Add(Ship s)
        {
            if (s == null || s.IsDead || Selected.Contains(s)) return;
            if (s.team != Team.Player) return;
            Selected.Add(s);
            s.Selected = true;
        }

        public void Remove(Ship s)
        {
            if (s == null) return;
            Selected.Remove(s);
            s.Selected = false;
        }

        public void Toggle(Ship s)
        {
            if (s == null) return;
            if (Selected.Contains(s)) Remove(s); else Add(s);
            GameEvents.RaiseSelectionChanged(Primary);
        }

        public void Clear()
        {
            for (int i = 0; i < Selected.Count; i++) if (Selected[i] != null) Selected[i].Selected = false;
            Selected.Clear();
            GameEvents.RaiseSelectionChanged(null);
        }

        public void SelectAll()
        {
            Clear();
            var ships = ShipRegistry.OfTeam(Team.Player);
            for (int i = 0; i < ships.Count; i++) Add(ships[i]);
            GameEvents.RaiseSelectionChanged(Primary);
        }

        public void SelectClassOnScreen(ShipClassType cls)
        {
            Clear();
            Rect view = RTSCamera.I.WorldViewRect();
            var ships = ShipRegistry.OfTeam(Team.Player);
            for (int i = 0; i < ships.Count; i++)
                if (ships[i].Stats.classType == cls && view.Contains(ships[i].Position)) Add(ships[i]);
            if (Selected.Count == 0)
                for (int i = 0; i < ships.Count; i++)
                    if (ships[i].Stats.classType == cls) Add(ships[i]);
            GameEvents.RaiseSelectionChanged(Primary);
        }

        public bool IsSelected(Ship s) => s != null && Selected.Contains(s);

        /// <summary>Pre-binds a control group (the three squadrons get 1, 2 and 3 at deployment).</summary>
        public void AssignGroup(int number, List<Ship> ships)
        {
            if (number < 1 || number > 9 || ships == null) return;
            var list = new List<Ship>(ships);
            _groups[number] = list;
            for (int i = 0; i < list.Count; i++) if (list[i] != null) list[i].ControlGroup = number;
        }

        public List<Ship> GetGroup(int number) => _groups.TryGetValue(number, out var g) ? g : null;
    }
}
