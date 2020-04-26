using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.FloatingScreen;
using BeatSaberMarkupLanguage.ViewControllers;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;
using ChatCore;
using ChatCore.Services.Twitch;
using ChatCore.Models.Twitch;
using ChatCore.Interfaces;
using ChatCore.Services;
using UnityEngine.SceneManagement;
using BS_Utils.Utilities;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;
using VRUIControls;

namespace EnhancedStreamChat.Chat
{
    [HotReload]
    public class ChatViewController : BSMLAutomaticViewController
    {
        private EnhancedFontInfo _chatFont;
        private Queue<EnhancedTextMeshProUGUIWithBackground> _activeChatMessages = new Queue<EnhancedTextMeshProUGUIWithBackground>();
        private ObjectPool<EnhancedTextMeshProUGUIWithBackground> _textPool;
        private FloatingScreen _chatScreen;
        private GameObject _gameObject;
        private ChatConfig _chatConfig;
        private Material _chatMoverMaterial;
        private bool _isInGame = false;
        private string _fontPath = Path.Combine(Environment.CurrentDirectory, "Cache", "FontAssets");

        private void Start()
        {
            _chatConfig = ChatConfig.instance;
            StartCoroutine(LoadFonts());
            SetupScreens();

            if (_textPool == null)
            {
                _textPool = new ObjectPool<EnhancedTextMeshProUGUIWithBackground>(20,
                    Constructor: () =>
                    {
                        var go = new GameObject();
                        DontDestroyOnLoad(go);
                        var msg = go.AddComponent<EnhancedTextMeshProUGUIWithBackground>();
                        msg.Text.enableWordWrapping = true;
                        msg.Text.FontInfo = _chatFont;
                        msg.SubText.enableWordWrapping = true;
                        msg.SubText.FontInfo = _chatFont;
                        UpdateChatMessage(msg);
                        return msg;
                    },
                    OnAlloc: (msg) =>
                    {
                        msg.gameObject.transform.SetParent(_chatContainer.transform, false);
                    },
                    OnFree: (msg) =>
                    {
                        try
                        {
                            msg.HighlightEnabled = false;
                            msg.AccentEnabled = false;
                            msg.SubTextEnabled = false;
                            msg.Text.text = null;
                            msg.Text.ChatMessage = null;
                            msg.SubText.text = null;
                            msg.SubText.ChatMessage = null;
                            msg.gameObject.SetActive(false);
                            msg.gameObject.transform.SetParent(rectTransform);
                            msg.Text.ClearImages();
                            msg.SubText.ClearImages();
                        }
                        catch (Exception ex)
                        {
                            Logger.log.Error($"An exception occurred while trying to free CustomText object. {ex.ToString()}");
                        }
                    }
                );
            }

            BSEvents.menuSceneActive += BSEvents_menuSceneActive;
            BSEvents.gameSceneActive += BSEvents_gameSceneActive;
            ChatConfig.instance.OnConfigChanged += Instance_OnConfigUpdated;
            UpdateChatUI();
        }

        protected override void DidDeactivate(DeactivationType deactivationType)
        {
            if (deactivationType == DeactivationType.NotRemovedFromHierarchy)
            {
                CleanupOldMessages(true);
            }
            base.DidDeactivate(deactivationType);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            BSEvents.menuSceneActive -= BSEvents_menuSceneActive;
            BSEvents.gameSceneActive -= BSEvents_gameSceneActive;
            ChatConfig.instance.OnConfigChanged -= Instance_OnConfigUpdated;
            foreach (var msg in _activeChatMessages)
            {
                Destroy(msg);
            }
            _activeChatMessages.Clear();
            Destroy(_gameObject);
            if (_textPool != null)
            {
                _textPool.Dispose();
                _textPool = null;
            }
            if (_chatScreen != null)
            {
                Destroy(_chatScreen);
                _chatScreen = null;
            }
            if(_chatFont != null)
            {
                Destroy(_chatFont.Font);
                _chatFont = null;
            }
            if(_chatMoverMaterial != null)
            {
                Destroy(_chatMoverMaterial);
                _chatMoverMaterial = null;
            }
            if (_loadedAssets.Count > 0)
            {
                foreach (var asset in _loadedAssets.Values)
                {
                    asset.Unload(true);
                }
                _loadedAssets.Clear();
            }
        }

        [UIAction("#post-parse")]
        private void PostParse()
        {
            // bg
            _backgroundColorSetting.editButton.onClick.AddListener(HideSettings);
            _backgroundColorSetting.modalColorPicker.cancelEvent += ShowSettings;
            _backgroundColorSetting.CurrentColor = _chatConfig.BackgroundColor.ToColor();
            // accent
            _accentColorSetting.editButton.onClick.AddListener(HideSettings);
            _accentColorSetting.modalColorPicker.cancelEvent += ShowSettings;
            _accentColorSetting.CurrentColor = _chatConfig.AccentColor.ToColor();
            // highlight
            _highlightColorSetting.editButton.onClick.AddListener(HideSettings);
            _highlightColorSetting.modalColorPicker.cancelEvent += ShowSettings;
            _highlightColorSetting.CurrentColor = _chatConfig.HighlightColor.ToColor();
            // ping
            _pingColorSetting.editButton.onClick.AddListener(HideSettings);
            _pingColorSetting.modalColorPicker.cancelEvent += ShowSettings;
            _pingColorSetting.CurrentColor = _chatConfig.PingColor.ToColor();

            _chatContainer.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void BSEvents_gameSceneActive()
        {
            _isInGame = true;
            AddToVRPointer();
            UpdateChatUI();
        }

        private void BSEvents_menuSceneActive()
        {
            _isInGame = false;
            AddToVRPointer();
            UpdateChatUI();
        }

        private void AddToVRPointer()
        {
            VRPointer pointer = null;
            if (_isInGame)
            {
                pointer = Resources.FindObjectsOfTypeAll<VRPointer>().Last();
            }
            else
            {
                pointer = Resources.FindObjectsOfTypeAll<VRPointer>().First();
            }
            if (_chatScreen.screenMover != null)
            {
                DestroyImmediate(_chatScreen.screenMover);
                _chatScreen.screenMover = pointer.gameObject.AddComponent<FloatingScreenMoverPointer>();
                _chatScreen.screenMover.Init(_chatScreen);
                _chatScreen.screenMover.OnRelease += floatingScreen_OnRelease;
                _chatScreen.screenMover.transform.SetAsFirstSibling();
            }
        }

        private void Instance_OnConfigUpdated(ChatConfig config)
        {
            MainThreadInvoker.Invoke(() =>
            {
                UpdateChatUI();
            });
        }

        private void SetupScreens()
        {
            if (_chatScreen == null)
            {
                _chatScreen = FloatingScreen.CreateFloatingScreen(new Vector2(ChatWidth, ChatHeight), true, ChatPosition, Quaternion.Euler(ChatRotation));
                var canvas = _chatScreen.GetComponent<Canvas>();
                canvas.sortingOrder = 3;
                _chatScreen.SetRootViewController(this, true);
                _gameObject = new GameObject();
                DontDestroyOnLoad(_gameObject);
                _chatMoverMaterial = Instantiate(BeatSaberUtils.UINoGlow);
                _chatMoverMaterial.color = Color.clear;
                var renderer = _chatScreen.handle.gameObject.GetComponent<Renderer>();
                renderer.material = _chatMoverMaterial;
                _chatScreen.transform.SetParent(_gameObject.transform);
                var bg = _chatScreen.gameObject.GetComponent<UnityEngine.UI.Image>();
                bg.material = Instantiate(BeatSaberUtils.UINoGlow);
                AddToVRPointer();
            }
        }

        private void floatingScreen_OnRelease(Vector3 pos, Quaternion rot)
        {
            ChatPosition = pos;
            ChatRotation = rot.eulerAngles;
            _chatConfig.Save();
        }

        private string BuildClearedMessage(EnhancedTextMeshProUGUI msg)
        {
            StringBuilder sb = new StringBuilder($"<color={msg.ChatMessage.Sender.Color}>{msg.ChatMessage.Sender.Name}</color>");
            var badgeEndIndex = msg.text.IndexOf("<color=");
            if (badgeEndIndex != -1)
            {
                sb.Insert(0, msg.text.Substring(0, badgeEndIndex));
            }
            sb.Append(": <color=#bbbbbbbb><message deleted></color>");
            return sb.ToString();
        }

        private void ClearMessage(EnhancedTextMeshProUGUIWithBackground msg)
        {
            // Only clear non-system messages
            if (!msg.Text.ChatMessage.IsSystemMessage)
            {
                msg.Text.text = BuildClearedMessage(msg.Text);
                msg.SubTextEnabled = false;
            }
            if (msg.SubText.ChatMessage != null && !msg.SubText.ChatMessage.IsSystemMessage)
            {
                msg.SubText.text = BuildClearedMessage(msg.SubText);
            }
        }

        private void UpdateChatUI()
        {
            rectTransform.localRotation = Quaternion.identity;
            ChatWidth = _chatConfig.ChatWidth;
            ChatHeight = _chatConfig.ChatHeight;
            FontSize = _chatConfig.FontSize;
            AccentColor = _chatConfig.AccentColor.ToColor();
            HighlightColor = _chatConfig.HighlightColor.ToColor();
            BackgroundColor = _chatConfig.BackgroundColor.ToColor();
            PingColor = _chatConfig.PingColor.ToColor();
            if (_isInGame)
            {
                ChatPosition = _chatConfig.Song_ChatPosition;
                ChatRotation = _chatConfig.Song_ChatRotation;
            }
            else
            {
                ChatPosition = _chatConfig.Menu_ChatPosition;
                ChatRotation = _chatConfig.Menu_ChatRotation;
            }
            _chatScreen.handle.transform.localScale = new Vector2(ChatWidth, ChatHeight);
            _chatScreen.handle.transform.localPosition = Vector3.zero;
            _chatScreen.handle.transform.localRotation = Quaternion.identity;
            AllowMovement = _chatConfig.AllowMovement;
            UpdateChatMessages();
        }

        private void UpdateChatMessages()
        {
            foreach (var msg in _activeChatMessages)
            {
                UpdateChatMessage(msg, true);
            }
        }

        private void UpdateChatMessage(EnhancedTextMeshProUGUIWithBackground msg, bool setAllDirty = false)
        {
            msg.Text.font = _chatFont.Font;
            msg.Text.overflowMode = TextOverflowModes.Overflow;
            msg.Text.alignment = TextAlignmentOptions.BottomLeft;
            msg.Text.lineSpacing = 1.5f;
            msg.Text.color = Color.white;
            msg.Text.fontSize = _chatConfig.FontSize;
            msg.Text.lineSpacing = 1.5f;

            msg.SubText.font = _chatFont.Font;
            msg.SubText.overflowMode = TextOverflowModes.Overflow;
            msg.SubText.alignment = TextAlignmentOptions.BottomLeft;
            msg.SubText.color = Color.white;
            msg.SubText.fontSize = _chatConfig.FontSize;
            msg.SubText.lineSpacing = 1.5f;

            if (msg.Text.ChatMessage != null)
            {
                msg.HighlightColor = msg.Text.ChatMessage.IsPing ? PingColor : HighlightColor;
                msg.AccentColor = AccentColor;
                msg.HighlightEnabled = msg.Text.ChatMessage.IsHighlighted || msg.Text.ChatMessage.IsPing;
                msg.AccentEnabled = !msg.Text.ChatMessage.IsPing && (msg.HighlightEnabled || msg.SubText.ChatMessage != null);
            }

            if (setAllDirty)
            {
                msg.Text.SetAllDirty();
                if (msg.SubTextEnabled)
                {
                    msg.SubText.SetAllDirty();
                }
            }
        }

        private void CleanupOldMessages(bool force = false)
        {
            while (_activeChatMessages.TryPeek(out var nextClear) && (force || nextClear.transform.localPosition.y > _chatConfig.ChatHeight + 100))
            {
                _textPool.Free(_activeChatMessages.Dequeue());
                //Logger.log.Info($"{_messageClearQueue.Count} messages shown");
            }
        }

        public void OnMessageCleared(string messageId)
        {
            if (messageId != null)
            {
                MainThreadInvoker.Invoke(() =>
                {
                    foreach (var msg in _activeChatMessages)
                    {
                        if (msg.Text.ChatMessage == null)
                        {
                            continue;
                        }
                        if (msg.Text.ChatMessage.Id == messageId)
                        {
                            ClearMessage(msg);
                        }
                    }
                });
            }
        }

        public void OnChatCleared(string userId)
        {
            MainThreadInvoker.Invoke(() =>
            {
                foreach (var msg in _activeChatMessages)
                {
                    if (msg.Text.ChatMessage == null)
                    {
                        continue;
                    }
                    if (userId == null || msg.Text.ChatMessage.Sender.Id == userId)
                    {
                        ClearMessage(msg);
                    }
                }
            });
        }

        public void OnJoinChannel(IChatService svc, IChatChannel channel)
        {
            MainThreadInvoker.Invoke(() =>
            {
                var newMsg = _textPool.Alloc();
                newMsg.Text.text = $"<color=#bbbbbbbb>[{svc.DisplayName}] Success joining {channel.Id}</color>";
                newMsg.HighlightEnabled = true;
                newMsg.HighlightColor = Color.gray.ColorWithAlpha(0.05f);
                newMsg.gameObject.SetActive(true);
                _activeChatMessages.Enqueue(newMsg);

                UpdateChatMessage(newMsg);
                CleanupOldMessages();
            });
        }

        EnhancedTextMeshProUGUIWithBackground _lastMessage;
        public async void OnTextMessageReceived(IChatService svc, IChatMessage msg)
        {
            if (_chatFont is null)
            {
                // TODO: maybe queue this message up or wait?
                return;
            }

            string parsedMessage = await ChatMessageBuilder.BuildMessage(msg, _chatFont);

            MainThreadInvoker.Invoke(() =>
            {
                if (_lastMessage != null && !msg.IsSystemMessage && _lastMessage.Text.ChatMessage.Id == msg.Id)
                {
                    // If the last message received had the same id and isn't a system message, then this was a sub-message of the original and may need to be highlighted along with the original message
                    _lastMessage.SubText.text = parsedMessage;
                    _lastMessage.SubText.ChatMessage = msg;
                    _lastMessage.SubTextEnabled = true;
                }
                else
                {
                    var newMsg = _textPool.Alloc();
                    newMsg.Text.ChatMessage = msg;
                    newMsg.Text.text = parsedMessage;
                    newMsg.gameObject.SetActive(true);
                    _activeChatMessages.Enqueue(newMsg);
                    _lastMessage = newMsg;
                }
                UpdateChatMessage(_lastMessage);
                CleanupOldMessages();
            });
        }

        [UIParams]
        internal BSMLParserParams parserParams;

        [UIComponent("background-color-setting")]
        ColorSetting _backgroundColorSetting;

        [UIComponent("accent-color-setting")]
        ColorSetting _accentColorSetting;

        [UIComponent("highlight-color-setting")]
        ColorSetting _highlightColorSetting;

        [UIComponent("ping-color-setting")]
        ColorSetting _pingColorSetting;

        [UIObject("ChatContainer")]
        GameObject _chatContainer;

        private Color _accentColor;
        [UIValue("accent-color")]
        public Color AccentColor
        {
            get => _accentColor;
            set
            {
                _accentColor = value;
                _chatConfig.AccentColor = "#" + ColorUtility.ToHtmlStringRGBA(value);
                UpdateChatMessages();
                NotifyPropertyChanged();
            }
        }

        private Color _highlightColor;
        [UIValue("highlight-color")]
        public Color HighlightColor
        {
            get => _highlightColor;
            set
            {
                _highlightColor = value;
                _chatConfig.HighlightColor = "#" + ColorUtility.ToHtmlStringRGBA(value);
                UpdateChatMessages();
                NotifyPropertyChanged();
            }
        }

        private Color _pingColor;
        [UIValue("ping-color")]
        public Color PingColor
        {
            get => _pingColor;
            set
            {
                _pingColor = value;
                _chatConfig.PingColor = "#" + ColorUtility.ToHtmlStringRGBA(value);
                UpdateChatMessages();
                NotifyPropertyChanged();
            }
        }

        private Color _backgroundColor;
        [UIValue("background-color")]
        public Color BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                _backgroundColor = value;
                _chatConfig.BackgroundColor = "#" + ColorUtility.ToHtmlStringRGBA(value);
                _chatScreen.gameObject.GetComponent<Image>().material.color = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue("font-size")]
        public float FontSize
        {
            get => _chatConfig.FontSize;
            set
            {
                _chatConfig.FontSize = value;
                UpdateChatMessages();
                NotifyPropertyChanged();
            }
        }

        private int _settingsWidth = 110;
        [UIValue("settings-width")]
        public int SettingsWidth
        {
            get => _settingsWidth;
            set
            {
                _settingsWidth = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue("chat-width")]
        public int ChatWidth
        {
            get => _chatConfig.ChatWidth;
            set
            {
                _chatConfig.ChatWidth = value;
                _chatScreen.ScreenSize = new Vector2(ChatWidth, ChatHeight);
                NotifyPropertyChanged();
            }
        }

        [UIValue("chat-height")]
        public int ChatHeight
        {
            get => _chatConfig.ChatHeight;
            set
            {
                _chatConfig.ChatHeight = value;
                _chatScreen.ScreenSize = new Vector2(ChatWidth, ChatHeight);
                NotifyPropertyChanged();
            }
        }

        [UIValue("chat-position")]
        public Vector3 ChatPosition
        {
            get => _isInGame ? _chatConfig.Song_ChatPosition : _chatConfig.Menu_ChatPosition;
            set
            {
                if (_isInGame)
                {
                    _chatConfig.Song_ChatPosition = value;
                }
                else
                {
                    _chatConfig.Menu_ChatPosition = value;
                }
                _chatScreen.ScreenPosition = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue("chat-rotation")]
        public Vector3 ChatRotation
        {
            get => _isInGame ? _chatConfig.Song_ChatRotation : _chatConfig.Menu_ChatRotation;
            set
            {
                if (_isInGame)
                {
                    _chatConfig.Song_ChatRotation = value;
                }
                else
                {
                    _chatConfig.Menu_ChatRotation = value;
                }
                _chatScreen.ScreenRotation = Quaternion.Euler(value);
                NotifyPropertyChanged();
            }
        }

        [UIValue("allow-movement")]
        public bool AllowMovement
        {
            get => _chatConfig.AllowMovement;
            set
            {
                _chatConfig.AllowMovement = value;
                _chatScreen.ShowHandle = value;
                NotifyPropertyChanged();
            }
        }

        [UIAction("on-settings-clicked")]
        private void OnSettingsClick()
        {
            Logger.log.Info("Settings clicked!");
        }

        [UIAction("#hide-settings")]
        private void OnHideSettings()
        {
            Logger.log.Info("Saving settings!");
            _chatConfig.Save();
        }

        private void HideSettings()
        {
            parserParams.EmitEvent("hide-settings");
        }

        private void ShowSettings()
        {
            parserParams.EmitEvent("show-settings");
        }

        private static Dictionary<string, AssetBundle> _loadedAssets = new Dictionary<string, AssetBundle>();
        private IEnumerator LoadFonts()
        {
            if (_chatFont != null)
            {
                yield break;
            }

            if (!Directory.Exists(_fontPath))
            {
                Directory.CreateDirectory(_fontPath);
            }

            string mainFontPath = Path.Combine(_fontPath, "main.fontasset");
            if (!File.Exists(mainFontPath))
            {
                File.WriteAllBytes(mainFontPath, BeatSaberMarkupLanguage.Utilities.GetResource(Assembly.GetExecutingAssembly(), "EnhancedStreamChat.Resources.Fonts.main"));
            }

            string symbolsPath = Path.Combine(_fontPath, "symbols.fontasset");
            if (!File.Exists(symbolsPath))
            {
                File.WriteAllBytes(symbolsPath, BeatSaberMarkupLanguage.Utilities.GetResource(Assembly.GetExecutingAssembly(), "EnhancedStreamChat.Resources.Fonts.symbols"));
            }

            Logger.log.Info("Loading fonts");
            List<TMP_FontAsset> fallbackFonts = new List<TMP_FontAsset>();

            if (!_loadedAssets.TryGetValue(mainFontPath, out var mainAsset))
            {
                mainAsset = AssetBundle.LoadFromFile(mainFontPath);
                _loadedAssets.Add(mainFontPath, mainAsset);
            }
            LoadFont(mainAsset, fallbackFonts);

            foreach (var fontAssetPath in Directory.GetFiles(_fontPath, "*.fontasset", SearchOption.TopDirectoryOnly))
            {
                if (Path.GetFileName(fontAssetPath) != "main.fontasset")
                {
                    //Logger.log.Info($"AssetBundleName: {fontAssetPath}");
                    if (!_loadedAssets.TryGetValue(fontAssetPath, out var fontAsset))
                    {
                        var fontLoadRequest = AssetBundle.LoadFromFileAsync(fontAssetPath);
                        yield return fontLoadRequest;
                        fontAsset = fontLoadRequest.assetBundle;
                        _loadedAssets.Add(fontAssetPath, fontAsset);
                    }
                    LoadFont(fontAsset, fallbackFonts);
                }
            }
            foreach (var font in fallbackFonts)
            {
                Logger.log.Info($"Adding {font.name} to fallback fonts!");
                _chatFont.Font.fallbackFontAssetTable.Add(font);
            }
            foreach (var msg in _activeChatMessages)
            {
                msg.Text.SetAllDirty();
                if (msg.SubTextEnabled)
                {
                    msg.SubText.SetAllDirty();
                }
            }
        }

        private void LoadFont(AssetBundle assetBundle, List<TMP_FontAsset> fallbackFonts)
        {
            foreach (var asset in assetBundle.LoadAllAssets())
            {
                if (asset is Font font)
                {
                    if (font.name == "main")
                    {
                        if (assetBundle.name == "main")
                        {
                            Logger.log.Info($"Main font: {font.fontNames[0]}");
                            _chatFont = new EnhancedFontInfo(BeatSaberUtils.SetupFont(TMP_FontAsset.CreateFontAsset(font)));
                            _chatFont.Font.name = font.fontNames[0] + " (Clone)";
                        }
                        else
                        {
                            //Logger.log.Info($"Fallback font: {font.fontNames[0]}");
                            var fallbackFont = BeatSaberUtils.SetupFont(TMP_FontAsset.CreateFontAsset(font));
                            fallbackFont.name = font.fontNames[0] + " (Clone)";
                            fallbackFonts.Add(fallbackFont);
                        }
                    }
                }
            }
        }
    }
}
