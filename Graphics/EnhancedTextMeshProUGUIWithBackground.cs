using EnhancedStreamChat.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedStreamChat.Graphics
{
    public class EnhancedTextMeshProUGUIWithBackground : MonoBehaviour
    {
        public EnhancedTextMeshProUGUI Text { get; internal set; }
        public EnhancedTextMeshProUGUI SubText { get; internal set; }

        public event Action OnLatePreRenderRebuildComplete;

        private Image _highlight, _accent;
        private VerticalLayoutGroup _verticalLayoutGroup;
        public Vector2 Size
        {
            get
            {
                return (transform as RectTransform).sizeDelta;
            }
            set
            {
                (transform as RectTransform).sizeDelta = value;
            }
        }

        public Color AccentColor
        {
            get
            {
                return _accent.color;
            }
            set
            {
                _accent.color = value;
            }
        }

        public Color HighlightColor
        {
            get
            {
                return _highlight.color;
            }
            set
            {
                _highlight.color = value;
            }
        }

        public bool HighlightEnabled
        {
            get
            {
                return _highlight.enabled;
            }
            set
            {
                _highlight.enabled = value;
                if (value)
                {
                    _verticalLayoutGroup.padding = new RectOffset(5, 5, 2, 2);
                }
                else
                {
                    _verticalLayoutGroup.padding = new RectOffset(5, 5, 1, 1);
                }
            }
        }

        public bool AccentEnabled
        {
            get
            {
                return _accent.enabled;
            }
            set
            {
                _accent.enabled = value;
            }
        }

        public bool SubTextEnabled
        {
            get
            {
                return SubText.enabled;
            }
            set
            {
                SubText.enabled = value;
                if(value)
                {
                    SubText.rectTransform.SetParent(gameObject.transform, false);
                }
                else
                {
                    SubText.rectTransform.SetParent(null, false);
                }
            }
        }

        private void Awake()
        {
            _highlight = gameObject.AddComponent<Image>();
            _highlight.material = BeatSaberUtils.UINoGlow;
            Text = new GameObject().AddComponent<EnhancedTextMeshProUGUI>();
            DontDestroyOnLoad(Text.gameObject);
            Text.OnLatePreRenderRebuildComplete += Text_OnLatePreRenderRebuildComplete;

            SubText = new GameObject().AddComponent<EnhancedTextMeshProUGUI>();
            DontDestroyOnLoad(SubText.gameObject);
            SubText.OnLatePreRenderRebuildComplete += Text_OnLatePreRenderRebuildComplete;

            _accent = new GameObject().AddComponent<Image>();
            DontDestroyOnLoad(_accent.gameObject);
            _accent.material = BeatSaberUtils.UINoGlow;
            _accent.color = Color.yellow;

            _verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
            _verticalLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
            _verticalLayoutGroup.spacing = 1;

            var highlightFitter = _accent.gameObject.AddComponent<LayoutElement>();
            highlightFitter.ignoreLayout = true;
            var textFitter = Text.gameObject.AddComponent<ContentSizeFitter>();
            textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var backgroundFitter = gameObject.AddComponent<ContentSizeFitter>();
            backgroundFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            SubTextEnabled = false;
            HighlightEnabled = false;
            AccentEnabled = false;
            _accent.gameObject.transform.SetParent(gameObject.transform, false);
            (_accent.gameObject.transform as RectTransform).anchorMin = new Vector2(0, 0.5f);
            (_accent.gameObject.transform as RectTransform).anchorMax = new Vector2(0, 0.5f);
            (_accent.gameObject.transform as RectTransform).sizeDelta = new Vector2(1, 10);
            (_accent.gameObject.transform as RectTransform).pivot = new Vector2(0, 0.5f);
            //var highlightLayoutGroup =_highlight.gameObject.AddComponent<VerticalLayoutGroup>();

            Text.rectTransform.SetParent(gameObject.transform, false);
        }

        private void OnDestroy()
        {
            Text.OnLatePreRenderRebuildComplete -= Text_OnLatePreRenderRebuildComplete;
            SubText.OnLatePreRenderRebuildComplete -= Text_OnLatePreRenderRebuildComplete;
        }

        private void Text_OnLatePreRenderRebuildComplete()
        {
            (_accent.gameObject.transform as RectTransform).sizeDelta = new Vector2(1, (transform as RectTransform).sizeDelta.y);
            OnLatePreRenderRebuildComplete?.Invoke();
        }
    }
}
