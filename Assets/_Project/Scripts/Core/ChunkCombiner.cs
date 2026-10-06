using System.Collections.Generic;
using UnityEngine;

namespace VRKart.Core
{
    // 자식들의 메시를 실행할 때 Cell Size(기본 100m) 격자 칸 × 머티리얼별로 합친다 (#48 성능).
    // 나무·주차된 차처럼 같은 모델이 많이 흩어진 장식용: 그리기 호출이 (개수 × 머티리얼) → (칸 × 머티리얼)로 줄고,
    // 칸 단위라 화면 밖 칸은 여전히 그리지 않는다. 씬에는 원본이 그대로 저장되므로 씬 파일 크기는 늘지 않는다.
    // 합친 뒤 원본 렌더러는 끈다(콜라이더는 그대로).
    public sealed class ChunkCombiner : MonoBehaviour
    {
        [SerializeField, Min(5f)] private float _cellSize = 100f;
        [SerializeField] private bool _castShadows;

        public int ChunkCount { get; private set; }

        private void Awake() => Combine();

        public void Combine()
        {
            var groups = new Dictionary<(int, int, Material), List<CombineInstance>>();
            var sources = new List<MeshRenderer>();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Mesh mesh = filter.sharedMesh;
                Vector3 c = renderer.bounds.center;
                int cx = Mathf.FloorToInt(c.x / _cellSize), cz = Mathf.FloorToInt(c.z / _cellSize);
                Material[] materials = renderer.sharedMaterials;
                for (int sub = 0; sub < mesh.subMeshCount && sub < materials.Length; sub++)
                {
                    var key = (cx, cz, materials[sub]);
                    if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = transform.worldToLocalMatrix * renderer.localToWorldMatrix });
                }
                sources.Add(renderer);
            }

            foreach (var kv in groups)
            {
                var mesh = new Mesh { name = $"{name}_chunk", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                var go = new GameObject($"Chunk {kv.Key.Item1},{kv.Key.Item2} {kv.Key.Item3.name}");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = kv.Key.Item3;
                renderer.shadowCastingMode = _castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (var renderer in sources) renderer.enabled = false;
            ChunkCount = groups.Count;
        }
    }
}
