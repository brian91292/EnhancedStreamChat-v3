namespace EnhancedStreamChat
{
    using BS_Utils.Utilities;
    using EnhancedStreamChat.Utilities;
    using StreamCore;
    using StreamCore.Interfaces;
    using StreamCore.Services.Twitch;
    using System;
    using System.Collections;
    using UnityEngine;

    public class ChatManager : PersistentSingleton<ChatManager>
    {
        StreamCoreInstance sc;
        void Start()
        {
            DontDestroyOnLoad(gameObject);

            sc = StreamCoreInstance.Create();
            var svc = sc.RunAllServices();
            svc.OnLogin += Svc_OnLogin;
            svc.OnJoinChannel += Svc_OnJoinChannel;
            svc.OnTextMessageReceived += Svc_OnTextMessageReceived;
            svc.OnChatCleared += Svc_OnChatCleared;
            svc.OnMessageCleared += Svc_OnMessageCleared;

            MainThreadInvoker.TouchInstance();
            ChatImageProvider.TouchInstance();

            BSEvents.menuSceneLoadedFresh += BSEvents_menuSceneLoadedFresh;
        }

        ChatViewController _chatViewController;
        private void Svc_OnMessageCleared(IStreamingService svc, string messageId)
        {
            _chatViewController?.OnMessageCleared(messageId);
        }

        private void Svc_OnChatCleared(IStreamingService svc, string userId)
        {
            _chatViewController?.OnChatCleared(userId);
        }

        private IEnumerator PresentTest()
        {
            yield return new WaitForSeconds(1);
            //testViewController = BeatSaberMarkupLanguage.BeatSaberUI.CreateViewController<ChatViewController>();
            //Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First().InvokeMethod("PresentViewController", new object[] { testViewController, null, false });
            _chatViewController = BeatSaberMarkupLanguage.BeatSaberUI.CreateViewController<ChatViewController>();
        }

        private void BSEvents_menuSceneLoadedFresh()
        {
            StartCoroutine(PresentTest());
        }

        private void Svc_OnTextMessageReceived(IStreamingService svc, IChatMessage msg)
        {
            _chatViewController?.OnTextMessageReceived(svc, msg);
        }

        private void Svc_OnJoinChannel(IStreamingService svc, IChatChannel channel)
        {
            Logger.log.Info($"Joined {channel.Id}");
        }

        private void Svc_OnLogin(IStreamingService svc)
        {
            if (svc is TwitchService twitch)
            {
                Logger.log.Info($"Twitch login success!");
                twitch.JoinChannel("xqcow"); // shuteye_orange, sah_yang
            }
        }
    }
}
