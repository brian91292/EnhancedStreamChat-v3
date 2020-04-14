using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    }
}
