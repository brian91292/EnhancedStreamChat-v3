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

namespace EnhancedStreamChat
{
#if DEBUG
    public class ChatViewController : HotReloadableViewController
    {
        public override string ResourceName => "EnhancedStreamChat.Resources.BSML.Chat.bsml";
        public override string ContentFilePath => @"C:\Users\brian\source\repos\EnhancedStreamChat-v3\Resources\BSML\Chat.bsml";
#else
    public class ChatViewController : BSMLViewController
    {
        public override string Content => BeatSaberMarkupLanguage.Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), "EnhancedStreamChat.Resources.BSML.Chat.bsml");
#endif

        private TMP_FontAsset _chatFont;
        private Queue<EnhancedTextMeshProUGUIWithBackground> _messageClearQueue = new Queue<EnhancedTextMeshProUGUIWithBackground>();
        private ObjectPool<EnhancedTextMeshProUGUIWithBackground> _textPool;

        protected FloatingScreen _floatingScreen;
        protected GameObject _gameObject;

        protected void Awake()
        {
            SetupScreen();
        }

        private void SetupScreen()
        {
            if (_floatingScreen == null)
            {
                _floatingScreen = FloatingScreen.CreateFloatingScreen(new Vector2(_chatWidth + 10, _chatHeight + 10), true, new Vector3(0, 4.3f, 2.3f), Quaternion.Euler(-30f, 0, 0));
                _floatingScreen.SetRootViewController(this, true);
                _floatingScreen.HandleSide = FloatingScreen.Side.Bottom;
                _floatingScreen.ShowHandle = false;
                _gameObject = new GameObject();
                DontDestroyOnLoad(_gameObject);
                _floatingScreen.transform.SetParent(_gameObject.transform);
                //_floatingScreen.gameObject.GetComponent<UnityEngine.UI.Image>().enabled = false;
            }
        }

        private void ClearMessage(EnhancedTextMeshProUGUIWithBackground msg)
        {
            StringBuilder sb = new StringBuilder($"<color={msg.Text.ChatMessage.Sender.Color}>{msg.Text.ChatMessage.Sender.Name}</color>");
            var badgeEndIndex = msg.Text.text.IndexOf("<color=");
            if (badgeEndIndex != -1)
            {
                sb.Insert(0, msg.Text.text.Substring(0, badgeEndIndex));
            }
            sb.Append(": <color=#99999955><message deleted></color>");
            msg.Text.text = sb.ToString();
        }

        public void OnMessageCleared(string messageId)
        {
            if (messageId != null)
            {
                MainThreadInvoker.Invoke(() =>
                {
                    foreach(var text in _messageClearQueue)
                    {
                        if(text.Text.ChatMessage.Id == messageId)
                        {
                            ClearMessage(text);
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
                    if (userId == null || msg.Text.ChatMessage.Sender.Id == userId)
                    {
                        ClearMessage(msg);
                    }
                }
            });
        }

        public async void OnTextMessageReceived(IStreamingService svc, IChatMessage msg)
        {
            if (_chatFont is null)
            {
                // TODO: maybe queue this message up or wait?
                return;
            }

            IChatUser loggedInUser = null;
            if(svc is TwitchService twitch)
            {
                loggedInUser = twitch.LoggedInUser;
            }
            bool isPing = false;
            if (loggedInUser != null)
            {
                isPing = msg.Message.Contains($"@{loggedInUser.Name}", StringComparison.OrdinalIgnoreCase);
            }
            string parsedMessage = parsedMessage = await ChatMessageBuilder.ParseMessage(msg, _chatFont);

            MainThreadInvoker.Invoke(() =>
            {
                var newMsg = _textPool.Alloc();
                newMsg.Text.ChatMessage = msg;
                newMsg.Text.text = parsedMessage;
                if(msg.IsSystemMessage && msg.IsHighlighted)
                {
                    newMsg.HighlightColor = Color.yellow;
                    newMsg.BackgroundColor = Color.grey.ColorWithAlpha(0.1f);
                    newMsg.BackgroundEnabled = true;
                }
                if (isPing)
                {
                    newMsg.HighlightColor = Color.clear;
                    newMsg.BackgroundColor = Color.red.ColorWithAlpha(0.1f);
                    newMsg.BackgroundEnabled = true;
                }
                newMsg.gameObject.SetActive(true);
                _messageClearQueue.Enqueue(newMsg);

                while(_messageClearQueue.TryPeek(out var nextClear) && nextClear.transform.localPosition.y > _chatHeight)
                {
                    _textPool.Free(_messageClearQueue.Dequeue());
                    //Logger.log.Info($"{_messageClearQueue.Count} messages shown");
                }
            });
        }

        [UIAction("on-settings-clicked")]
        private void OnSettingsClick()
        {
            Logger.log.Info("Settings clicked!");
        }

        private int _chatWidth = 140;
        [UIValue("chat-width")]
        public int ChatWidth
        {
            get => _chatWidth;
            set
            {
                _chatWidth = value;
                _floatingScreen.ScreenSize = new Vector2(_chatWidth + 10, _chatHeight + 10);
                NotifyPropertyChanged();
            }
        }

        private int _chatHeight = 160;
        [UIValue("chat-height")]
        public int ChatHeight
        {
            get => _chatHeight;
            set
            {
                _chatHeight = value;
                _floatingScreen.ScreenSize = new Vector2(_chatWidth + 10, _chatHeight + 10);
                NotifyPropertyChanged();
            }
        }

        [UIObject("ChatContainer")]
        GameObject _chatContainer;

        [UIAction("#post-parse")]
        private void PostParse()
        {
            if (_textPool is null)
            {
                _textPool = new ObjectPool<EnhancedTextMeshProUGUIWithBackground>(15,
                    Constructor: () =>
                    {
                        //var go = new GameObject();
                        //var bg = go.AddComponent<Image>();
                        //bg.color = Color.black.ColorWithAlpha(0.5f);
                        //var csf = bg.gameObject.AddComponent<ContentSizeFitter>();
                        //csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                        //csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

                        //var txt = go.AddComponent<EnhancedTextMeshProUGUI>();
                        //txt.color = Color.white;
                        //txt.fontSize = 4f;
                        //txt.enableWordWrapping = true;
                        //var csf2 = txt.gameObject.AddComponent<ContentSizeFitter>();
                        //csf2.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                        var go = new GameObject();
                        var msg = go.AddComponent<EnhancedTextMeshProUGUIWithBackground>();
                        msg.Text.color = Color.white;
                        msg.Text.fontSize = 4f;
                        msg.Text.enableWordWrapping = true;

                        return msg;
                    },
                    OnAlloc: (msg) =>
                    {
                        msg.Text.font = _chatFont;
                        //txt.material = BeatSaberUtils.UINoGlow;
                        //Logger.log.Info($"Text material is {txt.material.name}");
                        msg.gameObject.transform.SetParent(_chatContainer.transform, false);
                        msg.Text.overflowMode = TextOverflowModes.Overflow;
                        msg.Text.alignment = TextAlignmentOptions.BottomLeft;
                    },
                    OnFree: (msg) =>
                    {
                        try
                        {
                            msg.Text.text = null;
                            msg.BackgroundEnabled = false;
                            msg.gameObject.SetActive(false);
                            msg.gameObject.transform.SetParent(rectTransform);
                            msg.Text.ClearImages();
                        }
                        catch(Exception ex)
                        {
                            Logger.log.Error($"An exception occurred while trying to free CustomText object. {ex.ToString()}");
                        }
                    }
                );
            }
            if (_chatFont is null && !_isLoadingFonts)
            {
                StartCoroutine(LoadFonts());
            }

            SetupScreen();
            _chatContainer.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private bool _isLoadingFonts = false;
        private List<AssetBundle> _loadedAssets = new List<AssetBundle>();
        private IEnumerator LoadFonts()
        {
            _isLoadingFonts = true;
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
            }
            _isLoadingFonts = false;
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
                            _chatFont = SetupFont(TMP_FontAsset.CreateFontAsset(font));
                            _chatFont.name = font.fontNames[0] + " (Clone)";
                        }
                        else
                        {
                            //Logger.log.Info($"Fallback font: {font.fontNames[0]}");
                            var fallbackFont = SetupFont(TMP_FontAsset.CreateFontAsset(font));
                            fallbackFont.name = font.fontNames[0] + " (Clone)";
                            fallbackFonts.Add(fallbackFont);
                        }
                    }
                }
            }
        }

        // DaNike to the rescue 
        private static TMP_FontAsset SetupFont(TMP_FontAsset f)
        {
            var originalFont = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().Last(f2 => f2.name == "Teko-Medium SDF No Glow");
            var matCopy = Instantiate(originalFont.material);
            matCopy.mainTexture = f.material.mainTexture;
            matCopy.mainTextureOffset = f.material.mainTextureOffset;
            matCopy.mainTextureScale = f.material.mainTextureScale;
            f.material = matCopy;
            f = Instantiate(f);
            MaterialReferenceManager.AddFontAsset(f);
            return f;
        }
    }
}
