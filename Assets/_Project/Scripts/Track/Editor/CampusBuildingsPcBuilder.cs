using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using VRKart.Core;

namespace VRKart.Track.Editor
{
    // PC 시연 화질용 입체 건물 (#67). 메뉴 VRKart > 캠퍼스 > PC용 입체 건물 다시 만들기 (Track_Campus를 연 상태).
    // Campus/Buildings의 벽(평평한 면 + 사진)을 읽어서, 트랙 가까운 벽은 **창문을 벽 안으로 넣은 입체 외벽**으로 다시 만든다:
    //   기단 → 층마다 창대·인방·기둥 → 창 개구부(깊이 0.22m 창틀) → 유리(하늘·주변이 비치는 금속성 재질, 일부는 블라인드).
    // 먼 벽은 평면 그대로 복사. 결과는 Campus/Buildings_PC(꺼진 채 저장)이고, QualityTierObjects가 PC 단계에서만
    // 이것을 켜고 원래 벽·창문 띠를 끈다. Quest(Mobile)는 원래 가벼운 건물 그대로. 지붕·간판·차양·실외기는 원래 것을 같이 쓴다.
    // 건물의 위치·높이·색은 원래 데이터(OSM 윤곽 + 위성·로드뷰를 보고 정한 값) 그대로다.
    public static class CampusBuildingsPcBuilder
    {
        private const string MeshPath = "Assets/_Project/Models/Campus/Campus_BuildingsPC.asset";
        private const string MatDir = "Assets/_Project/Materials/CampusPC";
        private const float NearDistance = 60f, FloorHeight = 3.0f, BayWidth = 2.8f, Recess = 0.22f, Plinth = 0.9f;

        private static readonly string[] WallNames =
        {
            "Campus_FacadeTex_White", "Campus_FacadeTex_Gray", "Campus_FacadeTex_Lavender", "Campus_FacadeTex_Brick", "Campus_FacadeTex_Brown", "Campus_FacadeTex_Glass",
            "Campus_White", "Campus_FacadeDark", "Campus_Building", "Campus_Brick", "Campus_FacadeYellow", "Campus_FacadePink", "Campus_LightGray", "Campus_Lavender",
        };

        private sealed class Part
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector2> U = new List<Vector2>();
            public readonly List<int> T = new List<int>();

            // o = 바깥(보이는 쪽)
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 o, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int s = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d); U.Add(ua); U.Add(ub); U.Add(uc); U.Add(ud);
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), o) >= 0f) T.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
                else T.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
            }
        }

        private static readonly Dictionary<Material, Part> Parts = new Dictionary<Material, Part>();
        private static Part P(Material m) { if (!Parts.TryGetValue(m, out var p)) Parts[m] = p = new Part(); return p; }

        // ---------- 재질 ----------
        private static Texture2D Tex(string set, string kind) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/ThirdParty/AmbientCG/{set}/{set}_{kind}.jpg");

        private static Material Lit(string name, Color color, string set, float smoothness, float metallic = 0f, float bump = 1f)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", metallic);
            if (set != null)
            {
                m.SetTexture("_BaseMap", Tex(set, "Color"));
                var normal = Tex(set, "NormalGL");
                if (normal != null)
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal));
                    if (importer.textureType != TextureImporterType.NormalMap) { importer.textureType = TextureImporterType.NormalMap; importer.SaveAndReimport(); }
                    m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP");
                }
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Tile(Material m, float scale) { m.SetTextureScale("_BaseMap", Vector2.one * scale); return m; }

        // ---------- 메뉴 ----------
        [MenuItem("VRKart/캠퍼스/PC용 입체 건물 다시 만들기 (Track_Campus)")]
        public static void RebuildOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var root = GameObject.Find("Campus/Buildings");
            var layout = Object.FindAnyObjectByType<TrackLayout>();
            if (root == null || layout == null) { EditorUtility.DisplayDialog("PC용 입체 건물", "Track_Campus 씬을 연 뒤 실행하세요.", "확인"); return; }
            Build(root.transform, layout);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static void Build(Transform root, TrackLayout layout)
        {
            Parts.Clear();
            var rng = new System.Random(7);
            // 벽 종류별 PC 재질 (요철 맵 포함). 유리 = 금속성 매끈한 면 → 반사 프로브의 하늘·건물이 비침
            var wallMats = new Dictionary<string, Material>
            {
                { "Campus_FacadeTex_White", Lit("CampusPC_WallWhite", new Color(0.90f, 0.90f, 0.88f), "Plaster002", 0.18f) },
                { "Campus_FacadeTex_Gray", Lit("CampusPC_WallGray", new Color(0.72f, 0.73f, 0.75f), "Concrete031", 0.22f) },
                { "Campus_FacadeTex_Lavender", Lit("CampusPC_WallLavender", new Color(0.82f, 0.80f, 0.90f), "Plaster002", 0.18f) },
                { "Campus_FacadeTex_Brick", Tile(Lit("CampusPC_WallBrick", new Color(0.86f, 0.80f, 0.76f), "Bricks085", 0.12f), 3f) },
                { "Campus_FacadeTex_Brown", Tile(Lit("CampusPC_WallBrown", new Color(0.66f, 0.50f, 0.42f), "Bricks100", 0.15f), 2.5f) },
                { "Campus_FacadeTex_Glass", Lit("CampusPC_Mullion", new Color(0.13f, 0.14f, 0.16f), null, 0.55f, 0.7f) },
                { "Campus_White", Lit("CampusPC_PlasterWhite", new Color(0.95f, 0.95f, 0.93f), "Plaster002", 0.15f) },
                { "Campus_FacadeDark", Lit("CampusPC_PlasterDark", new Color(0.32f, 0.28f, 0.29f), "Plaster002", 0.15f) },
                { "Campus_Building", Lit("CampusPC_PlasterCream", new Color(0.88f, 0.86f, 0.80f), "Plaster002", 0.15f) },
                { "Campus_Brick", Tile(Lit("CampusPC_WallBrick", new Color(0.86f, 0.80f, 0.76f), "Bricks085", 0.12f), 3f) },
                { "Campus_FacadeYellow", Lit("CampusPC_PlasterYellow", new Color(0.90f, 0.85f, 0.64f), "Plaster002", 0.15f) },
                { "Campus_FacadePink", Lit("CampusPC_PlasterPink", new Color(0.87f, 0.64f, 0.68f), "Plaster002", 0.15f) },
                { "Campus_LightGray", Lit("CampusPC_PlasterGray", new Color(0.80f, 0.81f, 0.84f), "Plaster002", 0.15f) },
                { "Campus_Lavender", Lit("CampusPC_WallLavender", new Color(0.82f, 0.80f, 0.90f), "Plaster002", 0.18f) },
            };
            var glass = Lit("CampusPC_Glass", new Color(0.30f, 0.38f, 0.45f), null, 0.95f, 0.55f);
            var blind = Lit("CampusPC_Blind", new Color(0.72f, 0.71f, 0.66f), null, 0.25f);
            var frame = Lit("CampusPC_Frame", new Color(0.17f, 0.18f, 0.20f), null, 0.5f, 0.6f);
            var stone = Lit("CampusPC_Plinth", new Color(0.50f, 0.50f, 0.50f), "Concrete031", 0.25f);

            var samples = layout.Samples;
            var mobileOnly = new List<GameObject>();
            int detailed = 0, plain = 0, windows = 0;
            foreach (Transform child in root)
            {
                var filter = child.GetComponent<MeshFilter>();
                var renderer = child.GetComponent<MeshRenderer>();
                if (filter == null || renderer == null || filter.sharedMesh == null) continue;
                string matName = renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "";
                bool band = matName == "Campus_Window";   // 그려진 창문 띠: 가까운 벽은 진짜 창이 대신하고, 먼 벽은 그대로 복사
                if (!band && !WallNames.Contains(matName)) continue;
                mobileOnly.Add(child.gameObject);
                var original = renderer.sharedMaterial;
                var wall = wallMats.TryGetValue(matName, out var w) ? w : original;
                bool curtainWall = matName == "Campus_FacadeTex_Glass";
                var v = filter.sharedMesh.vertices; var n = filter.sharedMesh.normals; var uv = filter.sharedMesh.uv;
                for (int i = 0; i + 3 < v.Length; i += 4)
                {
                    Vector3 a = child.TransformPoint(v[i]), d = child.TransformPoint(v[i + 1]), dTop = child.TransformPoint(v[i + 2]), aTop = child.TransformPoint(v[i + 3]);
                    Vector3 normal = child.TransformDirection(n[i]);
                    bool isWall = Mathf.Abs(normal.y) < 0.1f && Mathf.Abs(a.y - d.y) < 0.01f && Mathf.Abs(aTop.y - dTop.y) < 0.01f;
                    float length = isWall ? Vector3.Distance(a, d) : 0f, baseY = a.y, topY = aTop.y;
                    Vector3 mid = (a + d) * 0.5f;
                    float bestSqr = float.MaxValue, trackY = 0f;
                    if (isWall)
                        for (int k = 0; k < samples.Count; k += 3)
                        {
                            float dx = samples[k].Position.x - mid.x, dz = samples[k].Position.z - mid.z, sqr = dx * dx + dz * dz;
                            if (sqr < bestSqr) { bestSqr = sqr; trackY = samples[k].Position.y; }
                        }
                    bool far = bestSqr > NearDistance * NearDistance;
                    if (band)
                    {
                        if (!isWall || far) P(original).Quad(a, d, dTop, aTop, isWall ? normal : Vector3.up, uv[i], uv[i + 1], uv[i + 2], uv[i + 3]);
                        continue;
                    }
                    if (far || !isWall)
                    {
                        // 먼 벽·옥탑 윗면: 원래 재질(사진 외벽)과 UV 그대로
                        Vector3 face = Vector3.Cross(d - a, aTop - a);
                        P(original).Quad(a, d, dTop, aTop, isWall ? normal : face * (Vector3.Dot(face, normal) >= 0f ? 1f : -1f), uv[i], uv[i + 1], uv[i + 2], uv[i + 3]);
                        plain++;
                        continue;
                    }
                    if (length < 2.2f || topY - baseY < 4.5f)
                    {
                        // 그대로 복사 (먼 벽, 옥탑 윗면 등)
                        P(wall).Quad(a, d, dTop, aTop, isWall ? normal : Vector3.Cross(d - a, aTop - a) * (Vector3.Dot(Vector3.Cross(d - a, aTop - a), normal) >= 0f ? 1f : -1f),
                            new Vector2(0f, baseY / 2.5f), new Vector2(length / 2.5f, baseY / 2.5f), new Vector2(length / 2.5f, topY / 2.5f), new Vector2(0f, topY / 2.5f));
                        plain++;
                        continue;
                    }
                    windows += Facade(a, d, baseY, topY, normal.normalized, Mathf.Max(baseY, trackY - 5f), wall, stone, frame, glass, blind, curtainWall, rng);
                    detailed++;
                }
            }

            // 메시 저장
            Directory.CreateDirectory(Path.GetDirectoryName(MeshPath));
            AssetDatabase.DeleteAsset(MeshPath);
            var old = root.parent.Find("Buildings_PC");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var pcRoot = new GameObject("Buildings_PC").transform;
            pcRoot.SetParent(root.parent, false);
            pcRoot.SetSiblingIndex(root.GetSiblingIndex() + 1);
            bool first = true; int triangles = 0;
            foreach (var kv in Parts.OrderBy(k => k.Key.name))
            {
                var mesh = new Mesh { name = "PC_" + kv.Key.name, indexFormat = kv.Value.V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(kv.Value.V); mesh.SetUVs(0, kv.Value.U); mesh.SetTriangles(kv.Value.T, 0);
                mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
                triangles += kv.Value.T.Count / 3;
                if (first) { AssetDatabase.CreateAsset(mesh, MeshPath); first = false; } else AssetDatabase.AddObjectToAsset(mesh, MeshPath);
                var go = new GameObject(kv.Key.name);
                go.transform.SetParent(pcRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = kv.Key;
                r.shadowCastingMode = kv.Key == glass || kv.Key == blind ? ShadowCastingMode.Off : ShadowCastingMode.On;
                r.lightProbeUsage = LightProbeUsage.Off;
            }
            AssetDatabase.SaveAssets();
            pcRoot.gameObject.SetActive(false);   // 씬에는 Quest 상태로 저장

            var tierOld = root.parent.Find("QualityTier");
            if (tierOld != null) Object.DestroyImmediate(tierOld.gameObject);
            var tier = new GameObject("QualityTier").AddComponent<QualityTierObjects>();
            tier.gameObject.AddComponent<PcLightingOverride>();   // PC 단계: 늦은 오후 해·안개
            tier.transform.SetParent(root.parent, false);
            var so = new SerializedObject(tier);
            var pcOnly = so.FindProperty("_pcOnly"); pcOnly.arraySize = 1; pcOnly.GetArrayElementAtIndex(0).objectReferenceValue = pcRoot.gameObject;
            var mobile = so.FindProperty("_mobileOnly"); mobile.arraySize = mobileOnly.Count;
            for (int i = 0; i < mobileOnly.Count; i++) mobile.GetArrayElementAtIndex(i).objectReferenceValue = mobileOnly[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[CampusBuildingsPcBuilder] 입체 벽 {detailed}면, 평면 {plain}면, 창문 {windows}개, 삼각형 {triangles:N0}");
        }

        // 벽 한 면(a → d, baseY ~ topY)을 입체 외벽으로. 반환 = 창문 수
        private static int Facade(Vector3 a, Vector3 d, float baseY, float topY, Vector3 n, float groundY, Material wall, Material stone, Material frame, Material glass, Material blind, bool curtainWall, System.Random rng)
        {
            Vector3 dir = (d - a); dir.y = 0f; float length = dir.magnitude; dir /= length;
            Vector3 origin = new Vector3(a.x, 0f, a.z);
            Vector3 At(float x, float y, float depth = 0f) => origin + dir * x + Vector3.up * y - n * depth;
            void Face(Material m, float x0, float x1, float y0, float y1, float depth = 0f)
            {
                if (x1 - x0 < 0.005f || y1 - y0 < 0.005f) return;
                P(m).Quad(At(x0, y0, depth), At(x1, y0, depth), At(x1, y1, depth), At(x0, y1, depth), n,
                    new Vector2(x0 / 2.5f, y0 / 2.5f), new Vector2(x1 / 2.5f, y0 / 2.5f), new Vector2(x1 / 2.5f, y1 / 2.5f), new Vector2(x0 / 2.5f, y1 / 2.5f));
            }

            float margin = curtainWall ? 0.25f : 0.6f, cap = curtainWall ? 0.35f : 0.5f;
            float rowsBottom = groundY + Plinth;
            int floors = Mathf.FloorToInt((topY - cap - rowsBottom) / FloorHeight);
            int bays = Mathf.FloorToInt((length - margin * 2f) / BayWidth);
            if (floors < 1 || bays < 1)
            {
                Face(wall, 0f, length, baseY, topY);
                return 0;
            }
            float floorH = (topY - cap - rowsBottom) / floors, bay = (length - margin * 2f) / bays;
            float sill = floorH * (curtainWall ? 0.10f : 0.30f), winH = floorH * (curtainWall ? 0.82f : 0.52f), pier = bay * (curtainWall ? 0.06f : 0.19f);

            Face(stone, 0f, length, baseY, rowsBottom);                              // 기단
            Face(wall, 0f, length, rowsBottom + floors * floorH, topY);                // 위쪽 띠
            Face(wall, 0f, margin, rowsBottom, rowsBottom + floors * floorH);          // 양 끝
            Face(wall, length - margin, length, rowsBottom, rowsBottom + floors * floorH);
            int count = 0;
            for (int f = 0; f < floors; f++)
            {
                float y0 = rowsBottom + f * floorH, ys = y0 + sill, yt = ys + winH, y1 = y0 + floorH;
                Face(wall, margin, length - margin, y0, ys);                         // 창대 아래
                Face(wall, margin, length - margin, yt, y1);                         // 인방 위
                for (int b = 0; b <= bays; b++)                                      // 창 사이 기둥
                {
                    float px0 = margin + b * bay - (b == 0 ? 0f : pier), px1 = margin + b * bay + (b == bays ? 0f : pier);
                    Face(wall, px0, px1, ys, yt);
                }
                for (int b = 0; b < bays; b++)
                {
                    float x0 = margin + b * bay + pier, x1 = margin + (b + 1) * bay - pier;
                    // 창틀(개구부 옆·위·아래 면) — 벽 두께가 보여 그림자가 생김
                    var fp = P(frame);
                    Vector2 z = Vector2.zero;
                    fp.Quad(At(x0, ys), At(x0, yt), At(x0, yt, Recess), At(x0, ys, Recess), dir, z, z, z, z);
                    fp.Quad(At(x1, ys), At(x1, yt), At(x1, yt, Recess), At(x1, ys, Recess), -dir, z, z, z, z);
                    fp.Quad(At(x0, ys), At(x1, ys), At(x1, ys, Recess), At(x0, ys, Recess), Vector3.up, z, z, z, z);
                    fp.Quad(At(x0, yt), At(x1, yt), At(x1, yt, Recess), At(x0, yt, Recess), Vector3.down, z, z, z, z);
                    // 유리 (일부는 블라인드 내린 창), 가운데 세로 창살
                    bool blinds = !curtainWall && rng.NextDouble() < 0.16;
                    Face(blinds ? blind : glass, x0, x1, ys, yt, Recess);
                    float mid = (x0 + x1) * 0.5f;
                    Face(frame, mid - 0.03f, mid + 0.03f, ys, yt, Recess - 0.03f);
                    count++;
                }
            }
            return count;
        }
    }
}
