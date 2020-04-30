using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using ChatCore.Interfaces;
using ChatCore.Models;
using ChatCore.Models.Twitch;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;

namespace EnhancedStreamChat.Chat
{
    public class ChatMessageBuilder
    {
        /// <summary>
        /// This function *blocks* the calling thread, and caches all the images required to display the message, then registers them with the provided font.
        /// </summary>
        /// <param name="msg">The chat message to get images from</param>
        /// <param name="font">The font to register these images to</param>
        public static bool PrepareImages(IChatMessage msg, EnhancedFontInfo font)
        {
            List<Task<EnhancedImageInfo>> tasks = new List<Task<EnhancedImageInfo>>();
            HashSet<string> pendingEmoteDownloads = new HashSet<string>();
            
            foreach (var emote in msg.Emotes)
            {
                if (pendingEmoteDownloads.Contains(emote.Id))
                {
                    continue;
                }
                if (!font.CharacterLookupTable.ContainsKey(emote.Id))
                {
                    pendingEmoteDownloads.Add(emote.Id);
                    TaskCompletionSource<EnhancedImageInfo> tcs = new TaskCompletionSource<EnhancedImageInfo>();
                    SharedCoroutineStarter.instance.StartCoroutine(ChatImageProvider.instance.DownloadImage(emote.Uri, "Emote", emote.Id, emote.IsAnimated, (info) =>
                    {
                        if (info != null)
                        {
                            if(!font.TryRegisterImageInfo(info, out var character))
                            {
                                Logger.log.Warn($"Failed to register emote \"{emote.Id}\" in font {font.Font.name}.");
                            }
                        }
                        tcs.SetResult(info);
                    }));
                    tasks.Add(tcs.Task);
                }
            }

            foreach (var badge in msg.Sender.Badges)
            {
                if (pendingEmoteDownloads.Contains(badge.Id))
                {
                    continue;
                }
                if (!font.CharacterLookupTable.ContainsKey(badge.Id))
                {
                    pendingEmoteDownloads.Add(badge.Id);
                    TaskCompletionSource<EnhancedImageInfo> tcs = new TaskCompletionSource<EnhancedImageInfo>();
                    SharedCoroutineStarter.instance.StartCoroutine(ChatImageProvider.instance.DownloadImage(badge.Uri, "Badge", badge.Id, false, (info) =>
                    {
                        if (info != null)
                        {
                            if (!font.TryRegisterImageInfo(info, out var character))
                            {
                                Logger.log.Warn($"Failed to register badge \"{badge.Id}\" in font {font.Font.name}.");
                            }
                        }
                        tcs.SetResult(info);
                    }));
                    tasks.Add(tcs.Task);
                }
            }

            // Wait on all the resources to be ready
            return Task.WaitAll(tasks.ToArray(), 15000);
        }

        public static async Task<string> BuildMessage(IChatMessage msg, EnhancedFontInfo font)
        {
            try
            {
                if(!PrepareImages(msg, font))
                {
                    Logger.log.Warn($"Failed to prepare some/all images for msg \"{msg.Message}\"!");
                    //return msg.Message;
                }

                ConcurrentStack<EnhancedImageInfo> badges = new ConcurrentStack<EnhancedImageInfo>();
                foreach (var badge in msg.Sender.Badges)
                {
                    if (!ChatImageProvider.instance.CachedImageInfo.TryGetValue(badge.Id, out var badgeInfo))
                    {
                        Logger.log.Warn($"Failed to find cached image info for badge \"{badge.Id}\"!");
                        continue;
                    }
                    badges.Push(badgeInfo);
                }

                StringBuilder sb = new StringBuilder(msg.Message);
                foreach (var emote in msg.Emotes)
                {
                    if (!ChatImageProvider.instance.CachedImageInfo.TryGetValue(emote.Id, out var replace))
                    {
                        Logger.log.Warn($"Emote {emote.Name} was missing from the emote dict! The request to {emote.Uri} may have timed out?");
                        continue;
                    }
                    //Logger.log.Info($"Emote: {emote.Name}, StartIndex: {emote.StartIndex}, EndIndex: {emote.EndIndex}, Len: {sb.Length}");
                    if(!font.TryGetCharacter(replace.ImageId, out uint character))
                    {
                        Logger.log.Warn($"Emote {emote.Name} was missing from the character dict! Font hay have run out of usable characters.");
                        continue;
                    }

                    // Replace emotes by index, in reverse order (msg.Emotes is sorted by emote.StartIndex in descending order)
                    sb.Replace(emote.Name, emote switch
                    {
                        TwitchEmote t when t.Bits > 0 => $"{char.ConvertFromUtf32((int)character)}\u00A0</noparse><color={t.Color}><size=77%><b>{t.Bits}\u00A0</b></size></color><noparse>",
                        _ => char.ConvertFromUtf32((int)character)
                    }, 
                    emote.StartIndex, emote.EndIndex - emote.StartIndex + 1);
                }

                if (msg.IsSystemMessage)
                {
                    // System messages get a grayish color to differenciate them from normal messages in chat, and do not receive a username/badge prefix
                    sb.Insert(0, $"<color=#bbbbbbbb>");
                    sb.Append("</color>");
                }
                else
                {
                    // Don't parse html tags in the message
                    sb.Insert(0, "<noparse>");
                    sb.Append("</noparse>");

                    if (msg.IsActionMessage)
                    {
                        // Message becomes the color of their name if it's an action message
                        sb.Insert(0, $"<color={msg.Sender.Color}><b>{msg.Sender.Name}</b> ");
                        sb.Append("</color>");
                    }
                    else
                    {
                        // Insert username w/ color
                        sb.Insert(0, $"<color={msg.Sender.Color}><b>{msg.Sender.Name}</b></color>: ");
                    }

                    for (int i = 0; i < msg.Sender.Badges.Length; i++)
                    {
                        // Insert user badges at the beginning of the string in reverse order
                        if (badges.TryPop(out var badge) && font.TryGetCharacter(badge.ImageId, out var character))
                        {
                            sb.Insert(0, $"{char.ConvertFromUtf32((int)character)} ");
                        }
                    }
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Logger.log.Error($"An exception occurred in ChatMessageBuilder while parsing msg with {msg.Emotes.Length} emotes. Msg: \"{msg.Message}\". {ex.ToString()}");
            }
            return msg.Message;
        }
    }
}
