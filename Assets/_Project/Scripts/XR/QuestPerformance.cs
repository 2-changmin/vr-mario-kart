#if UNITY_ANDROID && !UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace VRKart.XR
{
    // Quest 실기기 성능 설정·측정 (#15). Quest(Android) 빌드에서만 들어간다.
    // 1) 고정 포비티드 렌더링(FFR): 시야 가장자리를 낮은 해상도로 그려 GPU 부담을 줄인다(OpenXR FoveatedRenderingFeature가 켜져 있어야 함).
    //    가운데(보는 곳)는 그대로라 눈에 띄는 변화는 적다. 단계는 FoveationLevel(0 = 끔 ~ 1 = 가장 강함).
    // 2) 성능 로그: 5초마다 평균 fps·가장 느린 프레임·목표(72fps) 못 맞춘 프레임 수를 logcat에 남긴다.
    //    PC에서 `adb logcat -s Unity` 로 보고, 트랙마다 숫자를 docs/TEST_CHECKLIST.md에 적는다.
    public sealed class QuestPerformance : MonoBehaviour
    {
        private const float FoveationLevel = 0.5f;
        private const float LogInterval = 5f;
        private const float TargetFps = 72f;

        private XRDisplaySubsystem _display;
        private bool _foveationApplied;
        private float _elapsed;
        private int _frames;
        private int _slowFrames;
        private float _worst;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject(nameof(QuestPerformance));
            DontDestroyOnLoad(go);
            go.AddComponent<QuestPerformance>();
        }

        private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;

        private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

        // 씬이 바뀌면 로딩 프레임이 섞이지 않게 측정을 새로 시작한다
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => ResetWindow();

        private void Update()
        {
            if (!_foveationApplied) TryApplyFoveation();

            float dt = Time.unscaledDeltaTime;
            _elapsed += dt;
            _frames++;
            if (dt > 1.5f / TargetFps) _slowFrames++;
            if (dt > _worst) _worst = dt;

            if (_elapsed < LogInterval) return;
            Debug.Log($"[Perf] {SceneManager.GetActiveScene().name} 평균 {_frames / _elapsed:0.0}fps · 가장 느린 프레임 {_worst * 1000f:0.0}ms · 목표 못 맞춘 프레임 {_slowFrames}/{_frames} · FFR {(_foveationApplied ? FoveationLevel.ToString("0.0") : "없음")}");
            ResetWindow();
        }

        private void TryApplyFoveation()
        {
            if (_display == null)
            {
                var displays = new List<XRDisplaySubsystem>();
                SubsystemManager.GetSubsystems(displays);
                foreach (XRDisplaySubsystem display in displays)
                    if (display.running) { _display = display; break; }
                if (_display == null) return;
            }
            _display.foveatedRenderingLevel = FoveationLevel;
            _display.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.None;
            _foveationApplied = true;
        }

        private void ResetWindow()
        {
            _elapsed = 0f;
            _frames = 0;
            _slowFrames = 0;
            _worst = 0f;
        }
    }
}
#endif
