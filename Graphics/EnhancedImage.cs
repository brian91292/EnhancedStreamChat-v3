using BeatSaberMarkupLanguage.Animations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UI;

namespace EnhancedStreamChat.Graphics
{
    public class EnhancedImage : Image
    {
        public AnimationStateUpdater animStateUpdater { get; set; } = null;
    }
}
