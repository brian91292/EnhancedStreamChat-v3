using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.TextCore;

namespace EnhancedStreamChat.Graphics
{
    public class EnhancedFontInfo
    {
        public TMP_FontAsset Font { get; }
        public uint NextReplaceChar { get; private set; } = 0xe000;
        public ConcurrentDictionary<string, uint> CharacterLookupTable { get; } = new ConcurrentDictionary<string, uint>();
        public ConcurrentDictionary<uint, EnhancedImageInfo> ImageInfoLookupTable { get; } = new ConcurrentDictionary<uint, EnhancedImageInfo>();
        private object _lock = new object();

        public EnhancedFontInfo(TMP_FontAsset font)
        {
            Font = font;
        }

        public uint GetNextReplaceChar()
        {
            uint ret = NextReplaceChar++;
            // If we used up all the Private Use Area characters, move onto Supplementary Private Use Area-A
            if (NextReplaceChar > 0xF8FF && NextReplaceChar < 0xF0000)
            {
                Logger.log.Warn("Font is out of characters! Switching to overflow range.");
                NextReplaceChar = 0xF0000;
            }
            return ret;
        }

        public bool TryGetCharacter(string id, out uint character)
        {
            return CharacterLookupTable.TryGetValue(id, out character);
        }

        public bool TryGetImageInfo(uint character, out EnhancedImageInfo imageInfo)
        {
            return ImageInfoLookupTable.TryGetValue(character, out imageInfo);
        }

        public bool TryRegisterImageInfo(EnhancedImageInfo imageInfo, out uint replaceCharacter)
        {
            if (!CharacterLookupTable.ContainsKey(imageInfo.ImageId))
            {
                uint next;
                do
                {
                    next = GetNextReplaceChar();
                }
                while (Font.characterLookupTable.ContainsKey(next));
                Font.characterLookupTable.Add(next, new TMP_Character(next, new Glyph(next, new UnityEngine.TextCore.GlyphMetrics(0, 0, 0, 0, imageInfo.Width), new UnityEngine.TextCore.GlyphRect(0, 0, 0, 0))));
                CharacterLookupTable.TryAdd(imageInfo.ImageId, next);
                ImageInfoLookupTable.TryAdd(next, imageInfo);
                replaceCharacter = next;
                return true;
            }
            replaceCharacter = 0;
            return false;
        }

        public bool TryUnregisterImageInfo(string id, out uint unregisteredCharacter)
        {
            lock (_lock)
            {
                if (!CharacterLookupTable.TryGetValue(id, out var c))
                {
                    unregisteredCharacter = 0;
                    return false;
                }
                if (Font.characterLookupTable.ContainsKey(c))
                {
                    Font.characterLookupTable.Remove(c);
                }
                CharacterLookupTable.TryRemove(id, out unregisteredCharacter);
                return ImageInfoLookupTable.TryRemove(unregisteredCharacter, out var unregisteredImageInfo);
            }
        }
    }
}
