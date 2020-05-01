using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace EnhancedStreamChat.Utilities
{
    public static class BeatSaberUtils
    {
        private static Material _noGlow;
        public static Material UINoGlow
        {
            get
            {
                if (_noGlow == null)
                {
                    _noGlow = Resources.FindObjectsOfTypeAll<Material>().Where(m => m.name == "UINoGlow").FirstOrDefault();
                    if (_noGlow != null)
                    {
                        _noGlow = Material.Instantiate(_noGlow);
                    }
                }
                return _noGlow;
            }
        }

        // DaNike to the rescue 
        public static TMP_FontAsset SetupFont(TMP_FontAsset f)
        {
            var originalFont = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().Last(f2 => f2.name == "Teko-Medium SDF No Glow");
            var matCopy = UnityEngine.Object.Instantiate(originalFont.material);
            matCopy.mainTexture = f.material.mainTexture;
            matCopy.mainTextureOffset = f.material.mainTextureOffset;
            matCopy.mainTextureScale = f.material.mainTextureScale;
            f.material = matCopy;
            f = UnityEngine.Object.Instantiate(f);
            //MaterialReferenceManager.AddFontAsset(f);
            return f;
        }
    }
}
