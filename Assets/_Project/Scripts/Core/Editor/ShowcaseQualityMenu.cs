using UnityEditor;
using UnityEngine;

namespace VRKart.Core.Editor
{
    // 에디터에서 화질 단계 전환 (#67). PC 시연 화질 = 후처리·고해상도 그림자·SSAO(영상 녹화용), Quest 화질 = 기본(Mobile).
    // 현재 단계는 ProjectSettings/QualitySettings.asset에 저장되므로 커밋 전에 Quest 화질로 되돌릴 것.
    public static class ShowcaseQualityMenu
    {
        [MenuItem("VRKart/화질/PC 시연 화질 (후처리 켬, 녹화용)")]
        private static void Pc() => Set("PC");

        [MenuItem("VRKart/화질/Quest 화질 (기본)")]
        private static void Mobile() => Set("Mobile");

        // 열린 트랙 씬에 PC 시연용 조명(PcLightingOverride)을 붙인다. 값은 인스펙터에서 씬마다 조정
        [MenuItem("VRKart/화질/열린 씬에 PC 늦은 오후 조명 추가")]
        private static void AddLighting()
        {
            if (Object.FindAnyObjectByType<PcLightingOverride>() != null) { Debug.Log("[화질] 이미 PcLightingOverride가 있습니다."); return; }
            var go = new GameObject("PcLighting");
            go.AddComponent<PcLightingOverride>();
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "PC 조명 추가");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
        }

        private static void Set(string name)
        {
            int index = System.Array.IndexOf(QualitySettings.names, name);
            if (index < 0) return;
            QualitySettings.SetQualityLevel(index, true);
            Debug.Log($"[화질] {name} 단계로 전환. Play를 다시 시작하면 적용됩니다.");
        }
    }
}
