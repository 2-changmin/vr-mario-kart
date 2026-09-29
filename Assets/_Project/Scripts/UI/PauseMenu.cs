using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VRKart.Race;

namespace VRKart.UI
{
    // 일시정지 메뉴. 왼손 컨트롤러 메뉴(≡) 버튼으로 일시정지를 토글하고, 일시정지되면 플레이어 정면에 뜬다.
    // 표시는 RaceManager.PauseChanged만 따르므로 다른 곳에서 Pause()를 불러도 똑같이 뜬다.
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] private RaceManager _raceManager;
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _menuButton;

        [Header("배치")]
        [SerializeField, Min(0.5f)] private float _distance = 1.5f;
        [SerializeField] private float _heightOffset = -0.15f;

        private InputAction _pauseAction;

        public bool IsShown => _panel.activeSelf;

        private void Awake()
        {
            if (_raceManager == null) _raceManager = FindAnyObjectByType<RaceManager>();
            _panel.SetActive(false);

            // XRI 기본 입력 액션에 메뉴 버튼이 없어서 직접 만든다 (Quest Touch 왼손 ≡ 버튼)
            _pauseAction = new InputAction("Pause", InputActionType.Button);
            _pauseAction.AddBinding("<XRController>{LeftHand}/{MenuButton}");
            _pauseAction.AddBinding("<XRController>{LeftHand}/menu");
        }

        private void OnEnable()
        {
            _raceManager.PauseChanged += HandlePauseChanged;
            _pauseAction.performed += HandlePausePressed;
            _pauseAction.Enable();
            _resumeButton.onClick.AddListener(_raceManager.Resume);
            _restartButton.onClick.AddListener(_raceManager.Restart);
            _menuButton.onClick.AddListener(_raceManager.ExitToMenu);
        }

        private void OnDisable()
        {
            _raceManager.PauseChanged -= HandlePauseChanged;
            _pauseAction.performed -= HandlePausePressed;
            _pauseAction.Disable();
            _resumeButton.onClick.RemoveListener(_raceManager.Resume);
            _restartButton.onClick.RemoveListener(_raceManager.Restart);
            _menuButton.onClick.RemoveListener(_raceManager.ExitToMenu);
        }

        private void OnDestroy() => _pauseAction.Dispose();

        private void HandlePausePressed(InputAction.CallbackContext context) => _raceManager.TogglePause();

        private void HandlePauseChanged(bool paused)
        {
            if (paused) PlayerSpace.PlaceInFront(transform, _distance, _heightOffset);
            _panel.SetActive(paused);
        }
    }
}
