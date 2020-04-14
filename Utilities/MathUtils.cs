using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace EnhancedStreamChat.Utilities
{
    public static class MathUtils
    {
        public static Vector2 ScaleVec2(int width, int height, int maxWidth, int maxHeight)
        {
            if (width > maxWidth || height > maxHeight)
            {
                var ratioX = (double)maxWidth / width;
                var ratioY = (double)maxHeight / height;
                var ratio = Math.Min(ratioX, ratioY);

                var newWidth = (int)(width * ratio);
                var newHeight = (int)(height * ratio);

                return new Vector2(newWidth, newHeight);
            }
            return new Vector2(width, height);
        }
    }
}
