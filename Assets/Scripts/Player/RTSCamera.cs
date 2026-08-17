using UnityEngine;
using UnityEngine.InputSystem;

namespace Naval
{
    /// <summary>Top down tactical camera: middle mouse drag, WASD, edge scroll, smooth wheel zoom.</summary>
    public class RTSCamera : MonoBehaviour
    {
        public static RTSCamera I { get; private set; }

        public Camera Cam { get; private set; }

        public float minZoom = 45f;
        // high enough to frame the whole 4000-unit map, which a fleet spread across three
        // squadron spawns needs for a real strategic view
        public float maxZoom = 1300f;
        public float zoomSpeed = 0.18f;
        public float panSpeed = 1.7f;
        public float edgeSize = 12f;
        public bool edgeScroll = true;
        /// <summary>While the player is conning a ship the camera is locked to it and WASD belongs to the helm.</summary>
        public bool DirectMode { get; set; }

        float _targetZoom = 260f;
        Vector2 _targetPos;
        Vector2 _dragOrigin;
        bool _dragging;
        Ship _followTarget;

        public bool IsFollowing => _followTarget != null && !_followTarget.IsDead;
        public float Zoom => Cam != null ? Cam.orthographicSize : _targetZoom;
        /// <summary>World units per screen pixel - used to keep overlay elements a constant screen size.</summary>
        public float PixelScale => Cam != null ? Cam.orthographicSize * 2f / Screen.height : 0.1f;

        public static RTSCamera Create(Transform parent, Vector2 startPos)
        {
            // adopt the scene's main camera when there is one so URP's per camera data is preserved
            Camera cam = Camera.main;
            GameObject go;
            if (cam != null)
            {
                go = cam.gameObject;
            }
            else
            {
                go = new GameObject("MainCamera") { tag = "MainCamera" };
                go.transform.SetParent(parent, false);
                cam = go.AddComponent<Camera>();
            }

            var c = go.AddComponent<RTSCamera>();
            cam.orthographic = true;
            cam.orthographicSize = 260f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.05f, 0.09f);
            cam.transform.position = new Vector3(startPos.x, startPos.y, -20f);
            c.Cam = cam;
            c._targetPos = startPos;
            c._targetZoom = 260f;
            I = c;
            return c;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            HandleZoom(dt);
            HandlePan(dt);
            ApplyMotion(dt);
        }

        void HandleZoom(float dt)
        {
            float scroll = InputHub.Scroll;
            if (Mathf.Abs(scroll) > 0.01f && !UIManager.IsPointerOverUI(InputHub.MousePosition))
            {
                Vector2 before = ScreenToWorld(InputHub.MousePosition);
                _targetZoom = Mathf.Clamp(_targetZoom * (1f - Mathf.Sign(scroll) * zoomSpeed), minZoom, maxZoom);
                // keep the point under the cursor anchored
                Cam.orthographicSize = Mathf.Lerp(Cam.orthographicSize, _targetZoom, 0.6f);
                Vector2 after = ScreenToWorld(InputHub.MousePosition);
                _targetPos += before - after;
                _followTarget = null;
            }
        }

        void HandlePan(float dt)
        {
            // in direct control the camera stays on the ship and WASD drives the helm
            if (DirectMode) { _dragging = false; return; }

            // middle mouse drag
            if (InputHub.MiddleDown)
            {
                _dragging = true;
                _dragOrigin = ScreenToWorld(InputHub.MousePosition);
                _followTarget = null;
            }
            if (!InputHub.MiddleHeld) _dragging = false;

            if (_dragging)
            {
                Vector2 now = ScreenToWorld(InputHub.MousePosition);
                _targetPos += _dragOrigin - now;
            }

            // keyboard
            Vector2 move = InputHub.MoveAxis();
            if (move.sqrMagnitude > 0.01f) _followTarget = null;

            // screen edges - ignore the uninitialised (0,0) cursor so an unfocused window
            // does not quietly scroll the view into the corner of the map
            if (edgeScroll && Application.isFocused)
            {
                Vector2 m = InputHub.MousePosition;
                if (m.x > 1f && m.x < Screen.width - 1f && m.y > 1f && m.y < Screen.height - 1f)
                {
                    if (m.x < edgeSize) move.x -= 1f;
                    else if (m.x > Screen.width - edgeSize) move.x += 1f;
                    if (m.y < edgeSize) move.y -= 1f;
                    else if (m.y > Screen.height - edgeSize) move.y += 1f;
                }
            }

            if (move.sqrMagnitude > 0.01f)
                _targetPos += move.normalized * panSpeed * Cam.orthographicSize * dt;
        }

        void ApplyMotion(float dt)
        {
            if (IsFollowing) _targetPos = _followTarget.Position;

            float half = GameConfig.WorldSize * 0.5f + 200f;
            _targetPos.x = Mathf.Clamp(_targetPos.x, -half, half);
            _targetPos.y = Mathf.Clamp(_targetPos.y, -half, half);

            Cam.orthographicSize = Mathf.Lerp(Cam.orthographicSize, _targetZoom, 1f - Mathf.Exp(-12f * dt));
            Vector3 p = Cam.transform.position;
            float follow = DirectMode ? 22f : 14f;      // tighter tracking when conning a ship
            Vector2 lerped = Vector2.Lerp(new Vector2(p.x, p.y), _targetPos, 1f - Mathf.Exp(-follow * dt));
            Cam.transform.position = new Vector3(lerped.x, lerped.y, -20f);
        }

        // ------------------------------------------------------------------ API

        public Vector2 ScreenToWorld(Vector2 screen)
        {
            if (Cam == null) return Vector2.zero;
            Vector3 w = Cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 20f));
            return new Vector2(w.x, w.y);
        }

        public Vector2 WorldToScreen(Vector2 world)
        {
            if (Cam == null) return Vector2.zero;
            Vector3 s = Cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
            return new Vector2(s.x, s.y);
        }

        public void SetZoom(float zoom) => _targetZoom = Mathf.Clamp(zoom, minZoom, maxZoom);

        public void FocusOn(Vector2 pos, float? zoom = null)
        {
            _targetPos = pos;
            _followTarget = null;
            if (zoom.HasValue) _targetZoom = Mathf.Clamp(zoom.Value, minZoom, maxZoom);
        }

        public void Follow(Ship s)
        {
            _followTarget = s;
            if (s != null) _targetZoom = Mathf.Min(_targetZoom, 180f);
        }

        public void StopFollowing() => _followTarget = null;

        /// <summary>Frames every friendly ship - the 'back to the fleet' view.</summary>
        public void FleetOverview()
        {
            var ships = ShipRegistry.OfTeam(Team.Player);
            if (ships.Count == 0) { FocusOn(Vector2.zero, 700f); return; }
            Vector2 min = ships[0].Position, max = ships[0].Position;
            for (int i = 1; i < ships.Count; i++)
            {
                min = Vector2.Min(min, ships[i].Position);
                max = Vector2.Max(max, ships[i].Position);
            }
            Vector2 center = (min + max) * 0.5f;
            float size = Mathf.Max((max.y - min.y) * 0.6f, (max.x - min.x) * 0.6f / Mathf.Max(0.2f, Cam.aspect)) + 160f;
            FocusOn(center, size);
            _followTarget = null;
        }

        public Rect WorldViewRect()
        {
            float h = Cam.orthographicSize, w = h * Cam.aspect;
            Vector3 p = Cam.transform.position;
            return new Rect(p.x - w, p.y - h, w * 2f, h * 2f);
        }
    }
}
