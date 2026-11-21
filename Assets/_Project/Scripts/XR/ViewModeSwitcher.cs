using UnityEngine;

namespace VRKart.XR
{
    // 1인칭 / 3인칭 시점 전환 (#64). Kart_Player 루트에 붙고, 시작할 때 ViewSettings.ThirdPerson에 맞춰 한 번 적용한다.
    // 3인칭: 조종석(실내·핸들·유리 틴트·HUD 앵커)과 1인칭용 차 껍데기를 끄고 완성된 외형(ExteriorCar)을 켠 뒤,
    // 리센터 기준을 차 뒤·위의 ChaseEye로 바꾼다. 카메라는 ViewPivot 아래에서 카트에 단단히 붙어 따라간다(지연·흔들림 없음, NFR-03).
    // HUD는 조종석 앵커(CockpitHudAnchors)가 꺼져 있으면 알아서 시야 앞 패널 배치를 쓰므로 HUD 코드는 그대로다.
    // RaceHud(카운트다운 때 배치)·ViewRecenter(Start)보다 먼저 적용되도록 실행 순서를 앞당긴다.
    [DefaultExecutionOrder(-200)]
    public sealed class ViewModeSwitcher : MonoBehaviour
    {
        [SerializeField] private ViewRecenter _recenter;
        [SerializeField] private Transform _seatEye;       // 1인칭 눈 위치 (Cockpit/SeatEye)
        [SerializeField] private Transform _chaseEye;      // 3인칭 눈 위치 (차 뒤·위, forward = 카트 정면)
        [SerializeField] private GameObject[] _firstPersonOnly;   // Cockpit, 1인칭용 Car
        [SerializeField] private GameObject[] _thirdPersonOnly;   // ExteriorCar

        public static bool IsThirdPerson { get; private set; }

        private void Awake()
        {
            if (_recenter == null) _recenter = GetComponent<ViewRecenter>();
            Apply(ViewSettings.ThirdPerson);
        }

        private void OnDestroy() => IsThirdPerson = false;

        public void Apply(bool thirdPerson)
        {
            IsThirdPerson = thirdPerson;
            foreach (GameObject go in _firstPersonOnly) if (go != null) go.SetActive(!thirdPerson);
            foreach (GameObject go in _thirdPersonOnly) if (go != null) go.SetActive(thirdPerson);
            if (_recenter != null) _recenter.SetEye(thirdPerson ? _chaseEye : _seatEye);
        }
    }
}
