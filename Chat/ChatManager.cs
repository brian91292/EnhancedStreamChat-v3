﻿using BS_Utils.Utilities;
using EnhancedStreamChat.Utilities;
using ChatCore;
using ChatCore.Interfaces;
using ChatCore.Logging;
using ChatCore.Services;
using ChatCore.Services.Twitch;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using ChatCore.Config;
using BeatSaberMarkupLanguage;

namespace EnhancedStreamChat.Chat
{
    public class ChatManager : PersistentSingleton<ChatManager>
    {
        internal ChatCoreInstance _sc;
        internal ChatServiceMultiplexer _svcs;
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public override void OnEnable()
        {
            base.OnEnable();
            _sc = ChatCoreInstance.Create();
            //_sc.OnLogReceived += _sc_OnLogReceived;
            _svcs = _sc.RunAllServices();
            _svcs.OnJoinChannel += QueueOrSendOnJoinChannel;
            _svcs.OnTextMessageReceived += QueueOrSendOnTextMessageReceived;
            _svcs.OnChatCleared += QueueOrSendOnClearChat;
            _svcs.OnMessageCleared += QueueOrSendOnClearMessage;
            _svcs.OnChannelResourceDataCached += QueueOrSendOnChannelResourceDataCached;
            ChatImageProvider.TouchInstance();
            Task.Run(HandleOverflowMessageQueue);

            if (_chatDisplay != null)
            {
                DestroyImmediate(_chatDisplay.gameObject);
                _chatDisplay = null;
                MainThreadInvoker.ClearQueue();
            }
            _chatDisplay = BeatSaberUI.CreateViewController<ChatDisplay>();
            _chatDisplay.gameObject.SetActive(true);
        }

        private void _sc_OnLogReceived(CustomLogLevel level, string category, string log)
        {
            var newLevel = level switch
            {
                CustomLogLevel.Critical => IPA.Logging.Logger.Level.Critical,
                CustomLogLevel.Debug => IPA.Logging.Logger.Level.Debug,
                CustomLogLevel.Error => IPA.Logging.Logger.Level.Error,
                CustomLogLevel.Information => IPA.Logging.Logger.Level.Info,
                CustomLogLevel.Trace => IPA.Logging.Logger.Level.Trace,
                CustomLogLevel.Warning => IPA.Logging.Logger.Level.Warning,
                _ => IPA.Logging.Logger.Level.None
            };
            Logger.cclog.Log(newLevel, log);
        }

        public void OnDisable()
        {
            if(_svcs != null)
            {
                _svcs.OnJoinChannel -= QueueOrSendOnJoinChannel;
                _svcs.OnTextMessageReceived -= QueueOrSendOnTextMessageReceived;
                _svcs.OnChatCleared -= QueueOrSendOnClearChat;
                _svcs.OnMessageCleared -= QueueOrSendOnClearMessage;
                _svcs.OnChannelResourceDataCached -= QueueOrSendOnChannelResourceDataCached;
            }
            if (_sc != null)
            {
                //_sc.OnLogReceived -= _sc_OnLogReceived;
                _sc.StopAllServices();
            }
            if(_chatDisplay != null)
            {
                Destroy(_chatDisplay.gameObject);
                _chatDisplay = null;
            }
            MainThreadInvoker.ClearQueue();
            ChatImageProvider.ClearCache();
        }

        ChatDisplay _chatDisplay;

        private ConcurrentQueue<Action> _actionQueue = new ConcurrentQueue<Action>();
        private SemaphoreSlim _msgLock = new SemaphoreSlim(1, 1);
        private async Task HandleOverflowMessageQueue()
        {
            while (!_applicationIsQuitting)
            {
                if (_chatDisplay == null)
                {
                    // If _chatViewController isn't instantiated yet, lock the semaphore and wait until it is.
                    await _msgLock.WaitAsync();
                    while (_chatDisplay == null)
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

        private void QueueOrSendMessage<A>(IChatService svc, A a, Action<IChatService, A> action)
        {
            if (_chatDisplay == null || !_msgLock.Wait(50))
            {
                _actionQueue.Enqueue(() => action.Invoke(svc, a));
            }
            else
            {
                action.Invoke(svc, a);
                _msgLock.Release();
            }
        }
        private void QueueOrSendMessage<A, B>(IChatService svc, A a, B b, Action<IChatService, A, B> action)
        {
            if (_chatDisplay == null || !_msgLock.Wait(50))
            {
                _actionQueue.Enqueue(() => action.Invoke(svc, a, b));
            }
            else
            {
                action.Invoke(svc, a, b);
                _msgLock.Release();
            }
        }

        private void QueueOrSendOnChannelResourceDataCached(IChatService svc, IChatChannel channel, Dictionary<string, IChatResourceData> resources) => QueueOrSendMessage(svc, channel, resources, OnChannelResourceDataCached);
        private void OnChannelResourceDataCached(IChatService svc, IChatChannel channel, Dictionary<string, IChatResourceData> resources)
        {
            _chatDisplay.OnChannelResourceDataCached(channel, resources);
        }

        private void QueueOrSendOnTextMessageReceived(IChatService svc, IChatMessage msg) => QueueOrSendMessage(svc, msg, OnTextMesssageReceived);
        private void OnTextMesssageReceived(IChatService svc, IChatMessage msg)
        {
            _chatDisplay.OnTextMessageReceived(msg);
        }

        private void QueueOrSendOnJoinChannel(IChatService svc, IChatChannel channel) => QueueOrSendMessage(svc, channel, OnJoinChannel);
        private void OnJoinChannel(IChatService svc, IChatChannel channel)
        {
            _chatDisplay.OnJoinChannel(svc, channel);
        }

        private void QueueOrSendOnClearMessage(IChatService svc, string messageId) => QueueOrSendMessage(svc, messageId, OnClearMessage);
        private void OnClearMessage(IChatService svc, string messageId)
        {
            _chatDisplay.OnMessageCleared(messageId);
        }

        private void QueueOrSendOnClearChat(IChatService svc, string userId) => QueueOrSendMessage(svc, userId, OnClearChat);
        private void OnClearChat(IChatService svc, string userId)
        {
            _chatDisplay.OnChatCleared(userId);
        }
    }
}
