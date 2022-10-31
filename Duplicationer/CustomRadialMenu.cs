using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Duplicationer
{
    public static class CustomRadialMenu
    {
        public static bool isRadialMenuOpen { get; private set; } = false;
        public static CustomOption.ActivatedDelegate[] onActivatedDelegates = new CustomOption.ActivatedDelegate[16];
        public static int HideMenu()
        {
            var currentlyHighlightedSector = RadialMenu.singleton.currentlyHighlightedSector;

            if(currentlyHighlightedSector >=0  && currentlyHighlightedSector <= onActivatedDelegates.Length && onActivatedDelegates[currentlyHighlightedSector] != null)
            {
                onActivatedDelegates[currentlyHighlightedSector].Invoke();
            }

            isRadialMenuOpen = false;

            RadialMenu.hideMenu();

            return currentlyHighlightedSector;
        }

        public static void ShowMenu(params CustomOption[] options)
        {
            if (options.Length == 0) return;

            isRadialMenuOpen = true;

            var uIPreset = GlobalStateManager.getUIPreset();

            int maxSectors = RadialMenu.singleton.maxSectors;
            int length = Mathf.Min(maxSectors, options.Length);
            if (length > 0)
            {
                for (int index = 0; index < maxSectors; ++index)
                {
                    if (index < length)
                    {
                        RadialSector sector = RadialMenu.singleton.sectors[index];
                        GameObject gameObject = sector.gameObject;
                        RadialMenu.singleton.descriptions[index] = options[index].description;
                        RadialMenu.singleton.subTexts[index] = options[index].subText;
                        RadialMenu.singleton.backgroundColors[index] = uIPreset.windowBgColor;
                        onActivatedDelegates[index] = options[index].onActivated;
                        gameObject.SetActive(true);
                        float width = 360.0f / length;
                        float iconAngle = (index + 0.5f) * width * Mathf.Deg2Rad;
                        sector.setStyle(
                            index * width,
                            1.0f / length,
                            options[index].icon,
                            new Vector2(Mathf.Sin(iconAngle), Mathf.Cos(iconAngle)) * uIPreset.radialMenu_iconCenterOffsetPx,
                            Color.white,
                            options[index].hasSubText,
                            options[index].subText);
                    }
                    else
                    {
                        RadialMenu.singleton.sectors[index].gameObject.SetActive(false);
                        onActivatedDelegates[index] = null;
                    }
                }
            }
            RadialMenu.singleton.visibleSectors = length;
            RadialMenu.singleton.lastShownItemTemplate = null;
            RadialMenu.singleton.uiText_center.setText("Blueprint\nTool");
            RadialMenu.singleton.lastShownTime = Time.time;
            RadialMenu.singleton.showing = true;
            RadialMenu.singleton.gameObject.SetActive(true);
            GlobalStateManager.addCursorRequirementSoft();
            CursorManager.singleton.CenterCursor();
            RadialMenu.singleton.cachedMousePosition = CursorManager.singleton.mousePosition;
        }

        public class CustomOption
        {
            public delegate void ActivatedDelegate();
            public delegate bool IsEnabledDelegate();

            public string description;
            public Sprite icon;
            public bool hasSubText;
            public string subText;
            public ActivatedDelegate onActivated;
            private IsEnabledDelegate isEnabledHandler;

            public bool IsEnabled => isEnabledHandler == null ? true : isEnabledHandler.Invoke();

            public CustomOption(string description, Sprite icon, bool hasSubText, string subText, ActivatedDelegate onActivated, IsEnabledDelegate isEnabledHandler = null)
            {
                this.description = description;
                this.icon = icon;
                this.hasSubText = hasSubText;
                this.subText = subText;
                this.onActivated = onActivated;
                this.isEnabledHandler = isEnabledHandler;
            }
        }
    }
}
