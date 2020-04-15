using BS_Utils.Utilities;
using EnhancedStreamChat.Utilities;
using StreamCore;
using StreamCore.Interfaces;
using StreamCore.Services.Twitch;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
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
            sc = StreamCoreInstance.Create();
            var svc = sc.RunAllServices();
            svc.OnJoinChannel += Svc_OnJoinChannel;
            svc.OnTextMessageReceived += Svc_OnTextMessageReceived;
            svc.OnChatCleared += Svc_OnChatCleared;
            svc.OnMessageCleared += Svc_OnMessageCleared;
            MainThreadInvoker.TouchInstance();
            ChatImageProvider.TouchInstance();
            Task.Run(HandleOverflowMessageQueue);
            BSEvents.menuSceneLoadedFresh += BSEvents_menuSceneLoadedFresh;
        }

        ChatViewController _chatViewController;
        private IEnumerator PresentChat()
        {
            yield return new WaitForSeconds(1);
            _chatViewController = BeatSaberMarkupLanguage.BeatSaberUI.CreateViewController<ChatViewController>();
        }

        private void BSEvents_menuSceneLoadedFresh()
        {
            StartCoroutine(PresentChat());
        }

        private ConcurrentQueue<Action> _actionQueue = new ConcurrentQueue<Action>();
        private async Task HandleOverflowMessageQueue()
        {
            while (!_applicationIsQuitting)
            {
                if (_chatViewController == null)
                {
                    // If _chatViewController isn't instantiated yet, lock the semaphore and wait until it is.
                    await _msgLock.WaitAsync();
                    while (_chatViewController == null)
                    {
                        await Task.Delay(1000);
                    }
                }
                else
                {
                    // If _chatViewController is instantiated, wait here until the action queue has any actions.
                    while(_actionQueue.IsEmpty)
                    {
                        await Task.Delay(1000);
                    }
                    // Once an action is added to the queue, lock the semaphore before working through the queue.
                    await _msgLock.WaitAsync();
                }
                // Work through the queue of messages that has piled up one by one until they're all gone.
                while (_actionQueue.TryDequeue(out var action))
                {
                    Logger.log.Warn("Overflow!");
                    action?.Invoke();
                }
                // Release the lock, which will allow messages to pass through without the queue again
                _msgLock.Release();
            }
        }

        private SemaphoreSlim _msgLock = new SemaphoreSlim(1, 1);
        private void Svc_OnTextMessageReceived(IStreamingService svc, IChatMessage msg)
        {
            if (_chatViewController == null || !_msgLock.Wait(50))
            {
                _actionQueue.Enqueue(() => _chatViewController.OnTextMessageReceived(svc, msg));
            }
            else
            {
                _chatViewController.OnTextMessageReceived(svc, msg);
                _msgLock.Release();
            }
        }

        private void Svc_OnJoinChannel(IStreamingService svc, IChatChannel channel)
        {
            if (_chatViewController == null || !_msgLock.Wait(50))
            {
                _actionQueue.Enqueue(() => _chatViewController?.OnJoinChannel(svc, channel));
            }
            else
            {
                _chatViewController.OnJoinChannel(svc, channel);
                _msgLock.Release();
            }
        }

        private void Svc_OnMessageCleared(IStreamingService svc, string messageId)
        {
            if (_chatViewController == null || !_msgLock.Wait(50))
            {
                _actionQueue.Enqueue(() => _chatViewController?.OnMessageCleared(messageId));
            }
            else
            {
                _chatViewController.OnMessageCleared(messageId);
                _msgLock.Release();
            }
        }

        private void Svc_OnChatCleared(IStreamingService svc, string userId)
        {
            if (_chatViewController == null || !_msgLock.Wait(50))
            {
                _actionQueue.Enqueue(() => _chatViewController?.OnChatCleared(userId));
            }
            else
            {
                _chatViewController?.OnChatCleared(userId);
                _msgLock.Release();
            }
        }
    }
}
