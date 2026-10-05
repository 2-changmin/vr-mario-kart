using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VRKart.Track
{
    // 트랙 주변 지형(언덕) 메시 (#48). 높낮이가 큰 트랙에서 흙 둑 대신 쓴다.
    // Verge Width를 주면 갓길 가장자리에서 지형까지 비탈로 이어서, 벽을 숨긴 트랙이 주변과 한 땅처럼 보인다.
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
        [Tooltip("0보다 크면 갓길 바깥 가장자리에서 지형까지 이어지는 비탈을 이 폭(m)으로 만든다. 벽을 숨긴 트랙(Walls Visible 끔)에서 트랙과 지형 사이 틈·단차를 없앤다")]
        [SerializeField, Min(0f)] private float _vergeWidth;
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
            float shoulder = _builder.ShoulderEdge;
            float blend2 = _blendDistance * _blendDistance;
            Vector2 gridMin = min, gridMax = max;

            // 트랙 높이를 거리 가중 평균으로 섞은 자연 지형 높이 (벽 바깥 기준)
            float Natural(float qx, float qz, out float near, out float nearHeight)
            {
                float weightSum = 0f, heightSum = 0f, nearest2 = float.MaxValue;
                nearHeight = 0f;
                foreach (Vector3 p in points)
                {
                    float dx = p.x - qx, dz = p.z - qz, d2 = dx * dx + dz * dz;
                    float w = 1f / ((d2 + blend2) * (d2 + blend2));
                    weightSum += w;
                    heightSum += w * p.y;
                    if (d2 < nearest2) { nearest2 = d2; nearHeight = p.y; }
                }
                near = Mathf.Sqrt(nearest2);
                float roadBase = nearHeight - _roadClearance;
                float limit = Mathf.Max(0f, near - inner - 0.5f) * _sideSlope;
                float height = Mathf.Clamp(heightSum / weightSum, roadBase - limit, roadBase + limit);
                float border = Mathf.Min(Mathf.Min(qx - gridMin.x, gridMax.x - qx), Mathf.Min(qz - gridMin.y, gridMax.y - qz));
                return Mathf.Lerp(_edgeHeight, height, Mathf.SmoothStep(0f, 1f, border / _edgeFalloff));
            }

            float hidden = _vergeWidth > 0f ? shoulder + _vergeWidth * 0.5f : inner + 0.5f;   // 비탈 바깥 절반은 자연 높이 그대로(같은 머티리얼이라 겹쳐도 안 보임)
            var vertices = new Vector3[nx * nz];
            var uvs = new Vector2[nx * nz];
            for (int z = 0; z < nz; z++)
            {
                for (int x = 0; x < nx; x++)
                {
                    float px = min.x + x * _cellSize, pz = min.y + z * _cellSize;
                    float height = Natural(px, pz, out float nearest, out float nearestHeight);
                    // 도로·갓길(·비탈) 아래: 위에 덮이는 면보다 낮게
                    if (nearest < hidden)
                        height = _vergeWidth > 0f ? Mathf.Min(height, nearestHeight - _roadClearance) - 1f : nearestHeight - _roadClearance;
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

            if (_vergeWidth > 0f) BuildVerge(samples, shoulder, (qx, qz) => Natural(qx, qz, out _, out _));
        }

        // 갓길 바깥 가장자리(도로 높이)에서 자연 지형 높이까지 이어지는 비탈 + 바깥 끝에서 아래로 내린 치마(틈 가림).
        // 다른 구간이 가까이 지나가는 곳(두 갈래 길)은 서로 겹치지 않게 폭을 줄인다.
        private void BuildVerge(IReadOnlyList<TrackLayout.Sample> samples, float shoulder, System.Func<float, float, float> natural)
        {
            int n = samples.Count;
            var width = new float[n];
            for (int i = 0; i < n; i++)
            {
                float other = float.MaxValue;
                for (int j = 0; j < n; j += 2)
                {
                    if (Mathf.Abs(samples[j].Distance - samples[i].Distance) < 60f || Mathf.Abs(samples[j].Distance - samples[i].Distance) > _layout.Length - 60f) continue;
                    Vector3 d = samples[j].Position - samples[i].Position; d.y = 0f;
                    other = Mathf.Min(other, d.magnitude);
                }
                width[i] = Mathf.Clamp((other - 2f * shoulder) * 0.5f - 0.2f, 0.3f, _vergeWidth);
            }

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            foreach (float side in new[] { -1f, 1f })
            {
                int start = vertices.Count;
                for (int i = 0; i < n; i++)
                {
                    TrackLayout.Sample s = samples[i];
                    Vector3 right = s.Right.normalized * side;
                    Vector3 inner = s.Position + right * shoulder + Vector3.down * 0.02f;
                    Vector3 outer = s.Position + right * (shoulder + width[i]);
                    float limit = width[i] * 0.6f;
                    outer.y = Mathf.Clamp(natural(outer.x, outer.z), s.Position.y - limit, s.Position.y + limit);
                    vertices.Add(inner); vertices.Add(outer); vertices.Add(outer + Vector3.down * 2.5f);
                    uvs.Add(new Vector2(inner.x, inner.z) * _uvScale); uvs.Add(new Vector2(outer.x, outer.z) * _uvScale); uvs.Add(new Vector2(outer.x, outer.z) * _uvScale + Vector2.up * 0.3f);
                }
                for (int i = 0; i < n - 1; i++)
                {
                    int a = start + i * 3, b = a + 3;
                    if (side > 0f) { triangles.AddRange(new[] { a, b, b + 1, a, b + 1, a + 1 }); }
                    else { triangles.AddRange(new[] { a, b + 1, b, a, a + 1, b + 1 }); }
                    // 치마는 양면
                    triangles.AddRange(new[] { a + 1, b + 1, b + 2, a + 1, b + 2, a + 2, a + 1, b + 2, b + 1, a + 1, a + 2, b + 2 });
                }
            }

            var mesh = new Mesh { name = "Verge (generated)", hideFlags = HideFlags.DontSave, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Verge (generated)") { hideFlags = HideFlags.DontSave, layer = GrassLayer };
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
