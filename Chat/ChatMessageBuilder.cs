using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using StreamCore.Interfaces;
using StreamCore.Models;
using StreamCore.Models.Twitch;
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
        public static bool PrepareImages(IChatMessage msg, TMP_FontAsset font)
        {
            List<Task<EnhancedImageInfo>> tasks = new List<Task<EnhancedImageInfo>>();
            HashSet<string> pendingEmoteDownloads = new HashSet<string>();
            
            foreach (var emote in msg.Emotes)
            {
                if (pendingEmoteDownloads.Contains(emote.Id))
                {
                    continue;
                }
                if (!ChatImageProvider.instance.CachedImageInfo.ContainsKey(emote.Id))
                {
                    pendingEmoteDownloads.Add(emote.Id);
                    TaskCompletionSource<EnhancedImageInfo> tcs = new TaskCompletionSource<EnhancedImageInfo>();
                    SharedCoroutineStarter.instance.StartCoroutine(ChatImageProvider.instance.DownloadImage(emote.Uri, "Emote", emote.Id, emote.IsAnimated, (info) =>
                    {
                        if (info != null)
                        {
                            if (!EnhancedTextMeshProUGUI.TryRegisterImageInfo(font, info.Character, info))
                            {
                                Logger.log.Info($"Failed to register {emote.Id} in font {font.name}");
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
                if (!ChatImageProvider.instance.CachedImageInfo.ContainsKey(badge.Id))
                {
                    pendingEmoteDownloads.Add(badge.Id);
                    TaskCompletionSource<EnhancedImageInfo> tcs = new TaskCompletionSource<EnhancedImageInfo>();
                    SharedCoroutineStarter.instance.StartCoroutine(ChatImageProvider.instance.DownloadImage(badge.Uri, "Badge", badge.Id, false, (info) =>
                    {
                        if (info != null)
                        {
                            if (!EnhancedTextMeshProUGUI.TryRegisterImageInfo(font, info.Character, info))
                            {
                                Logger.log.Info($"Failed to register {badge.Id} in font {font.name}");
                            }
                        }
                        tcs.SetResult(info);
                    }));
                    tasks.Add(tcs.Task);
                }
            }

            // Wait on all the resources to be ready
            return Task.WaitAll(tasks.ToArray(), 30000);
        } 


        public static async Task<string> BuildMessage(IChatMessage msg, TMP_FontAsset font)
        {
            try
            {
                if(!PrepareImages(msg, font))
                {
                    Logger.log.Warn($"Failed to prepare images for msg \"{msg.Message}\"!");
                    return msg.Message;
                }

                ConcurrentStack<EnhancedImageInfo> badges = new ConcurrentStack<EnhancedImageInfo>();
                foreach (var badge in msg.Sender.Badges)
                {
                    if (!ChatImageProvider.instance.CachedImageInfo.TryGetValue(badge.Id, out var badgeInfo))
                    {
                        Logger.log.Warn($"Failed to find cached image info for badge \"{badge.Id}\"!");
                    }
                    badges.Push(badgeInfo);
                }

                StringBuilder sb = new StringBuilder(msg.Message);
                foreach (var emote in msg.Emotes)
                {
                    if (ChatImageProvider.instance.CachedImageInfo.TryGetValue(emote.Id, out var replace))
                    {
                        //Logger.log.Info($"Emote: {emote.Name}, StartIndex: {emote.StartIndex}, EndIndex: {emote.EndIndex}, Len: {sb.Length}");
                        string replaceStr = char.ConvertFromUtf32(replace.Character);
                        if(emote is TwitchEmote twitch && twitch.Bits > 0)
                        {
                            replaceStr = $"{replaceStr} </noparse><color={twitch.Color}><size=60%><b>{twitch.Bits}</b></size></color><noparse>";
                        }
                        // Replace emotes by index, in reverse order (msg.Emotes is sorted by emote.StartIndex in descending order)
                        sb.Replace(emote.Name, replaceStr, emote.StartIndex, emote.EndIndex - emote.StartIndex + 1);
                    }
                    else
                    {
                        Logger.log.Warn($"Emote {emote.Name} was missing from the emote dict! The request may have timed out?");
                    }
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
                        if (badges.TryPop(out var badge))
                        {
                            sb.Insert(0, $"{badge.Character} ");
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
