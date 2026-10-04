using System;
using UnityEngine;

namespace VRKart.XR
{
    public enum ComfortPreset { Off, On }

    // 시야를 가리는 방식. WindowTint = 차 옆유리가 짙어짐(기본, 영상에서도 자연스러움),
    // Soft = 화면 가장자리를 반투명하게 어둡게, Black = 화면 가장자리를 검게(가장 강함)
    public enum ComfortVignetteStyle { WindowTint, Soft, Black }

    // 멀미 저감 설정 (FR-XR-06, 07). 회전·가감속할 때 주변 시야를 가린다(VignetteStyle 방식으로). 설정 UI(#12)는 setter만 부르고 화면을 닫을 때 Save()를 한 번 부른다.
    // 값이 바뀌면 Changed → 비네팅·수평 유지가 바로 반영된다. 저장 키는 GameSettings와 같은 "settings.…" 형식.
    public static class ComfortSettings
    {
        public const float DefaultVignetteIntensity = 0.6f;

        private const string KeyVignette = "settings.vignette.enabled";
        private const string KeyIntensity = "settings.vignette.intensity";
        private const string KeyHorizonLock = "settings.horizonLock";
        private const string KeyShowLabel = "settings.comfort.showLabel";
        private const string KeyStyle = "settings.vignette.style";

        private static bool _loaded;
        private static bool _vignetteEnabled;
        private static float _vignetteIntensity;
        private static bool _horizonLock;
        private static bool _showPresetLabel;
        private static ComfortVignetteStyle _vignetteStyle;

        public static event Action Changed;

        public static bool VignetteEnabled
        {
            get { Load(); return _vignetteEnabled; }
            set { Load(); if (_vignetteEnabled == value) return; _vignetteEnabled = value; Changed?.Invoke(); }
        }

        public static ComfortVignetteStyle VignetteStyle
        {
            get { Load(); return _vignetteStyle; }
            set { Load(); if (_vignetteStyle == value) return; _vignetteStyle = value; Changed?.Invoke(); }
        }

        // 0 ~ 1. 가장 크게 움직일 때 얼마나 가리는지
        public static float VignetteIntensity
        {
            get { Load(); return _vignetteIntensity; }
            set { Load(); value = Mathf.Clamp01(value); if (Mathf.Approximately(_vignetteIntensity, value)) return; _vignetteIntensity = value; Changed?.Invoke(); }
        }

        // 카트가 기울어져도 시야의 수평을 유지 (P2)
        public static bool HorizonLock
        {
            get { Load(); return _horizonLock; }
            set { Load(); if (_horizonLock == value) return; _horizonLock = value; Changed?.Invoke(); }
        }

        // 멀미 평가(#46)용: 현재 프리셋을 시야에 작게 계속 표시. 기본은 꺼짐 (전환 직후에는 잠깐 표시됨)
        public static bool ShowPresetLabel
        {
            get { Load(); return _showPresetLabel; }
            set { Load(); if (_showPresetLabel == value) return; _showPresetLabel = value; Changed?.Invoke(); }
        }

        // 편의 옵션이 하나라도 켜져 있으면 On
        public static ComfortPreset CurrentPreset => VignetteEnabled || HorizonLock ? ComfortPreset.On : ComfortPreset.Off;

        // 평가 실험용 한 번에 전환: Off = 비네팅·수평 유지 모두 끔, On = 모두 켬 (강도는 지금 값, 0이면 기본값)
        public static void ApplyPreset(ComfortPreset preset)
        {
            Load();
            bool on = preset == ComfortPreset.On;
            _vignetteEnabled = on;
            _horizonLock = on;
            if (on && _vignetteIntensity <= 0f) _vignetteIntensity = DefaultVignetteIntensity;
            Changed?.Invoke();
        }

        public static void Save()
        {
            Load();
            PlayerPrefs.SetInt(KeyVignette, _vignetteEnabled ? 1 : 0);
            PlayerPrefs.SetFloat(KeyIntensity, _vignetteIntensity);
            PlayerPrefs.SetInt(KeyHorizonLock, _horizonLock ? 1 : 0);
            PlayerPrefs.SetInt(KeyShowLabel, _showPresetLabel ? 1 : 0);
            PlayerPrefs.SetInt(KeyStyle, (int)_vignetteStyle);
            PlayerPrefs.Save();
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _vignetteEnabled = PlayerPrefs.GetInt(KeyVignette, 1) == 1;
            _vignetteIntensity = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyIntensity, DefaultVignetteIntensity));
            _horizonLock = PlayerPrefs.GetInt(KeyHorizonLock, 1) == 1;
            _showPresetLabel = PlayerPrefs.GetInt(KeyShowLabel, 0) == 1;
            int style = PlayerPrefs.GetInt(KeyStyle, (int)ComfortVignetteStyle.WindowTint);
            _vignetteStyle = System.Enum.IsDefined(typeof(ComfortVignetteStyle), style) ? (ComfortVignetteStyle)style : ComfortVignetteStyle.WindowTint;
        }

        // 도메인 리로드 없이 Play할 때 이전 값·구독이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _loaded = false;
            Changed = null;
        }
    }
}
