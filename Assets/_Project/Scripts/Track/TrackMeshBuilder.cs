using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VRKart.Track
{
    // TrackLayout 중심선을 따라 도로·연석·갓길·벽 메시와 콜라이더를 만든다.
    // 만든 오브젝트는 씬에 저장하지 않고(HideFlags.DontSave) 켜질 때마다 다시 만든다 → 레이아웃을 고치면 바로 반영된다.
    [ExecuteAlways, RequireComponent(typeof(TrackLayout))]
    public sealed class TrackMeshBuilder : MonoBehaviour
    {
        private const int RoadLayer = 8;
        private const int GrassLayer = 9;
        private const int WallLayer = 10;
        private const float WallBottom = -0.3f;   // 벽 아래를 땅속까지 내려서 바깥에서 볼 때 틈이 안 보이게

        [Header("폭 / 높이 (m)")]
        [SerializeField, Min(2f)] private float _roadWidth = 10f;
        [SerializeField, Min(0f)] private float _lineWidth = 0.25f;     // 도로 가장자리 흰 선
        [SerializeField, Min(0f)] private float _curbWidth = 1f;        // 커브 구간 가장자리 빨강/흰 연석
        [SerializeField, Min(0f)] private float _shoulderWidth = 3f;    // 갓길 (Grass 레이어 → 감속)
        [SerializeField, Min(0.1f)] private float _wallThickness = 0.5f;
        [SerializeField, Min(0.1f)] private float _wallHeight = 1f;
        [SerializeField, Min(0.5f)] private float _stripeLength = 2f;   // 연석·벽 줄무늬 한 칸 길이

        [Header("머티리얼")]
        [SerializeField] private Material _roadMaterial;
        [SerializeField] private Material _lineMaterial;
        [SerializeField] private Material _curbMaterial;
        [SerializeField] private Material _shoulderMaterial;
        [SerializeField] private Material _wallMaterial;

        private readonly List<UnityEngine.Object> _generated = new List<UnityEngine.Object>();
        private TrackLayout _layout;

        // 도로 중심에서 벽 바깥면까지 (체크포인트 폭, 장식 배치 거리의 기준)
        public float HalfWidth => _roadWidth * 0.5f + _shoulderWidth + _wallThickness;

        private void OnEnable()
        {
            _layout = GetComponent<TrackLayout>();
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
            // OnValidate 안에서는 오브젝트를 만들 수 없어서 한 프레임 미룬다
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

            float half = _roadWidth * 0.5f;
            float shoulderEnd = half + _shoulderWidth;
            float wallEnd = shoulderEnd + _wallThickness;

            var road = new MeshData();
            road.Strip(samples, -half, half, 0f, _roadWidth, _ => true);
            Create("Road", RoadLayer, road, _roadMaterial, collider: true, shadows: false);

            var lines = new MeshData();
            lines.Strip(samples, -half, -half + _lineWidth, 0.01f, _stripeLength * 2f, _ => true);
            lines.Strip(samples, half - _lineWidth, half, 0.01f, _stripeLength * 2f, _ => true);
            Create("Lines", RoadLayer, lines, _lineMaterial, collider: false, shadows: false);

            var curbs = new MeshData();
            Func<int, bool> curved = i => samples[i].Curvature != 0f || samples[i + 1].Curvature != 0f;
            curbs.Strip(samples, -half, -half + _curbWidth, 0.02f, _stripeLength * 2f, curved);
            curbs.Strip(samples, half - _curbWidth, half, 0.02f, _stripeLength * 2f, curved);
            Create("Curbs", RoadLayer, curbs, _curbMaterial, collider: false, shadows: false);

            var shoulders = new MeshData();
            shoulders.Strip(samples, -shoulderEnd, -half, 0f, _roadWidth, _ => true);
            shoulders.Strip(samples, half, shoulderEnd, 0f, _roadWidth, _ => true);
            Create("Shoulders", GrassLayer, shoulders, _shoulderMaterial, collider: true, shadows: false);

            var walls = new MeshData();
            walls.Wall(samples, -wallEnd, -shoulderEnd, WallBottom, _wallHeight, _stripeLength * 2f);
            walls.Wall(samples, shoulderEnd, wallEnd, WallBottom, _wallHeight, _stripeLength * 2f);
            Create("Walls", WallLayer, walls, _wallMaterial, collider: true, shadows: true);
        }

        private void Create(string name, int layer, MeshData data, Material material, bool collider, bool shadows)
        {
            var mesh = new Mesh { name = $"{name} (generated)", hideFlags = HideFlags.DontSave };
            data.Apply(mesh);
            var go = new GameObject($"{name} (generated)") { hideFlags = HideFlags.DontSave, layer = layer };
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);   // 샘플이 월드 좌표라서
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            _generated.Add(go);
            _generated.Add(mesh);
        }

        private void Clear()
        {
            foreach (UnityEngine.Object obj in _generated)
            {
                if (obj == null) continue;
                if (Application.isPlaying) Destroy(obj);
                else DestroyImmediate(obj);
            }
            _generated.Clear();
        }

        // 버텍스를 모아서 메시로 만드는 도우미. 구간마다 사각형 하나씩(버텍스 4개) 만든다.
        private sealed class MeshData
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int> _triangles = new List<int>();

            // 중심선 기준 오른쪽 offset from~to 사이의 수평 띠. include(i)가 true인 구간(i → i+1)만.
            public void Strip(IReadOnlyList<TrackLayout.Sample> samples, float from, float to, float y, float uvLength, Func<int, bool> include)
            {
                for (int i = 0; i < samples.Count - 1; i++)
                {
                    if (!include(i)) continue;
                    TrackLayout.Sample a = samples[i];
                    TrackLayout.Sample b = samples[i + 1];
                    Vector3 up = Vector3.up * y;
                    Quad(a.Position + a.Right * from + up, a.Position + a.Right * to + up,
                         b.Position + b.Right * from + up, b.Position + b.Right * to + up,
                         Vector3.up, Vector3.up, a.Distance / uvLength, b.Distance / uvLength);
                }
            }

            // from~to 사이 두께, bottom~top 높이의 벽: from 쪽 면, 윗면, to 쪽 면
            public void Wall(IReadOnlyList<TrackLayout.Sample> samples, float from, float to, float bottom, float top, float uvLength)
            {
                for (int i = 0; i < samples.Count - 1; i++)
                {
                    TrackLayout.Sample a = samples[i];
                    TrackLayout.Sample b = samples[i + 1];
                    Vector3 h = Vector3.up * (top - bottom);
                    Vector3 down = Vector3.up * bottom;
                    Vector3 a0 = a.Position + a.Right * from + down, a1 = a.Position + a.Right * to + down;
                    Vector3 b0 = b.Position + b.Right * from + down, b1 = b.Position + b.Right * to + down;
                    float va = a.Distance / uvLength, vb = b.Distance / uvLength;
                    // from 쪽 면 (바깥을 -Right로 봄), 윗면, to 쪽 면 (+Right로 봄)
                    Quad(a0, a0 + h, b0, b0 + h, -a.Right, -b.Right, va, vb);
                    Quad(a0 + h, a1 + h, b0 + h, b1 + h, Vector3.up, Vector3.up, va, vb);
                    Quad(a1 + h, a1, b1 + h, b1, a.Right, b.Right, va, vb);
                }
            }

            // a0-a1이 구간 시작 쪽 가로선, b0-b1이 끝 쪽. 위(법선 쪽)에서 볼 때 앞면이 되도록 감는다.
            private void Quad(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, Vector3 normalA, Vector3 normalB, float va, float vb)
            {
                int start = _vertices.Count;
                _vertices.Add(a0); _vertices.Add(a1); _vertices.Add(b0); _vertices.Add(b1);
                _normals.Add(normalA); _normals.Add(normalA); _normals.Add(normalB); _normals.Add(normalB);
                _uvs.Add(new Vector2(0f, va)); _uvs.Add(new Vector2(1f, va)); _uvs.Add(new Vector2(0f, vb)); _uvs.Add(new Vector2(1f, vb));
                _triangles.Add(start); _triangles.Add(start + 2); _triangles.Add(start + 1);
                _triangles.Add(start + 1); _triangles.Add(start + 2); _triangles.Add(start + 3);
            }

            public void Apply(Mesh mesh)
            {
                if (_vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetUVs(0, _uvs);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
            }
        }
    }
}
