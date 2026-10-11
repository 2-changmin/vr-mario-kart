using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VRKart.AI;
using VRKart.Items;
using VRKart.Kart;
using VRKart.TimeAttack;
using VRKart.UI;

namespace VRKart.Race.Editor
{
    // 통합 자동 테스트(#16, docs/TEST_CHECKLIST.md):
    //   메뉴 → 그랑프리(Track_Main) → 일시정지/재개 → 완주·결과 → 재시작 → 일시정지 → 메뉴
    //   → 타임어택 → 완주·기록 저장 → 재시작 → 고스트 재생 → 메뉴.
    // 플레이어 카트도 AI 입력으로 바꾸고 4배속(약 2분). 버튼은 화면을 누르는 대신 각 UI의 Button.onClick을 직접 부른다.
    // 메뉴 VRKart > 통합 자동 테스트, 배치 모드 -executeMethod VRKart.Race.Editor.RaceFlowSmokeTest.Start
    // 결과: Logs/RaceFlowTest.txt (+ 콘솔). 내 서킷 타임어택 기록(persistentDataPath/TimeAttack/Track_Main.ghost)은
    // 테스트 전에 Logs에 백업하고 끝나면 되돌린다.
    [InitializeOnLoad]
    public static class RaceFlowSmokeTest
    {
        private const string Key = "RaceFlowSmokeTest.Running";
        private const string HadGhostKey = "RaceFlowSmokeTest.HadGhost";
        private static readonly StringBuilder Log = new StringBuilder();
        private static readonly List<string> Errors = new List<string>();
        private static string s_step = "menu1";
        private static float s_stepStart;
        private static RaceManager s_rm;
        private static bool s_swapped, s_pausedOnce, s_ghostSeen;
        private static Vector3 s_ghostPos;
        private static int s_pass, s_fail;

        private static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
        private static string GhostFile => Path.Combine(Application.persistentDataPath, "TimeAttack", "Track_Main.ghost");
        private static string GhostBackup => Path.Combine(Out, "Track_Main.ghost.bak");

        static RaceFlowSmokeTest()
        {
            EditorApplication.update += Update;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    Errors.Add($"[{type}] {message}\n{stack.Split('\n').FirstOrDefault()}");
            };
        }

        [MenuItem("VRKart/통합 자동 테스트 (메뉴→레이스→결과→타임어택)")]
        public static void Start()
        {
            if (EditorApplication.isPlaying) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Out);
            Log.Clear(); Errors.Clear();
            s_step = "menu1"; s_stepStart = 0f; s_pass = s_fail = 0; s_rm = null; s_swapped = s_pausedOnce = s_ghostSeen = false;
            SessionState.SetBool(HadGhostKey, false);
            if (File.Exists(GhostFile)) { File.Copy(GhostFile, GhostBackup, true); File.Delete(GhostFile); SessionState.SetBool(HadGhostKey, true); }
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity");
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Check(string what, bool ok, string detail = "")
        {
            if (ok) s_pass++; else s_fail++;
            Log.AppendLine($"{(ok ? "✅" : "❌")} [{s_step}] {what}{(detail != "" ? " — " + detail : "")}");
        }

        private static T Field<T>(object o, string name) =>
            (T)o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

        private static void Click(object o, string button) => Field<Button>(o, button).onClick.Invoke();

        private static string Text(object o, string field) => Field<TMPro.TMP_Text>(o, field)?.text ?? "";

        private static void Next(string step)
        {
            s_step = step; s_stepStart = Time.realtimeSinceStartup;
            s_swapped = false; s_pausedOnce = false; s_rm = null;
        }

        // 플레이어 카트 입력을 AI로 바꾸고 4배속
        private static bool SwapToAi()
        {
            s_rm = Object.FindAnyObjectByType<RaceManager>();
            if (s_rm == null || s_rm.Player == null) return false;
            var player = s_rm.Player;
            Object.Destroy(player.GetComponent<PlayerKartInput>());
            var ai = player.gameObject.AddComponent<AIKartInput>();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(KartController).GetField("_input", flags).SetValue(player.GetComponent<KartController>(), ai);
            typeof(ItemHolder).GetField("_input", flags).SetValue(player.GetComponent<ItemHolder>(), ai);
            foreach (var v in player.GetComponentsInChildren<SteeringWheelVisual>()) typeof(SteeringWheelVisual).GetField("_input", flags).SetValue(v, ai);
            Time.timeScale = 4f;
            return true;
        }

        private static void Update()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            if (s_stepStart == 0f) s_stepStart = Time.realtimeSinceStartup;
            float t = Time.realtimeSinceStartup - s_stepStart;
            if (t > 400f) { Check("시간 초과", false, s_step); Finish(); return; }

            switch (s_step)
            {
                case "menu1":
                {
                    var menu = Object.FindAnyObjectByType<MainMenu>();
                    if (menu == null || t < 1f) return;
                    Check("메인 메뉴 열림", true);
                    Click(menu, "_settingsButton"); Check("설정 열기", menu.IsSettingsOpen);
                    Click(menu, "_backButton"); Check("설정 닫기(뒤로)", !menu.IsSettingsOpen);
                    var tracks = (System.Array)typeof(MainMenu).GetField("Tracks", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                    var index = typeof(MainMenu).GetField("s_trackIndex", BindingFlags.NonPublic | BindingFlags.Static);
                    for (int i = 0; i < tracks.Length; i++)
                    {
                        var track = tracks.GetValue((int)index.GetValue(null));
                        if (((string)track.GetType().GetField("Item1").GetValue(track)).Contains("Track_Main")) break;
                        Click(menu, "_trackButton");
                    }
                    Check("트랙 버튼으로 서킷 선택", Text(menu, "_trackText").Contains("서킷"), Text(menu, "_trackText"));
                    if (TimeAttackSettings.IsTimeAttack) Click(menu, "_modeButton");
                    Check("모드 = 그랑프리", !TimeAttackSettings.IsTimeAttack, Text(menu, "_modeText"));
                    Click(menu, "_startButton");
                    Next("gp1");
                    return;
                }
                case "gp1":
                {
                    if (!s_swapped)
                    {
                        if (t < 1f || !SwapToAi()) return;
                        s_swapped = true;
                        Check("레이스 씬 로드", s_rm.gameObject.scene.name == "Track_Main", s_rm.gameObject.scene.name);
                        Check("참가자 4명", Object.FindObjectsByType<RaceProgress>(FindObjectsSortMode.None).Length == 4);
                        return;
                    }
                    var pause = Object.FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
                    if (!s_pausedOnce && s_rm.State == RaceState.Racing && s_rm.RaceTime > 15f)
                    {
                        s_pausedOnce = true;
                        s_rm.Pause();
                        Check("일시정지 메뉴 표시", pause != null && pause.IsShown);
                        Check("일시정지 중 시간 멈춤", Time.timeScale == 0f, $"timeScale {Time.timeScale}");
                        Click(pause, "_resumeButton");
                        Check("재개", !s_rm.IsPaused && !pause.IsShown);
                        Time.timeScale = 4f;
                        return;
                    }
                    var result = Object.FindAnyObjectByType<ResultScreen>(FindObjectsInactive.Include);
                    if (result != null && result.IsShown)
                    {
                        Check("완주 → 결과 화면", true, $"레이스 {s_rm.RaceTime:F1}s");
                        Check("결과 순위 4명", s_rm.GetResults().Count == 4);
                        Time.timeScale = 1f;
                        Click(result, "_restartButton");
                        Next("gp2");
                    }
                    return;
                }
                case "gp2":
                {
                    if (!s_swapped)
                    {
                        if (t < 1f || !SwapToAi()) return;
                        s_swapped = true;
                        Check("결과 → 재시작(씬 다시 로드)", s_rm.State == RaceState.Waiting || s_rm.State == RaceState.Countdown, s_rm.State.ToString());
                        return;
                    }
                    if (s_rm.State != RaceState.Racing || s_rm.RaceTime < 5f) return;
                    var pause = Object.FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
                    s_rm.Pause();
                    Click(pause, "_menuButton");
                    Next("menu2");
                    return;
                }
                case "menu2":
                {
                    var menu = Object.FindAnyObjectByType<MainMenu>();
                    if (menu == null || t < 1f) return;
                    Check("일시정지 → 메뉴로", true);
                    Check("메뉴로 돌아온 뒤 시간 정상", Time.timeScale == 1f, $"timeScale {Time.timeScale}");
                    Click(menu, "_modeButton");
                    Check("모드 → 타임어택", TimeAttackSettings.IsTimeAttack, Text(menu, "_modeText"));
                    Click(menu, "_startButton");
                    Next("ta1");
                    return;
                }
                case "ta1":
                {
                    if (!s_swapped)
                    {
                        if (t < 1f || !SwapToAi()) return;
                        s_swapped = true;
                        Check("타임어택: 참가자 1명", Object.FindObjectsByType<RaceProgress>(FindObjectsSortMode.None).Length == 1);
                        Check("타임어택: 아이템 박스 없음", Object.FindObjectsByType<ItemBox>(FindObjectsSortMode.None).Length == 0);
                        Check("첫 판: 고스트 없음", Object.FindAnyObjectByType<GhostCar>() == null);
                        return;
                    }
                    var result = Object.FindAnyObjectByType<ResultScreen>(FindObjectsInactive.Include);
                    if (result != null && result.IsShown)
                    {
                        Check("타임어택 완주 → 결과", true, $"{s_rm.RaceTime:F1}s");
                        Check("기록 파일 저장", File.Exists(GhostFile));
                        Time.timeScale = 1f;
                        Click(result, "_restartButton");
                        Next("ta2");
                    }
                    return;
                }
                case "ta2":
                {
                    if (!s_swapped) { if (t < 1f || !SwapToAi()) return; s_swapped = true; return; }
                    var ghost = Object.FindAnyObjectByType<GhostCar>();
                    if (!s_ghostSeen && ghost != null) { s_ghostSeen = true; s_ghostPos = ghost.transform.position; Check("둘째 판: 고스트 등장", true); }
                    if (s_rm.State != RaceState.Racing || s_rm.RaceTime < 12f) return;
                    float moved = ghost != null ? Vector3.Distance(ghost.transform.position, s_ghostPos) : 0f;
                    Check("고스트가 달림", moved > 20f, ghost != null ? $"{moved:F0}m" : "없음");
                    var pause = Object.FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
                    s_rm.Pause();
                    Click(pause, "_menuButton");
                    Next("menu3");
                    return;
                }
                case "menu3":
                {
                    if (Object.FindAnyObjectByType<MainMenu>() == null || t < 1f) return;
                    Check("타임어택 → 메뉴로", true);
                    Finish();
                    return;
                }
            }
        }

        private static void Finish()
        {
            var sb = new StringBuilder($"통과 {s_pass} / 실패 {s_fail}\n").Append(Log);
            sb.AppendLine($"\n오류 로그 {Errors.Count}개");
            foreach (var e in Errors.Distinct().Take(20)) sb.AppendLine(e);
            File.WriteAllText(Path.Combine(Out, "RaceFlowTest.txt"), sb.ToString());
            if (s_fail == 0 && Errors.Count == 0) Debug.Log("[RaceFlowSmokeTest] " + sb);
            else Debug.LogWarning("[RaceFlowSmokeTest] " + sb);

            SessionState.SetBool(Key, false);
            Time.timeScale = 1f;
            EditorApplication.ExitPlaymode();
            // 테스트가 만든 기록을 지우고 원래 기록을 되돌림
            if (File.Exists(GhostFile)) File.Delete(GhostFile);
            if (SessionState.GetBool(HadGhostKey, false) && File.Exists(GhostBackup)) File.Copy(GhostBackup, GhostFile, true);
            if (Application.isBatchMode) EditorApplication.Exit(s_fail == 0 ? 0 : 1);
        }
    }
}
