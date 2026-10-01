using System;
using UnityEngine;

namespace VRKart.Audio
{
    // 배경음악 / 효과음 볼륨(0~1, PlayerPrefs). 전체 볼륨은 GameSettings.MasterVolume(= AudioListener.volume).
    // 소리를 내는 컴포넌트는 기본 볼륨 × 이 값으로 재생하고, Changed가 오면 다시 적용한다.
    public static class AudioVolumes
    {
        private const string MusicKey = "settings.musicVolume";
        private const string SfxKey = "settings.sfxVolume";

        public static event Action Changed;

        public static float Music
        {
            get => PlayerPrefs.GetFloat(MusicKey, 0.8f);
            set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); Changed?.Invoke(); }
        }

        public static float Sfx
        {
            get => PlayerPrefs.GetFloat(SfxKey, 1f);
            set { PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value)); Changed?.Invoke(); }
        }
    }
}
