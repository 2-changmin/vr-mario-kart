using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRKart.Race
{
    // 씬은 Build Profiles의 Scene List에 등록돼 있어야 로드된다. 실행 중에는 SceneFader로 페이드하며 비동기 로드한다(#66).
    public static class SceneLoader
    {
        public const string MainMenu = "MainMenu";
        public const string TrackMain = "Track_Main";
        public const string TrackTest = "Track_Test";
        public const string TrackCampus = "Track_Campus";
        public const string TrackBridge = "Track_Bridge";
        public const string TrackCampusNight = "Track_CampusNight";

        public static bool Load(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"[SceneLoader] '{sceneName}' 씬이 Build Profiles의 Scene List에 없어 불러올 수 없습니다.");
                return false;
            }

            Time.timeScale = 1f;
            if (Application.isPlaying) SceneFader.Load(sceneName);   // 어둡게 → 비동기 로드 → 밝게 (#66)
            else SceneManager.LoadScene(sceneName);
            return true;
        }

        public static bool LoadMainMenu() => Load(MainMenu);

        public static bool ReloadCurrent() => Load(SceneManager.GetActiveScene().name);
    }
}
