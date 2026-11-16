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

        private static void Set(string name)
        {
            int index = System.Array.IndexOf(QualitySettings.names, name);
            if (index < 0) return;
            QualitySettings.SetQualityLevel(index, true);
            Debug.Log($"[화질] {name} 단계로 전환. Play를 다시 시작하면 적용됩니다.");
        }
    }
}
