using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.FloatingScreen;
using BeatSaberMarkupLanguage.ViewControllers;
using BS_Utils.Utilities;
using ChatCore.Interfaces;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using HMUI;
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
        private bool _isInGame = false;

        private void Awake()
        {
            _chatConfig = ChatConfig.instance;
            CreateChatFont();
            SetupScreens();

            /// Update message position origin
            (transform.GetChild(0).transform as RectTransform).pivot = new Vector2(0.5f, 0f);

            TextPool = new ObjectPool<EnhancedTextMeshProUGUIWithBackground>(25,
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
                    msg.transform.SetParent(transform.GetChild(0).transform, false);
                    msg.gameObject.SetActive(false);
                    UpdateMessage(msg);
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

            _waitForEndOfFrame = new WaitForEndOfFrame();
            _waitUntilMessagePositionsNeedUpdate = new WaitUntil(() => _updateMessagePositions == true);
        }
        protected override void DidActivate(bool p_FirstActivation, bool p_AddedToHierarchy, bool p_ScreenSystemEnabling)
        {
            /// Forward event
            base.DidActivate(p_FirstActivation, p_AddedToHierarchy, p_ScreenSystemEnabling);

            if (p_FirstActivation)
            {
                GetComponent<CurvedCanvasSettings>().SetRadius(0f);

                ChatConfig.instance.OnConfigChanged += Instance_OnConfigChanged;
                BSEvents.menuSceneActive += BSEvents_menuSceneActive;
                BSEvents.gameSceneActive += BSEvents_gameSceneActive;

                SharedCoroutineStarter.instance.StartCoroutine(UpdateMessagePositions());
            }
        }

        // TODO: eventually figure out a way to make this more modular incase we want to create multiple instances of ChatDisplay
        private static ConcurrentQueue<IChatMessage> _backupMessageQueue = new ConcurrentQueue<IChatMessage>();
        protected override void OnDestroy()
        {
            base.OnDestroy();
            ChatConfig.instance.OnConfigChanged -= Instance_OnConfigChanged;
            BSEvents.menuSceneActive -= BSEvents_menuSceneActive;
            BSEvents.gameSceneActive -= BSEvents_gameSceneActive;
            SharedCoroutineStarter.instance.StopCoroutine(UpdateMessagePositions());
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
        private ImageView _bg = null;
        private void SetupScreens()
        {
            if (_chatScreen == null)
            {
                _chatScreen = FloatingScreen.CreateFloatingScreen(new Vector2(ChatWidth, ChatHeight), true, ChatPosition, Quaternion.identity);
                _chatScreen.GetComponent<CurvedCanvasSettings>().SetRadius(0f);

                var canvas = _chatScreen.GetComponent<Canvas>();
                canvas.sortingOrder = 3;

                _chatScreen.SetRootViewController(this, AnimationType.None);

                _rootGameObject = new GameObject();
                DontDestroyOnLoad(_rootGameObject);
                _chatMoverMaterial = Instantiate(BeatSaberUtils.UINoGlowMaterial);
                _chatMoverMaterial.color = Color.clear;
                var renderer = _chatScreen.handle.gameObject.GetComponent<Renderer>();
                renderer.material = _chatMoverMaterial;
                _chatScreen.transform.SetParent(_rootGameObject.transform);
                _chatScreen.ScreenRotation = Quaternion.Euler(ChatRotation);

                _chatScreen.HandleReleased += floatingScreen_OnRelease;

                _bg = _chatScreen.transform.GetChild(0).gameObject.AddComponent<ImageView>();
                _bg.sprite = Resources.FindObjectsOfTypeAll<Sprite>().First(x => x.name == "MainScreenMask");
                _bg.type = Image.Type.Sliced;
                _bg.material = Instantiate(BeatSaberUtils.UINoGlowMaterial);
                _bg.preserveAspect = true;
                _bg.color = BackgroundColor;
            }
        }

        private void Instance_OnConfigChanged(ChatConfig obj)
        {
            UpdateChatUI();
        }

        private void floatingScreen_OnRelease(object sender, FloatingScreenHandleEventArgs e)
        {
            if (_isInGame)
            {
                _chatConfig.Song_ChatPosition = e.Position;
                _chatConfig.Song_ChatRotation = e.Rotation.eulerAngles;
            }
            else
            {
                _chatConfig.Menu_ChatPosition = e.Position;
                _chatConfig.Menu_ChatRotation = e.Rotation.eulerAngles;
            }
            _chatConfig.Save();
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

        private bool _updateMessagePositions = false;
        private WaitUntil _waitUntilMessagePositionsNeedUpdate;
        private WaitForEndOfFrame _waitForEndOfFrame;
        private IEnumerator UpdateMessagePositions()
        {
            while (this.isInViewControllerHierarchy)
            {
                yield return _waitUntilMessagePositionsNeedUpdate;
                yield return _waitForEndOfFrame;
                float msgPos = ReverseChatOrder ? ChatHeight : 0;
                foreach (var chatMsg in _messages.AsEnumerable().Reverse())
                {
                    var msgHeight = (chatMsg.transform as RectTransform).sizeDelta.y;
                    if (ReverseChatOrder)
                    {
                        msgPos -= msgHeight;
                    }
                    chatMsg.transform.localPosition = new Vector3(0, msgPos);
                    if (!ReverseChatOrder)
                    {
                        msgPos += msgHeight;
                    }
                }
                _updateMessagePositions = false;
            }
        }

        private void OnRenderRebuildComplete()
        {
            _updateMessagePositions = true;
        }

        public void AddMessage(EnhancedTextMeshProUGUIWithBackground newMsg)
        {
            _messages.Enqueue(newMsg);
            UpdateMessage(newMsg);
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
            UpdateMessages();
        }

        private void UpdateMessages()
        {
            foreach (var msg in _messages)
            {
                UpdateMessage(msg, true);
            }
            _updateMessagePositions = true;
        }

        private void UpdateMessage(EnhancedTextMeshProUGUIWithBackground msg, bool setAllDirty = false)
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

        private void ClearOldMessages()
        {
            while (_messages.TryPeek(out var msg) && ReverseChatOrder ? msg.transform.localPosition.y < 0 - (msg.transform as RectTransform).sizeDelta.y : msg.transform.localPosition.y >= ChatConfig.instance.ChatHeight)
            {
                _messages.TryDequeue(out msg);
                TextPool.Free(msg);
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
            if (userId == null)
                return;

            MainThreadInvoker.Invoke(() =>
            {
                foreach (var msg in _messages)
                {
                    if (msg.Text.ChatMessage == null)
                        continue;

                    if (msg.Text.ChatMessage.Sender.Id == userId)
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
                if (_chatConfig.PreCacheAnimatedEmotes)
                {
                    foreach (var emote in resources)
                    {
                        if (emote.Value.IsAnimated)
                        {
                            StartCoroutine(ChatImageProvider.instance.PrecacheAnimatedImage(emote.Value.Uri, emote.Key, 110));
                            count++;
                        }
                    }
                    Logger.log.Info($"Pre-cached {count} animated emotes.");
                }
                else
                {
                    Logger.log.Warn("Pre-caching of animated emotes disabled by the user. If you're experiencing lag, re-enable emote precaching.");
                }
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
                    UpdateMessage(_lastMessage);
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

            TMP_FontAsset font = null;
            string fontName = _chatConfig.SystemFontName;
            if (!FontManager.TryGetTMPFontByFamily(fontName, out font))
            {
                Logger.log.Error($"Could not find font {fontName}! Falling back to Segoe UI");
                fontName = "Segoe UI";
            }
            font.material.shader = BeatSaberUtils.TMPNoGlowFontShader;
            _chatFont = new EnhancedFontInfo(font);

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
