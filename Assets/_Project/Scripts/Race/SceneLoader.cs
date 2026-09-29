using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRKart.Race
{
    // 씬은 Build Profiles의 Scene List에 등록돼 있어야 로드된다.
    public static class SceneLoader
    {
        public const string MainMenu = "MainMenu";
        public const string TrackMain = "Track_Main";
        public const string TrackTest = "Track_Test";

        public static bool Load(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"[SceneLoader] '{sceneName}' 씬이 Build Profiles의 Scene List에 없어 불러올 수 없습니다.");
                return false;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
            return true;
        }

        public static bool LoadMainMenu() => Load(MainMenu);

        public static bool ReloadCurrent() => Load(SceneManager.GetActiveScene().name);
    }
}
