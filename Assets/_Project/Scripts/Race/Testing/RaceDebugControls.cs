using UnityEngine;
using UnityEngine.InputSystem;
using VRKart.Core;

namespace VRKart.Race.Testing
{
    // 임시: HUD(#13)와 일시정지 메뉴(#12)가 나오기 전까지 RaceManager 이벤트를 콘솔로 확인하고 키보드로 조작한다.
    // F5 = 일시정지/재개, F6 = 재시작, F7 = 메뉴로 (알파벳·방향키는 XR Interaction Simulator가 사용 중)
    public sealed class RaceDebugControls : MonoBehaviour
    {
        [SerializeField] private RaceManager _raceManager;

        private void Awake()
        {
            if (_raceManager == null) _raceManager = FindAnyObjectByType<RaceManager>();
        }

        private void OnEnable()
        {
            if (_raceManager == null) return;
            _raceManager.OnCountdownTick += LogCountdown;
            _raceManager.OnLapCompleted += LogLap;
            _raceManager.OnParticipantFinished += LogParticipantFinished;
            _raceManager.OnRaceFinished += LogRaceFinished;
            _raceManager.PauseChanged += LogPause;
        }

        private void OnDisable()
        {
            if (_raceManager == null) return;
            _raceManager.OnCountdownTick -= LogCountdown;
            _raceManager.OnLapCompleted -= LogLap;
            _raceManager.OnParticipantFinished -= LogParticipantFinished;
            _raceManager.OnRaceFinished -= LogRaceFinished;
            _raceManager.PauseChanged -= LogPause;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _raceManager == null) return;

            if (keyboard.f5Key.wasPressedThisFrame) _raceManager.TogglePause();
            if (keyboard.f6Key.wasPressedThisFrame) _raceManager.Restart();
            if (keyboard.f7Key.wasPressedThisFrame) _raceManager.ExitToMenu();
        }

        private void LogCountdown(int n) => Debug.Log(n > 0 ? $"[Race] {n}" : "[Race] GO!");

        private void LogLap(IRaceParticipant participant, int lap, float lapTime) =>
            Debug.Log($"[Race] {Name(participant)} {lap}랩 {FormatTime(lapTime)}");

        private void LogParticipantFinished(IRaceParticipant participant, float totalTime) =>
            Debug.Log($"[Race] {Name(participant)} 완주 {FormatTime(totalTime)}");

        private void LogRaceFinished()
        {
            foreach (RaceResult result in _raceManager.GetResults())
                Debug.Log($"[Race] 결과 {result.Position}위 {result.Participant.name} {(result.IsFinished ? FormatTime(result.TotalTime) : "미완주")}");
        }

        private void LogPause(bool paused) => Debug.Log(paused ? "[Race] 일시정지" : "[Race] 재개");

        private static string Name(IRaceParticipant participant) => (participant as Component)?.name ?? "?";

        private static string FormatTime(float seconds) => $"{(int)(seconds / 60f)}:{seconds % 60f:00.000}";
    }
}
