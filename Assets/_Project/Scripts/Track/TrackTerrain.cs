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
        [Tooltip("비탈 머티리얼 (비우면 지형과 같음)")]
        [SerializeField] private Material _vergeMaterial;
        [Tooltip("평평하게 깎을 구역: 볼록 사각형 4점씩(y = 그 꼭짓점의 지형 높이). 트랙 밖에 넓은 도로·강가처럼 평평한 곳을 만들 때. 이 안에서는 비탈도 만들지 않는다")]
        [SerializeField] private Vector3[] _flatQuads = System.Array.Empty<Vector3>();
        [Tooltip("있으면 지형이 도로보다 Wall Min Height 넘게 높은 곳의 비탈을 수직 석축으로 (산비탈 캠퍼스 도로)")]
        [SerializeField] private Material _retainingWallMaterial;
        [SerializeField, Min(0.3f)] private float _wallMinHeight = 1.2f;
        [SerializeField, Min(0.5f)] private float _wallMaxHeight = 5f;
        [Tooltip("석축 위 산울타리 (비우면 없음)")]
        [SerializeField] private Material _hedgeMaterial;
        [Tooltip("있으면 지형이 도로보다 1m 넘게 낮은 곳의 갓길 끝에 난간")]
        [SerializeField] private Material _railMaterial;
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
                if (FlatHeight(qx, qz, out float flat)) { near = float.MaxValue; nearHeight = flat; return flat; }
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
                    if (nearest < hidden && nearest < float.MaxValue)
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

        // (x, z)가 평평한 구역 안이면 그 높이 (사각형을 삼각형 둘로 나눠 무게중심 보간)
        public bool FlatHeight(float x, float z, out float height)
        {
            height = 0f;
            for (int i = 0; i + 3 < _flatQuads.Length; i += 4)
            {
                Vector3 a = _flatQuads[i], b = _flatQuads[i + 1], c = _flatQuads[i + 2], d = _flatQuads[i + 3];
                if (x < Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x)) || x > Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x)) ||
                    z < Mathf.Min(Mathf.Min(a.z, b.z), Mathf.Min(c.z, d.z)) || z > Mathf.Max(Mathf.Max(a.z, b.z), Mathf.Max(c.z, d.z))) continue;
                if (InTriangle(x, z, a, b, c, out height) || InTriangle(x, z, a, c, d, out height)) return true;
            }
            return false;
        }

        private static bool InTriangle(float x, float z, Vector3 a, Vector3 b, Vector3 c, out float height)
        {
            height = 0f;
            float det = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(det) < 1e-6f) return false;
            float l1 = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / det;
            float l2 = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / det;
            float l3 = 1f - l1 - l2;
            if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f) return false;
            height = l1 * a.y + l2 * b.y + l3 * c.y;
            return true;
        }

        // 비탈 메시 조립 도우미 (머티리얼마다 하나)
        private sealed class Strip
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector2> U = new List<Vector2>();
            public readonly List<int> T = new List<int>();

            // a-b-c-d 사각형. twoSided면 뒷면도
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float uvScale, bool twoSided = false)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                float w = (b - a).magnitude * uvScale, h = (d - a).magnitude * uvScale;
                U.Add(new Vector2(0, 0)); U.Add(new Vector2(w, 0)); U.Add(new Vector2(w, h)); U.Add(new Vector2(0, h));
                T.AddRange(new[] { i, i + 3, i + 2, i, i + 2, i + 1 });
                if (twoSided) T.AddRange(new[] { i, i + 2, i + 3, i, i + 1, i + 2 });
            }
        }

        // 갓길 바깥 가장자리(도로 높이)에서 자연 지형 높이까지 이어지는 비탈 + 바깥 끝에서 아래로 내린 치마(틈 가림).
        // 다른 구간이 가까이 지나가는 곳(두 갈래 길)은 서로 겹치지 않게 폭을 줄인다.
        // Retaining Wall Material이 있으면 지형이 도로보다 높은 곳은 수직 석축(+ 위 산울타리),
        // Rail Material이 있으면 지형이 도로보다 낮은 곳은 갓길 끝에 난간을 세운다 (산비탈 캠퍼스 도로처럼).
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

            var ground = new Strip();
            var wall = new Strip();
            var hedge = new Strip();
            var rail = new Strip();
            foreach (float side in new[] { -1f, 1f })
            {
                var p0 = new Vector3[n]; var p1 = new Vector3[n]; var p2 = new Vector3[n];
                var cut = new bool[n]; var fill = new bool[n];
                for (int i = 0; i < n; i++)
                {
                    TrackLayout.Sample s = samples[i];
                    Vector3 right = s.Right.normalized * side;
                    float road = s.Position.y, w = width[i];
                    Vector3 outer = s.Position + right * (shoulder + w);
                    float nat = natural(outer.x, outer.z);
                    cut[i] = _retainingWallMaterial != null && w > 1.5f && nat > road + _wallMinHeight;
                    fill[i] = _railMaterial != null && nat < road - 1f;
                    float limit = w * 0.6f;
                    float outerY = cut[i] ? Mathf.Min(nat, road + _wallMaxHeight) : Mathf.Clamp(nat, road - limit, road + limit);
                    p0[i] = s.Position + right * shoulder + Vector3.down * 0.02f;
                    p1[i] = s.Position + right * (shoulder + 0.5f);
                    p1[i].y = cut[i] ? outerY : Mathf.Lerp(road, outerY, 0.5f / Mathf.Max(w, 0.5f));
                    p2[i] = outer; p2[i].y = outerY;
                }
                RemoveShortRuns(cut, 8);
                RemoveShortRuns(fill, 6);
                for (int i = 0; i < n - 1; i++)
                {
                    int j = i + 1;
                    if (_builder.SharedDistance(i, side) > 0f || _builder.SharedDistance(j, side) > 0f) continue;   // 다른 구간과 붙은 공유 차로 쪽
                    if (FlatHeight(p2[i].x, p2[i].z, out _) || FlatHeight(p2[j].x, p2[j].z, out _)) continue;           // 평평한 구역(넓은 도로 등) 쪽
                    // 오른쪽(side +1)은 진행 방향 오른쪽이 바깥 → 감는 방향을 맞춤
                    void Face(Strip st, Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, float uv, bool two = false)
                    {
                        if (side > 0f) st.Quad(a0, a1, b1, b0, uv, two); else st.Quad(a0, b0, b1, a1, uv, two);
                    }
                    bool wallHere = cut[i] || cut[j];
                    Face(wallHere ? wall : ground, p0[i], p1[i], p0[j], p1[j], wallHere ? 0.5f : _uvScale);
                    Face(ground, p1[i], p2[i], p1[j], p2[j], _uvScale);
                    Face(ground, p2[i], p2[i] + Vector3.down * 2.5f, p2[j], p2[j] + Vector3.down * 2.5f, _uvScale, true);   // 치마
                    if (wallHere && _hedgeMaterial != null)
                    {
                        Vector3 r0 = (p2[i] - p1[i]); r0.y = 0f; r0 = r0.normalized; Vector3 r1 = (p2[j] - p1[j]); r1.y = 0f; r1 = r1.normalized;
                        Vector3 h0 = p1[i] + r0 * 0.15f, h1 = p1[j] + r1 * 0.15f, up = Vector3.up * 0.9f;
                        Face(hedge, h0, h0 + up, h1, h1 + up, 0.5f, true);
                        Face(hedge, h0 + up, h0 + up + r0 * 1.1f, h1 + up, h1 + up + r1 * 1.1f, 0.5f, true);
                    }
                    if (fill[i] && fill[j])
                    {
                        Vector3 a = p0[i] + (p1[i] - p0[i]).normalized * 0.25f, b = p0[j] + (p1[j] - p0[j]).normalized * 0.25f;
                        a.y = samples[i].Position.y; b.y = samples[j].Position.y;
                        foreach (float y in new[] { 1.0f, 0.5f })
                            rail.Quad(a + Vector3.up * y, b + Vector3.up * y, b + Vector3.up * (y + 0.07f), a + Vector3.up * (y + 0.07f), 1f, true);
                        if (i % 3 == 0)
                        {
                            Vector3 t = (b - a); t.y = 0f; t = t.normalized * 0.05f;
                            rail.Quad(a - t, a + t, a + t + Vector3.up * 1.07f, a - t + Vector3.up * 1.07f, 1f, true);
                        }
                    }
                }
            }

            AddVergePart("Verge (generated)", ground, _vergeMaterial != null ? _vergeMaterial : _material, collider: true);   // 장식 배치 때 보이는 가장 낮은 면을 찾도록
            if (wall.T.Count > 0) AddVergePart("RetainingWalls (generated)", wall, _retainingWallMaterial, collider: true);
            if (hedge.T.Count > 0) AddVergePart("Hedges (generated)", hedge, _hedgeMaterial, collider: false);
            if (rail.T.Count > 0) AddVergePart("Rails (generated)", rail, _railMaterial, collider: false);
        }

        // 짧게 끊기는 구간(석축·난간이 몇 m만 나왔다 사라지는 곳)은 없앤다
        private static void RemoveShortRuns(bool[] flags, int minRun)
        {
            int i = 0;
            while (i < flags.Length)
            {
                if (!flags[i]) { i++; continue; }
                int j = i;
                while (j < flags.Length && flags[j]) j++;
                if (j - i < minRun) for (int k = i; k < j; k++) flags[k] = false;
                i = j;
            }
        }

        private void AddVergePart(string name, Strip strip, Material material, bool collider)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(strip.V);
            mesh.SetUVs(0, strip.U);
            mesh.SetTriangles(strip.T, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = GrassLayer };
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
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
