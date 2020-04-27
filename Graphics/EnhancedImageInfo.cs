using BeatSaberMarkupLanguage.Animations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace EnhancedStreamChat.Graphics
{
    public class EnhancedImageInfo
    {
        public string ImageId { get; internal set; }
        public Sprite Sprite { get; internal set; }
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public AnimationControllerData AnimControllerData { get; internal set; }
    }
}
