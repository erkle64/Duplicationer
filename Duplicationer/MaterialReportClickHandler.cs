using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Duplicationer
{
    internal class MaterialReportClickHandler : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler
    {
        private TextMeshProUGUI _textRenderer;
        private int _currentLine = -1;

        public event System.Action<int> OnLineChanged;
        public event System.Action<int> OnLineClicked;

        void Start()
        {
            _textRenderer = GetComponent<TextMeshProUGUI>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var line = TMP_TextUtilities.FindNearestLine(_textRenderer, eventData.position, null);
            if (line >= 0 && line < _textRenderer.textInfo.lineCount - 1)
            {
                OnLineClicked?.Invoke(line);
            }
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            var line = TMP_TextUtilities.FindNearestLine(_textRenderer, eventData.position, null);
            if (line != _currentLine)
            {
                _currentLine = line;
                var lineCount = _textRenderer.textInfo.lineCount;

                var text = _textRenderer.text;
                text = text.Replace("<mark>", "").Replace("</mark>", "");

                if (line < lineCount - 1)
                {
                    var startCharacterIndex = 0;
                    if (line > 0) startCharacterIndex = text.NthIndexOf("\n", line) + 1;

                    var endCharacterIndex = text.Length;
                    if (line < lineCount - 1) endCharacterIndex = text.NthIndexOf("\n", line + 1) - 1;

                    _textRenderer.text = text.Insert(endCharacterIndex, "</mark>").Insert(startCharacterIndex, "<mark>");
                }
                else
                {
                    _textRenderer.text = text;
                }

                OnLineChanged?.Invoke(line);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _textRenderer.text = _textRenderer.text.Replace("<mark>", "").Replace("</mark>", "");
            _currentLine = -1;
            OnLineChanged?.Invoke(-1);
        }
    }
}
