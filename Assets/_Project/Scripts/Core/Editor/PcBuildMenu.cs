using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VRKart.Core.Editor
{
    // PC 점검용 Windows 빌드 (#80). 에디터 없이 로비 → 레이스 → 결과를 점검·시연하기 위한 실행 파일.
    // 헤드셋이 없으면 PC 운전 모드(키보드·마우스)가 자동으로 켜지고, Windows 기본 품질 단계는 PC(시연 화질)다.
    // 결과물은 레포 밖 취급인 Builds/Windows/ 에 생긴다(.gitignore). 빌드가 끝나면 원래 플랫폼(Android)으로 되돌린다.
    [InitializeOnLoad]
    public static class PcBuildMenu
    {
        private const string OutputDir = "Builds/Windows";
        private const string ExeName = "VRKart.exe";
        private const string RestoreKey = "VRKart.PcBuild.RestoreTarget";

        // 빌드 직후 바로 되돌리면 플랫폼 전환이 무시될 때가 있어서(전환으로 예약된 스크립트 컴파일 중),
        // 되돌릴 플랫폼을 저장해 두고 컴파일·도메인 리로드가 끝난 뒤 다시 확인한다.
        static PcBuildMenu() => EditorApplication.delayCall += RestoreTargetIfNeeded;

        [MenuItem("VRKart/빌드/PC 점검용 빌드 (Windows)")]
        private static void BuildWindows()
        {
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            BuildTarget original = EditorUserBuildSettings.activeBuildTarget;
            if (original != BuildTarget.StandaloneWindows64) EditorPrefs.SetInt(RestoreKey, (int)original);

            // 먼저 Windows로 전환한다. URP는 빌드할 때 "활성 플랫폼의 품질 단계에서 쓰는 기능"만 남기고 셰이더·리소스를 빼는데,
            // Android(Mobile 단계 = SSAO·후처리 없음)인 채로 빌드하면 PC 단계의 SSAO·후처리 리소스가 빠져서 매 프레임 렌더링 오류가 난다.
            if (original != BuildTarget.StandaloneWindows64)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Path.Combine(OutputDir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            string sizeMb = (summary.totalSize / (1024f * 1024f)).ToString("0.0");
            Debug.Log($"[PC 빌드] {summary.result} · {summary.totalTime.TotalMinutes:0.0}분 · {sizeMb}MB · 경고 {summary.totalWarnings} · 오류 {summary.totalErrors} → {Path.GetFullPath(options.locationPathName)}");

            RestoreTargetIfNeeded();
            if (summary.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(options.locationPathName);
        }

        private static void RestoreTargetIfNeeded()
        {
            if (!EditorPrefs.HasKey(RestoreKey) || BuildPipeline.isBuildingPlayer) return;
            var target = (BuildTarget)EditorPrefs.GetInt(RestoreKey);
            if (EditorUserBuildSettings.activeBuildTarget == target)
            {
                EditorPrefs.DeleteKey(RestoreKey);
                return;
            }
            // 키는 실제로 바뀐 것을 다음 확인에서 본 뒤에 지운다(전환 요청이 무시되면 리로드 후 다시 시도)
            Debug.Log($"[PC 빌드] 플랫폼을 {target}(으)로 되돌립니다.");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target);
        }
    }
}
