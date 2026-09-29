using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRKart.Race;

namespace VRKart.UI
{
    // 메인 메뉴: 시작 / 설정(볼륨) / 종료. XR 트래킹이 잡힌 뒤(한 프레임 뒤) 플레이어 정면에 놓는다.
    public sealed class MainMenu : MonoBehaviour
    {
        [SerializeField] private string _raceScene = SceneLoader.TrackTest;
        [SerializeField] private GameObject _mainPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _quitButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private Slider _volumeSlider;
        [SerializeField] private TMP_Text _volumeValueText;

        [Header("배치")]
        [SerializeField, Min(0.5f)] private float _distance = 1.5f;
        [SerializeField] private float _heightOffset = -0.1f;

        public bool IsSettingsOpen => _settingsPanel.activeSelf;

        public void ShowMain()
        {
            _mainPanel.SetActive(true);
            _settingsPanel.SetActive(false);
        }

        public void ShowSettings()
        {
            _volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
            UpdateVolumeText();
            _mainPanel.SetActive(false);
            _settingsPanel.SetActive(true);
        }

        private IEnumerator Start()
        {
            ShowMain();
            yield return null;
            PlayerSpace.PlaceInFront(transform, _distance, _heightOffset);
        }

        private void OnEnable()
        {
            _startButton.onClick.AddListener(HandleStart);
            _settingsButton.onClick.AddListener(ShowSettings);
            _quitButton.onClick.AddListener(HandleQuit);
            _backButton.onClick.AddListener(HandleBack);
            _volumeSlider.onValueChanged.AddListener(HandleVolumeChanged);
        }

        private void OnDisable()
        {
            _startButton.onClick.RemoveListener(HandleStart);
            _settingsButton.onClick.RemoveListener(ShowSettings);
            _quitButton.onClick.RemoveListener(HandleQuit);
            _backButton.onClick.RemoveListener(HandleBack);
            _volumeSlider.onValueChanged.RemoveListener(HandleVolumeChanged);
        }

        private void HandleStart() => SceneLoader.Load(_raceScene);

        private void HandleBack()
        {
            GameSettings.Save();
            ShowMain();
        }

        private void HandleVolumeChanged(float value)
        {
            GameSettings.MasterVolume = value;
            UpdateVolumeText();
        }

        private void UpdateVolumeText() => _volumeValueText.text = Mathf.RoundToInt(_volumeSlider.value * 100f) + "%";

        private void HandleQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
