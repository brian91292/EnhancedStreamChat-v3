using BeatSaberMarkupLanguage.Animations;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace EnhancedStreamChat.Chat
{
    public class ChatImageProvider : PersistentSingleton<ChatImageProvider>
    {
        private Dictionary<string, EnhancedImageInfo> _cachedImageInfo = new Dictionary<string, EnhancedImageInfo>();
        public ReadOnlyDictionary<string, EnhancedImageInfo> CachedImageInfo { get; internal set; }
        private char _replaceChar = '\uE000';
        private void Awake()
        {
            CachedImageInfo = new ReadOnlyDictionary<string, EnhancedImageInfo>(_cachedImageInfo);
        }
        public IEnumerator DownloadImage(string uri, string category, string id, bool isAnimated, Action<EnhancedImageInfo> OnDownloadComplete, bool isRetry = false)
        {
            if (!_cachedImageInfo.TryGetValue(id, out var imageInfo))
            {
                if (string.IsNullOrEmpty(uri))
                {
                    Logger.log.Error($"URI is null or empty in request for resource {id}. Aborting!");
                    OnDownloadComplete?.Invoke(null);
                    yield break;
                }
                //Logger.log.Info($"Requesting image from uri: {uri}");
                Sprite sprite = null;
                int spriteWidth = 0, spriteHeight = 0;
                AnimationControllerData animControllerData = null;
                using (UnityWebRequest wr = UnityWebRequest.Get(uri))
                {
                    yield return wr.SendWebRequest();

                    if (wr.isHttpError)
                    {
                        // Failed to download due to http error, don't retry
                        OnDownloadComplete?.Invoke(null);
                        yield break;
                    }

                    if (wr.isNetworkError)
                    {
                        if (!isRetry)
                        {
                            Logger.log.Error($"A network error occurred during request to {uri}. Retrying in 3 seconds... {wr.error}");
                            yield return new WaitForSeconds(3);
                            StartCoroutine(DownloadImage(uri, category, id, isAnimated, OnDownloadComplete, true));
                            yield break;
                        }
                        OnDownloadComplete?.Invoke(null);
                        yield break;
                    }

                    if (isAnimated)
                    {
                        AnimationLoader.Process(AnimationType.GIF, wr.downloadHandler.data,
                            (tex, atlas, delays, width, height) =>
                            {
                                animControllerData = AnimationController.instance.Register(id, tex, atlas, delays);
                                sprite = animControllerData.sprite;
                                spriteWidth = width;
                                spriteHeight = height;
                            }
                        );
                        yield return new WaitUntil(() => sprite != null);
                    }
                    else
                    {
                        try
                        {
                            sprite = GraphicUtils.LoadSpriteRaw(wr.downloadHandler.data);
                            spriteWidth = sprite.texture.width;
                            spriteHeight = sprite.texture.height;
                        }
                        catch (Exception ex)
                        {
                            Logger.log.Error(ex);
                            sprite = null;
                        }
                    }
                }
                if (sprite != null)
                {
                    float scale = 1.0f;
                    float minSize = category == "Emote" ? 100 : 90;
                    if (spriteHeight < minSize)
                    {
                        scale = minSize / spriteHeight;
                    }
                    spriteWidth = (int)(scale * spriteWidth);
                    spriteHeight = (int)(scale * spriteHeight);

                    sprite.texture.wrapMode = TextureWrapMode.Clamp;
                    imageInfo = new EnhancedImageInfo()
                    {
                        Sprite = sprite,
                        Width = spriteWidth,
                        Height = spriteHeight,
                        Character = _replaceChar++,
                        AnimControllerData = animControllerData
                    };
                    //Logger.log.Info($"Caching image info for {id}. {(_replaceChar - '\uE000')} images have been cached.");
                    _cachedImageInfo.Add(id, imageInfo);
                }
            }
            OnDownloadComplete?.Invoke(imageInfo);
        }
    }
}
