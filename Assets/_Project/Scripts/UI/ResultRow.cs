using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRKart.Race;

namespace VRKart.UI
{
    // 결과 화면 순위표의 한 줄
    public sealed class ResultRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text _positionText;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private Image _background;
        [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 0.06f);
        [SerializeField] private Color _playerColor = new Color(1f, 0.78f, 0.2f, 0.35f);

        public void Set(RaceResult result, string displayName)
        {
            _positionText.text = TimeFormat.Ordinal(result.Position);
            _nameText.text = displayName;
            _timeText.text = result.IsFinished ? TimeFormat.Format(result.TotalTime) : "RACING";
            _background.color = result.IsPlayer ? _playerColor : _normalColor;
        }
    }
}
