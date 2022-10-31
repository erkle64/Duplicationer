using System.Collections.Generic;
using System.IO;
using TMPro;
using UnhollowerBaseLib;
using UnityEngine;
using static Duplicationer.BepInExLoader;

namespace Duplicationer
{
    public static class ResourceExt
    {
        static Dictionary<string, Texture2D> loadedTextures = new Dictionary<string, Texture2D>();
        static Texture2D[] allTextures1 = null;
        static Texture2D[] allTextures2 = null;

        private static System.Collections.Generic.Dictionary<int, int> iconSizes = new System.Collections.Generic.Dictionary<int, int>() {
            { 0, 1024 },
            { 512, 512 },
            { 256, 256 },
            { 128, 128 },
            { 96, 96 },
            { 64, 64 }
        };

        public static void RegisterTexture(string name, Texture2D texture)
        {
            loadedTextures[name] = texture;
        }

        private static Sprite CreateSprite(Texture2D texture)
        {
            return Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100.0f);
        }

        private static Texture2D ResizeTexture(Texture2D inputTexture, int width, int height)
        {
            var outputTexture = new Texture2D(width, height, inputTexture.format, false, true);
            Graphics.ConvertTexture(inputTexture, outputTexture);
            return outputTexture;
        }

        public static Sprite LoadSprite(string identifier)
        {
            string iconPath = Path.Combine(iconFolder, identifier);
            Sprite mainSprite = null;

            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            var iconTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            iconTexture.LoadImage(new Il2CppStructArray<byte>(File.ReadAllBytes(iconPath)), true);
            int index = 0;
            foreach (var entry in iconSizes)
            {
                var sizeId = entry.Key;
                var size = entry.Value;
                var sizeIdentifier = identifier + ((sizeId > 0) ? "_" + sizeId.ToString() : "");
                var texture = (sizeId > 0) ? ResizeTexture(iconTexture, size, size) : iconTexture;
                texture.name = sizeIdentifier;
                var sprite = CreateSprite(texture);
                ResourceDB.dict_icons[sizeId][GameRoot.generateStringHash64(sizeIdentifier)] = sprite;
                if(sizeId == 0) mainSprite = sprite;

                ++index;
            }

            watch.Stop();
            log.LogInfo(string.Format("Loading icon '{0}' from '{1}' took {2}ms", identifier, iconPath, watch.ElapsedMilliseconds));

            return mainSprite;
        }

        public static Texture2D FindTexture(string name)
        {
            Texture2D result;
            if (loadedTextures.TryGetValue(name, out result)) return result;
            log.LogInfo(string.Format("Searching for texture '{0}'", name));

            if (allTextures1 == null) allTextures1 = Resources.FindObjectsOfTypeAll<Texture2D>();
            foreach (Texture2D texture in allTextures1)
            {
                if (texture.name == name)
                {
                    loadedTextures.Add(name, texture);
                    return texture;
                }
            }

            if (allTextures2 == null) allTextures2 = Resources.LoadAll<Texture2D>("");
            foreach (Texture2D texture in allTextures2)
            {
                if (texture.name == name)
                {
                    loadedTextures.Add(name, texture);
                    return texture;
                }
            }

            var icon = ResourceDB.getIcon(name);
            if(icon != null && icon.texture != null)
            {
                loadedTextures.Add(name, icon.texture);
                return icon.texture;
            }

            loadedTextures.Add(name, null);
            log.LogError("Could not find texture: " + name);
            return null;
        }


        static System.Collections.Generic.Dictionary<string, TMP_FontAsset> loadedFonts = new System.Collections.Generic.Dictionary<string, TMP_FontAsset>();
        static TMP_FontAsset[] allFonts1 = null;
        static TMP_FontAsset[] allFonts2 = null;

        public static void RegisterFont(string name, TMP_FontAsset font)
        {
            loadedFonts[name] = font;
        }

        public static TMP_FontAsset FindFont(string name)
        {
            TMP_FontAsset result;
            if (loadedFonts.TryGetValue(name, out result))
            {
                return result;
            }
            else
            {
                log.LogInfo(string.Format("Searching for font '{0}'", name));

                if (allFonts1 == null) allFonts1 = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                foreach (TMP_FontAsset font in allFonts1)
                {
                    if (font.name == name)
                    {
                        loadedFonts.Add(name, font);
                        return font;
                    }
                }

                if (allFonts2 == null) allFonts2 = Resources.LoadAll<TMP_FontAsset>("");
                foreach (TMP_FontAsset font in allFonts2)
                {
                    if (font.name == name)
                    {
                        loadedFonts.Add(name, font);
                        return font;
                    }
                }

                loadedFonts.Add(name, null);
                log.LogError("Could not find font: " + name);
                return null;
            }
        }
    }
}
