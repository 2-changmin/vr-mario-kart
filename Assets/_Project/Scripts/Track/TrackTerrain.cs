using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VRKart.Track
{
    // 트랙 주변 지형(언덕) 메시 (#48). 높낮이가 큰 트랙에서 흙 둑 대신 쓴다.
    // 격자 점마다 트랙 높이를 거리 가중 평균으로 섞어 높이를 정하고, 도로·갓길 아래는 도로보다 조금 낮게,
    // 벽 바깥은 도로 높이에서 서서히 벗어나게, 가장자리는 Edge Height로 내려가게 만든다.
    // TrackMeshBuilder처럼 씬에 저장하지 않고(HideFlags.DontSave) 켜질 때마다 다시 만든다.
    [ExecuteAlways, RequireComponent(typeof(TrackLayout), typeof(TrackMeshBuilder))]
    public sealed class TrackTerrain : MonoBehaviour
    {
        private const int GrassLayer = 9;

        [SerializeField, Min(1f)] private float _cellSize = 4f;
        [Tooltip("트랙 바깥으로 지형을 넓히는 거리 (m)")]
        [SerializeField, Min(0f)] private float _margin = 80f;
        [Tooltip("지형 가장자리에서 Edge Height로 내려가는 폭 (m)")]
        [SerializeField, Min(1f)] private float _edgeFalloff = 50f;
        [Tooltip("지형 가장자리 높이(월드 y). 바닥 평면과 맞춘다")]
        [SerializeField] private float _edgeHeight = -7f;
        [Tooltip("도로·갓길 아래로 지형이 내려가는 깊이 (m)")]
        [SerializeField, Min(0f)] private float _roadClearance = 0.4f;
        [Tooltip("벽 바깥으로 1m 갈 때 도로 높이에서 벗어날 수 있는 높이 (m)")]
        [SerializeField, Min(0f)] private float _sideSlope = 0.5f;
        [Tooltip("높이를 섞을 때 가중치가 절반이 되는 거리 (m). 작을수록 가까운 트랙 높이를 따른다")]
        [SerializeField, Min(1f)] private float _blendDistance = 15f;
        [SerializeField] private Material _material;
        [SerializeField, Min(0.01f)] private float _uvScale = 0.125f;

        private readonly List<Object> _generated = new List<Object>();
        private TrackLayout _layout;
        private TrackMeshBuilder _builder;

        private void OnEnable()
        {
            _layout = GetComponent<TrackLayout>();
            _builder = GetComponent<TrackMeshBuilder>();
            _layout.Changed += RequestRebuild;
            Rebuild();
        }

        private void OnDisable()
        {
            if (_layout != null) _layout.Changed -= RequestRebuild;
            Clear();
        }

        private void OnValidate() => RequestRebuild();

        private void RequestRebuild()
        {
#if UNITY_EDITOR
            EditorApplication.delayCall -= DelayedRebuild;
            EditorApplication.delayCall += DelayedRebuild;
#else
            Rebuild();
#endif
        }

#if UNITY_EDITOR
        private void DelayedRebuild()
        {
            if (this != null && isActiveAndEnabled) Rebuild();
        }
#endif

        public void Rebuild()
        {
            Clear();
            IReadOnlyList<TrackLayout.Sample> samples = _layout.Samples;
            if (samples.Count < 2) return;

            // 4m 간격으로 줄인 트랙 점 (x, z, 높이)
            var points = new List<Vector3>();
            for (int i = 0; i < samples.Count - 1; i += Mathf.Max(1, Mathf.RoundToInt(_cellSize)))
                points.Add(samples[i].Position);

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 p in points)
            {
                min = Vector2.Min(min, new Vector2(p.x, p.z));
                max = Vector2.Max(max, new Vector2(p.x, p.z));
            }
            min -= Vector2.one * _margin;
            max += Vector2.one * _margin;
            int nx = Mathf.CeilToInt((max.x - min.x) / _cellSize) + 1;
            int nz = Mathf.CeilToInt((max.y - min.y) / _cellSize) + 1;

            float inner = _builder.HalfWidth;
            float blend2 = _blendDistance * _blendDistance;
            var vertices = new Vector3[nx * nz];
            var uvs = new Vector2[nx * nz];
            for (int z = 0; z < nz; z++)
            {
                for (int x = 0; x < nx; x++)
                {
                    float px = min.x + x * _cellSize, pz = min.y + z * _cellSize;
                    float weightSum = 0f, heightSum = 0f, nearest2 = float.MaxValue, nearestHeight = 0f;
                    foreach (Vector3 p in points)
                    {
                        float dx = p.x - px, dz = p.z - pz, d2 = dx * dx + dz * dz;
                        float w = 1f / ((d2 + blend2) * (d2 + blend2));
                        weightSum += w;
                        heightSum += w * p.y;
                        if (d2 < nearest2) { nearest2 = d2; nearestHeight = p.y; }
                    }
                    float height = heightSum / weightSum;
                    float nearest = Mathf.Sqrt(nearest2);
                    float roadBase = nearestHeight - _roadClearance;
                    float limit = Mathf.Max(0f, nearest - inner - 0.5f) * _sideSlope;
                    height = Mathf.Clamp(height, roadBase - limit, roadBase + limit);

                    float edge = Mathf.Min(Mathf.Min(px - min.x, max.x - px), Mathf.Min(pz - min.y, max.y - pz));
                    height = Mathf.Lerp(_edgeHeight, height, Mathf.SmoothStep(0f, 1f, edge / _edgeFalloff));

                    vertices[z * nx + x] = new Vector3(px, height, pz);
                    uvs[z * nx + x] = new Vector2(px, pz) * _uvScale;
                }
            }

            var triangles = new int[(nx - 1) * (nz - 1) * 6];
            int t = 0;
            for (int z = 0; z < nz - 1; z++)
            {
                for (int x = 0; x < nx - 1; x++)
                {
                    int a = z * nx + x, b = a + 1, c = a + nx, d = c + 1;
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
                }
            }

            var mesh = new Mesh
            {
                name = "Terrain (generated)",
                hideFlags = HideFlags.DontSave,
                indexFormat = vertices.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Terrain (generated)") { hideFlags = HideFlags.DontSave, layer = GrassLayer };
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);   // 샘플이 월드 좌표라서
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;   // 장식 배치용 레이캐스트, 튕겨 나간 아이템 받기
            _generated.Add(go);
            _generated.Add(mesh);
        }

        private void Clear()
        {
            foreach (Object obj in _generated)
            {
                if (obj == null) continue;
                if (Application.isPlaying) Destroy(obj);
                else DestroyImmediate(obj);
            }
            _generated.Clear();
        }
    }
}
