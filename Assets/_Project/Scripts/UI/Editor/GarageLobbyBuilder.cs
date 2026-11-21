using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using VRKart.Race;
using VRKart.Track;

namespace VRKart.UI.Editor
{
    // 차고 로비 (#66 A안). 메뉴 VRKart > 로비 > 차고 로비 다시 만들기.
    //  1) 트랙 4개를 차례로 열어 썸네일(Textures/UI/TrackThumb_<씬>.png)을 찍고 길이·랩 수를 읽는다.
    //  2) MainMenu 씬에 차고(Lobby/Garage)를 만든다: 반사되는 콘크리트 바닥, 세로 패널 벽, 천장 조명 띠, 턴테이블 위 조명 사각틀,
    //     벽 로고, 타이어 더미·공구함, 트랙 포스터, 턴테이블(돌아가는 판 + Kart_Player의 차 외형). 재질별로 메시를 합쳐 그리기 호출을 줄인다.
    //     반사 프로브 1개(박스 투영)를 굽는다(차·턴테이블은 빼고). 조명은 해 1개(천장·벽은 그림자 안 만듦) + 환경광 + Unlit 조명 띠.
    //  3) UI_MainMenu 프리팹: 타이틀 패널("트리거를 눌러 시작"), 트랙 카드(썸네일·이름·길이·랩·최고 기록, < >), 버튼 두 줄 배치.
    //  4) 빌드 첫 씬 = MainMenu (#65), 새 글자를 폰트에 추가.
    // 플레이어는 원점에서 +Z를 본다. 메뉴판은 왼쪽 앞, 턴테이블은 오른쪽 앞.
    public static class GarageLobbyBuilder
    {
        private const string MenuScene = "Assets/_Project/Scenes/MainMenu.unity";
        private const string MenuPrefab = "Assets/_Project/Prefabs/UI/UI_MainMenu.prefab";
        private const string KartPrefab = "Assets/_Project/Prefabs/Kart/Kart_Player.prefab";
        private const string ShowcasePrefab = "Assets/_Project/Prefabs/Kart/Kart_AI_Sedan_Red.prefab";   // 바퀴·브레이크등까지 있는 완성 외형
        private const string FontPath = "Assets/_Project/Fonts/Pretendard-SemiBold SDF.asset";
        private const string MatDir = "Assets/_Project/Materials/Lobby";
        private const string MeshPath = "Assets/_Project/Models/Lobby/Lobby_Garage.asset";
        private const string ThumbDir = "Assets/_Project/Textures/UI";

        private static readonly (string Scene, string Label)[] Tracks =
        {
            (SceneLoader.TrackMain, "서킷"), (SceneLoader.TrackCampus, "동아대 캠퍼스"),
            (SceneLoader.TrackBridge, "광안대교 야간"), (SceneLoader.TrackCampusNight, "캠퍼스 야간"),
        };

        // 방 크기와 턴테이블 위치
        private const float RoomX = 7f, RoomBack = -3f, RoomFront = 11f, RoomH = 5f;
        private static readonly Vector3 TurntableCenter = new Vector3(1.0f, 0f, 4.4f);
        private const float DiscRadius = 2.8f, DiscTop = 0.08f;

        private static readonly Color Cyan = new Color(0.30f, 0.90f, 1.00f);

        [MenuItem("VRKart/로비/차고 로비 다시 만들기 (썸네일 포함)")]
        public static void RebuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Rebuild();
        }

        // 로비 전체를 다시 만들지 않고 전시 차만 교체(씬 diff 최소화)
        [MenuItem("VRKart/로비/전시 차만 다시 놓기")]
        public static void RebuildShowcaseCarMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RebuildShowcaseCar();
        }

        public static void RebuildShowcaseCar()
        {
            var scene = EditorSceneManager.OpenScene(MenuScene);
            var table = GameObject.Find("Lobby/Turntable");
            if (table == null) { Debug.LogError("[GarageLobbyBuilder] Lobby/Turntable이 없습니다. 차고 로비부터 만드세요."); return; }
            var old = table.transform.Find("ShowcaseCar");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            table.transform.rotation = Quaternion.identity;
            PlaceShowcaseCar(table.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[GarageLobbyBuilder] 전시 차 다시 놓기 완료");
        }

        public static void Rebuild()
        {
            var cards = CaptureThumbnails();
            EnsureGlyphs("트랙 선택 트리거를 눌러 시작 기록 없음 최고 랩 km · 동아대 캠퍼스 레이싱 차고 VR KART RACING GARAGE <> 0123456789.:");
            var scene = EditorSceneManager.OpenScene(MenuScene);
            BuildGarage(cards);
            RevertMenuSizeOverride();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            BuildMenuPrefab(cards);
            SetFirstScene();
            EnsureFadeMaterial();
            AssetDatabase.SaveAssets();
            Debug.Log("[GarageLobbyBuilder] 차고 로비 완료");
        }

        // ---------------------------------------------------------------- 1) 썸네일
        private static MainMenu.TrackCard[] CaptureThumbnails()
        {
            Directory.CreateDirectory(ThumbDir);
            var cards = new List<MainMenu.TrackCard>();
            foreach (var (sceneName, _) in Tracks)
            {
                EditorSceneManager.OpenScene($"Assets/_Project/Scenes/{sceneName}.unity");
                foreach (var b in Object.FindObjectsByType<TrackMeshBuilder>(FindObjectsSortMode.None)) b.Rebuild();
                foreach (var t in Object.FindObjectsByType<TrackTerrain>(FindObjectsSortMode.None)) t.Rebuild();
                var layout = Object.FindAnyObjectByType<TrackLayout>();
                var raceTrack = Object.FindAnyObjectByType<RaceTrack>();
                var s = layout.Evaluate(layout.Length * 0.985f);   // 출발선 조금 뒤에서 출발 그리드 쪽으로
                var camera = new GameObject("ThumbCamera").AddComponent<Camera>();
                camera.fieldOfView = 58f; camera.nearClipPlane = 0.1f; camera.farClipPlane = 2000f;
                camera.transform.position = s.Position - s.Forward * 6f + Vector3.up * 3.4f + s.Right * 1.5f;
                camera.transform.LookAt(s.Position + s.Forward * 26f + Vector3.up * 0.8f);
                string path = $"{ThumbDir}/TrackThumb_{sceneName}.png";
                Render(camera, 768, 432, path);
                Object.DestroyImmediate(camera.gameObject);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
                cards.Add(new MainMenu.TrackCard
                {
                    Scene = sceneName,
                    Thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(path),
                    LengthMeters = layout.Length,
                    Laps = raceTrack != null ? raceTrack.TotalLaps : 3,
                });
            }
            return cards.ToArray();
        }

        private static void Render(Camera camera, int w, int h, string path)
        {
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        // ---------------------------------------------------------------- 2) 차고
        private sealed class Part
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> U = new List<Vector2>();
            public readonly List<int> T = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, float uvScale)
            {
                // a-b-c-d 반시계(바깥에서 볼 때), UV는 월드 크기 기준
                int s = V.Count;
                Vector3 ax = (b - a).normalized, ay = (d - a).normalized;
                foreach (var p in new[] { a, b, c, d })
                {
                    V.Add(p); N.Add(n);
                    U.Add(new Vector2(Vector3.Dot(p - a, ax), Vector3.Dot(p - a, ay)) / uvScale);
                }
                // 앞면 = n 쪽에서 볼 때 시계 방향(유니티) ⇔ Cross(b - a, c - a)가 n과 같은 쪽
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) >= 0f) T.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
                else T.AddRange(new[] { s, s + 3, s + 2, s, s + 2, s + 1 });
            }

            // 축 정렬 상자. faces: 그릴 면만(바깥 기준) +x -x +y -y +z -z
            public void Box(Vector3 min, Vector3 max, float uvScale = 1f, string faces = "XxYyZz")
            {
                Vector3 p000 = new Vector3(min.x, min.y, min.z), p100 = new Vector3(max.x, min.y, min.z), p010 = new Vector3(min.x, max.y, min.z), p110 = new Vector3(max.x, max.y, min.z);
                Vector3 p001 = new Vector3(min.x, min.y, max.z), p101 = new Vector3(max.x, min.y, max.z), p011 = new Vector3(min.x, max.y, max.z), p111 = new Vector3(max.x, max.y, max.z);
                if (faces.Contains('X')) Quad(p100, p101, p111, p110, Vector3.right, uvScale);
                if (faces.Contains('x')) Quad(p001, p000, p010, p011, Vector3.left, uvScale);
                if (faces.Contains('Y')) Quad(p010, p110, p111, p011, Vector3.up, uvScale);
                if (faces.Contains('y')) Quad(p000, p001, p101, p100, Vector3.down, uvScale);
                if (faces.Contains('Z')) Quad(p101, p001, p011, p111, Vector3.forward, uvScale);
                if (faces.Contains('z')) Quad(p000, p100, p110, p010, Vector3.back, uvScale);
            }

            // 세로 원기둥(옆면 + 윗면 + 선택: 아랫면)
            public void Cylinder(Vector3 baseCenter, float radius, float height, int segments = 48, bool bottom = false)
            {
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    int s = V.Count;
                    V.Add(baseCenter + d0 * radius); V.Add(baseCenter + d1 * radius);
                    V.Add(baseCenter + d1 * radius + Vector3.up * height); V.Add(baseCenter + d0 * radius + Vector3.up * height);
                    N.Add(d0); N.Add(d1); N.Add(d1); N.Add(d0);
                    float u0 = a0 * radius, u1 = a1 * radius;
                    U.Add(new Vector2(u0, 0f)); U.Add(new Vector2(u1, 0f)); U.Add(new Vector2(u1, height)); U.Add(new Vector2(u0, height));
                    T.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
                    Fan(baseCenter + Vector3.up * height, d0, d1, radius, Vector3.up);
                    if (bottom) Fan(baseCenter, d0, d1, radius, Vector3.down);
                }
            }

            private void Fan(Vector3 c, Vector3 d0, Vector3 d1, float r, Vector3 n)
            {
                int s = V.Count;
                V.Add(c); V.Add(c + d0 * r); V.Add(c + d1 * r);
                N.Add(n); N.Add(n); N.Add(n);
                U.Add(new Vector2(c.x, c.z)); U.Add(new Vector2((c + d0 * r).x, (c + d0 * r).z)); U.Add(new Vector2((c + d1 * r).x, (c + d1 * r).z));
                if (n.y > 0f) T.AddRange(new[] { s, s + 2, s + 1 }); else T.AddRange(new[] { s, s + 1, s + 2 });
            }

            // 바닥에 누운 고리(위를 봄)
            public void Ring(Vector3 center, float inner, float outer, int segments = 96)
            {
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    int s = V.Count;
                    V.Add(center + d0 * inner); V.Add(center + d0 * outer); V.Add(center + d1 * outer); V.Add(center + d1 * inner);
                    for (int k = 0; k < 4; k++) { N.Add(Vector3.up); U.Add(Vector2.zero); }
                    T.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
                }
            }

            // 눕힌 타이어(가운데가 빈 원통, 축 = Y)
            public void Tire(Vector3 baseCenter, float outer, float inner, float height, int segments = 32)
            {
                Cylinder(baseCenter, outer, height, segments);
                // 가운데 구멍은 짙은 원판으로 덮어서 표현(윗면 위 1mm)
                Ring(baseCenter + Vector3.up * (height + 0.001f), 0.001f, inner, segments);
            }
        }

        private static readonly Dictionary<Material, Part> Parts = new Dictionary<Material, Part>();
        private static Part P(Material m) { if (!Parts.TryGetValue(m, out var p)) Parts[m] = p = new Part(); return p; }

        private static Material Mat(string name, Color color, float smoothness, float metallic = 0f, string texSet = null, float tiling = 1f, bool unlit = false, Texture tex = null)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetColor("_BaseColor", color);
            if (!unlit) { m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", metallic); }
            if (texSet != null)
            {
                var color2 = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/ThirdParty/AmbientCG/{texSet}/{texSet}_Color.jpg");
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/ThirdParty/AmbientCG/{texSet}/{texSet}_NormalGL.jpg");
                m.SetTexture("_BaseMap", color2);
                if (normal != null && !unlit) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
                m.SetTextureScale("_BaseMap", Vector2.one / tiling);
            }
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void BuildGarage(MainMenu.TrackCard[] cards)
        {
            Parts.Clear();
            var floor = Mat("Lobby_Floor", new Color(0.40f, 0.41f, 0.43f), 0.78f, 0f, "Concrete031", 4f);
            var wall = Mat("Lobby_Wall", new Color(0.24f, 0.25f, 0.27f), 0.22f, 0f, "Concrete031", 3f);
            var panel = Mat("Lobby_WallPanel", new Color(0.10f, 0.11f, 0.12f), 0.45f, 0.2f);
            var ceiling = Mat("Lobby_Ceiling", new Color(0.06f, 0.06f, 0.07f), 0.2f);
            var strip = Mat("Lobby_LightStrip", new Color(1f, 0.98f, 0.94f), 0f, unlit: true);
            var accent = Mat("Lobby_Accent", Cyan, 0f, unlit: true);
            var discMat = Mat("Lobby_Turntable", new Color(0.30f, 0.31f, 0.33f), 0.72f, 0.55f);
            var baseMat = Mat("Lobby_TurntableBase", new Color(0.16f, 0.16f, 0.17f), 0.55f, 0.6f);
            var rubber = Mat("Lobby_Rubber", new Color(0.035f, 0.035f, 0.04f), 0.25f);
            var toolRed = Mat("Lobby_ToolRed", new Color(0.60f, 0.06f, 0.05f), 0.6f, 0.35f);
            var steel = Mat("Lobby_Steel", new Color(0.62f, 0.63f, 0.65f), 0.65f, 0.9f);
            var hazard = Mat("Lobby_Yellow", new Color(0.95f, 0.72f, 0.08f), 0.4f);

            // 방: 바닥·천장·벽 (안쪽 면만)
            P(floor).Box(new Vector3(-RoomX, -0.1f, RoomBack), new Vector3(RoomX, 0f, RoomFront), 1f, "Y");
            P(ceiling).Box(new Vector3(-RoomX, RoomH, RoomBack), new Vector3(RoomX, RoomH + 0.1f, RoomFront), 1f, "y");
            P(wall).Box(new Vector3(-RoomX - 0.2f, 0f, RoomBack), new Vector3(-RoomX, RoomH, RoomFront), 1f, "X");
            P(wall).Box(new Vector3(RoomX, 0f, RoomBack), new Vector3(RoomX + 0.2f, RoomH, RoomFront), 1f, "x");
            P(wall).Box(new Vector3(-RoomX, 0f, RoomBack - 0.2f), new Vector3(RoomX, RoomH, RoomBack), 1f, "Z");
            // 뒷벽(정면): 세로 패널 + 사이 틈
            P(wall).Box(new Vector3(-RoomX, 0f, RoomFront), new Vector3(RoomX, RoomH, RoomFront + 0.2f), 1f, "z");
            for (float x = -RoomX + 0.3f; x < RoomX - 0.3f; x += 0.62f)
                P(panel).Box(new Vector3(x, 0.15f, RoomFront - 0.08f), new Vector3(x + 0.56f, RoomH - 0.35f, RoomFront), 1f, "zXx");
            // 걸레받이 + 벽 청록 띠(높이 1m)
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * RoomX;
                P(panel).Box(new Vector3(Mathf.Min(x, x - side * 0.04f), 0f, RoomBack), new Vector3(Mathf.Max(x, x - side * 0.04f), 0.15f, RoomFront), 1f, side < 0 ? "XY" : "xY");
                P(accent).Box(new Vector3(Mathf.Min(x, x - side * 0.02f), 0.98f, RoomBack), new Vector3(Mathf.Max(x, x - side * 0.02f), 1.02f, RoomFront), 1f, side < 0 ? "X" : "x");
            }
            P(accent).Box(new Vector3(-RoomX, 0.98f, RoomFront - 0.1f), new Vector3(RoomX, 1.02f, RoomFront - 0.08f), 1f, "z");

            // 천장 조명: 긴 띠 3줄 + 턴테이블 위 사각틀(소프트박스)
            foreach (float x in new[] { -4.5f, -1.5f, 4.5f })
                P(strip).Box(new Vector3(x - 0.09f, RoomH - 0.06f, RoomBack + 0.8f), new Vector3(x + 0.09f, RoomH, RoomFront - 0.8f), 1f, "yXxZz");
            {
                Vector3 c = TurntableCenter; float half = 3.0f, y = RoomH - 0.5f, w = 0.14f;
                P(strip).Box(new Vector3(c.x - half, y - 0.05f, c.z - half), new Vector3(c.x + half, y, c.z - half + w), 1f, "yXxZz");
                P(strip).Box(new Vector3(c.x - half, y - 0.05f, c.z + half - w), new Vector3(c.x + half, y, c.z + half), 1f, "yXxZz");
                P(strip).Box(new Vector3(c.x - half, y - 0.05f, c.z - half), new Vector3(c.x - half + w, y, c.z + half), 1f, "yXxZz");
                P(strip).Box(new Vector3(c.x + half - w, y - 0.05f, c.z - half), new Vector3(c.x + half, y, c.z + half), 1f, "yXxZz");
                foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })   // 매다는 줄
                    P(steel).Box(new Vector3(c.x + corner.x * half - 0.01f, y, c.z + corner.y * half - 0.01f), new Vector3(c.x + corner.x * half + 0.01f, RoomH, c.z + corner.y * half + 0.01f), 1f, "XxZz");
            }

            // 턴테이블 받침 + 청록 고리 (고정)
            P(baseMat).Cylinder(TurntableCenter, DiscRadius + 0.12f, 0.03f, 96);
            P(accent).Ring(TurntableCenter + Vector3.up * 0.031f, DiscRadius + 0.02f, DiscRadius + 0.10f);
            P(accent).Ring(TurntableCenter + Vector3.up * 0.002f, DiscRadius + 0.55f, DiscRadius + 0.58f);   // 바닥 바깥 고리

            // 왼쪽 벽: 타이어 더미 3개, 트랙 포스터 4장 / 오른쪽 벽: 공구함 3개
            for (int k = 0; k < 3; k++)
            {
                Vector3 b = new Vector3(-RoomX + 0.55f, 0f, 7.6f + k * 0.78f);
                int count = 4 - (k == 1 ? 1 : 0);
                for (int i = 0; i < count; i++) P(rubber).Tire(b + Vector3.up * (i * 0.25f), 0.34f, 0.2f, 0.24f);
            }
            foreach (float z in new[] { 1.6f, 3.0f, 4.4f })
            {
                float x0 = RoomX - 0.55f, x1 = RoomX - 0.05f;
                P(toolRed).Box(new Vector3(x0, 0.08f, z - 0.6f), new Vector3(x1, 1.0f, z + 0.6f), 1f, "xYZz");
                P(steel).Box(new Vector3(x0 - 0.02f, 1.0f, z - 0.62f), new Vector3(x1, 1.04f, z + 0.62f), 1f, "xYZz");
                for (int d = 0; d < 4; d++)   // 서랍 손잡이
                    P(steel).Box(new Vector3(x0 - 0.03f, 0.25f + d * 0.2f, z - 0.35f), new Vector3(x0, 0.27f + d * 0.2f, z + 0.35f), 1f, "xYy");
                P(rubber).Box(new Vector3(x0, 0f, z - 0.55f), new Vector3(x1, 0.08f, z + 0.55f), 1f, "xZz");   // 바퀴 자리
            }
            // 바닥 안전선(출입 통로 표시)
            for (float z = RoomBack + 0.6f; z < RoomFront - 0.5f; z += 0.9f)
                P(hazard).Box(new Vector3(-RoomX + 1.6f, 0f, z), new Vector3(-RoomX + 1.7f, 0.002f, z + 0.5f), 1f, "Y");

            // 오브젝트로 만들기
            var oldLobby = GameObject.Find("Lobby");
            if (oldLobby != null) Object.DestroyImmediate(oldLobby);
            var lobby = new GameObject("Lobby").transform;
            var garage = new GameObject("Garage").transform; garage.SetParent(lobby, false);
            Directory.CreateDirectory(Path.GetDirectoryName(MeshPath));
            AssetDatabase.DeleteAsset(MeshPath);
            bool first = true;
            var noShadow = new HashSet<Material> { floor, wall, panel, ceiling, strip, accent, hazard };
            foreach (var kv in Parts.OrderBy(k => k.Key.name))
            {
                var mesh = new Mesh { name = "Garage_" + kv.Key.name, indexFormat = kv.Value.V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(kv.Value.V); mesh.SetNormals(kv.Value.N); mesh.SetUVs(0, kv.Value.U); mesh.SetTriangles(kv.Value.T, 0);
                mesh.RecalculateTangents(); mesh.RecalculateBounds();
                if (first) { AssetDatabase.CreateAsset(mesh, MeshPath); first = false; } else AssetDatabase.AddObjectToAsset(mesh, MeshPath);
                var go = new GameObject(kv.Key.name);
                go.transform.SetParent(garage, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = kv.Key;
                r.shadowCastingMode = noShadow.Contains(kv.Key) ? ShadowCastingMode.Off : ShadowCastingMode.On;
                r.lightProbeUsage = LightProbeUsage.Off;
            }

            // 벽 충돌(밖으로 걸어 나가지 않게)
            var colliders = new GameObject("WallColliders").transform; colliders.SetParent(lobby, false);
            void Col(Vector3 center, Vector3 size) { var g = new GameObject("Wall"); g.transform.SetParent(colliders, false); g.transform.position = center; g.AddComponent<BoxCollider>().size = size; }
            Col(new Vector3(-RoomX - 0.1f, RoomH / 2f, (RoomBack + RoomFront) / 2f), new Vector3(0.2f, RoomH, RoomFront - RoomBack));
            Col(new Vector3(RoomX + 0.1f, RoomH / 2f, (RoomBack + RoomFront) / 2f), new Vector3(0.2f, RoomH, RoomFront - RoomBack));
            Col(new Vector3(0f, RoomH / 2f, RoomBack - 0.1f), new Vector3(RoomX * 2f, RoomH, 0.2f));
            Col(new Vector3(0f, RoomH / 2f, RoomFront + 0.1f), new Vector3(RoomX * 2f, RoomH, 0.2f));

            // 트랙 포스터(왼쪽 벽, 안쪽을 봄): 썸네일 + 청록 테두리
            var posters = new GameObject("Posters").transform; posters.SetParent(lobby, false);
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i].Thumbnail == null) continue;
                var m = Mat($"Lobby_Poster_{cards[i].Scene}", Color.white, 0f, unlit: true, tex: cards[i].Thumbnail);
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = "Poster_" + cards[i].Scene;
                q.transform.SetParent(posters, false);
                q.transform.position = new Vector3(-RoomX + 0.03f, 2.55f, -0.6f + i * 1.85f);
                q.transform.rotation = Quaternion.LookRotation(Vector3.left);   // 쿼드 앞면 = -forward 방향에서 보임 → 방 안쪽(+x)에서 보이게
                q.transform.localScale = new Vector3(1.6f, 0.9f, 1f);
                var qr = q.GetComponent<MeshRenderer>();
                qr.sharedMaterial = m; qr.shadowCastingMode = ShadowCastingMode.Off; qr.lightProbeUsage = LightProbeUsage.Off;
                var frame = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(frame.GetComponent<Collider>());
                frame.name = "Frame"; frame.transform.SetParent(q.transform, false);
                frame.transform.localPosition = new Vector3(0f, 0f, 0.005f);
                frame.transform.localScale = new Vector3(1.05f, 1.09f, 1f);
                var fr = frame.GetComponent<MeshRenderer>();
                fr.sharedMaterial = accent; fr.shadowCastingMode = ShadowCastingMode.Off; fr.lightProbeUsage = LightProbeUsage.Off;
            }

            // 벽 로고 (TextMeshPro 3D)
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var logo = new GameObject("Logo").AddComponent<TextMeshPro>();
            logo.transform.SetParent(lobby, false);
            logo.transform.position = new Vector3(0f, 3.55f, RoomFront - 0.12f);
            logo.font = font; logo.fontSize = 14f; logo.alignment = TextAlignmentOptions.Center; logo.color = Cyan;
            logo.characterSpacing = 18f; logo.enableWordWrapping = false; logo.text = "VR KART";
            logo.rectTransform.sizeDelta = new Vector2(12f, 2f);
            var sub = new GameObject("LogoSub").AddComponent<TextMeshPro>();
            sub.transform.SetParent(lobby, false);
            sub.transform.position = new Vector3(0f, 2.62f, RoomFront - 0.12f);
            sub.font = font; sub.fontSize = 4.2f; sub.alignment = TextAlignmentOptions.Center; sub.color = new Color(0.85f, 0.88f, 0.92f);
            sub.characterSpacing = 30f; sub.enableWordWrapping = false; sub.text = "RACING GARAGE";
            sub.rectTransform.sizeDelta = new Vector2(12f, 1f);

            // 턴테이블: 돌아가는 판 + 차
            var table = new GameObject("Turntable").transform; table.SetParent(lobby, false);
            table.position = TurntableCenter;
            table.gameObject.AddComponent<Turntable>();
            var discPart = new Part();
            discPart.Cylinder(Vector3.zero + Vector3.up * 0.03f, DiscRadius, DiscTop - 0.03f, 96);
            for (int i = 0; i < 12; i++)   // 판 위 홈(돌아가는 게 보이게)
            {
                float a = i * Mathf.PI / 6f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), r = Vector3.Cross(Vector3.up, d) * 0.012f;
                Vector3 p0 = d * 0.6f + Vector3.up * (DiscTop + 0.001f), p1 = d * (DiscRadius - 0.15f) + Vector3.up * (DiscTop + 0.001f);
                discPart.Quad(p0 - r, p1 - r, p1 + r, p0 + r, Vector3.up, 1f);
            }
            var discMesh = new Mesh { name = "Turntable_Disc" };
            discMesh.SetVertices(discPart.V); discMesh.SetNormals(discPart.N); discMesh.SetUVs(0, discPart.U); discMesh.SetTriangles(discPart.T, 0);
            discMesh.RecalculateTangents(); discMesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(discMesh, MeshPath);
            var disc = new GameObject("Disc"); disc.transform.SetParent(table, false);
            disc.AddComponent<MeshFilter>().sharedMesh = discMesh;
            var dr = disc.AddComponent<MeshRenderer>(); dr.sharedMaterial = discMat; dr.lightProbeUsage = LightProbeUsage.Off;

            PlaceShowcaseCar(table);

            // 조명·환경
            var oldGround = GameObject.Find("Ground");
            if (oldGround != null) { var gr = oldGround.GetComponent<MeshRenderer>(); if (gr != null) gr.enabled = false; }   // 충돌만 남김(바닥)
            var sun = RenderSettings.sun != null ? RenderSettings.sun : Object.FindAnyObjectByType<Light>();
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(68f, 200f, 0f);
                sun.color = new Color(1f, 0.97f, 0.93f);
                sun.intensity = 0.95f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.85f;
                RenderSettings.sun = sun;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.40f, 0.42f, 0.46f);
            RenderSettings.ambientEquatorColor = new Color(0.28f, 0.29f, 0.31f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.14f, 0.15f);
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            // 반사 프로브(차·판은 빼고 굽기)
            var probeGo = new GameObject("ReflectionProbe"); probeGo.transform.SetParent(lobby, false);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Custom;
            probe.resolution = 256; probe.hdr = true; probe.boxProjection = true;
            probe.size = new Vector3(RoomX * 2f, RoomH, RoomFront - RoomBack);
            probeGo.transform.position = new Vector3(0f, RoomH / 2f, (RoomBack + RoomFront) / 2f);   // 방 전체를 감싸는 상자(박스 투영)
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor; probe.backgroundColor = Color.black;
            probe.nearClipPlane = 0.1f; probe.farClipPlane = 40f;
            table.gameObject.SetActive(false);
            var ui = Object.FindObjectsByType<MainMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (ui != null) ui.gameObject.SetActive(false);
            try
            {
                string dir = "Assets/_Project/Textures/ReflectionProbes/MainMenu";
                Directory.CreateDirectory(dir);
                string path = $"{dir}/Garage.exr";
                if (Lightmapping.BakeReflectionProbe(probe, path))
                {
                    AssetDatabase.ImportAsset(path);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.maxTextureSize = 256; importer.textureCompression = TextureImporterCompression.Compressed; importer.SaveAndReimport();
                    probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                }
                else Debug.LogError("[GarageLobbyBuilder] 반사 프로브 굽기 실패");
            }
            finally
            {
                table.gameObject.SetActive(true);
                if (ui != null) ui.gameObject.SetActive(true);
            }

            int tris = Parts.Values.Sum(p => p.T.Count / 3);
            Debug.Log($"[GarageLobbyBuilder] 차고 재질 {Parts.Count}개(그리기 {Parts.Count}번), 삼각형 {tris:N0}");
        }

        // 전시 차를 턴테이블 위 가운데에 놓고 접지 그림자를 붙인다(차 바닥 = 판 윗면)
        private static void PlaceShowcaseCar(Transform table)
        {
            var car = ShowcaseCar();
            if (car == null) return;
            car.SetParent(table, true);
            car.rotation = Quaternion.Euler(0f, -35f, 0f) * car.rotation;   // 처음엔 비스듬히 앞을 보게 (돌린 뒤에 경계를 재야 가운데에 맞는다)
            // 새로 만든 렌더러의 bounds는 아직 갱신 전이라 메시 경계를 직접 변환해서 잰다
            var bounds = new Bounds(car.position, Vector3.zero); bool any = false;
            foreach (var f in car.GetComponentsInChildren<MeshFilter>())
            {
                var b = f.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var corner = f.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                    if (!any) { bounds = new Bounds(corner, Vector3.zero); any = true; } else bounds.Encapsulate(corner);
                }
            }
            car.position += new Vector3(TurntableCenter.x - bounds.center.x, DiscTop - bounds.min.y, TurntableCenter.z - bounds.center.z);
            // 접지 그림자: 차 밑 부드러운 타원(판과 같이 돎) — 바퀴가 판에 닿아 보이게
            var blob = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(blob.GetComponent<Collider>());
            blob.name = "ContactShadow";
            blob.transform.SetParent(car, false);
            // 차를 옮긴 뒤의 가운데(= 턴테이블 중심) 바로 위. 크기는 차의 축소 비율을 빼고 월드 기준 2.5m × 5.4m
            blob.transform.localPosition = car.InverseTransformPoint(new Vector3(TurntableCenter.x, DiscTop + 0.004f, TurntableCenter.z));
            blob.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Vector3 carScale = car.lossyScale;
            blob.transform.localScale = new Vector3(2.5f / carScale.x, 5.4f / carScale.z, 1f / carScale.y);
            var br = blob.GetComponent<MeshRenderer>();
            br.sharedMaterial = ContactShadowMaterial();
            br.shadowCastingMode = ShadowCastingMode.Off; br.receiveShadows = false; br.lightProbeUsage = LightProbeUsage.Off; br.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        // 전시 차: AI 세단의 완성 외형(차체·바퀴·브레이크등)을 복사하고 도장만 플레이어 차 색으로. 스크립트·충돌·그림자 판·번호판 없음.
        // (Kart_Player의 차체는 1인칭용이라 실내·바퀴가 빠져 있어서 밖에서 보면 껍데기만 보인다)
        private static Transform ShowcaseCar()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ShowcasePrefab)?.transform.Find("Car");
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(KartPrefab)?.transform.Find("Car/Body")?.GetComponent<MeshRenderer>();
            if (source == null) { Debug.LogWarning("[GarageLobbyBuilder] 전시 차 외형을 찾지 못했습니다"); return null; }
            Material playerPaint = player != null ? player.sharedMaterials.FirstOrDefault(m => m != null && m.name.StartsWith("CarConcept_Paint_Player")) : null;
            var car = new GameObject("ShowcaseCar").transform;
            car.SetPositionAndRotation(source.position, source.rotation);
            car.localScale = source.lossyScale;   // 원본 Car는 0.82배. 루트에 같은 배율을 줘야 차체와 바퀴 위치가 함께 줄어든다
            foreach (var mr in source.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool raceNumber = false;
                for (var t = mr.transform; t != source; t = t.parent) if (t.name == "RaceNumber") { raceNumber = true; break; }
                if (mr.name == "Shadow" || raceNumber) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var go = new GameObject(mr.name);
                go.transform.SetParent(car, false);
                go.transform.localPosition = source.InverseTransformPoint(mr.transform.position);
                go.transform.localRotation = Quaternion.Inverse(source.rotation) * mr.transform.rotation;
                Vector3 ls = mr.transform.lossyScale, ss = source.lossyScale;
                go.transform.localScale = new Vector3(ls.x / ss.x, ls.y / ss.y, ls.z / ss.z);
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = go.AddComponent<MeshRenderer>();
                var mats = mr.sharedMaterials;
                if (playerPaint != null) for (int i = 0; i < mats.Length; i++) if (mats[i] != null && mats[i].name.StartsWith("CarConcept_Paint_AI")) mats[i] = playerPaint;
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.lightProbeUsage = LightProbeUsage.Off;
            }
            return car;
        }

        // 가운데가 짙고 가장자리로 사라지는 그림자 판(투명 Unlit, 텍스처는 직접 생성)
        private static Material ContactShadowMaterial()
        {
            const string texPath = "Assets/_Project/Textures/UI/ContactShadow.png";
            if (!File.Exists(texPath))
            {
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.35f, 1f, d)) * 0.82f;
                        tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                    }
                tex.Apply();
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
                importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp; importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            var m = Mat("Lobby_ContactShadow", Color.white, 0f, unlit: true, tex: AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------------------------------------------------------------- 3) 메뉴판
        private static void BuildMenuPrefab(MainMenu.TrackCard[] cards)
        {
            var root = PrefabUtility.LoadPrefabContents(MenuPrefab);
            try
            {
                var menu = root.GetComponent<MainMenu>();
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                var main = root.transform.Find("MainPanel");
                var panelImage = main.GetComponent<Image>();

                RectTransform Rect(Transform t, Vector2 pos, Vector2 size) { var rt = (RectTransform)t; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size; return rt; }
                TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, Vector2 pos, Vector2 box)
                {
                    var old = parent.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject);
                    var go = new GameObject(name, typeof(RectTransform));
                    go.transform.SetParent(parent, false);
                    var t = go.AddComponent<TextMeshProUGUI>();
                    t.font = font; t.fontSize = size; t.color = color; t.alignment = TextAlignmentOptions.Center; t.enableWordWrapping = false; t.text = text; t.raycastTarget = false;
                    Rect(go.transform, pos, box);
                    return t;
                }
                void Label(Transform button, string text, float size, Color? color = null)
                {
                    var l = button.Find("Label").GetComponent<TMP_Text>();
                    l.enableAutoSizing = false; l.text = text; l.fontSize = size; if (color.HasValue) l.color = color.Value;
                }

                // --- 메인 패널 배치 ---
                var title = main.Find("Title");
                Rect(title, new Vector2(0f, 338f), new Vector2(900f, 84f));
                var titleText = title.GetComponent<TMP_Text>(); titleText.text = "트랙 선택"; titleText.color = Cyan; titleText.characterSpacing = 4f;
                titleText.enableAutoSizing = false; titleText.fontSize = 56f;

                // 썸네일 틀 + 이미지
                var oldFrame = main.Find("TrackCard"); if (oldFrame != null) Object.DestroyImmediate(oldFrame.gameObject);
                var frame = new GameObject("TrackCard", typeof(RectTransform), typeof(Image));
                frame.transform.SetParent(main, false);
                frame.transform.SetSiblingIndex(1);
                Rect(frame.transform, new Vector2(0f, 150f), new Vector2(528f, 304f));
                var frameImage = frame.GetComponent<Image>(); frameImage.color = Cyan; frameImage.raycastTarget = false;
                var thumbGo = new GameObject("Thumbnail", typeof(RectTransform), typeof(RawImage));
                thumbGo.transform.SetParent(frame.transform, false);
                Rect(thumbGo.transform, Vector2.zero, new Vector2(512f, 288f));
                var thumb = thumbGo.GetComponent<RawImage>(); thumb.raycastTarget = false;
                if (cards.Length > 0) thumb.texture = cards[0].Thumbnail;

                // < > 버튼: 기존 트랙 버튼 = 다음(>), 복제해서 이전(<)
                var next = main.Find("Button_Track");
                Rect(next, new Vector2(345f, 150f), new Vector2(110f, 160f));
                Label(next, ">", 84f);
                var oldPrev = main.Find("Button_TrackPrev"); if (oldPrev != null) Object.DestroyImmediate(oldPrev.gameObject);
                var prev = Object.Instantiate(next.gameObject, main).transform;
                prev.name = "Button_TrackPrev";
                Rect(prev, new Vector2(-345f, 150f), new Vector2(110f, 160f));
                Label(prev, "<", 84f);

                var trackName = Text(main, "TrackName", Tracks[0].Label, 54f, Color.white, new Vector2(0f, -48f), new Vector2(900f, 66f));
                var trackInfo = Text(main, "TrackInfo", "", 38f, new Color(0.78f, 0.84f, 0.90f), new Vector2(0f, -108f), new Vector2(900f, 52f));

                Rect(main.Find("Button_Mode"), new Vector2(-235f, -205f), new Vector2(430f, 96f));
                var start = main.Find("Button_Start");
                Rect(start, new Vector2(235f, -205f), new Vector2(430f, 96f));
                start.GetComponent<Image>().color = Color.white;
                var startButton = start.GetComponent<Button>();
                var colors = startButton.colors;
                colors.normalColor = Cyan; colors.highlightedColor = Color.Lerp(Cyan, Color.white, 0.45f); colors.selectedColor = Cyan;
                colors.pressedColor = Cyan * 0.7f; colors.colorMultiplier = 1f;
                startButton.colors = colors;
                Label(start, "시작", 50f, new Color(0.03f, 0.07f, 0.10f));
                Rect(main.Find("Button_Settings"), new Vector2(-235f, -312f), new Vector2(430f, 78f));
                Rect(main.Find("Button_Quit"), new Vector2(235f, -312f), new Vector2(430f, 78f));

                // --- 타이틀 패널 ---
                var oldTitle = root.transform.Find("TitlePanel"); if (oldTitle != null) Object.DestroyImmediate(oldTitle.gameObject);
                var titlePanel = new GameObject("TitlePanel", typeof(RectTransform), typeof(Image), typeof(Button));
                titlePanel.transform.SetParent(root.transform, false);
                var tpr = (RectTransform)titlePanel.transform;
                tpr.anchorMin = Vector2.zero; tpr.anchorMax = Vector2.one; tpr.offsetMin = tpr.offsetMax = Vector2.zero;
                var tImage = titlePanel.GetComponent<Image>();
                tImage.sprite = panelImage.sprite; tImage.type = panelImage.type; tImage.color = panelImage.color; tImage.pixelsPerUnitMultiplier = panelImage.pixelsPerUnitMultiplier;
                var tButton = titlePanel.GetComponent<Button>(); tButton.transition = Selectable.Transition.None; tButton.targetGraphic = tImage;
                var logo = Text(titlePanel.transform, "Logo", "VR KART", 150f, Cyan, new Vector2(0f, 120f), new Vector2(960f, 180f));
                logo.characterSpacing = 16f;
                Text(titlePanel.transform, "Sub", "동아대 캠퍼스 레이싱", 46f, new Color(0.85f, 0.88f, 0.92f), new Vector2(0f, -10f), new Vector2(900f, 60f));
                var hint = Text(titlePanel.transform, "Hint", "트리거를 눌러 시작", 46f, Color.white, new Vector2(0f, -220f), new Vector2(900f, 60f));
                titlePanel.SetActive(false);

                // --- MainMenu 필드 ---
                var so = new SerializedObject(menu);
                so.FindProperty("_trackText").objectReferenceValue = trackName;
                so.FindProperty("_prevTrackButton").objectReferenceValue = prev.GetComponent<Button>();
                so.FindProperty("_trackThumbnail").objectReferenceValue = thumb;
                so.FindProperty("_trackInfoText").objectReferenceValue = trackInfo;
                so.FindProperty("_titlePanel").objectReferenceValue = titlePanel;
                so.FindProperty("_titleButton").objectReferenceValue = tButton;
                so.FindProperty("_titleHint").objectReferenceValue = hint;
                so.FindProperty("_sideOffset").floatValue = -0.62f;
                so.FindProperty("_yawOffset").floatValue = -20f;
                var list = so.FindProperty("_cards");
                list.arraySize = cards.Length;
                for (int i = 0; i < cards.Length; i++)
                {
                    var e = list.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("Scene").stringValue = cards[i].Scene;
                    e.FindPropertyRelative("Thumbnail").objectReferenceValue = cards[i].Thumbnail;
                    e.FindPropertyRelative("LengthMeters").floatValue = cards[i].LengthMeters;
                    e.FindPropertyRelative("Laps").intValue = cards[i].Laps;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, MenuPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------------------------------------------------------------- 4) 빌드 첫 씬·폰트
        // 씬의 UI_MainMenu가 판 크기를 760으로 덮어써서 설정 화면 아래 버튼이 판 밖으로 나오던 문제(#65) → 프리팹 값(1000 × 830)을 따르게
        private static void RevertMenuSizeOverride()
        {
            var menu = Object.FindObjectsByType<MainMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (menu == null) return;
            menu.gameObject.SetActive(true);   // 반사 프로브를 굽는 동안 꺼 둔 것을 확실히 켠 채 저장
            if (!PrefabUtility.IsPartOfPrefabInstance(menu.gameObject)) return;
            var rt = (RectTransform)menu.transform;
            var so = new SerializedObject(rt);
            PrefabUtility.RevertPropertyOverride(so.FindProperty("m_SizeDelta"), InteractionMode.AutomatedAction);
        }

        private static void SetFirstScene()
        {
            var list = EditorBuildSettings.scenes.ToList();
            int i = list.FindIndex(s => s.path == MenuScene);
            if (i <= 0) return;
            var menu = list[i]; menu.enabled = true;
            list.RemoveAt(i); list.Insert(0, menu);
            EditorBuildSettings.scenes = list.ToArray();
        }

        // 씬 전환 페이드 재질(Resources에 두어 빌드에 셰이더가 포함되게)
        private static void EnsureFadeMaterial()
        {
            const string path = "Assets/_Project/Resources/ScreenFade.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            Directory.CreateDirectory("Assets/_Project/Resources");
            var m = new Material(Shader.Find("VRKart/ScreenFade")) { name = "ScreenFade" };
            m.SetColor("_Color", new Color(0f, 0f, 0f, 0f));
            AssetDatabase.CreateAsset(m, path);
        }

        private static void EnsureGlyphs(string text)
        {
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            string missing = new string(text.Where(c => c != ' ' && !fa.HasCharacter(c)).Distinct().ToArray());
            if (missing.Length == 0) return;
            if (fa.sourceFontFile == null)
            {
                var src = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/Fonts/Pretendard/Pretendard-SemiBold.otf");
                typeof(TMP_FontAsset).GetField("m_SourceFontFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(fa, src);
            }
            fa.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            bool ok = fa.TryAddCharacters(missing, out string notFound);
            fa.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            File.AppendAllText("Assets/_Project/Fonts/Pretendard_Characters.txt", "\n" + missing);
            Debug.Log($"[GarageLobbyBuilder] 폰트 글자 추가 '{missing}' ok={ok} 없음='{notFound}'");
        }
    }
}
