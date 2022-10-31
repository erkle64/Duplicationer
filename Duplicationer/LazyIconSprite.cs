using UnityEngine;
using static Duplicationer.BepInExLoader;

namespace Duplicationer
{
    public class LazyIconSprite
    {
        private Sprite sprite = null;
        private string iconName;

        public LazyIconSprite(string iconName)
        {
            this.iconName = iconName;
        }

        private Sprite FetchSprite()
        {
            sprite = ResourceDB.getIcon(iconName, 0);
            if (sprite == null) log.LogWarning("Failed to find icon 'icons-8-foundation-modified'");

            return sprite;
        }

        public Sprite Sprite => sprite == null ? FetchSprite() : sprite;
    }
}
