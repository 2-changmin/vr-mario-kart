using UnityEngine;

namespace VRKart.XR
{
    // 시점 설정 (#64, FR-XR-08). 1인칭(운전석) / 3인칭(차 뒤·위). 기본은 1인칭.
    // 설정 UI(MainMenu)는 setter만 부르고 화면을 닫을 때 Save()를 한 번 부른다. 레이스 중에는 바꾸지 않고, 다음 출발부터 적용된다(ViewModeSwitcher).
    public static class ViewSettings
    {
        private const string KeyThirdPerson = "settings.view.thirdPerson";

        private static bool _loaded;
        private static bool _thirdPerson;

        public static bool ThirdPerson
        {
            get { Load(); return _thirdPerson; }
            set { Load(); _thirdPerson = value; PlayerPrefs.SetInt(KeyThirdPerson, value ? 1 : 0); }
        }

        public static void Save() => PlayerPrefs.Save();

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _thirdPerson = PlayerPrefs.GetInt(KeyThirdPerson, 0) == 1;
        }

        // 에디터에서 도메인 리로드를 끄고 Play해도 저장값을 다시 읽도록
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _loaded = false;
    }
}
