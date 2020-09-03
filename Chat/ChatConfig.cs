using ChatCore.Config;
using System;
using System.IO;
using System.Reflection;
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

        [ConfigSection("Main")]
        [ConfigMeta(Comment = "When set to true, animated emotes will be precached in memory when the game starts.")]
        public bool PreCacheAnimatedEmotes = true;

        [ConfigSection("UI")]
        [ConfigMeta(Comment = "The name of the system font to be used in chat")]
        public string SystemFontName = "Segoe UI";
        [ConfigMeta(Comment = "The background color of the chat")]
        public Color BackgroundColor = Color.black.ColorWithAlpha(0.5f);
        [ConfigMeta(Comment = "The base color of the chat text.")]
        public Color TextColor = Color.white;
        [ConfigMeta(Comment = "The accent color to be used on system messages")]
        public Color AccentColor = new Color(0.57f, 0.28f, 1f, 1f);
        [ConfigMeta(Comment = "The highlight color to be used on system messages")]
        public Color HighlightColor = new Color(0.57f, 0.28f, 1f, 0.06f);
        [ConfigMeta(Comment = "The color pings will be highlighted as in chat")]
        public Color PingColor = new Color(1f, 0f, 0f, 0.13f);

        [ConfigSection("General Layout")]
        [ConfigMeta(Comment = "The width of the chat")]
        public int ChatWidth = 120;
        [ConfigMeta(Comment = "The height of the chat")]
        public int ChatHeight = 140;
        [ConfigMeta(Comment = "The size of the font")]
        public float FontSize = 3.4f;
        [ConfigMeta(Comment = "Allow movement of the chat")]
        public bool AllowMovement = false;
        [ConfigMeta(Comment = "Sync positions and rotations for the chat in menu and in-game")]
        public bool SyncOrientation = false;
        [ConfigMeta(Comment = "Reverse the order of the chat")]
        public bool ReverseChatOrder = false;

        [ConfigSection("In-Menu Layout")]
        [ConfigMeta(Comment = "The world position of the chat while at the main menu")]
        public Vector3 Menu_ChatPosition = new Vector3(0, 3.75f, 2.5f);
        [ConfigMeta(Comment = "The world rotation of the chat while at the main menu")]
        public Vector3 Menu_ChatRotation = new Vector3(325, 0, 0);

        [ConfigSection("In-Song Layout")]
        [ConfigMeta(Comment = "The world position of the chat while in-song")]
        public Vector3 Song_ChatPosition = new Vector3(0, 3.75f, 2.5f);
        [ConfigMeta(Comment = "The world rotation of the chat while in-song")]
        public Vector3 Song_ChatRotation = new Vector3(325, 0, 0);


        private ChatConfig(string configDirectory, string configName) : base(configDirectory, configName, Path.Combine(Environment.CurrentDirectory, "UserData", "StreamCore", "TwitchLoginInfo.ini"))
        {
            Logger.log.Info("Config initialized.");
        }
    }
}
