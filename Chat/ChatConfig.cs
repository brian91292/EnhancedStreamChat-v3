using StreamCore;
using StreamCore.Config;
using StreamCore.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace EnhancedStreamChat.Chat
{
    [ConfigHeader(
        " _______  __    _  __   __  _______  __    _  _______  _______  ______  \n" +
        "|       ||  |  | ||  | |  ||   _   ||  |  | ||       ||       ||      | \n" +
        "|    ___||   |_| ||  |_|  ||  |_|  ||   |_| ||       ||    ___||  _    |\n" +
        "|   |___ |       ||       ||       ||       ||       ||   |___ | | |   |\n" +
        "|    ___||  _    ||       ||       ||  _    ||      _||    ___|| |_|   |\n" +
        "|   |___ | | |   ||   _   ||   _   || | |   ||     |_ |   |___ |       |\n" +
        "|_______||_|  |__||__| |__||__| |__||_|  |__||_______||_______||______| \n" +
        "         _______  _______  ______    _______  _______  __   __          \n" +
        "        |       ||       ||    _ |  |       ||   _   ||  |_|  |         \n" +
        "        |  _____||_     _||   | ||  |    ___||  |_|  ||       |         \n" +
        "        | |_____   |   |  |   |_||_ |   |___ |       ||       |         \n" +
        "        |_____  |  |   |  |    __  ||    ___||       ||       |         \n" +
        "         _____| |  |   |  |   |  | ||   |___ |   _   || ||_|| |         \n" +
        "        |_______|  |___|  |___|  |_||_______||__| |__||_|   |_|         \n" +
        "     _______  __   __  _______  _______        __   __  _______         \n" +
        "    |       ||  | |  ||   _   ||       |      |  | |  ||       |        \n" +
        "    |       ||  |_|  ||  |_|  ||_     _|      |  |_|  ||___    |        \n" +
        "    |       ||       ||       |  |   |        |       | ___|   |        \n" +
        "    |      _||       ||       |  |   |        |       ||___    |        \n" +
        "    |     |_ |   _   ||   _   |  |   |         |     |  ___|   |        \n" +
        "    |_______||__| |__||__| |__|  |___|          |___|  |_______|        \n" +
        "                                                                        \n")]
        
    public class ChatConfig : ConfigBase<ChatConfig>
    {
        public static ChatConfig instance { get; private set; } = new ChatConfig(Path.Combine(Environment.CurrentDirectory, "UserData"), Assembly.GetExecutingAssembly().GetName().Name);

        [ConfigSection("Layout")]
        [ConfigMeta(Comment = "The world position of the chat")]
        public Vector3 Position = new Vector3(0, 4.1f, 2.3f);
        [ConfigMeta(Comment = "The world rotation of the chat")]
        public Vector3 Rotation = new Vector3(-20f, 0, 0);
        [ConfigMeta(Comment = "The width of the chat")]
        public int ChatWidth = 130;
        [ConfigMeta(Comment = "The height of the chat")]
        public int ChatHeight = 140;
        [ConfigMeta(Comment = "The size of the font")]
        public float FontSize = 3.4f;
        [ConfigMeta(Comment = "Allow movement of the chat")]
        public bool AllowMovement = false;

        [ConfigSection("Colors")]
        [ConfigMeta(Comment = "The accent color to be used on system messages")]
        public string AccentColor = "#9147FFFF";
        [ConfigMeta(Comment = "The highlight color to be used on system messages")]
        public string HighlightColor = "#9147FF10";
        [ConfigMeta(Comment = "The color pings will be highlighted as in chat")]
        public string PingColor = "#FF000022";

        private ChatConfig(string configDirectory, string configName) : base(configDirectory, configName)
        {
            Logger.log.Info("Config initialized.");
        }
    }
}
