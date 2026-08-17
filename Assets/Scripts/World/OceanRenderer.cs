using UnityEngine;

namespace Naval
{
    /// <summary>Single full-map quad that renders sea, depth shading, waves, foam and land.</summary>
    public class OceanRenderer : MonoBehaviour
    {
        public static OceanRenderer I { get; private set; }

        Material _mat;
        MeshRenderer _mr;

        public static OceanRenderer Create(Transform parent, WorldMap map)
        {
            var go = new GameObject("Ocean");
            go.transform.SetParent(parent, false);
            var o = go.AddComponent<OceanRenderer>();
            o.Init(map);
            return o;
        }

        void Init(WorldMap map)
        {
            I = this;
            float half = map.Half + 400f;   // overscan so the edge of the map is never visible

            var mesh = new Mesh { name = "ocean_quad" };
            mesh.vertices = new[]
            {
                new Vector3(-half, -half, 0f), new Vector3(half, -half, 0f),
                new Vector3(half, half, 0f), new Vector3(-half, half, 0f)
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            _mr = gameObject.AddComponent<MeshRenderer>();

            _mat = new Material(Shader.Find("Naval/Ocean"));
            _mat.SetTexture("_HeightTex", map.HeightTexture);
            _mat.SetVector("_WorldMin", new Vector4(-map.Half, -map.Half, 0, 0));
            _mat.SetVector("_WorldSize", new Vector4(map.Size, map.Size, 0, 0));
            _mr.sharedMaterial = _mat;
            _mr.sortingOrder = -1000;
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            transform.position = new Vector3(0f, 0f, 1f);
        }

        void LateUpdate()
        {
            if (_mat == null || RTSCamera.I == null) return;
            // fade fine surface detail as the camera pulls back so the sea does not alias into speckle
            float detail = Mathf.Clamp01(Mathf.InverseLerp(560f, 190f, RTSCamera.I.Zoom));
            _mat.SetFloat("_Detail", detail);
        }

        public void SetWeather(Vector2 waveDir, float choppiness, float foam, Color tint)
        {
            if (_mat == null) return;
            _mat.SetVector("_WaveDir", new Vector4(waveDir.x, waveDir.y, 0, 0));
            _mat.SetFloat("_Choppiness", choppiness);
            _mat.SetFloat("_FoamAmount", foam);
            _mat.SetColor("_Tint", tint);
        }
    }
}
