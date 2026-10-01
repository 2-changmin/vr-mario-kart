using UnityEngine;

namespace VRKart.UI
{
    // 조종석 실내에서 HUD 대시보드 화면이 붙을 자리. RaceHud가 플레이어 카트에서 이 컴포넌트를 찾으면
    // DashLeft/DashRight를 앵커의 자식으로 옮긴다(앵커 forward = 눈 → 화면 방향, 앵커 스케일 = 화면 크기 배율).
    // 화면 위치를 바꾸려면 앵커만 옮기면 된다. (디자인 규칙: 이슈 #40)
    public sealed class CockpitHudAnchors : MonoBehaviour
    {
        [SerializeField] private Transform _left;
        [SerializeField] private Transform _right;

        public Transform Left => _left;
        public Transform Right => _right;

        // 패널을 앵커에 딱 맞춘다. 패널 원래 스케일(0.001)에 앵커 스케일이 곱해진다.
        public static void Dock(Transform panel, Transform anchor)
        {
            if (panel == null || anchor == null) return;
            panel.SetParent(anchor, false);
            panel.localPosition = Vector3.zero;
            panel.localRotation = Quaternion.identity;
            if (panel is RectTransform rect) rect.anchoredPosition3D = Vector3.zero;
        }
    }
}
