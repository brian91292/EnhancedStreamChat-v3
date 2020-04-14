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

namespace EnhancedStreamChat
{
    [Plugin(RuntimeOptions.DynamicInit)]
    public class Plugin
    {
        internal static Plugin instance { get; private set; }
        internal static string Name => "EnhancedStreamChat";

        [Init]
        public void Init(IPALogger logger)
        {
            instance = this;
            Logger.log = logger;
            Logger.log.Debug("Logger initialized.");
        }

        [OnEnable]
        public void OnEnable()
        {
            try
            {
                ChatManager.TouchInstance();
            }
            catch(Exception ex)
            {
                Logger.log.Error(ex);
            }
        }

        [OnDisable]
        public void OnDisable()
        {
            // TODO: actually make this disableable?
            Logger.log.Debug("OnDisable");
        }
    }
}
