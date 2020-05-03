using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.FloatingScreen;
using BeatSaberMarkupLanguage.ViewControllers;
using BS_Utils.Utilities;
using ChatCore.Interfaces;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUIControls;
using Color = UnityEngine.Color;
using Image = UnityEngine.UI.Image;

namespace EnhancedStreamChat.Chat
{
    public partial class ChatDisplay : BSMLAutomaticViewController
    {
        public ObjectPool<EnhancedTextMeshProUGUIWithBackground> TextPool { get; internal set; }
        private Queue<EnhancedTextMeshProUGUIWithBackground> _messages = new Queue<EnhancedTextMeshProUGUIWithBackground>();
        private ChatConfig _chatConfig;
        private EnhancedFontInfo _chatFont;
        //private Canvas _chatCanvas;
        //private Image _chatBackground;
        private bool _isInGame = false;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _chatConfig = ChatConfig.instance;
            CreateChatFont();
            SetupScreens();

            (transform as RectTransform).pivot = new Vector2(0.5f, 0f);

            TextPool = new ObjectPool<EnhancedTextMeshProUGUIWithBackground>(20,
                Constructor: () =>
                {
                    var go = new GameObject();
                    DontDestroyOnLoad(go);
                    var msg = go.AddComponent<EnhancedTextMeshProUGUIWithBackground>();
                    msg.Text.enableWordWrapping = true;
                    msg.Text.FontInfo = _chatFont;
                    msg.SubText.enableWordWrapping = true;
                    msg.SubText.FontInfo = _chatFont;
                    (msg.transform as RectTransform).pivot = new Vector2(0.5f, 0);
                    msg.transform.SetParent(transform, false);
                    msg.gameObject.SetActive(false);
                    UpdateChatMessage(msg);
                    return msg;
                },
                OnFree: (msg) =>
                {
                    try
                    {
                        msg.HighlightEnabled = false;
                        msg.AccentEnabled = false;
                        msg.SubTextEnabled = false;
                        msg.Text.text = "";
                        msg.Text.ChatMessage = null;
                        msg.SubText.text = "";
                        msg.SubText.ChatMessage = null;
                        msg.OnLatePreRenderRebuildComplete -= OnRenderRebuildComplete;
                        msg.gameObject.SetActive(false);
                        msg.Text.ClearImages();
                        msg.SubText.ClearImages();
                    }
                    catch (Exception ex)
                    {
                        Logger.log.Error($"An exception occurred while trying to free CustomText object. {ex.ToString()}");
                    }
                }
            );
            ChatConfig.instance.OnConfigChanged += Instance_OnConfigChanged;
            BSEvents.menuSceneActive += BSEvents_menuSceneActive;
            BSEvents.gameSceneActive += BSEvents_gameSceneActive;
        }

        // TODO: eventually figure out a way to make this more modular incase we want to create multiple instances of ChatDisplay
        private static ConcurrentQueue<IChatMessage> _backupMessageQueue = new ConcurrentQueue<IChatMessage>();
        protected override void OnDestroy()
        {
            base.OnDestroy();

            ChatConfig.instance.OnConfigChanged -= Instance_OnConfigChanged;
            BSEvents.menuSceneActive -= BSEvents_menuSceneActive;
            BSEvents.gameSceneActive -= BSEvents_gameSceneActive;
            foreach (var msg in _messages)
            {
                msg.OnLatePreRenderRebuildComplete -= OnRenderRebuildComplete;
                if (msg.Text.ChatMessage != null)
                {
                    _backupMessageQueue.Enqueue(msg.Text.ChatMessage);
                }
                if (msg.SubText.ChatMessage != null)
                {
                    _backupMessageQueue.Enqueue(msg.SubText.ChatMessage);
                }
                Destroy(msg);
            }
            _messages.Clear();
            Destroy(_rootGameObject);
            if (TextPool != null)
            {
                TextPool.Dispose();
                TextPool = null;
            }
            if (_chatScreen != null)
            {
                Destroy(_chatScreen);
                _chatScreen = null;
            }
            if (_chatFont != null)
            {
                Destroy(_chatFont.Font);
                _chatFont = null;
            }
            if (_chatMoverMaterial != null)
            {
                Destroy(_chatMoverMaterial);
                _chatMoverMaterial = null;
            }
        }

        private FloatingScreen _chatScreen;
        private GameObject _rootGameObject;
        private Material _chatMoverMaterial;
        private Image _bg;
        private void SetupScreens()
        {
            if (_chatScreen == null)
            {
                _chatScreen = FloatingScreen.CreateFloatingScreen(new Vector2(ChatWidth, ChatHeight), true, ChatPosition, Quaternion.Euler(ChatRotation));
                var canvas = _chatScreen.GetComponent<Canvas>();
                canvas.sortingOrder = 3;
                _chatScreen.SetRootViewController(this, true);
                _rootGameObject = new GameObject();
                DontDestroyOnLoad(_rootGameObject);
                _chatMoverMaterial = Instantiate(BeatSaberUtils.UINoGlow);
                _chatMoverMaterial.color = Color.clear;
                var renderer = _chatScreen.handle.gameObject.GetComponent<Renderer>();
                renderer.material = _chatMoverMaterial;
                _chatScreen.transform.SetParent(_rootGameObject.transform);
                _bg = _chatScreen.gameObject.GetComponent<UnityEngine.UI.Image>();
                _bg.material = Instantiate(BeatSaberUtils.UINoGlow);
                _bg.color = BackgroundColor;
                AddToVRPointer();
            }
        }

        private void floatingScreen_OnRelease(Vector3 pos, Quaternion rot)
        {
            if (_isInGame)
            {
                _chatConfig.Song_ChatPosition = pos;
                _chatConfig.Song_ChatRotation = rot.eulerAngles;
            }
            else
            {
                _chatConfig.Menu_ChatPosition = pos;
                _chatConfig.Menu_ChatRotation = rot.eulerAngles;
            }
            _chatConfig.Save();
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

        private void OnRenderRebuildComplete()
        {
            UpdateChatMessagePositions();
        }

        public void AddMessage(EnhancedTextMeshProUGUIWithBackground newMsg)
        {
            _messages.Enqueue(newMsg);
            newMsg.transform.localPosition = ReverseChatOrder ? new Vector3(0f, ChatHeight - (newMsg.transform as RectTransform).sizeDelta.y) : Vector3.zero;
            UpdateChatMessage(newMsg);
            ClearOldMessages();
            newMsg.OnLatePreRenderRebuildComplete += OnRenderRebuildComplete;
            newMsg.gameObject.SetActive(true);
        }

        private void UpdateChatUI()
        {
            ChatWidth = _chatConfig.ChatWidth;
            ChatHeight = _chatConfig.ChatHeight;
            FontSize = _chatConfig.FontSize;
            AccentColor = _chatConfig.AccentColor;
            HighlightColor = _chatConfig.HighlightColor;
            BackgroundColor = _chatConfig.BackgroundColor;
            PingColor = _chatConfig.PingColor;
            TextColor = _chatConfig.TextColor;
            ReverseChatOrder = _chatConfig.ReverseChatOrder;
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
            foreach (var msg in _messages)
            {
                UpdateChatMessage(msg, true);
            }
            UpdateChatMessagePositions();
        }

        private void UpdateChatMessagePositions()
        {
            float msgPos = ReverseChatOrder ? ChatHeight : 0;
            foreach (var chatMsg in _messages.AsEnumerable().Reverse())
            {
                var msgHeight = (chatMsg.transform as RectTransform).sizeDelta.y;
                if(ReverseChatOrder)
                {
                    msgPos -= msgHeight;
                }
                chatMsg.transform.localPosition = new Vector3(0, msgPos);
                if (!ReverseChatOrder)
                {
                    msgPos += msgHeight;
                }
            }
        }

        private void Instance_OnConfigChanged(ChatConfig obj)
        {
            UpdateChatUI();
        }

        private void ClearOldMessages()
        {
            while (_messages.TryPeek(out var msg) && ReverseChatOrder ? msg.transform.localPosition.y < 0 - (msg.transform as RectTransform).sizeDelta.y : msg.transform.localPosition.y >= ChatConfig.instance.ChatHeight)
            {
                _messages.TryDequeue(out msg);
                TextPool.Free(msg);
            }
        }

        private void UpdateChatMessage(EnhancedTextMeshProUGUIWithBackground msg, bool setAllDirty = false)
        {
            (msg.transform as RectTransform).sizeDelta = new Vector2(ChatWidth, (msg.transform as RectTransform).sizeDelta.y);
            msg.Text.font = _chatFont.Font;
            msg.Text.overflowMode = TextOverflowModes.Overflow;
            msg.Text.alignment = TextAlignmentOptions.BottomLeft;
            msg.Text.lineSpacing = 1.5f;
            msg.Text.color = TextColor;
            msg.Text.fontSize = FontSize;
            msg.Text.lineSpacing = 1.5f;

            msg.SubText.font = _chatFont.Font;
            msg.SubText.overflowMode = TextOverflowModes.Overflow;
            msg.SubText.alignment = TextAlignmentOptions.BottomLeft;
            msg.SubText.color = TextColor;
            msg.SubText.fontSize = FontSize;
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

        private string BuildClearedMessage(EnhancedTextMeshProUGUI msg)
        {
            StringBuilder sb = new StringBuilder($"<color={msg.ChatMessage.Sender.Color}>{msg.ChatMessage.Sender.DisplayName}</color>");
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
            UpdateChatMessagePositions();
        }

        public void OnMessageCleared(string messageId)
        {
            if (messageId != null)
            {
                MainThreadInvoker.Invoke(() =>
                {
                    foreach (var msg in _messages)
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
                foreach (var msg in _messages)
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
                var newMsg = TextPool.Alloc();
                newMsg.Text.text = $"<color=#bbbbbbbb>[{svc.DisplayName}] Success joining {channel.Id}</color>";
                newMsg.HighlightEnabled = true;
                newMsg.HighlightColor = Color.gray.ColorWithAlpha(0.05f);
                AddMessage(newMsg);
            });
        }

        public void OnChannelResourceDataCached(IChatChannel channel, Dictionary<string, IChatResourceData> resources)
        {
            MainThreadInvoker.Invoke(() =>
            {
                int count = 0;
                foreach (var emote in resources)
                {
                    if (emote.Value.IsAnimated)
                    {
                        //Logger.log.Info($"Caching animated emote {emote.Key}");
                        StartCoroutine(ChatImageProvider.instance.DownloadImage(emote.Value.Uri, "Emote", emote.Key, true));
                        count++;
                    }
                }
                Logger.log.Info($"{count} animated emotes total.");
            });
        }

        EnhancedTextMeshProUGUIWithBackground _lastMessage;
        public async void OnTextMessageReceived(IChatMessage msg)
        {
            string parsedMessage = await ChatMessageBuilder.BuildMessage(msg, _chatFont);
            MainThreadInvoker.Invoke(() =>
            {
                if (_lastMessage != null && !msg.IsSystemMessage && _lastMessage.Text.ChatMessage.Id == msg.Id)
                {
                    // If the last message received had the same id and isn't a system message, then this was a sub-message of the original and may need to be highlighted along with the original message
                    _lastMessage.SubText.text = parsedMessage;
                    _lastMessage.SubText.ChatMessage = msg;
                    _lastMessage.SubTextEnabled = true;
                    UpdateChatMessage(_lastMessage);
                }
                else
                {
                    var newMsg = TextPool.Alloc();
                    newMsg.Text.ChatMessage = msg;
                    newMsg.Text.text = parsedMessage;
                    AddMessage(newMsg);
                    _lastMessage = newMsg;
                }
            });
        }

        private void CreateChatFont()
        {
            if (_chatFont != null)
            {
                return;
            }

            // yield return new WaitUntil(() => FontManager.IsInitialized);

            TMP_FontAsset font = null;
            string fontName = _chatConfig.SystemFontName;
            if (!FontManager.TryGetTMPFontByFamily(fontName, out font))
            {
                Logger.log.Error($"Could not find font {fontName}! Falling back to Segoe UI");
                fontName = "Segoe UI";
            }
            _chatFont = new EnhancedFontInfo(BeatSaberUtils.SetupFont(font));

            foreach (var msg in _messages)
            {
                msg.Text.SetAllDirty();
                if (msg.SubTextEnabled)
                {
                    msg.SubText.SetAllDirty();
                }
            }

            while (_backupMessageQueue.TryDequeue(out var msg))
            {
                OnTextMessageReceived(msg);
            }
        }
    }
}
