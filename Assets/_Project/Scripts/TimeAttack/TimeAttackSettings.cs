using UnityEngine;

namespace VRKart.TimeAttack
{
    // 타임어택 모드 선택과 고스트 표시 설정 (#47). 메인 메뉴(UI)는 setter만 부르고, 설정 화면을 닫을 때 Save()를 한 번 부른다.
    // 모드는 메뉴 ↔ 트랙 씬을 오가는 동안만 기억하고(트랙 선택과 같음), 고스트 표시는 PlayerPrefs에 저장한다.
    public static class TimeAttackSettings
    {
        private const string KeyShowGhost = "settings.timeAttack.showGhost";

        private static bool _loaded;
        private static bool _showGhost;

        // true면 다음에 여는 트랙 씬이 타임어택(AI·아이템 박스 없이 혼자 주행, 기록·고스트 저장)
        public static bool IsTimeAttack { get; set; }

        // 타임어택에서 최고 기록 주행을 반투명 차로 같이 달리게 할지
        public static bool ShowGhost
        {
            get { Load(); return _showGhost; }
            set { Load(); _showGhost = value; PlayerPrefs.SetInt(KeyShowGhost, value ? 1 : 0); }
        }

        public static void Save() => PlayerPrefs.Save();

        // 에디터에서 도메인 리로드를 끄고 Play해도 이전 Play의 모드가 남지 않도록
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsTimeAttack = false;
            _loaded = false;
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _showGhost = PlayerPrefs.GetInt(KeyShowGhost, 1) == 1;
        }
    }
}
