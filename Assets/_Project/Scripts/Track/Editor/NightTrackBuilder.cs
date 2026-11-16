using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace VRKart.Track.Editor
{
    // 야간 트랙 도구 (#67).
    //  - VRKart > 야간 트랙 > 캠퍼스 야간 다시 만들기: Track_Campus를 복사해 Track_CampusNight를 다시 만든다(.meta GUID 유지).
    //    캠퍼스(코스·건물·배치)를 고친 뒤에 누르면 야간 씬이 따라온다. 재질 교체·불빛 웅덩이·밤 설정·안내 표지·조명 굽기까지 한 번에.
    //  - VRKart > 야간 트랙 > 열린 씬에 야간 안내 다시 만들기: 열린 야간 씬(TrackNightLights가 있는 씬)의 길 안내만 다시 만든다.
    //    안내 = 빛나는 차선(Unlit) + 도로 양옆 반사 표지(10m마다, 오른쪽 흰색·왼쪽 호박색) + 급커브 바깥쪽 화살표 표지 + 전조등 불빛 판 재질 연결.
    //    밤에는 달 그림자를 끈다(거의 안 보이는데 그림자 패스 비용만 듦).
    // 광안대교(Track_Bridge)의 바다·다리·건물은 처음 만들 때의 일회성 스크립트 결과라 이 도구로 다시 만들지 않는다(씬에서 직접 수정).
    public static class NightTrackBuilder
    {
        private const string CampusScene = "Assets/_Project/Scenes/Track_Campus.unity";
        private const string CampusNightScene = "Assets/_Project/Scenes/Track_CampusNight.unity";
        private const string MatDir = "Assets/_Project/Materials/Night";
        private const string TexDir = "Assets/_Project/Textures/Night";
        private const string MeshDir = "Assets/_Project/Models/Night";

        // ---------- 메뉴 ----------

        [MenuItem("VRKart/야간 트랙/캠퍼스 야간 다시 만들기 (Track_Campus 복사)")]
        public static void RebuildCampusNight()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);   // 덮어쓸 씬이 열려 있지 않게
            File.Copy(CampusScene, CampusNightScene, true);                               // .meta는 그대로 → GUID 유지
            AssetDatabase.ImportAsset(CampusNightScene, ImportAssetOptions.ForceUpdate);
            var scene = EditorSceneManager.OpenScene(CampusNightScene);
            ConvertCampusToNight();
            ApplyNightSettings(160f, 1000f, Quaternion.Euler(38f, 140f, 0f));
            BuildGuides();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            TrackLightingBaker.BakeOpenScene();
            Debug.Log("[NightTrackBuilder] Track_CampusNight 다시 만들기 완료");
        }

        [MenuItem("VRKart/야간 트랙/열린 씬에 야간 안내 다시 만들기")]
        public static void RebuildGuidesOpenScene()
        {
            if (Object.FindAnyObjectByType<TrackNightLights>() == null)
            {
                EditorUtility.DisplayDialog("야간 안내", "열린 씬에 TrackNightLights가 없습니다(야간 트랙이 아님).", "확인");
                return;
            }
            var scene = EditorSceneManager.GetActiveScene();
            BuildGuides();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ---------- 재질 ----------

        private static Material Unlit(string name, Color color, Texture texture = null, Vector2? tiling = null, bool doubleSided = false)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            if (texture != null) { material.SetTexture("_BaseMap", texture); material.SetTextureScale("_BaseMap", tiling ?? Vector2.one); }
            if (doubleSided) material.SetFloat("_Cull", 0f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D Texture(string file, TextureWrapMode wrap, bool alpha = false)
        {
            string path = $"{TexDir}/{file}";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && (importer.wrapMode != wrap || importer.alphaIsTransparency != alpha))
            {
                importer.wrapMode = wrap; importer.alphaIsTransparency = alpha; importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // 가산 합성 재질: VFX_Spark(파티클 Unlit 가산)를 복사해 텍스처·색만 바꾼다
        private static Material Additive(string name, Color color, Texture texture)
        {
            string path = $"{MatDir}/{name}.mat";
            var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/VFX/VFX_Spark.mat");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(source); AssetDatabase.CreateAsset(material, path); }
            else material.CopyPropertiesFromMaterial(source);
            material.SetColor("_BaseColor", color);
            if (texture != null) material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- 밤 설정 (하늘·달·환경광·안개) ----------

        private static void ApplyNightSettings(float fogStart, float fogEnd, Quaternion moonRotation)
        {
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Night_Sky.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.05f, 0.055f, 0.10f);
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            var moon = RenderSettings.sun;
            if (moon != null)
            {
                moon.name = "Moon";
                moon.color = new Color(0.62f, 0.70f, 1f);
                moon.intensity = 0.25f;
                moon.transform.rotation = moonRotation;
            }
            var night = GameObject.Find("NightLights") ?? new GameObject("NightLights");
            if (night.GetComponent<TrackNightLights>() == null) night.AddComponent<TrackNightLights>();
        }

        // ---------- 길 안내 ----------

        private sealed class MeshData
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int>[] Triangles = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };   // 흰 반사, 호박 반사, 화살표, 기둥

            public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int s = Vertices.Count;
                Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
                Uvs.Add(ua); Uvs.Add(ub); Uvs.Add(uc); Uvs.Add(ud);
                Triangles[sub].AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3, s, s + 2, s + 1, s, s + 3, s + 2 });   // 양면
            }

            public void Box(int sub, Vector3 center, Vector3 size, Quaternion rotation)
            {
                Vector3 h = size * 0.5f;
                Vector3 P(float x, float y, float z) => center + rotation * new Vector3(x * h.x, y * h.y, z * h.z);
                var z0 = Vector2.zero;
                Quad(sub, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), z0, z0, z0, z0);
                Quad(sub, P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), z0, z0, z0, z0);
                Quad(sub, P(1, -1, -1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), z0, z0, z0, z0);
                Quad(sub, P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), z0, z0, z0, z0);
                Quad(sub, P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), z0, z0, z0, z0);
            }
        }

        // 열린 야간 씬의 길 안내를 다시 만든다
        public static void BuildGuides()
        {
            var layout = Object.FindAnyObjectByType<TrackLayout>();
            var builder = Object.FindAnyObjectByType<TrackMeshBuilder>();
            var night = Object.FindAnyObjectByType<TrackNightLights>();
            string sceneName = EditorSceneManager.GetActiveScene().name;

            // 재질
            var lineWhite = Unlit("Night_LineWhite", new Color(0.80f, 0.81f, 0.84f));
            var lineYellow = Unlit("Night_LineYellow", new Color(0.72f, 0.56f, 0.10f));
            var reflectorWhite = Unlit("Night_ReflectorWhite", new Color(0.95f, 0.97f, 1f));
            var reflectorAmber = Unlit("Night_ReflectorAmber", new Color(1f, 0.62f, 0.10f));
            var chevron = Unlit("Night_Chevron", Color.white, Texture("Night_Chevron.png", TextureWrapMode.Clamp), null, true);
            var pole = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Night_Pole.mat");
            var beam = Additive("Night_HeadlightBeam", new Color(1f, 0.93f, 0.78f, 0.42f), Texture("Night_Beam.png", TextureWrapMode.Clamp, true));

            // 빛나는 차선: 가장자리 = 흰색, 가운데 = 노랑 (가운데 선이 없던 트랙은 새로 생김)
            var so = new SerializedObject(builder);
            so.FindProperty("_lineMaterial").objectReferenceValue = lineWhite;
            so.FindProperty("_centerLineMaterial").objectReferenceValue = lineYellow;
            float roadHalf = so.FindProperty("_roadWidth").floatValue * 0.5f;
            float shoulder = so.FindProperty("_shoulderWidth").floatValue;
            bool walls = so.FindProperty("_wallsVisible").boolValue;
            float wallHeight = so.FindProperty("_wallHeight").floatValue, wallThickness = so.FindProperty("_wallThickness").floatValue;
            so.ApplyModifiedPropertiesWithoutUndo();
            builder.Rebuild();

            // 전조등 불빛 판 재질, 달 그림자 끔, 환경광(길 윤곽이 보일 만큼)
            var nso = new SerializedObject(night);
            nso.FindProperty("_beamMaterial").objectReferenceValue = beam;
            nso.ApplyModifiedPropertiesWithoutUndo();
            if (RenderSettings.sun != null) RenderSettings.sun.shadows = LightShadows.None;
            RenderSettings.ambientSkyColor = new Color(0.12f, 0.14f, 0.26f);
            RenderSettings.ambientEquatorColor = new Color(0.10f, 0.10f, 0.16f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.04f, 0.06f);

            var samples = layout.Samples;
            float length = layout.Length;
            // 같은 길을 두 번 지나는 구간(공유 차로)에서 반대 차로 위에 놓이지 않게
            bool OnOtherPass(Vector3 p, float distance)
            {
                float r = roadHalf + 0.6f;
                for (int i = 0; i < samples.Count; i += 2)
                {
                    float along = Mathf.Abs(samples[i].Distance - distance);
                    if (Mathf.Min(along, length - along) < 45f) continue;
                    float dx = samples[i].Position.x - p.x, dz = samples[i].Position.z - p.z;
                    if (dx * dx + dz * dz < r * r && Mathf.Abs(samples[i].Position.y - p.y) < 4f) return true;
                }
                return false;
            }

            var data = new MeshData();
            // 반사 표지: 벽이 보이면 벽 윗면, 아니면 갓길 바깥쪽 낮은 말뚝
            float reflectorLat = walls ? roadHalf + shoulder + wallThickness * 0.5f : roadHalf + shoulder - 0.15f;
            float reflectorY = walls ? wallHeight + 0.09f : 0.5f;
            int reflectors = 0, chevrons = 0;
            for (float d = 5f; d < length; d += 10f)
            {
                var s = layout.Evaluate(d);
                var rotation = Quaternion.LookRotation(s.Forward);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 p = s.Position + s.Right * (side * reflectorLat);
                    if (OnOtherPass(p, d)) continue;
                    int sub = side > 0f ? 0 : 1;
                    data.Box(sub, p + Vector3.up * reflectorY, new Vector3(0.24f, 0.22f, 0.24f), rotation);
                    if (!walls) data.Box(3, p + Vector3.up * (reflectorY * 0.5f - 0.04f), new Vector3(0.07f, reflectorY - 0.08f, 0.07f), rotation);
                    reflectors++;
                }
            }
            // 급커브(반지름 75m 미만) 바깥쪽 화살표 표지, 14m마다
            float chevronLat = roadHalf + shoulder + (walls ? wallThickness + 0.9f : 0.9f);
            for (float d = 0f; d < length; d += 14f)
            {
                var s = layout.Evaluate(d);
                if (Mathf.Abs(s.Curvature) < 1f / 75f) continue;
                float outer = s.Curvature > 0f ? -1f : 1f;          // 오른쪽 커브면 바깥 = 왼쪽
                Vector3 foot = s.Position + s.Right * (outer * chevronLat);
                if (OnOtherPass(foot, d)) continue;
                Vector3 center = foot + Vector3.up * 1.55f, right = s.Right * 0.95f, up = Vector3.up * 0.48f;
                bool pointRight = s.Curvature > 0f;
                Vector2 u0 = new Vector2(pointRight ? 0f : 1f, 0f), u1 = new Vector2(pointRight ? 1f : 0f, 0f);
                data.Quad(2, center - right - up, center + right - up, center + right + up, center - right + up,
                    u0, u1, new Vector2(u1.x, 1f), new Vector2(u0.x, 1f));
                data.Box(3, foot + Vector3.up * 0.55f, new Vector3(0.09f, 1.1f, 0.09f), Quaternion.LookRotation(s.Forward));
                chevrons++;
            }

            var mesh = new Mesh { name = $"{sceneName}_Guides", indexFormat = data.Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(data.Vertices);
            mesh.SetUVs(0, data.Uvs);
            mesh.subMeshCount = 4;
            for (int i = 0; i < 4; i++) mesh.SetTriangles(data.Triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveMesh(mesh, $"{MeshDir}/{sceneName}_Guides.asset");

            var environment = GameObject.Find("Environment");
            var old = environment.transform.Find("NightGuides");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("NightGuides");
            go.transform.SetParent(environment.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshDir}/{sceneName}_Guides.asset");
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { reflectorWhite, reflectorAmber, chevron, pole };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            Debug.Log($"[NightTrackBuilder] {sceneName}: 반사 표지 {reflectors}개, 화살표 {chevrons}개");
        }

        private static void SaveMesh(Mesh mesh, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { EditorUtility.CopySerialized(mesh, existing); EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
        }

        // ---------- 캠퍼스 → 야간 (재질 교체, 가로등 불빛 웅덩이) ----------

        private static void ConvertCampusToNight()
        {
            foreach (var b in Object.FindObjectsByType<TrackMeshBuilder>(FindObjectsSortMode.None)) b.Rebuild();
            var layout = Object.FindAnyObjectByType<TrackLayout>();
            var windows = Texture("Night_Windows.png", TextureWrapMode.Repeat);
            // 사진 외벽 → 창문 불빛 (원래 텍스처 한 장 = floors층 → 창문 텍스처 16층 × 8칸에 맞춰 타일링)
            Material Facade(string tag, float floors, Color tint) =>
                Unlit($"CampusNight_Facade{tag}", tint, windows, new Vector2(floors * 2.4f / 20.8f, floors / 16f));
            var map = new Dictionary<string, Material>
            {
                { "Campus_FacadeTex_White", Facade("White", 8f, Color.white) },
                { "Campus_FacadeTex_Gray", Facade("Gray", 6f, new Color(0.92f, 0.92f, 0.95f)) },
                { "Campus_FacadeTex_Lavender", Facade("Lavender", 6f, new Color(0.92f, 0.9f, 1f)) },
                { "Campus_FacadeTex_Brick", Facade("Brick", 6f, new Color(1f, 0.92f, 0.85f)) },
                { "Campus_FacadeTex_Brown", Facade("Brown", 6f, new Color(1f, 0.9f, 0.8f)) },
                { "Campus_FacadeTex_Glass", Facade("Glass", 10f, new Color(0.9f, 0.95f, 1f)) },
                { "Campus_Window", Unlit("CampusNight_WindowBand", new Color(0.46f, 0.37f, 0.24f)) },
                { "Campus_Lamp", Unlit("CampusNight_Lamp", new Color(1f, 0.84f, 0.58f)) },
                { "Campus_Sign", Unlit("CampusNight_Sign", new Color(0.10f, 0.20f, 0.46f)) },
                { "Campus_SignBlue", Unlit("CampusNight_SignBlue", new Color(0.18f, 0.40f, 0.90f)) },
                { "Campus_SignGreen", Unlit("CampusNight_SignGreen", new Color(0.14f, 0.62f, 0.34f)) },
                { "Campus_SignRed", Unlit("CampusNight_SignRed", new Color(0.85f, 0.16f, 0.16f)) },
                { "Campus_SignYellow", Unlit("CampusNight_SignYellow", new Color(0.95f, 0.78f, 0.12f)) },
                { "Campus_LightGreen", Unlit("CampusNight_LightGreen", new Color(0.15f, 1f, 0.45f)) },
                { "Campus_LightRed", Unlit("CampusNight_LightRed", new Color(1f, 0.12f, 0.1f)) },
            };
            var lampRenderers = new List<MeshRenderer>();
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    if (materials[i].name == "Campus_Lamp") lampRenderers.Add(renderer);
                    if (map.TryGetValue(materials[i].name, out var replacement)) { materials[i] = replacement; changed = true; }
                }
                if (changed) renderer.sharedMaterials = materials;
            }

            // 가로등 머리(가로등 메시 정점을 3m 칸으로 묶음) 아래, 트랙 16m 안이면 노면 불빛 웅덩이
            var heads = new List<Vector3>();
            foreach (var renderer in lampRenderers)
            {
                var lampMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                if (lampMesh == null) continue;
                var cells = new Dictionary<Vector3Int, List<Vector3>>();
                foreach (var v in lampMesh.vertices)
                {
                    Vector3 w = renderer.transform.TransformPoint(v);
                    var key = new Vector3Int(Mathf.FloorToInt(w.x / 3f), Mathf.FloorToInt(w.y / 3f), Mathf.FloorToInt(w.z / 3f));
                    if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<Vector3>();
                    list.Add(w);
                }
                foreach (var list in cells.Values) heads.Add(list.Aggregate(Vector3.zero, (a, b) => a + b) / list.Count);
            }
            var merged = new List<Vector3>();
            foreach (var h in heads.OrderBy(h => h.x)) if (!merged.Any(m => (m - h).sqrMagnitude < 16f)) merged.Add(h);

            var samples = layout.Samples;
            var vertices = new List<Vector3>(); var uvs = new List<Vector2>(); var triangles = new List<int>();
            foreach (var h in merged)
            {
                TrackLayout.Sample best = samples[0];
                float bestSqr = float.MaxValue;
                foreach (var s in samples)
                {
                    float sqr = (s.Position.x - h.x) * (s.Position.x - h.x) + (s.Position.z - h.z) * (s.Position.z - h.z);
                    if (sqr < bestSqr) { bestSqr = sqr; best = s; }
                }
                if (bestSqr > 16f * 16f || h.y - best.Position.y > 14f || h.y < best.Position.y) continue;
                float lateral = Mathf.Clamp(Vector3.Dot(h - best.Position, best.Right), -6f, 6f);
                Vector3 c = best.Position + best.Right * lateral + Vector3.up * 0.05f, f = best.Forward * 8f, r = best.Right * 8f;
                int s0 = vertices.Count;
                vertices.AddRange(new[] { c - r - f, c - r + f, c + r + f, c + r - f });
                uvs.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });
                triangles.AddRange(new[] { s0, s0 + 1, s0 + 2, s0, s0 + 2, s0 + 3 });
            }
            var environment = GameObject.Find("Environment");
            var oldPools = environment.transform.Find("NightPools");
            if (oldPools != null) Object.DestroyImmediate(oldPools.gameObject);
            var mesh = new Mesh { name = "Track_CampusNight_LightPools" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string meshPath = $"{MeshDir}/Track_CampusNight_LightPools.asset";
            SaveMesh(mesh, meshPath);
            var go = new GameObject("NightPools");
            go.transform.SetParent(environment.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            var poolRenderer = go.AddComponent<MeshRenderer>();
            poolRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Night_LightPool.mat");
            poolRenderer.shadowCastingMode = ShadowCastingMode.Off;
            poolRenderer.receiveShadows = false;
            poolRenderer.lightProbeUsage = LightProbeUsage.Off;
            Debug.Log($"[NightTrackBuilder] 캠퍼스 야간: 가로등 {merged.Count}곳, 불빛 웅덩이 {triangles.Count / 6}개");
        }
    }
}
