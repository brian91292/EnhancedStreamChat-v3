using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.FloatingScreen;
using BeatSaberMarkupLanguage.ViewControllers;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using StreamCore.Interfaces;
using StreamCore.Services;
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
using StreamCore;
using StreamCore.Services.Twitch;
using StreamCore.Models.Twitch;
using UnityEngine.SceneManagement;
using BS_Utils.Utilities;

namespace EnhancedStreamChat.Chat
{
    [HotReload]
    public class ChatViewController : BSMLAutomaticViewController
    { 

        private TMP_FontAsset _chatFont;
        private Queue<EnhancedTextMeshProUGUIWithBackground> _messageClearQueue = new Queue<EnhancedTextMeshProUGUIWithBackground>();
        private ObjectPool<EnhancedTextMeshProUGUIWithBackground> _textPool;
        private FloatingScreen _chatScreen;
        private GameObject _gameObject;
        private ChatConfig _chatConfig;
        private Color _accentColor, _highlightColor, _pingColor, _backgroundColor;
        private bool _isInGame = false;

        protected void Awake()
        {
            ChatConfig.instance.OnConfigChanged += Instance_OnConfigUpdated;
            _chatConfig = ChatConfig.instance;
            SetupScreens();

            _textPool = new ObjectPool<EnhancedTextMeshProUGUIWithBackground>(20,
                Constructor: () =>
                {
                    var go = new GameObject();
                    DontDestroyOnLoad(go);
                    var msg = go.AddComponent<EnhancedTextMeshProUGUIWithBackground>();
                    msg.Text.enableWordWrapping = true;
                    msg.SubText.enableWordWrapping = true;
                    UpdateChatMessage(msg);
                    return msg;
                },
                OnAlloc: (msg) =>
                {
                    //txt.material = BeatSaberUtils.UINoGlow;
                    //Logger.log.Info($"Text material is {txt.material.name}");
                    msg.gameObject.transform.SetParent(_chatContainer.transform, false);
                },
                OnFree: (msg) =>
                {
                    try
                    {
                        msg.HighlightEnabled = false;
                        msg.AccentEnabled = false;
                        msg.SubTextShown = false;
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

            BSEvents.menuSceneActive += BSEvents_menuSceneActive;
            BSEvents.gameSceneActive += BSEvents_gameSceneActive;

            StartCoroutine(LoadFonts());
            Instance_OnConfigUpdated(ChatConfig.instance);
        }

        private void BSEvents_gameSceneActive()
        {
            _isInGame = true;
            UpdateChatUI();
        }

        private void BSEvents_menuSceneActive()
        {
            _isInGame = false;
            UpdateChatUI();
        }

        private void Instance_OnConfigUpdated(ChatConfig config)
        {
            MainThreadInvoker.Invoke(() =>
            {
                _chatConfig = config;

                if (!ColorUtility.TryParseHtmlString(config.AccentColor, out _accentColor))
                {
                    Logger.log.Warn($"NotifyAccentColor {config.AccentColor} is not a valid color.");
                    _accentColor = Color.yellow;
                }
                if (!ColorUtility.TryParseHtmlString(config.HighlightColor, out _highlightColor))
                {
                    Logger.log.Warn($"NotifyHighlightColor {config.HighlightColor} is not a valid color.");
                    _highlightColor = Color.grey.ColorWithAlpha(0.03f);
                }
                if (!ColorUtility.TryParseHtmlString(config.PingColor, out _pingColor))
                {
                    Logger.log.Warn($"PingColor {config.PingColor} is not a valid color.");
                    _highlightColor = Color.red.ColorWithAlpha(0.1f);
                }
                if(!ColorUtility.TryParseHtmlString(config.BackgroundColor, out _backgroundColor))
                {
                    Logger.log.Warn($"PingColor {config.BackgroundColor} is not a valid color.");
                    _backgroundColor = Color.black.ColorWithAlpha(0.4f);
                }
                UpdateChatUI();
            });
        }

        private void SetupScreens()
        {
            if (_chatScreen == null)
            {
                _chatScreen = FloatingScreen.CreateFloatingScreen(new Vector2(_chatConfig.ChatWidth, _chatConfig.ChatHeight), true, _chatConfig.Menu_ChatPosition, Quaternion.Euler(_chatConfig.Menu_ChatRotation));
                _chatScreen.SetRootViewController(this, true);
                _chatScreen.HandleSide = FloatingScreen.Side.Bottom;
                var renderer = _chatScreen.handle.gameObject.GetComponent<Renderer>();
                renderer.material = Instantiate(BeatSaberUtils.UINoGlow);
                renderer.material.color = Color.clear;
                //_floatingScreen.ShowHandle = _chatConfig.AllowMovement;
                _chatScreen.screenMover.OnRelease += floatingScreen_OnRelease;
                _gameObject = new GameObject();
                DontDestroyOnLoad(_gameObject);
                _chatScreen.transform.SetParent(_gameObject.transform);
                var bg = _chatScreen.gameObject.GetComponent<UnityEngine.UI.Image>();
                bg.material = Instantiate(BeatSaberUtils.UINoGlow);
                //bg.enabled = false;
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
                msg.SubTextShown = false;
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
            AllowMovement = _chatConfig.AllowMovement;
            FontSize = _chatConfig.FontSize;
            _chatScreen.gameObject.GetComponent<Image>().material.color = _backgroundColor;
            _chatScreen.handle.transform.localScale = new Vector2(ChatWidth, ChatHeight);
            _chatScreen.handle.transform.localPosition = new Vector3(0, 0, 0);
        }

        private void UpdateChatMessages()
        {
            foreach (var msg in _messageClearQueue)
            {
                UpdateChatMessage(msg, true);
            }
        }

        private void UpdateChatMessage(EnhancedTextMeshProUGUIWithBackground msg, bool setAllDirty = false)
        {
            msg.Text.font = _chatFont;
            msg.Text.overflowMode = TextOverflowModes.Overflow;
            msg.Text.alignment = TextAlignmentOptions.BottomLeft;
            msg.Text.lineSpacing = 1.5f;
            msg.Text.color = Color.white;
            msg.Text.fontSize = _chatConfig.FontSize;
            msg.Text.lineSpacing = 1.5f;

            msg.SubText.font = _chatFont;
            msg.SubText.overflowMode = TextOverflowModes.Overflow;
            msg.SubText.alignment = TextAlignmentOptions.BottomLeft;
            msg.SubText.color = Color.white;
            msg.SubText.fontSize = _chatConfig.FontSize;
            msg.SubText.lineSpacing = 1.5f;

            if (msg.Text.ChatMessage != null)
            {
                msg.HighlightColor = msg.Text.ChatMessage.IsPing ? _pingColor : _highlightColor;
                msg.AccentColor = _accentColor;
                msg.HighlightEnabled = msg.Text.ChatMessage.IsHighlighted || msg.Text.ChatMessage.IsPing;
                msg.AccentEnabled = !msg.Text.ChatMessage.IsPing && (msg.HighlightEnabled || msg.SubText.ChatMessage != null);
            }

            if(setAllDirty)
            {
                msg.Text.SetAllDirty();
                if(msg.SubTextShown)
                {
                    msg.SubText.SetAllDirty();
                }
            }
        }

        private void CleanupOldMessages(bool force = false)
        {
            while (_messageClearQueue.TryPeek(out var nextClear) && (force || nextClear.transform.localPosition.y > _chatConfig.ChatHeight + 100))
            {
                _textPool.Free(_messageClearQueue.Dequeue());
                //Logger.log.Info($"{_messageClearQueue.Count} messages shown");
            }
        }

        public void OnMessageCleared(string messageId)
        {
            if (messageId != null)
            {
                MainThreadInvoker.Invoke(() =>
                {
                    foreach (var msg in _messageClearQueue)
                    {
                        if(msg.Text.ChatMessage == null)
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
                foreach (var msg in _messageClearQueue)
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

        public void OnJoinChannel(IStreamingService svc, IChatChannel channel)
        {
            MainThreadInvoker.Invoke(() =>
            {
                var newMsg = _textPool.Alloc();
                newMsg.Text.text = $"<color=#bbbbbbbb>Success joining {channel.Id}</color>";
                newMsg.HighlightEnabled = true;
                newMsg.HighlightColor = Color.gray.ColorWithAlpha(0.1f);
                newMsg.gameObject.SetActive(true);
                _messageClearQueue.Enqueue(newMsg);

                UpdateChatMessage(newMsg);
                CleanupOldMessages();
            });
        }

        EnhancedTextMeshProUGUIWithBackground _lastMessage;
        public async void OnTextMessageReceived(IStreamingService svc, IChatMessage msg)
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
                    _lastMessage.SubTextShown = true;
                }
                else
                {
                    var newMsg = _textPool.Alloc();
                    newMsg.Text.ChatMessage = msg;
                    newMsg.Text.text = parsedMessage;
                    newMsg.gameObject.SetActive(true);
                    _messageClearQueue.Enqueue(newMsg);
                    _lastMessage = newMsg;
                }
                UpdateChatMessage(_lastMessage);
                CleanupOldMessages();
            });
        }

        [UIAction("on-settings-clicked")]
        private void OnSettingsClick()
        {
            Logger.log.Info("Settings clicked!");
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

        [UIValue("menu-chat-position")]
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

        [UIValue("menu-chat-rotation")]
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

        [UIAction("#hide-settings")]
        private void HideSettings()
        {
            Logger.log.Info("Saving settings!");
            _chatConfig.Save();
        }

        [UIObject("ChatContainer")]
        GameObject _chatContainer;

        [UIAction("#post-parse")]
        private void PostParse()
        {
            _chatContainer.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        protected override void DidDeactivate(DeactivationType deactivationType)
        {
            if(deactivationType == DeactivationType.NotRemovedFromHierarchy)
            {
                CleanupOldMessages(true);
            }
            base.DidDeactivate(deactivationType);
        }

        private List<AssetBundle> _loadedAssets = new List<AssetBundle>();
        private IEnumerator LoadFonts()
        {
            string fontsPath = Path.Combine(Environment.CurrentDirectory, "Cache", "FontAssets");
            if (!Directory.Exists(fontsPath))
            {
                Directory.CreateDirectory(fontsPath);
            }

            string mainFontPath = Path.Combine(fontsPath, "main");
            if (!File.Exists(mainFontPath))
            {
                File.WriteAllBytes(mainFontPath, BeatSaberMarkupLanguage.Utilities.GetResource(Assembly.GetExecutingAssembly(), "EnhancedStreamChat.Resources.Fonts.NotoSans-Regular"));
            }

            Logger.log.Info("Loading fonts");
            List<TMP_FontAsset> fallbackFonts = new List<TMP_FontAsset>();

            var mainAsset = AssetBundle.LoadFromFile(mainFontPath);
            _loadedAssets.Add(mainAsset);
            LoadFont(mainAsset, fallbackFonts);

            foreach (var fontAssetPath in Directory.GetFiles(fontsPath, "*", SearchOption.TopDirectoryOnly))
            {
                if (Path.GetFileName(fontAssetPath) != "main")
                {
                    //Logger.log.Info($"AssetBundleName: {fontAssetPath}");
                    var fontLoadRequest = AssetBundle.LoadFromFileAsync(fontAssetPath);
                    yield return fontLoadRequest;
                    _loadedAssets.Add(fontLoadRequest.assetBundle);
                    LoadFont(fontLoadRequest.assetBundle, fallbackFonts);
                }
            }

            foreach (var font in fallbackFonts)
            {
                Logger.log.Info($"Adding {font.name} to fallback fonts!");
                _chatFont.fallbackFontAssetTable.Add(font);
            }
            foreach (var msg in _messageClearQueue)
            {
                msg.Text.SetAllDirty();
                if (msg.SubTextShown)
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
                            _chatFont = BeatSaberUtils.SetupFont(TMP_FontAsset.CreateFontAsset(font));
                            _chatFont.name = font.fontNames[0] + " (Clone)";
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
