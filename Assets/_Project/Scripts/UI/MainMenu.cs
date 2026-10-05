using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRKart.Audio;
using VRKart.Race;
using VRKart.XR;

namespace VRKart.UI
{
    // 메인 메뉴: 트랙 선택(서킷 / 동아대 캠퍼스, #48) / 시작 / 설정(전체·배경음악·효과음 볼륨, 멀미 저감 켬/끔·강도, 수평 유지) / 종료. XR 트래킹이 잡힌 뒤(한 프레임 뒤) 플레이어 정면에 놓는다.
    public sealed class MainMenu : MonoBehaviour
    {
        [SerializeField] private string _raceScene = SceneLoader.TrackMain;
        [SerializeField] private GameObject _mainPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _quitButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private Slider _volumeSlider;
        [SerializeField] private TMP_Text _volumeValueText;
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private TMP_Text _musicValueText;
        [SerializeField] private Slider _sfxSlider;
        [SerializeField] private TMP_Text _sfxValueText;

        [Header("트랙 선택 (#48)")]
        [SerializeField] private Button _trackButton;
        [SerializeField] private TMP_Text _trackText;

        [Header("멀미 저감 (ComfortSettings, #6)")]
        [SerializeField] private Button _comfortButton;
        [SerializeField] private TMP_Text _comfortText;
        [SerializeField] private Slider _comfortSlider;
        [SerializeField] private TMP_Text _comfortValueText;
        [SerializeField] private Button _horizonButton;
        [SerializeField] private TMP_Text _horizonText;

        [Header("배치")]
        [SerializeField, Min(0.5f)] private float _distance = 1.5f;
        [SerializeField] private float _heightOffset = -0.1f;

        // 고를 수 있는 트랙 (씬 이름, 메뉴 표시 이름). 메뉴로 돌아와도 마지막 선택을 기억한다.
        private static readonly (string Scene, string Label)[] Tracks =
        {
            (SceneLoader.TrackMain, "서킷"),
            (SceneLoader.TrackCampus, "동아대 캠퍼스"),
        };
        private static int s_trackIndex;

        public bool IsSettingsOpen => _settingsPanel.activeSelf;

        public void ShowMain()
        {
            _mainPanel.SetActive(true);
            _settingsPanel.SetActive(false);
            UpdateTrackText();
        }

        public void ShowSettings()
        {
            _volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
            if (_musicSlider != null) _musicSlider.SetValueWithoutNotify(AudioVolumes.Music);
            if (_sfxSlider != null) _sfxSlider.SetValueWithoutNotify(AudioVolumes.Sfx);
            if (_comfortSlider != null) _comfortSlider.SetValueWithoutNotify(ComfortSettings.VignetteIntensity);
            UpdateVolumeText();
            UpdateComfortText();
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
            if (_trackButton != null) _trackButton.onClick.AddListener(NextTrack);
            _settingsButton.onClick.AddListener(ShowSettings);
            _quitButton.onClick.AddListener(HandleQuit);
            _backButton.onClick.AddListener(HandleBack);
            _volumeSlider.onValueChanged.AddListener(HandleVolumeChanged);
            if (_musicSlider != null) _musicSlider.onValueChanged.AddListener(HandleMusicChanged);
            if (_sfxSlider != null) _sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
            if (_comfortButton != null) _comfortButton.onClick.AddListener(ToggleComfort);
            if (_comfortSlider != null) _comfortSlider.onValueChanged.AddListener(HandleComfortChanged);
            if (_horizonButton != null) _horizonButton.onClick.AddListener(ToggleHorizon);
        }

        private void OnDisable()
        {
            _startButton.onClick.RemoveListener(HandleStart);
            if (_trackButton != null) _trackButton.onClick.RemoveListener(NextTrack);
            _settingsButton.onClick.RemoveListener(ShowSettings);
            _quitButton.onClick.RemoveListener(HandleQuit);
            _backButton.onClick.RemoveListener(HandleBack);
            _volumeSlider.onValueChanged.RemoveListener(HandleVolumeChanged);
            if (_musicSlider != null) _musicSlider.onValueChanged.RemoveListener(HandleMusicChanged);
            if (_sfxSlider != null) _sfxSlider.onValueChanged.RemoveListener(HandleSfxChanged);
            if (_comfortButton != null) _comfortButton.onClick.RemoveListener(ToggleComfort);
            if (_comfortSlider != null) _comfortSlider.onValueChanged.RemoveListener(HandleComfortChanged);
            if (_horizonButton != null) _horizonButton.onClick.RemoveListener(ToggleHorizon);
        }

        // 트랙 선택 버튼이 없으면 Race Scene 그대로
        private void HandleStart() => SceneLoader.Load(_trackButton != null ? Tracks[s_trackIndex].Scene : _raceScene);

        private void NextTrack()
        {
            s_trackIndex = (s_trackIndex + 1) % Tracks.Length;
            UpdateTrackText();
        }

        private void UpdateTrackText()
        {
            if (_trackText != null) _trackText.text = "트랙: " + Tracks[s_trackIndex].Label;
        }

        private void HandleBack()
        {
            GameSettings.Save();
            ComfortSettings.Save();
            ShowMain();
        }

        private void HandleVolumeChanged(float value)
        {
            GameSettings.MasterVolume = value;
            UpdateVolumeText();
        }

        private void HandleMusicChanged(float value)
        {
            AudioVolumes.Music = value;
            UpdateVolumeText();
        }

        private void HandleSfxChanged(float value)
        {
            AudioVolumes.Sfx = value;
            UpdateVolumeText();
        }

        private void UpdateVolumeText()
        {
            _volumeValueText.text = Percent(_volumeSlider);
            if (_musicValueText != null) _musicValueText.text = Percent(_musicSlider);
            if (_sfxValueText != null) _sfxValueText.text = Percent(_sfxSlider);
        }

        private static string Percent(Slider slider) => Mathf.RoundToInt(slider.value * 100f) + "%";

        private void ToggleComfort()
        {
            ComfortSettings.VignetteEnabled = !ComfortSettings.VignetteEnabled;
            UpdateComfortText();
        }

        private void ToggleHorizon()
        {
            ComfortSettings.HorizonLock = !ComfortSettings.HorizonLock;
            UpdateComfortText();
        }

        private void HandleComfortChanged(float value)
        {
            ComfortSettings.VignetteIntensity = value;
            UpdateComfortText();
        }

        // 멀미 저감이 꺼져 있으면 강도 슬라이더도 비활성
        private void UpdateComfortText()
        {
            if (_comfortText != null) _comfortText.text = ComfortSettings.VignetteEnabled ? "켬" : "끔";
            if (_horizonText != null) _horizonText.text = ComfortSettings.HorizonLock ? "켬" : "끔";
            if (_comfortSlider != null) _comfortSlider.interactable = ComfortSettings.VignetteEnabled;
            if (_comfortValueText != null && _comfortSlider != null) _comfortValueText.text = Percent(_comfortSlider);
        }

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
