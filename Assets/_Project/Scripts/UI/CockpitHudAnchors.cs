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
        [Tooltip("미니맵 자리 (#48). 비우면 앞유리 오른쪽 위(눈에서 오른쪽 24°, 위 7°, 0.62m)에 만든다")]
        [SerializeField] private Transform _map;

        public Transform Left => _left;
        public Transform Right => _right;

        public Transform Map
        {
            get
            {
                if (_map != null || _left == null) return _map;
                // 눈 위치(대시 화면 두 장의 방향으로 역산 ≈ (0, 1.05, -0.35)) 기준 오른쪽 24°, 위 7°, 0.62m
                _map = new GameObject("HudAnchor_Map").transform;
                _map.SetParent(_left.parent, false);
                _map.localRotation = Quaternion.Euler(-7f, 24f, 0f);
                _map.localPosition = new Vector3(0f, 1.05f, -0.35f) + _map.localRotation * Vector3.forward * 0.62f;
                _map.localScale = _left.localScale;
                return _map;
            }
        }

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
