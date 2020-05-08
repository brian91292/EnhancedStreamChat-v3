using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Animations;
using EnhancedStreamChat.Utilities;
using ChatCore.Interfaces;
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
using IPA.Utilities.Async;

namespace EnhancedStreamChat.Graphics
{
    public class EnhancedTextMeshProUGUI : TextMeshProUGUI
    {
        public IChatMessage ChatMessage { get; set; } = null;
        public EnhancedFontInfo FontInfo { get; set; } = null;
        private static object _lock = new object();
        public event Action OnLatePreRenderRebuildComplete;

        private static ObjectPool<EnhancedImage> _imagePool = new ObjectPool<EnhancedImage>(50,
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

                });
                for (int i = 0; i < textInfo.characterCount; i++)
                {
                    TMP_CharacterInfo c = textInfo.characterInfo[i];
                    if (!c.isVisible || string.IsNullOrEmpty(text) || c.index >= text.Length)
                    {
                        // Skip invisible/empty/out of range chars
                        continue;
                    }

                    uint character = text[c.index];
                    if(c.index + 1 < text.Length && char.IsSurrogatePair(text[c.index], text[c.index + 1]))
                    {
                        // If it's a surrogate pair, convert the character
                        character = (uint)char.ConvertToUtf32(text[c.index], text[c.index + 1]);
                    }

                    if (FontInfo == null || !FontInfo.TryGetImageInfo(character, out var imageInfo) || imageInfo is null)
                    {
                        // Skip characters that have no imageInfo registered
                        continue;
                    }

                    MainThreadInvoker.Invoke(() =>
                    {
                        var img = _imagePool.Alloc();
                        try
                        {
                            if (imageInfo.AnimControllerData != null)
                            {
                                img.animStateUpdater.controllerData = imageInfo.AnimControllerData;
                                img.sprite = imageInfo.AnimControllerData.sprites[imageInfo.AnimControllerData.uvIndex];
                            }
                            else
                            {
                                img.sprite = imageInfo.Sprite;
                            }
                            img.material = BeatSaberUtils.UINoGlowMaterial;
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
                    });
                }
            }
            base.Rebuild(update);
            if (update == CanvasUpdate.LatePreRender)
            {
                MainThreadInvoker.Invoke(OnLatePreRenderRebuildComplete);
            }
        }
    }
}
