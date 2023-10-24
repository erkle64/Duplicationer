using UnityEngine;

namespace Duplicationer
{
    public class LazySprite
    {
        private string assetPath;

        private Sprite sprite = null;

        public LazySprite(string assetPath)
        {
            this.assetPath = assetPath;
        }

        public Sprite Sprite
        {
            get
            {
                if (sprite != null) return sprite;
                sprite = DuplicationerPlugin.GetAsset<Sprite>(assetPath);
                if (sprite == null) throw new System.Exception(assetPath);
                return sprite;
            }
        }
    }
}
