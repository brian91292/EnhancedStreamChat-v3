using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Animations;
using EnhancedStreamChat.Utilities;
using StreamCore.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore;
using UnityEngine.UI;

namespace EnhancedStreamChat.Graphics
{
    public class EnhancedTextMeshProUGUI : TextMeshProUGUI
    {
        public IChatMessage ChatMessage { get; set; } = null;
        private static object _lock = new object();

        public event Action OnLatePreRenderRebuildComplete;

        private static Dictionary<TMP_FontAsset, Dictionary<char, EnhancedImageInfo>> _fontLookupTable { get; } = new Dictionary<TMP_FontAsset, Dictionary<char, EnhancedImageInfo>>();

        private static ObjectPool<EnhancedImage> _imagePool = new ObjectPool<EnhancedImage>(
            Constructor: () =>
            {
                var img = new GameObject().AddComponent<EnhancedImage>();
                DontDestroyOnLoad(img.gameObject);
                img.color = Color.white;
                img.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                img.rectTransform.pivot = new Vector2(0, 0);
                img.animStateUpdater = img.gameObject.AddComponent<AnimationStateUpdater>();
                img.animStateUpdater.image = img;
                img.gameObject.SetActive(false);
                return img;
            },
            OnFree: (img) =>
            {
                try
                {
                    img.animStateUpdater.controllerData = null;
                    img.gameObject.SetActive(false);
                    img.rectTransform.SetParent(null);
                    img.sprite = null;
                }
                catch(Exception ex)
                {
                    Logger.log.Error($"Exception while freeing EnhancedImage in EnhancedTextMeshProUGUI. {ex.ToString()}");
                }
            }
        );

        public static bool TryGetImageInfo(TMP_FontAsset font, char c, out EnhancedImageInfo imageInfo)
        {
            lock (_lock)
            {
                if (!_fontLookupTable.TryGetValue(font, out var fontLookupTable))
                {
                    imageInfo = null;
                    return false;
                }
                return fontLookupTable.TryGetValue(c, out imageInfo);
            }
        }

        public static bool TryRegisterImageInfo(TMP_FontAsset font, char c, EnhancedImageInfo imageInfo)
        {
            lock (_lock)
            {
                if (imageInfo == null)
                {
                    return false;
                }
                if (!_fontLookupTable.TryGetValue(font, out var fontLookupTable))
                {
                    fontLookupTable = new Dictionary<char, EnhancedImageInfo>();
                    _fontLookupTable.Add(font, fontLookupTable);
                }
                if (!fontLookupTable.ContainsKey(c))
                {
                    if (font.characterLookupTable.ContainsKey(c))
                    {
                        // Can't register an image for a character that already has something assigned to it
                        return false;
                    }
                    font.characterLookupTable.Add(c, new TMP_Character(c, new Glyph(c, new UnityEngine.TextCore.GlyphMetrics(imageInfo.Width, imageInfo.Height, 0, 0, imageInfo.Width), new UnityEngine.TextCore.GlyphRect(0, 0, 0, 0))));
                    fontLookupTable.Add(c, imageInfo);
                    return true;
                }
                return false;
            }
        }

        public static bool TryUnregisterImageInfo(TMP_FontAsset font, char c)
        {
            lock (_lock)
            {
                if (!_fontLookupTable.TryGetValue(font, out var fontLookupTable))
                {
                    return false;
                }
                if (!fontLookupTable.ContainsKey(c))
                {
                    return false;
                }
                if (font.characterLookupTable.ContainsKey(c))
                {
                    font.characterLookupTable.Remove(c);
                }
                return fontLookupTable.Remove(c);
            }
        }

        public static bool TryUnregisterFont(TMP_FontAsset font)
        {
            lock(_lock)
            {
                return _fontLookupTable.Remove(font);
            }
        }

        public void ClearImages()
        {
            foreach (var enhancedImage in _currentImages)
            {
                _imagePool.Free(enhancedImage);
            }
            _currentImages.Clear();
        }

        private List<EnhancedImage> _currentImages = new List<EnhancedImage>();
        public override void Rebuild(CanvasUpdate update)
        {
            if (update == CanvasUpdate.LatePreRender)
            {
                MainThreadInvoker.Invoke(() =>
                {
                    ClearImages();
                    for (int i = 0; i < textInfo.characterCount; i++)
                    {
                        TMP_CharacterInfo c = textInfo.characterInfo[i];
                        if (!c.isVisible || string.IsNullOrEmpty(text) || c.index >= text.Length)
                        {
                            // Skip invisible/empty/out of range chars
                            continue;
                        }
                        if (!_fontLookupTable.TryGetValue(font, out var imageLookupTable) || !imageLookupTable.TryGetValue(text[c.index], out var imageInfo) || imageInfo is null)
                        {
                            // Skip unregistered fonts and characters that have no imageInfo registered
                            continue;
                        }
                        var img = _imagePool.Alloc();
                        try
                        {
                            if (imageInfo.AnimControllerData != null)
                            {
                                img.animStateUpdater.controllerData = imageInfo.AnimControllerData;
                                img.sprite = imageInfo.AnimControllerData.sprites[0];
                            }
                            else
                            {
                                img.sprite = imageInfo.Sprite;
                            }
                            img.material = BeatSaberUtils.UINoGlow;
                            img.rectTransform.localScale = new Vector3(fontScale * 1.08f, fontScale * 1.08f, fontScale * 1.08f);
                            img.rectTransform.sizeDelta = new Vector2(imageInfo.Width, imageInfo.Height);
                            img.rectTransform.SetParent(rectTransform, false);
                            img.rectTransform.localPosition = c.topLeft - new Vector3(0, imageInfo.Height * fontScale * 0.558f / 2);
                            img.rectTransform.localRotation = Quaternion.identity;
                            img.gameObject.SetActive(true);
                            _currentImages.Add(img);
                        }
                        catch (Exception ex)
                        {
                            Logger.log.Error($"Exception while trying to overlay sprite. {ex.ToString()}");
                            _imagePool.Free(img);
                        }
                    }
                });
            }
            base.Rebuild(update);
            if (update == CanvasUpdate.LatePreRender)
            {
                MainThreadInvoker.Invoke(OnLatePreRenderRebuildComplete);
            }
        }
    }
}
