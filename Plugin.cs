using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using UnityEngine.SceneManagement;
using UnityEngine;
using IPALogger = IPA.Logging.Logger;
using EnhancedStreamChat.Chat;
using IPA.Loader;
using System.Reflection;

namespace EnhancedStreamChat
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        internal static Plugin instance { get; private set; }
        internal static string Name => "EnhancedStreamChat";
        internal static string Version => _meta.Version.ToString() ?? Assembly.GetExecutingAssembly().GetName().Version.ToString();

        private static PluginMetadata _meta;

        [Init]
        public void Init(IPALogger logger, PluginMetadata meta)
        {
            instance = this;
            _meta = meta;
            Logger.log = logger;
            Logger.log.Debug("Logger initialized.");
            var config = ChatConfig.instance;

        }
        [OnStart]
        public void OnApplicationStart()
        {
            BS_Utils.Utilities.BSEvents.lateMenuSceneLoadedFresh += (x) =>
            {
                try
                {
                    ChatManager.instance.enabled = true;
                }
                catch (Exception ex)
                {
                    Logger.log.Error(ex);
                }
            };
        }

        [OnDisable]
        public void OnDisable()
        {
            ChatManager.instance.enabled = false;
        }
    }
}
