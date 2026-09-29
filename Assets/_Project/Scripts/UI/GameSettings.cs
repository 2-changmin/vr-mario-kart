using UnityEngine;

namespace VRKart.UI
{
    // 저장되는 사용자 설정(PlayerPrefs). 비네팅 같은 멀미 저감 옵션은 #6 ComfortSettings가 맡는다.
    public static class GameSettings
    {
        private const string MasterVolumeKey = "settings.masterVolume";

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            set
            {
                float volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MasterVolumeKey, volume);
                AudioListener.volume = volume;
            }
        }

        // 슬라이더를 끄는 동안 매번 디스크에 쓰지 않도록, 설정 화면을 닫을 때 한 번 저장한다
        public static void Save() => PlayerPrefs.Save();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplySaved() => AudioListener.volume = MasterVolume;
    }
}
