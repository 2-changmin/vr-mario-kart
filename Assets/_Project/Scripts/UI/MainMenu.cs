using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRKart.Audio;
using VRKart.Race;
using VRKart.TimeAttack;
using VRKart.XR;

namespace VRKart.UI
{
    // 메인 메뉴(차고 로비, #66): 타이틀("트리거를 눌러 시작") → 트랙 카드(썸네일·길이·랩 수·내 타임어택 최고 기록, ◀ ▶로 고르기, #48)
    // / 모드 선택(그랑프리 / 타임어택, #47) / 시작 / 설정(전체·배경음악·효과음 볼륨, 멀미 저감 켬/끔·강도, 수평 유지, 고스트 표시) / 종료.
    // XR 트래킹이 잡힌 뒤(한 프레임 뒤) 플레이어 앞(옆으로 조금 비켜서, 턴테이블 위 차가 보이게)에 놓는다.
    public sealed class MainMenu : MonoBehaviour
    {
        // 트랙 카드 표시용 정보 (VRKart > 로비 > 차고 로비 다시 만들기 가 채움)
        [System.Serializable]
        public struct TrackCard
        {
            public string Scene;
            public Texture2D Thumbnail;
            public float LengthMeters;
            public int Laps;
        }

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

        [Header("트랙 선택 (#48) — _trackButton = 다음(▶)")]
        [SerializeField] private Button _trackButton;
        [SerializeField] private TMP_Text _trackText;
        [SerializeField] private Button _prevTrackButton;
        [SerializeField] private RawImage _trackThumbnail;
        [SerializeField] private TMP_Text _trackInfoText;
        [SerializeField] private TrackCard[] _cards;

        [Header("타이틀 (#66, 앱을 켰을 때 한 번)")]
        [SerializeField] private GameObject _titlePanel;
        [SerializeField] private Button _titleButton;
        [SerializeField] private TMP_Text _titleHint;

        [Header("타임어택 (TimeAttackSettings, #47)")]
        [SerializeField] private Button _modeButton;
        [SerializeField] private TMP_Text _modeText;
        [SerializeField] private Button _ghostButton;
        [SerializeField] private TMP_Text _ghostText;

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
        [SerializeField] private float _sideOffset;      // + = 오른쪽 (m)
        [SerializeField] private float _yawOffset;       // 플레이어 쪽으로 틀기 (도)

        // 고를 수 있는 트랙 (씬 이름, 메뉴 표시 이름). 메뉴로 돌아와도 마지막 선택을 기억한다.
        private static readonly (string Scene, string Label)[] Tracks =
        {
            (SceneLoader.TrackMain, "서킷"),
            (SceneLoader.TrackCampus, "동아대 캠퍼스"),
            (SceneLoader.TrackBridge, "광안대교 야간"),
            (SceneLoader.TrackCampusNight, "캠퍼스 야간"),
        };
        private static int s_trackIndex;
        private static bool s_titleShown;

        public bool IsSettingsOpen => _settingsPanel.activeSelf;

        public void ShowTitle()
        {
            s_titleShown = true;
            if (_titlePanel == null) { ShowMain(); return; }
            _titlePanel.SetActive(true);
            _mainPanel.SetActive(false);
            _settingsPanel.SetActive(false);
        }

        public void ShowMain()
        {
            if (_titlePanel != null) _titlePanel.SetActive(false);
            _mainPanel.SetActive(true);
            _settingsPanel.SetActive(false);
            UpdateTrackText();
            UpdateModeText();
        }

        public void ShowSettings()
        {
            _volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
            if (_musicSlider != null) _musicSlider.SetValueWithoutNotify(AudioVolumes.Music);
            if (_sfxSlider != null) _sfxSlider.SetValueWithoutNotify(AudioVolumes.Sfx);
            if (_comfortSlider != null) _comfortSlider.SetValueWithoutNotify(ComfortSettings.VignetteIntensity);
            UpdateVolumeText();
            UpdateComfortText();
            UpdateGhostText();
            if (_titlePanel != null) _titlePanel.SetActive(false);
            _mainPanel.SetActive(false);
            _settingsPanel.SetActive(true);
        }

        private IEnumerator Start()
        {
            if (!s_titleShown) ShowTitle(); else ShowMain();   // 레이스에서 돌아오면 바로 메뉴
            yield return null;
            if (PlayerSpace.PlaceInFront(transform, _distance, _heightOffset) && (_sideOffset != 0f || _yawOffset != 0f))
            {
                transform.position += transform.right * _sideOffset;
                transform.Rotate(0f, _yawOffset, 0f, Space.World);
            }
        }

        // 타이틀의 "트리거를 눌러 시작" 깜빡임
        private void Update()
        {
            if (_titleHint == null || _titlePanel == null || !_titlePanel.activeSelf) return;
            var c = _titleHint.color;
            c.a = 0.55f + 0.45f * Mathf.Cos(Time.unscaledTime * 3f);
            _titleHint.color = c;
        }

        private void OnEnable()
        {
            _startButton.onClick.AddListener(HandleStart);
            if (_trackButton != null) _trackButton.onClick.AddListener(NextTrack);
            if (_prevTrackButton != null) _prevTrackButton.onClick.AddListener(PreviousTrack);
            if (_titleButton != null) _titleButton.onClick.AddListener(ShowMain);
            if (_modeButton != null) _modeButton.onClick.AddListener(ToggleMode);
            _settingsButton.onClick.AddListener(ShowSettings);
            _quitButton.onClick.AddListener(HandleQuit);
            _backButton.onClick.AddListener(HandleBack);
            _volumeSlider.onValueChanged.AddListener(HandleVolumeChanged);
            if (_musicSlider != null) _musicSlider.onValueChanged.AddListener(HandleMusicChanged);
            if (_sfxSlider != null) _sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
            if (_comfortButton != null) _comfortButton.onClick.AddListener(ToggleComfort);
            if (_comfortSlider != null) _comfortSlider.onValueChanged.AddListener(HandleComfortChanged);
            if (_horizonButton != null) _horizonButton.onClick.AddListener(ToggleHorizon);
            if (_ghostButton != null) _ghostButton.onClick.AddListener(ToggleGhost);
        }

        private void OnDisable()
        {
            _startButton.onClick.RemoveListener(HandleStart);
            if (_trackButton != null) _trackButton.onClick.RemoveListener(NextTrack);
            if (_prevTrackButton != null) _prevTrackButton.onClick.RemoveListener(PreviousTrack);
            if (_titleButton != null) _titleButton.onClick.RemoveListener(ShowMain);
            if (_modeButton != null) _modeButton.onClick.RemoveListener(ToggleMode);
            _settingsButton.onClick.RemoveListener(ShowSettings);
            _quitButton.onClick.RemoveListener(HandleQuit);
            _backButton.onClick.RemoveListener(HandleBack);
            _volumeSlider.onValueChanged.RemoveListener(HandleVolumeChanged);
            if (_musicSlider != null) _musicSlider.onValueChanged.RemoveListener(HandleMusicChanged);
            if (_sfxSlider != null) _sfxSlider.onValueChanged.RemoveListener(HandleSfxChanged);
            if (_comfortButton != null) _comfortButton.onClick.RemoveListener(ToggleComfort);
            if (_comfortSlider != null) _comfortSlider.onValueChanged.RemoveListener(HandleComfortChanged);
            if (_horizonButton != null) _horizonButton.onClick.RemoveListener(ToggleHorizon);
            if (_ghostButton != null) _ghostButton.onClick.RemoveListener(ToggleGhost);
        }

        // 트랙 선택 버튼이 없으면 Race Scene 그대로
        private void HandleStart() => SceneLoader.Load(_trackButton != null ? Tracks[s_trackIndex].Scene : _raceScene);

        private void NextTrack()
        {
            s_trackIndex = (s_trackIndex + 1) % Tracks.Length;
            UpdateTrackText();
        }

        private void PreviousTrack()
        {
            s_trackIndex = (s_trackIndex + Tracks.Length - 1) % Tracks.Length;
            UpdateTrackText();
        }

        // 카드 정보가 있으면 이름만(카드 형태), 없으면 예전처럼 "트랙: 이름"
        private void UpdateTrackText()
        {
            var track = Tracks[s_trackIndex];
            int card = -1;
            if (_cards != null) for (int i = 0; i < _cards.Length; i++) if (_cards[i].Scene == track.Scene) { card = i; break; }
            if (_trackText != null) _trackText.text = card >= 0 ? track.Label : "트랙: " + track.Label;
            if (card < 0) return;
            if (_trackThumbnail != null) _trackThumbnail.texture = _cards[card].Thumbnail;
            if (_trackInfoText != null)
            {
                var record = TimeAttackRecord.Load(track.Scene);
                string best = record != null && record.BestTotal > 0f ? "최고 " + TimeFormat.Format(record.BestTotal) : "기록 없음";
                _trackInfoText.text = $"{_cards[card].LengthMeters / 1000f:0.0}km  ·  {_cards[card].Laps}랩  ·  {best}";
            }
        }

        // 그랑프리(AI와 레이스) ↔ 타임어택(혼자 주행 + 기록·고스트). 선택은 트랙과 같이 메뉴로 돌아와도 유지된다
        private void ToggleMode()
        {
            TimeAttackSettings.IsTimeAttack = !TimeAttackSettings.IsTimeAttack;
            UpdateModeText();
        }

        private void UpdateModeText()
        {
            if (_modeText != null) _modeText.text = TimeAttackSettings.IsTimeAttack ? "모드: 타임어택" : "모드: 그랑프리";
        }

        private void HandleBack()
        {
            GameSettings.Save();
            ComfortSettings.Save();
            TimeAttackSettings.Save();
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

        private void ToggleGhost()
        {
            TimeAttackSettings.ShowGhost = !TimeAttackSettings.ShowGhost;
            UpdateGhostText();
        }

        private void UpdateGhostText()
        {
            if (_ghostText != null) _ghostText.text = TimeAttackSettings.ShowGhost ? "켬" : "끔";
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
