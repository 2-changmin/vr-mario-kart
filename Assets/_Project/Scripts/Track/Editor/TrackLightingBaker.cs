using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using VRKart.Kart;

namespace VRKart.Track.Editor
{
    // 트랙 조명 굽기: 메뉴 VRKart > 트랙 조명 굽기 (열린 씬). 트랙·건물을 고친 뒤 다시 누르면 된다.
    //  1) 라이트 프로브: 트랙을 따라 8m마다(좌·중·우 × 낮게·높게). 하늘이 가려진 건물 사이·나무 아래에서 카트가 어두워지고 주변 반사광 색을 받는다.
    //     해는 Mixed(Baked Indirect)라 직접광·그림자는 지금처럼 실시간, 반사광만 프로브에 들어간다. 라이트맵은 만들지 않는다
    //     (도로·지형이 실행 중에 만들어져서 라이트맵을 저장할 수 없음). 카트가 아닌 렌더러는 프로브를 쓰지 않는다(Light Probes = Off).
    //  2) 반사 프로브: 트랙을 따라 70m마다, 128px 큐브맵(Textures/ReflectionProbes/<씬>/). 차 도장·크롬·유리에 주변 건물·도로·하늘이 비친다.
    // 굽는 동안 카트는 꺼서 찍히지 않게 한다.
    public static class TrackLightingBaker
    {
        private const float LightProbeSpacing = 8f;
        private const float ReflectionSpacing = 70f;

        [MenuItem("VRKart/트랙 조명 굽기 (열린 씬)")]
        public static void BakeOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (Object.FindAnyObjectByType<TrackLayout>() == null)
            {
                EditorUtility.DisplayDialog("트랙 조명 굽기", "열린 씬에 TrackLayout이 없습니다.", "확인");
                return;
            }
            Bake(scene.path);
        }

        // 배치 모드용: -executeMethod VRKart.Track.Editor.TrackLightingBaker.BakeAllTracks
        public static void BakeAllTracks()
        {
            foreach (var path in new[] { "Assets/_Project/Scenes/Track_Main.unity", "Assets/_Project/Scenes/Track_Campus.unity" })
            {
                EditorSceneManager.OpenScene(path);
                Bake(path);
            }
        }

        private static void Bake(string scenePath)
        {
            var scene = EditorSceneManager.GetActiveScene();
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);
            foreach (var b in Object.FindObjectsByType<TrackMeshBuilder>(FindObjectsSortMode.None)) b.Rebuild();
            foreach (var t in Object.FindObjectsByType<TrackTerrain>(FindObjectsSortMode.None)) t.Rebuild();
            var layout = Object.FindAnyObjectByType<TrackLayout>();
            var env = GameObject.Find("Environment");

            var karts = Object.FindObjectsByType<KartController>(FindObjectsSortMode.None).Select(k => k.gameObject).Where(g => g.activeSelf).ToList();
            foreach (var k in karts) k.SetActive(false);
            try
            {
                // 카트가 아닌 렌더러는 라이트 프로브를 쓰지 않음(큰 메시를 프로브 한 점으로 칠하면 어색함)
                var scenery = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => !(r is ParticleSystemRenderer)).ToList();
                foreach (var r in scenery)
                    if (r.lightProbeUsage != LightProbeUsage.Off) { Undo.RecordObject(r, "Light probes off"); r.lightProbeUsage = LightProbeUsage.Off; }

                BakeLightProbes(sceneName, layout, env, scenery);
                BakeReflectionProbes(sceneName, layout, env);
            }
            finally
            {
                foreach (var k in karts) k.SetActive(true);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[TrackLightingBaker] {sceneName}: 조명 굽기 완료");
        }

        private static Transform Holder(GameObject env, string name)
        {
            var old = env != null ? env.transform.Find(name) : GameObject.Find(name)?.transform;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var t = new GameObject(name).transform;
            if (env != null) t.SetParent(env.transform, false);
            return t;
        }

        private static void BakeLightProbes(string sceneName, TrackLayout layout, GameObject env, List<Renderer> scenery)
        {
            var group = Holder(env, "LightProbes").gameObject.AddComponent<LightProbeGroup>();
            var points = new List<Vector3>();
            int n = Mathf.CeilToInt(layout.Length / LightProbeSpacing);
            for (int i = 0; i < n; i++)
            {
                var s = layout.Evaluate(layout.Length * i / n);
                foreach (float side in new[] { -6f, 0f, 6f })
                    foreach (float h in new[] { 0.4f, 2.6f })
                        points.Add(s.Position + s.Right * side + Vector3.up * h);
            }
            group.probePositions = points.ToArray();

            // 해: 직접광·그림자는 실시간 그대로, 반사광만 굽기
            var sun = RenderSettings.sun;
            if (sun != null) { Undo.RecordObject(sun, "Mixed sun"); sun.lightmapBakeType = LightmapBakeType.Mixed; }

            string settingsPath = $"Assets/_Project/Settings/Lighting/{sceneName}_Lighting.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(settingsPath);
            if (settings == null) { settings = new LightingSettings { name = $"{sceneName}_Lighting" }; AssetDatabase.CreateAsset(settings, settingsPath); }
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            settings.maxBounces = 2;
            settings.directSampleCount = 16;
            settings.indirectSampleCount = 128;
            settings.environmentSampleCount = 128;
            settings.lightProbeSampleCountMultiplier = 4f;
            settings.lightmapResolution = 1f;
            settings.lightmapMaxSize = 512;
            EditorUtility.SetDirty(settings);
            Lightmapping.lightingSettings = settings;

            // 굽는 동안만: 모든 장면 물체가 빛을 막고 반사하게(ContributeGI), 라이트맵 대신 프로브로 받게 → 끝나면 원래대로
            var saved = new List<(GameObject go, StaticEditorFlags flags, ReceiveGI receive)>();
            foreach (var r in scenery)
            {
                if (!(r is MeshRenderer mr) || r.GetComponent<MeshFilter>() == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var go = r.gameObject;
                saved.Add((go, GameObjectUtility.GetStaticEditorFlags(go), mr.receiveGI));
                GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(go) | StaticEditorFlags.ContributeGI);
                mr.receiveGI = ReceiveGI.LightProbes;
            }
            try
            {
                Lightmapping.Clear();
                if (!Lightmapping.Bake()) Debug.LogError("[TrackLightingBaker] 라이트 프로브 굽기 실패");
            }
            finally
            {
                foreach (var (go, flags, receive) in saved)
                {
                    if (go == null) continue;
                    GameObjectUtility.SetStaticEditorFlags(go, flags);
                    var mr = go.GetComponent<MeshRenderer>();
                    if (mr != null) mr.receiveGI = receive;
                }
            }
            Debug.Log($"[TrackLightingBaker] {sceneName}: 라이트 프로브 {points.Count}개, 굽기에 쓴 물체 {saved.Count}개");
        }

        private static void BakeReflectionProbes(string sceneName, TrackLayout layout, GameObject env)
        {
            var root = Holder(env, "ReflectionProbes");
            string dir = $"Assets/_Project/Textures/ReflectionProbes/{sceneName}";
            if (AssetDatabase.IsValidFolder(dir)) AssetDatabase.DeleteAsset(dir);
            Directory.CreateDirectory(dir);
            int n = Mathf.CeilToInt(layout.Length / ReflectionSpacing);
            for (int i = 0; i < n; i++)
            {
                var s = layout.Evaluate(layout.Length * i / n);
                var go = new GameObject($"Probe_{i:00}");
                go.transform.SetParent(root, false);
                go.transform.position = s.Position + Vector3.up * 1.6f;
                var p = go.AddComponent<ReflectionProbe>();
                p.mode = ReflectionProbeMode.Custom;
                p.resolution = 128;
                p.size = new Vector3(ReflectionSpacing * 2f, 70f, ReflectionSpacing * 2f);   // 이웃 프로브와 겹쳐서 섞임
                p.boxProjection = false;   // 바깥 풍경이라 무한 거리로
                p.blendDistance = 20f;
                p.hdr = true;
                p.nearClipPlane = 0.3f;
                p.farClipPlane = 1000f;
                p.shadowDistance = 60f;
                p.clearFlags = ReflectionProbeClearFlags.Skybox;
                string path = $"{dir}/Probe_{i:00}.exr";
                if (!Lightmapping.BakeReflectionProbe(p, path)) { Debug.LogError($"[TrackLightingBaker] 반사 프로브 {i} 굽기 실패"); continue; }
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.maxTextureSize = 128;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
                p.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(path);
            }
            Debug.Log($"[TrackLightingBaker] {sceneName}: 반사 프로브 {n}개");
        }
    }
}
