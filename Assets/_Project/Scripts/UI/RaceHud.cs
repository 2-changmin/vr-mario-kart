using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRKart.Core;
using VRKart.Items;
using VRKart.Race;
using VRKart.TimeAttack;

namespace VRKart.UI
{
    // 인게임 HUD 루트. 카운트다운이 시작될 때 플레이어 눈 위치에 배치되고(PlayerSpace), 일시정지 중과 플레이어 완주 후에는 숨긴다.
    // 자식의 대시보드(눈앞 아래)와 가운데 메시지(RaceMessages)는 루트 기준 위치에 미리 놓여 있다.
    // 플레이어 카트에 CockpitHudAnchors(조종석 화면 자리)가 있으면 대시보드 두 장을 그 자리로 옮긴다.
    // 타임어택(#47)에서는 순위가 늘 1위라 순위 칸에 이 트랙의 최고 기록을 대신 보여 준다.
    public sealed class RaceHud : MonoBehaviour
    {
        [SerializeField] private RaceManager _raceManager;
        [SerializeField] private GameObject _content;
        [SerializeField] private Transform _dashLeft;
        [SerializeField] private Transform _dashRight;

        [Header("대시보드")]
        [SerializeField] private TMP_Text _lapText;
        [SerializeField] private TMP_Text _rankText;
        [SerializeField] private TMP_Text _speedText;
        [SerializeField] private TMP_Text _timeText;

        [Header("아이템 칸 (플레이어 ItemHolder)")]
        [SerializeField] private Image _itemIcon;
        [SerializeField] private Sprite _boosterIcon;
        [SerializeField] private Sprite _bananaIcon;
        [SerializeField] private Sprite _shellIcon;
        [SerializeField, Min(1f)] private float _itemPopScale = 1.35f;   // 아이템을 받으면 잠깐 커졌다가 돌아옴

        private RaceProgress _player;
        private IKart _playerKart;
        private int _participantCount;
        private int _shownLap = -1;
        private int _shownRank = -1;
        private int _shownSpeed = -1;
        private int _shownTenths = -1;
        private bool _docked;
        private RaceMinimap _minimap;
        private ItemHolder _playerItems;
        private float _itemPop;
        private TimeAttackSession _timeAttack;

        public bool IsShown => _content.activeSelf;

        public void Show()
        {
            PlayerSpace.PlaceInFront(transform, 0f, 0f);
            DockToCockpit();
            EnsureMinimap();
            SetVisible(true);
            Refresh(true);
        }

        public void Hide() => SetVisible(false);

        private void Awake()
        {
            if (_raceManager == null) _raceManager = FindAnyObjectByType<RaceManager>();
            _content.SetActive(false);
        }

        // 조종석 화면 자리가 있으면 대시보드를 옮긴다. 옮긴 뒤에는 _content 밖에 있으므로 보이기/숨기기를 따로 한다.
        private void DockToCockpit()
        {
            if (_docked || transform.root == transform) return;
            var anchors = transform.root.GetComponentInChildren<CockpitHudAnchors>();
            if (anchors == null) return;
            CockpitHudAnchors.Dock(_dashLeft, anchors.Left);
            CockpitHudAnchors.Dock(_dashRight, anchors.Right);
            _docked = true;
        }

        // 미니맵 (#48): 조종석이 있으면 앞유리 오른쪽 위, 없으면 HUD 안 오른쪽 위
        private void EnsureMinimap()
        {
            if (_minimap != null) return;
            var anchors = transform.root != transform ? transform.root.GetComponentInChildren<CockpitHudAnchors>() : null;
            if (anchors != null && anchors.Map != null)
            {
                _minimap = RaceMinimap.Create(anchors.Map, _raceManager);
            }
            else
            {
                _minimap = RaceMinimap.Create(_content.transform, _raceManager, 0.0016f);
                _minimap.transform.localPosition = new Vector3(0.75f, 0.42f, 2f);
            }
        }

        private void SetVisible(bool visible)
        {
            _content.SetActive(visible);
            if (_minimap != null && _minimap.transform.parent != _content.transform) _minimap.gameObject.SetActive(visible);
            if (!_docked) return;
            if (_dashLeft != null) _dashLeft.gameObject.SetActive(visible);
            if (_dashRight != null) _dashRight.gameObject.SetActive(visible);
        }

        private void Start()
        {
            _player = _raceManager.Player;
            _playerKart = _player != null ? _player.GetComponent<IKart>() : null;
            _participantCount = _raceManager.GetResults().Count;
            _timeAttack = TimeAttackSession.Current;
            _playerItems = _player != null ? _player.GetComponent<ItemHolder>() : null;
            if (_playerItems != null) _playerItems.ItemChanged += ShowItem;
            ShowItem(_playerItems != null ? _playerItems.CurrentItem : ItemType.None, pop: false);
        }

        private void OnDestroy()
        {
            if (_playerItems != null) _playerItems.ItemChanged -= ShowItem;
        }

        private void ShowItem(ItemType item) => ShowItem(item, pop: true);

        private void ShowItem(ItemType item, bool pop)
        {
            if (_itemIcon == null) return;
            Sprite sprite = item switch
            {
                ItemType.Booster => _boosterIcon,
                ItemType.Banana => _bananaIcon,
                ItemType.Shell => _shellIcon,
                _ => null,
            };
            _itemIcon.sprite = sprite;
            _itemIcon.enabled = sprite != null;
            _itemPop = pop && sprite != null ? 1f : 0f;
            _itemIcon.rectTransform.localScale = Vector3.one;
        }

        private void OnEnable()
        {
            if (_raceManager == null) return;
            _raceManager.StateChanged += HandleStateChanged;
            _raceManager.OnRaceFinished += Hide;
            _raceManager.PauseChanged += HandlePauseChanged;
        }

        private void OnDisable()
        {
            if (_raceManager == null) return;
            _raceManager.StateChanged -= HandleStateChanged;
            _raceManager.OnRaceFinished -= Hide;
            _raceManager.PauseChanged -= HandlePauseChanged;
        }

        private void HandleStateChanged(RaceState state)
        {
            if (state == RaceState.Countdown) Show();
        }

        // 일시정지 메뉴를 대시보드가 가리지 않도록 숨겼다가, 재개하면 그 자리에 다시 보인다
        private void HandlePauseChanged(bool paused) => SetVisible(!paused);

        private void Update()
        {
            if (_content.activeSelf) Refresh(false);
            if (_itemPop > 0f && _itemIcon != null)
            {
                _itemPop = Mathf.Max(0f, _itemPop - Time.deltaTime * 4f);
                _itemIcon.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, _itemPopScale, _itemPop);
            }
        }

        private void Refresh(bool force)
        {
            if (_player == null) return;

            int lap = _player.CurrentLap;
            if (force || lap != _shownLap)
            {
                _shownLap = lap;
                _lapText.text = $"{lap}<size=60%>/{_player.TotalLaps}</size>";
            }

            if (_timeAttack != null)
            {
                if (force) _rankText.text = "<size=60%>최고 </size>" + (_timeAttack.HasRecord ? TimeFormat.FormatTenths(_timeAttack.BestTotal) : "--:--.-");
            }
            else
            {
                int rank = _player.Rank;
                if (force || rank != _shownRank)
                {
                    _shownRank = rank;
                    _rankText.text = $"{rank}위<size=60%> /{_participantCount}</size>";
                }
            }

            int speed = _playerKart != null ? Mathf.RoundToInt(Mathf.Abs(_playerKart.CurrentSpeed) * 3.6f) : 0;
            if (force || speed != _shownSpeed)
            {
                _shownSpeed = speed;
                _speedText.text = speed.ToString();
            }

            float raceTime = _raceManager.RaceTime;
            int tenths = Mathf.FloorToInt(raceTime * 10f);
            if (force || tenths != _shownTenths)
            {
                _shownTenths = tenths;
                _timeText.text = TimeFormat.FormatTenths(raceTime);
            }
        }
    }
}
