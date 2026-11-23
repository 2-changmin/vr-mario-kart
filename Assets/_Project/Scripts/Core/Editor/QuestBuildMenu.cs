using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VRKart.Core.Editor
{
    // Quest 실기기 빌드 (#15). Builds/Android/VRKart.apk (.gitignore, 레포에 올리지 않음).
    // "설치·실행"은 USB로 연결된 Quest(개발자 모드 + USB 디버깅 허용)에 adb로 설치하고 바로 실행한다(Build And Run).
    // "개발 빌드"는 Unity 프로파일러를 USB로 붙일 수 있지만 조금 느리므로, fps 측정은 일반 빌드로 한다.
    public static class QuestBuildMenu
    {
        private const string OutputPath = "Builds/Android/VRKart.apk";

        [MenuItem("VRKart/빌드/Quest APK 빌드")]
        private static void Build() => BuildApk(BuildOptions.None);

        [MenuItem("VRKart/빌드/Quest APK 빌드 + 기기에 설치·실행")]
        private static void BuildAndRun() => BuildApk(BuildOptions.AutoRunPlayer);

        [MenuItem("VRKart/빌드/Quest 개발 빌드 + 설치·실행 (프로파일러)")]
        private static void BuildDevelopment() =>
            BuildApk(BuildOptions.AutoRunPlayer | BuildOptions.Development | BuildOptions.ConnectWithProfiler);

        private static void BuildApk(BuildOptions buildOptions)
        {
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                // URP 셰이더 정리가 활성 플랫폼 기준이라 Android로 먼저 바꾼다 (PcBuildMenu와 같은 이유)
                if (!EditorUtility.DisplayDialog("Quest 빌드", "활성 플랫폼을 Android로 바꿔야 합니다. 바꾼 뒤 다시 메뉴를 눌러 주세요.", "Android로 전환", "취소")) return;
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = buildOptions,
            };

            BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
            string sizeMb = (summary.totalSize / (1024f * 1024f)).ToString("0.0");
            Debug.Log($"[Quest 빌드] {summary.result} · {summary.totalTime.TotalMinutes:0.0}분 · {sizeMb}MB · 경고 {summary.totalWarnings} · 오류 {summary.totalErrors} → {Path.GetFullPath(OutputPath)}");
        }
    }
}
