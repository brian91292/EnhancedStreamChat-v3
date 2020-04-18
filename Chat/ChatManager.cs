using BS_Utils.Utilities;
using EnhancedStreamChat.Utilities;
using StreamCore;
using StreamCore.Interfaces;
using StreamCore.Services.Twitch;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace EnhancedStreamChat.Chat
{
    public class ChatManager : PersistentSingleton<ChatManager>
    {
        StreamCoreInstance _sc;
        void Start()
        {
            DontDestroyOnLoad(gameObject);
            _sc = StreamCoreInstance.Create();
            var svcs = _sc.RunAllServices();
            svcs.OnJoinChannel += (svc, channel) => QueueOrSendMessage(svc, channel, OnJoinChannel);
            svcs.OnTextMessageReceived += (svc, msg) => QueueOrSendMessage(svc, msg, OnTextMesssageReceived);
            svcs.OnChatCleared += (svc, uid) => QueueOrSendMessage(svc, uid, OnClearChat);
            svcs.OnMessageCleared += (svc, mid) => QueueOrSendMessage(svc, mid, OnClearMessage);
            MainThreadInvoker.TouchInstance();
            ChatImageProvider.TouchInstance();
            Task.Run(HandleOverflowMessageQueue);
            BSEvents.menuSceneLoadedFresh += BSEvents_menuSceneLoadedFresh;
        }

        ChatViewController _chatViewController;
        private void BSEvents_menuSceneLoadedFresh()
        {
            if (_chatViewController != null)
            {
                Destroy(_chatViewController.gameObject);
                _chatViewController = null;
                MainThreadInvoker.ClearQueue();
            }
            _chatViewController = BeatSaberMarkupLanguage.BeatSaberUI.CreateViewController<ChatViewController>();
        }

        private ConcurrentQueue<Action> _actionQueue = new ConcurrentQueue<Action>();
        private SemaphoreSlim _msgLock = new SemaphoreSlim(1, 1);
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
                        //Logger.log.Info("Queue is empty.");
                        await Task.Delay(1000);
                    }
                    // Once an action is added to the queue, lock the semaphore before working through the queue.
                    await _msgLock.WaitAsync();
                }
                int i = 0;
                DateTime start = DateTime.UtcNow;
                Stopwatch stopwatch = Stopwatch.StartNew();
                // Work through the queue of messages that has piled up one by one until they're all gone.
                while (_actionQueue.TryDequeue(out var action))
                {
                    action.Invoke();
                    i++;
                }
                stopwatch.Stop();
                Logger.log.Warn($"{i} overflowed actions were executed in {stopwatch.ElapsedTicks/TimeSpan.TicksPerMillisecond}ms.");
                // Release the lock, which will allow messages to pass through without the queue again
                _msgLock.Release();
            }
        }

        private void QueueOrSendMessage<T>(IStreamingService svc, T data, Action<IStreamingService, T> action)
        {
            if (_chatViewController == null || !_msgLock.Wait(50))
            {
                _actionQueue.Enqueue(() => action.Invoke(svc, data));
            }
            else
            {
                action.Invoke(svc, data);
                _msgLock.Release();
            }
        }

        private void OnTextMesssageReceived(IStreamingService svc, IChatMessage msg)
        {
            _chatViewController.OnTextMessageReceived(svc, msg);
        }

        private void OnJoinChannel(IStreamingService svc, IChatChannel channel)
        {
            _chatViewController.OnJoinChannel(svc, channel);
        }

        private void OnClearMessage(IStreamingService svc, string messageId)
        {
            _chatViewController.OnMessageCleared(messageId);
        }

        private void OnClearChat(IStreamingService svc, string userId)
        {
            _chatViewController.OnChatCleared(userId);
        }
    }
}
