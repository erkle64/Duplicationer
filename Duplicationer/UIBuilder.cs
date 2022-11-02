using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

namespace Duplicationer
{
    public class UIBuilder
    {
        public UIBuilder(GameObject gameObject, UIBuilder parent)
        {
            GameObject = gameObject;
            Parent = parent;
        }

        public static UIBuilder BeginWith(GameObject gameObject)
        {
            return new UIBuilder(gameObject, null);
        }

        public delegate void OnClickDelegate();

        public GameObject GameObject { get; private set; }
        public UIBuilder Parent { get; private set; }
        public UIBuilder Done => Parent;
        public void End(bool validate = true)
        {
            if (validate && Parent != null && Parent.GameObject != null) throw new Exception(string.Format("Invalid UI Builder End: {0}", Parent.GameObject.name));
        }

        public UIBuilder Keep(ref GameObject gameObject)
        {
            gameObject = GameObject;
            return this;
        }

        public UIBuilder Keep<T>(ref T component) where T : MonoBehaviour
        {
            component = GameObject.GetComponent<T>();
            return this;
        }

        public UIBuilder SetOffsets(float offsetMinX, float offsetMinY, float offsetMaxX, float offsetMaxY)
        {
            var transform = GameObject.transform.Cast<RectTransform>();
            transform.offsetMin = new Vector2(offsetMinX, offsetMinY);
            transform.offsetMax = new Vector2(offsetMaxX, offsetMaxY);
            return this;
        }

        public UIBuilder SetPivot(float pivotX, float pivotY)
        {
            var transform = GameObject.transform.Cast<RectTransform>();
            transform.pivot = new Vector2(pivotX, pivotY);
            return this;
        }

        public UIBuilder SetAnchor(float anchorMinX, float anchorMinY, float anchorMaxX, float anchorMaxY)
        {
            var transform = GameObject.transform.Cast<RectTransform>();
            transform.anchorMin = new Vector2(anchorMinX, anchorMinY);
            transform.anchorMax = new Vector2(anchorMaxX, anchorMaxY);
            return this;
        }

        public UIBuilder SetRectTransform(float offsetMinX, float offsetMinY, float offsetMaxX, float offsetMaxY, float pivotX, float pivotY, float anchorMinX, float anchorMinY, float anchorMaxX, float anchorMaxY)
        {
            SetPivot(pivotX, pivotY);
            SetAnchor(anchorMinX, anchorMinY, anchorMaxX, anchorMaxY);
            SetOffsets(offsetMinX, offsetMinY, offsetMaxX, offsetMaxY);
            return this;
        }

        public UIBuilder SetRotation(float degrees)
        {
            var transform = GameObject.transform.Cast<RectTransform>();
            transform.rotation = Quaternion.EulerAngles(0.0f, 0.0f, degrees * Mathf.Deg2Rad);
            return this;
        }

        public UIBuilder SetHorizontalLayout(RectOffset padding, float spacing, TextAnchor childAlignment, bool reverseArrangement, bool childControlWidth, bool childControlHeight, bool childForceExpandWidth, bool childForceExpandHeight, bool childScaleWidth, bool childScaleHeight)
        {
            var component = GameObject.AddComponent<HorizontalLayoutGroup>();
            component.padding = padding;
            component.spacing = spacing;
            component.childAlignment = childAlignment;
            component.reverseArrangement = reverseArrangement;
            component.childControlWidth = childControlWidth;
            component.childControlHeight = childControlHeight;
            component.childForceExpandWidth = childForceExpandWidth;
            component.childForceExpandHeight = childForceExpandHeight;
            component.childScaleWidth = childScaleWidth;
            component.childScaleHeight = childScaleHeight;

            return this;
        }

        public UIBuilder SetVerticalLayout(RectOffset padding, float spacing, TextAnchor childAlignment, bool reverseArrangement, bool childControlWidth, bool childControlHeight, bool childForceExpandWidth, bool childForceExpandHeight, bool childScaleWidth, bool childScaleHeight)
        {
            var component = GameObject.AddComponent<VerticalLayoutGroup>();
            component.padding = padding;
            component.spacing = spacing;
            component.childAlignment = childAlignment;
            component.reverseArrangement = reverseArrangement;
            component.childControlWidth = childControlWidth;
            component.childControlHeight = childControlHeight;
            component.childForceExpandWidth = childForceExpandWidth;
            component.childForceExpandHeight = childForceExpandHeight;
            component.childScaleWidth = childScaleWidth;
            component.childScaleHeight = childScaleHeight;

            return this;
        }

        public UIBuilder AutoSize(ContentSizeFitter.FitMode horizontalFit = ContentSizeFitter.FitMode.Unconstrained, ContentSizeFitter.FitMode verticalFit = ContentSizeFitter.FitMode.PreferredSize)
        {
            var component = GameObject.AddComponent<ContentSizeFitter>();
            component.horizontalFit = horizontalFit;
            component.verticalFit = verticalFit;

            return this;
        }

        public UIBuilder Panel(string name, string textureName, Color color, Vector4 border, Image.Type imageType = Image.Type.Sliced, ContentSizeFitter.FitMode horizontalFit = ContentSizeFitter.FitMode.Unconstrained, ContentSizeFitter.FitMode verticalFit = ContentSizeFitter.FitMode.PreferredSize)
        {
            var go = new GameObject(name,
                UnhollowerRuntimeLib.Il2CppType.Of<Image>(),
                UnhollowerRuntimeLib.Il2CppType.Of<Outline>(),
                UnhollowerRuntimeLib.Il2CppType.Of<ContentSizeFitter>());
            if (GameObject != null) go.transform.SetParent(GameObject.transform, false);

            var image = go.GetComponent<Image>();
            image.type = imageType;
            image.sprite = GetSprite(textureName, border);
            image.color = color;

            var contentSizeFitter = go.GetComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = horizontalFit;
            contentSizeFitter.verticalFit = verticalFit;

            return new UIBuilder(go, this);
        }

        public UIBuilder Header(string name, string textureName, Color color, Vector4 border, Image.Type imageType = Image.Type.Sliced)
        {
            var go = new GameObject(name,
                UnhollowerRuntimeLib.Il2CppType.Of<Image>());
            if (GameObject != null) go.transform.SetParent(GameObject.transform, false);

            var image = go.GetComponent<Image>();
            image.type = imageType;
            image.sprite = GetSprite(textureName, border);
            image.color = color;

            return new UIBuilder(go, this);
        }

        public UIBuilder Button(string name, string textureName, Color color, Vector4 border, Image.Type imageType = Image.Type.Sliced)
        {
            var go = new GameObject(name,
                UnhollowerRuntimeLib.Il2CppType.Of<Image>(),
                UnhollowerRuntimeLib.Il2CppType.Of<Button>());
            if (GameObject != null) go.transform.SetParent(GameObject.transform, false);

            var image = go.GetComponent<Image>();
            image.type = imageType;
            image.sprite = GetSprite(textureName, border);
            image.color = color;

            return new UIBuilder(go, this);
        }

        public UIBuilder SetTransitionColors(Color normalColor, Color highlightedColor, Color pressedColor, Color selectedColor, Color disabledColor, float colorMultiplier, float fadeDuration)
        {
            var selectable = GameObject.GetComponent<Selectable>();
            if (selectable != null)
            {
                var colors = new ColorBlock();
                colors.normalColor = normalColor;
                colors.highlightedColor = highlightedColor;
                colors.pressedColor = pressedColor;
                colors.selectedColor = selectedColor;
                colors.disabledColor = disabledColor;
                colors.colorMultiplier = colorMultiplier;
                colors.fadeDuration = fadeDuration;

                selectable.transition = Selectable.Transition.ColorTint;
                selectable.colors = colors;
            }

            return this;
        }

        public UIBuilder SetOnClick(UnityAction action)
        {
            var button = GameObject.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(action);

            return this;
        }

        public UIBuilder Element(string name)
        {
            var go = new GameObject(name, UnhollowerRuntimeLib.Il2CppType.Of<RectTransform>());
            if(GameObject != null) go.transform.SetParent(GameObject.transform, false);

            return new UIBuilder(go, this);
        }

        public UIBuilder Element_Text(string text, string fontName, float fontSize, Color fontColor, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var component = GameObject.AddComponent<TextMeshProUGUI>();
            component.text = text;
            component.font = ResourceExt.FindFont(fontName);
            component.fontSize = fontSize;
            component.color = fontColor;
            component.alignment = alignment;

            return this;
        }

        public UIBuilder Element_Image(string textureName, Color color, Vector4 border, Image.Type imageType = Image.Type.Simple)
        {
            var component = GameObject.AddComponent<Image>();
            component.type = imageType;
            component.sprite = GetSprite(textureName, border);
            component.color = color;

            return this;
        }

        public UIBuilder Element_Tooltip(string text)
        {
            var component = GameObject.AddComponent<UIRaycastTooltip>();
            component.setTooltipText(text);

            return this;
        }

        public delegate void DoDelegate(UIBuilder builder);
        public UIBuilder Do(DoDelegate action)
        {
            action(this);
            return this;
        }

        public LayoutElementBuilder Layout()
        {
            return new LayoutElementBuilder(this);
        }

        private static Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private static Sprite GetSprite(string textureName, Vector4 border)
        {
            Sprite sprite;
            if (sprites.TryGetValue(textureName, out sprite)) return sprite;

            var texture = ResourceExt.FindTexture(textureName);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100.0f, 0, SpriteMeshType.FullRect, border);
        }

        public class LayoutElementBuilder
        {
            public LayoutElementBuilder(UIBuilder parent)
            {
                Parent = parent;
                component = Parent.GameObject.AddComponent<LayoutElement>();
            }

            public LayoutElementBuilder MinWidth(float value) { component.minWidth = value; return this; }
            public LayoutElementBuilder MinHeight(float value) { component.minHeight = value; return this; }
            public LayoutElementBuilder PreferredWidth(float value) { component.preferredWidth = value; return this; }
            public LayoutElementBuilder PreferredHeight(float value) { component.preferredHeight = value; return this; }
            public LayoutElementBuilder FlexibleWidth(float value) { component.flexibleWidth = value; return this; }
            public LayoutElementBuilder FlexibleHeight(float value) { component.flexibleHeight = value; return this; }

            public UIBuilder Parent { get; private set; }
            public UIBuilder Done => Parent;

            private LayoutElement component;
        }
    }
}
