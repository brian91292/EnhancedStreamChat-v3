using ChatCore;
using ChatCore.Config;
using ChatCore.Models;
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
        " _______  __    _  __   __  _______  __    _  _______  _______  ______  ",
        "|       ||  |  | ||  | |  ||   _   ||  |  | ||       ||       ||      | ",
        "|    ___||   |_| ||  |_|  ||  |_|  ||   |_| ||       ||    ___||  _    |",
        "|   |___ |       ||       ||       ||       ||       ||   |___ | | |   |",
        "|    ___||  _    ||       ||       ||  _    ||      _||    ___|| |_|   |",
        "|   |___ | | |   ||   _   ||   _   || | |   ||     |_ |   |___ |       |",
        "|_______||_|  |__||__| |__||__| |__||_|  |__||_______||_______||______| ",
        "         _______  _______  ______    _______  _______  __   __          ",
        "        |       ||       ||    _ |  |       ||   _   ||  |_|  |         ",
        "        |  _____||_     _||   | ||  |    ___||  |_|  ||       |         ",
        "        | |_____   |   |  |   |_||_ |   |___ |       ||       |         ",
        "        |_____  |  |   |  |    __  ||    ___||       ||       |         ",
        "         _____| |  |   |  |   |  | ||   |___ |   _   || ||_|| |         ",
        "        |_______|  |___|  |___|  |_||_______||__| |__||_|   |_|         ",
        "      _______  __   __  _______  _______        __   __  _______        ",
        "     |       ||  | |  ||   _   ||       |      |  | |  ||       |       ",
        "     |       ||  |_|  ||  |_|  ||_     _|      |  |_|  ||___    |       ",
        "     |       ||       ||       |  |   |        |       | ___|   |       ",
        "     |      _||       ||       |  |   |        |       ||___    |       ",
        "     |     |_ |   _   ||   _   |  |   |         |     |  ___|   |       ",
        "     |_______||__| |__||__| |__|  |___|          |___|  |_______|       ",
        "                                                                        ")]
    public class ChatConfig : StreamCoreConfigConverter<ChatConfig>
    {
        public static ChatConfig instance { get; private set; } = new ChatConfig(Path.Combine(Environment.CurrentDirectory, "UserData"), Assembly.GetExecutingAssembly().GetName().Name);

        [ConfigSection("Colors")]
        [ConfigMeta(Comment = "The background color of the chat")]
        public string BackgroundColor = "#0000007F";
        [ConfigMeta(Comment = "The base color of the chat text.")]
        public string TextColor = "#FFFFFFFF";
        [ConfigMeta(Comment = "The accent color to be used on system messages")]
        public string AccentColor = "#9147FFFF";
        [ConfigMeta(Comment = "The highlight color to be used on system messages")]
        public string HighlightColor = "#9147FF10";
        [ConfigMeta(Comment = "The color pings will be highlighted as in chat")]
        public string PingColor = "#FF000022";

        [ConfigSection("General Layout")]
        [ConfigMeta(Comment = "The width of the chat")]
        public int ChatWidth = 120;
        [ConfigMeta(Comment = "The height of the chat")]
        public int ChatHeight = 140;
        [ConfigMeta(Comment = "The size of the font")]
        public float FontSize = 3.4f;
        [ConfigMeta(Comment = "Allow movement of the chat")]
        public bool AllowMovement = false;
        [ConfigMeta(Comment = "Reverse the order of the chat")]
        public bool ReverseChatOrder = false;
        [ConfigMeta(Comment = "The amount of space between lines in a chat message")]
        public float LineSpacing = 1.5f;

        [ConfigSection("In-Menu Layout")]
        [ConfigMeta(Comment = "The world position of the chat while at the main menu")]
        public Vector3 Menu_ChatPosition = new Vector3(0, 4.1f, 2.3f);
        [ConfigMeta(Comment = "The world rotation of the chat while at the main menu")]
        public Vector3 Menu_ChatRotation = new Vector3(-20f, 0, 0);

        [ConfigSection("In-Song Layout")]
        [ConfigMeta(Comment = "The world position of the chat while in-song")]
        public Vector3 Song_ChatPosition = new Vector3(0, 4.1f, 2.3f);
        [ConfigMeta(Comment = "The world rotation of the chat while in-song")]
        public Vector3 Song_ChatRotation = new Vector3(-20f, 0, 0);


        private ChatConfig(string configDirectory, string configName) : base(configDirectory, configName, Path.Combine(Environment.CurrentDirectory, "UserData", "StreamCore", "TwitchLoginInfo.ini"))
        {
            Logger.log.Info("Config initialized.");
        }
    }
}
