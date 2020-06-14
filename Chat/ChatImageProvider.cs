using BeatSaberMarkupLanguage.Animations;
using ChatCore.Models;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace EnhancedStreamChat.Chat
{
    public class ActiveDownload
    {
        public bool IsCompleted = false;
        public UnityWebRequest Request;
        public Action<byte[]> Finally;
    }

    public class ChatImageProvider : PersistentSingleton<ChatImageProvider>
    {
        private ConcurrentDictionary<string, EnhancedImageInfo> _cachedImageInfo = new ConcurrentDictionary<string, EnhancedImageInfo>();
        public ReadOnlyDictionary<string, EnhancedImageInfo> CachedImageInfo { get; internal set; }

        private ConcurrentDictionary<string, ActiveDownload> _activeDownloads = new ConcurrentDictionary<string, ActiveDownload>();
        private ConcurrentDictionary<string, Texture2D> _cachedSpriteSheets = new ConcurrentDictionary<string, Texture2D>();
        
        private void Awake()
        {
            CachedImageInfo = new ReadOnlyDictionary<string, EnhancedImageInfo>(_cachedImageInfo);
        }

        /// <summary>
        /// Retrieves the requested content from the provided Uri. 
        /// <para>
        /// The <paramref name="Finally"/> callback will *always* be called for this function. If it returns an empty byte array, that should be considered a failure.
        /// </para>
        /// </summary>
        /// <param name="uri">The resource location</param>
        /// <param name="Finally">A callback that occurs after the resource is retrieved. This will always occur even if the resource is already cached.</param>
        public IEnumerator DownloadContent(string uri, Action<byte[]> Finally, bool isRetry = false)
        {
            if (string.IsNullOrEmpty(uri))
            {
                Logger.log.Error($"URI is null or empty in request for resource {uri}. Aborting!");
                Finally?.Invoke(null);
                yield break;
            }

            if (!isRetry && _activeDownloads.TryGetValue(uri, out var activeDownload))
            {
                Logger.log.Info($"Request already active for {uri}");
                activeDownload.Finally -= Finally;
                activeDownload.Finally += Finally;
                yield return new WaitUntil(() => activeDownload.IsCompleted);
                yield break;
            }

            using (UnityWebRequest wr = UnityWebRequest.Get(uri))
            {
                activeDownload = new ActiveDownload()
                {
                    Finally = Finally,
                    Request = wr
                };
                _activeDownloads.TryAdd(uri, activeDownload);

                yield return wr.SendWebRequest();
                if (wr.isHttpError)
                {
                    // Failed to download due to http error, don't retry
                    Logger.log.Error($"An http error occurred during request to {uri}. Aborting! {wr.error}");
                    activeDownload.Finally?.Invoke(new byte[0]);
                    _activeDownloads.TryRemove(uri, out var d1);
                    yield break;
                }

                if (wr.isNetworkError)
                {
                    if (!isRetry)
                    {
                        Logger.log.Error($"A network error occurred during request to {uri}. Retrying in 3 seconds... {wr.error}");
                        yield return new WaitForSeconds(3);
                        StartCoroutine(DownloadContent(uri, Finally, true));
                        yield break;
                    }
                    activeDownload.Finally?.Invoke(new byte[0]);
                    _activeDownloads.TryRemove(uri, out var d2);
                    yield break;
                }

                var data = wr.downloadHandler.data;
                activeDownload.Finally?.Invoke(data);
                activeDownload.IsCompleted = true;
                _activeDownloads.TryRemove(uri, out var d3);
            }
        }

        public IEnumerator PrecacheAnimatedImage(string uri, string id, int forcedHeight = -1)
        {
            yield return TryCacheSingleImage(id, uri, true);
        }


        private void SetImageHeight(ref int spriteHeight, ref int spriteWidth, int height)
        {
            float scale = 1.0f;
            if (spriteHeight != (float)height)
            {
                scale = (float)height / spriteHeight;
            }
            spriteWidth = (int)(scale * spriteWidth);
            spriteHeight = (int)(scale * spriteHeight);
        }

        public IEnumerator TryCacheSingleImage(string id, string uri, bool isAnimated, Action<EnhancedImageInfo> Finally = null, int forcedHeight = -1)
        {
            if(_cachedImageInfo.TryGetValue(id, out var info))
            {
                Finally?.Invoke(info);
                yield break;
            }

            byte[] bytes = new byte[0];
            yield return ChatImageProvider.instance.DownloadContent(uri, (b) =>
            {
                bytes = b;
            });

            if (bytes.Length > 0)
            {
                //Logger.log.Info($"Finished download content for emote {id}!");
                yield return OnSingleImageCached(bytes, id, isAnimated, Finally, forcedHeight);
            }
            else
            {
                Logger.log.Info($"Received no bytes when requesting image with id {id}!");
            }
        }

        public IEnumerator OnSingleImageCached(byte[] bytes, string id, bool isAnimated, Action<EnhancedImageInfo> Finally = null, int forcedHeight = -1)
        {
            Sprite sprite = null;
            int spriteWidth = 0, spriteHeight = 0;
            AnimationControllerData animControllerData = null;
            if (isAnimated)
            {
                AnimationLoader.Process(AnimationType.GIF, bytes, (tex, atlas, delays, width, height) =>
                {
                    animControllerData = AnimationController.instance.Register(id, tex, atlas, delays);
                    sprite = animControllerData.sprite;
                    spriteWidth = width;
                    spriteHeight = height;
                });
                yield return new WaitUntil(() => animControllerData != null);
            }
            else
            {
                try
                {
                    sprite = GraphicUtils.LoadSpriteRaw(bytes);
                    spriteWidth = sprite.texture.width;
                    spriteHeight = sprite.texture.height;
                }
                catch (Exception ex)
                {
                    Logger.log.Error(ex);
                    sprite = null;
                }
            }
            EnhancedImageInfo ret = null;
            if (sprite != null)
            {
                if (forcedHeight != -1)
                {
                    SetImageHeight(ref spriteWidth, ref spriteHeight, forcedHeight);
                }
                ret = new EnhancedImageInfo()
                {
                    ImageId = id,
                    Sprite = sprite,
                    Width = spriteWidth,
                    Height = spriteHeight,
                    AnimControllerData = animControllerData
                };
                _cachedImageInfo[id] = ret;
            }
            Finally?.Invoke(ret);
        }

        public void TryCacheSpriteSheetImage(string id, string uri, ImageRect rect, Action<EnhancedImageInfo> Finally = null, int forcedHeight = -1)
        {
            if (_cachedImageInfo.TryGetValue(id, out var info))
            {
                Finally?.Invoke(info);
                return;
            }

            if(_cachedSpriteSheets.TryGetValue(uri, out var tex))
            {
                CacheSpriteSheetImage(id, rect, tex, Finally, forcedHeight);
            }
            else
            {
                StartCoroutine(ChatImageProvider.instance.DownloadContent(uri, (bytes) =>
                {
                    Logger.log.Info($"Finished download content for emote {id}!");
                    var tex = GraphicUtils.LoadTextureRaw(bytes);
                    _cachedSpriteSheets[uri] = tex;

                    CacheSpriteSheetImage(id, rect, tex, Finally, forcedHeight);
                }));
            }
        }

        private void CacheSpriteSheetImage(string id, ImageRect rect, Texture2D tex, Action<EnhancedImageInfo> Finally = null, int forcedHeight = -1)
        {
            int spriteWidth = rect.width, spriteHeight = rect.height;
            Sprite sprite = Sprite.Create(tex, new Rect(rect.x, tex.height - rect.y - spriteHeight, spriteWidth, spriteHeight), new Vector2(0, 0));
            sprite.texture.wrapMode = TextureWrapMode.Clamp;
            EnhancedImageInfo ret = null;
            if (sprite != null)
            {
                if (forcedHeight != -1)
                {
                    SetImageHeight(ref spriteWidth, ref spriteHeight, forcedHeight);
                }
                ret = new EnhancedImageInfo()
                {
                    ImageId = id,
                    Sprite = sprite,
                    Width = spriteWidth,
                    Height = spriteHeight,
                    AnimControllerData = null
                };
                _cachedImageInfo[id] = ret;
            }
            Finally?.Invoke(ret);
        }

        internal static void ClearCache()
        {
            if (instance._cachedImageInfo.Count > 0)
            {
                foreach (var info in instance._cachedImageInfo.Values)
                {
                    Destroy(info.Sprite);
                }
                instance._cachedImageInfo.Clear();
            }
        }
    }
}
