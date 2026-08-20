using UnityEngine;

namespace Naval
{
    /// <summary>
    /// Single entry point. Drop this component on one GameObject in an empty scene and the whole
    /// game - world, physics, systems, camera, UI - is constructed at runtime.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Match")]
        public GameMode startMode = GameMode.Domination;
        public int seed = 0;
        public WeatherType startWeather = WeatherType.Clear;
        [Tooltip("Skip the fleet selection screen and drop straight into deployment.")]
        public bool skipMenu = false;
        [Tooltip("Enemy AI skill. Elite uses inference and terrain cover and reacts fastest.")]
        public AIDifficulty enemyDifficulty = AIDifficulty.Elite;

        [Header("World")]
        public int heightmapResolution = 512;

        Transform _root;

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;

            // top-down naval combat: no gravity, and ships need room to push each other around
            Physics2D.gravity = Vector2.zero;
            Physics2D.velocityIterations = 6;
            Physics2D.positionIterations = 3;

            if (seed == 0) seed = Random.Range(1, 999999);

            var rootGo = new GameObject("~Naval");
            _root = rootGo.transform;

            // ---- presentation services --------------------------------------
            ParticleFX.Create(_root);
            LineDrawer.Create(_root);
            AudioManager.Create(_root);

            // ---- world ------------------------------------------------------
            var map = WorldMap.Create(_root);
            var grid = NavGrid.Create(_root);
            map.Generate(seed, startMode, heightmapResolution);
            grid.Build(map);

            OceanRenderer.Create(_root, map);
            FogOfWarRenderer.Create(_root, map);
            SmokeSystem.Create(_root);
            WeatherSystem.Create(_root, startWeather);

            // ---- simulation --------------------------------------------------
            DetectionSystem.Create(_root);
            ProjectileSystem.Create(_root);

            // ---- player ------------------------------------------------------
            var cam = RTSCamera.Create(_root, map.PlayerDeployCenter);
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            SelectionManager.Create(_root);
            CommandSystem.Create(_root);
            ControlModeManager.Create(_root);
            DirectShipController.Create(_root);

            // ---- presentation overlays ---------------------------------------
            WorldOverlay.Create(_root);
            DebugOverlay.Create(_root);
            UIManager.Create(_root);

            // ---- match --------------------------------------------------------
            var gm = GameManager.Create(_root);
            gm.EnemyDifficulty = enemyDifficulty;
            if (skipMenu) gm.BeginMatch(startMode, seed, false);   // world above was built with this seed
            else gm.EnterMenu(true);
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            GameEvents.ClearAll();
            ShipRegistry.Clear();
            SpriteFactory.Clear();
        }
    }
}
