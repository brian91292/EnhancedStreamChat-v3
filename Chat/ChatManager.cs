using BS_Utils.Utilities;
using EnhancedStreamChat.Utilities;
using StreamCore;
using StreamCore.Interfaces;
using StreamCore.Services.Twitch;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace EnhancedStreamChat.Chat
{
    public class ChatManager : PersistentSingleton<ChatManager>
    {
        StreamCoreInstance sc;
        void Start()
        {
            DontDestroyOnLoad(gameObject);
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

        private IEnumerator PresentChat()
        {
            yield return new WaitForSeconds(1);
            _chatViewController = BeatSaberMarkupLanguage.BeatSaberUI.CreateViewController<ChatViewController>();
            sc = StreamCoreInstance.Create();
            var svc = sc.RunAllServices();
            svc.OnLogin += Svc_OnLogin;
            svc.OnJoinChannel += Svc_OnJoinChannel;
            svc.OnTextMessageReceived += Svc_OnTextMessageReceived;
            svc.OnChatCleared += Svc_OnChatCleared;
            svc.OnMessageCleared += Svc_OnMessageCleared;

            MainThreadInvoker.TouchInstance();
            ChatImageProvider.TouchInstance();
        }

        private void BSEvents_menuSceneLoadedFresh()
        {
            StartCoroutine(PresentChat());
        }

        private void Svc_OnTextMessageReceived(IStreamingService svc, IChatMessage msg)
        {
            _chatViewController.OnTextMessageReceived(svc, msg);
        }

        private void Svc_OnJoinChannel(IStreamingService svc, IChatChannel channel)
        {
            _chatViewController?.OnJoinChannel(svc, channel);
        }

        private void Svc_OnLogin(IStreamingService svc)
        {
            if (svc is TwitchService twitch)
            {
                Logger.log.Info($"Twitch login success!");
            }
        }
    }
}
