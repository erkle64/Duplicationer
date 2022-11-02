using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Directory = System.IO.Directory;
using Path = System.IO.Path;
using File = System.IO.File;
using BepInEx.Configuration;

namespace Duplicationer
{
    [BepInPlugin(GUID, MODNAME, VERSION)]
    public class BepInExLoader : BepInEx.IL2CPP.BasePlugin
    {
        public const string
            MODNAME = "Duplicationer",
            AUTHOR = "erkle64",
            GUID = "com." + AUTHOR + "." + MODNAME,
            VERSION = "0.1.2";

        public static BepInEx.Logging.ManualLogSource log;

        public static string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static string dataFolder = Path.Combine(assemblyFolder, MODNAME);
        public static string iconFolder = Path.Combine(dataFolder, "icons");

        private static bool blueprintModeActive = false;

        private static MethodInfo methodRenderDragBuild = null;

        internal const string blueprintFilename = "blueprint.json";

        private static CustomHandheldMode[] customHandheldModes = new CustomHandheldMode[1]
        {
            new BlueprintToolCHM()
        };

        private static ConfigEntry<int> configMaxQueuedEventsPerFrame;
        private static ConfigEntry<int> configMaxBuildingValidationsPerFrame;
        private static ConfigEntry<int> configMaxTerrainValidationsPerFrame;

        private static ConfigEntry<string> configToggleBlueprintToolKey;
        public static KeyCode ToggleBlueprintToolKey { get; private set; }

        private static ConfigEntry<string> configPasteBlueprintKey;
        public static KeyCode PasteBlueprintKey { get; private set; }

        internal static bool IsAltHeld => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        internal static bool IsKeyboardInputAllowed => !GlobalStateManager.IsInputFieldFocused() && !(EscapeMenu.singleton != null && EscapeMenu.singleton.enabled);
        internal static bool IsMouseInputAllowed => !GlobalStateManager.isCursorOverUIElement() && !(EscapeMenu.singleton != null && EscapeMenu.singleton.enabled);

        private struct HandheldData
        {
            public int currentlySetMode;

            public HandheldData(int currentlySetMode)
            {
                this.currentlySetMode = currentlySetMode;
            }
        }
        private static Dictionary<ulong, HandheldData> handheldData = new Dictionary<ulong, HandheldData>();

        public BepInExLoader()
        {
            log = Log;
        }

        public override void Load()
        {
            log.LogMessage("Loading Duplicationer");

            configMaxQueuedEventsPerFrame = Config.Bind("Events", "MaxQueuedEventsPerFrame", 20, "");
            ActionManager.MaxQueuedEventsPerFrame = Mathf.Max(1, configMaxQueuedEventsPerFrame.Value);

            configMaxBuildingValidationsPerFrame = Config.Bind("Events", "MaxBuildingValidationsPerFrame", 4, "");
            BlueprintManager.MaxBuildingValidationsPerFrame = Mathf.Max(1, configMaxBuildingValidationsPerFrame.Value);

            configMaxTerrainValidationsPerFrame = Config.Bind("Events", "MaxTerrainValidationsPerFrame", 20, "");
            BlueprintManager.MaxTerrainValidationsPerFrame = Mathf.Max(1, configMaxTerrainValidationsPerFrame.Value);

            configToggleBlueprintToolKey = Config.Bind("Input", "ToggleBlueprintToolKey", "K", "Keyboard key for toggling the blueprint tool.\nValid values: Backspace, Tab, Clear, Return, Pause, Escape, Space, Exclaim,\nDoubleQuote, Hash, Dollar, Percent, Ampersand, Quote, LeftParen, RightParen,\nAsterisk, Plus, Comma, Minus, Period, Slash,\nAlpha0, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9,\nColon, Semicolon, Less, Equals, Greater, Question, At,\nLeftBracket, Backslash, RightBracket, Caret, Underscore, BackQuote,\nA, B, C, D, E, F, G, H, I, J, K, L, M,\nN, O, P, Q, R, S, T, U, V, W, X, Y, Z,\nLeftCurlyBracket, Pipe, RightCurlyBracket, Tilde, Delete,\nKeypad0, Keypad1, Keypad2, Keypad3, Keypad4, Keypad5, Keypad6, Keypad7, Keypad8, Keypad9,\nKeypadPeriod, KeypadDivide, KeypadMultiply, KeypadMinus, KeypadPlus, KeypadEnter, KeypadEquals,\nUpArrow, DownArrow, RightArrow, LeftArrow,\nInsert, Home, End, PageUp, PageDown,\nF1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12, F13, F14, F15,\nNumlock, CapsLock, ScrollLock,\nRightShift, LeftShift, RightControl, LeftControl, RightAlt, LeftAlt, RightApple, RightApple,\nLeftCommand, LeftCommand, LeftWindows, RightWindows, AltGr,\nHelp, Print, SysReq, Break, Menu,\nMouse0, Mouse1, Mouse2, Mouse3, Mouse4, Mouse5, Mouse6");
            var keyName = configToggleBlueprintToolKey.Value;
            try
            {
                ToggleBlueprintToolKey = (KeyCode)Enum.Parse(typeof(KeyCode), keyName, true);
            }
            catch (ArgumentException)
            {
                ToggleBlueprintToolKey = KeyCode.K;
            }

            configPasteBlueprintKey = Config.Bind("Input", "PasteBlueprintKey", "J", "Keyboard shortcut key for confirm paste");
            keyName = configPasteBlueprintKey.Value;
            try
            {
                PasteBlueprintKey = (KeyCode)Enum.Parse(typeof(KeyCode), keyName, true);
            }
            catch (ArgumentException)
            {
                PasteBlueprintKey = KeyCode.J;
            }

            if (!Directory.Exists(dataFolder)) Directory.CreateDirectory(dataFolder);

            try
            {
                var harmony = new Harmony(GUID);
                harmony.PatchAll(typeof(Patch));

                methodRenderDragBuild = AccessTools.Method(typeof(GameRoot), "_renderDragBuild");
                Debug.Assert(methodRenderDragBuild != null);
            }
            catch
            {
                log.LogError("Harmony - FAILED to Apply Patch's!");
            }
        }

        private static HandheldData GetHandheldData(Character character)
        {
            HandheldData data;
            if (!handheldData.TryGetValue(character.usernameHash, out data)) handheldData[character.usernameHash] = data = new HandheldData(0);
            return data;
        }

        private static void SetHandheldData(Character instance, HandheldData data)
        {
            handheldData[instance.usernameHash] = data;
        }

        internal static bool HasCustomData(BlueprintManager.BlueprintData.BuildableObjectData.CustomData[] customData, string identifier)
        {
            foreach (var data in customData) if (data.identifier == identifier) return true;
            return false;
        }

        internal static T GetCustomData<T>(BlueprintManager.BlueprintData.BuildableObjectData.CustomData[] customData, string identifier)
        {
            foreach (var data in customData) if (data.identifier == identifier) return (T)Convert.ChangeType(data.value, typeof(T));
            return default;
        }

        internal static void GetCustomDataList<T>(BlueprintManager.BlueprintData.BuildableObjectData.CustomData[] customData, string identifier, List<T> list)
        {
            foreach (var data in customData) if (data.identifier == identifier) list.Add((T)Convert.ChangeType(data.value, typeof(T)));
        }

        public static GameObject CreateCanvasObject(RenderMode renderMode, CanvasScaler.ScreenMatchMode screenMatchMode, float referencePixelsPerUnit)
            => new GameObject("Canvas",
                UnhollowerRuntimeLib.Il2CppType.Of<Canvas>(),
                UnhollowerRuntimeLib.Il2CppType.Of<CanvasScaler>(),
                UnhollowerRuntimeLib.Il2CppType.Of<GraphicRaycaster>());

        public static GameObject CreateEventSystemObject()
            => new GameObject("EventSystem",
                UnhollowerRuntimeLib.Il2CppType.Of<EventSystem>(),
                UnhollowerRuntimeLib.Il2CppType.Of<StandaloneInputModule>(),
                UnhollowerRuntimeLib.Il2CppType.Of<BaseInput>());

        public static GameObject DefaultCanvasGO => GameRoot.getDefaultCanvas();

        private static void CancelBlueprintMode()
        {
            if (!blueprintModeActive) return;

            blueprintModeActive = false;
            BlueprintManager.HideBlueprint();

            var character = GameRoot.getClientCharacter();

            HandheldData data = GetHandheldData(character);
            if (data.currentlySetMode >= 4)
            {
                customHandheldModes[data.currentlySetMode - 4].Exit();
                data.currentlySetMode = 0;
                SetHandheldData(character, data);
            }

            if (character != null) character.clientData.setEquipmentMode(0);
        }

        [HarmonyPatch]
        public class Patch
        {
            private static ulong lastSpawnedBuildableWrapperEntityId = 0;

            [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.OnGameInitializationDone))]
            [HarmonyPostfix]
            public static void GameCamera_OnGameInitializationDone()
            {
                ActionManager.OnGameInitializationDone();
                BlueprintManager.OnGameInitializationDone();

                var testPath = Path.Combine(dataFolder, blueprintFilename);
                if (File.Exists(testPath))
                {
                    BlueprintManager.LoadBlueprint(File.ReadAllText(testPath));
                }
            }

            [HarmonyPatch(typeof(InputProxy), nameof(InputProxy.Update))]
            [HarmonyPrefix]
            public static void Update()
            {
                BlueprintManager.Update();
                ActionManager.Update();
            }

            [HarmonyPatch(typeof(HandheldTabletHH), nameof(HandheldTabletHH._updateBehavoir))]
            [HarmonyPrefix]
            public static bool HandheldTabletHH_updateBehavoir(HandheldTabletHH __instance)
            {
                if (!__instance.relatedCharacter.sessionOnly_isClientCharacter) return true;

                HandheldData data = GetHandheldData(__instance.relatedCharacter);
                if (data.currentlySetMode < 4) return true;

                int customHandheldModeIndex = data.currentlySetMode - 4;
                if (customHandheldModeIndex < customHandheldModes.Length)
                {
                    var customHandheldMode = customHandheldModes[customHandheldModeIndex];
                    if (Input.GetKeyDown(KeyCode.Mouse1) && IsMouseInputAllowed)
                    {
                        customHandheldMode.ShowMenu();
                    }
                    if (CustomRadialMenu.isRadialMenuOpen && !Input.GetKey(KeyCode.Mouse1))
                    {
                        int selected = CustomRadialMenu.HideMenu();
                        if (selected >= 0)
                        {
                            customHandheldMode.HideMenu(selected);
                        }
                    }

                    customHandheldMode.UpdateBehavoir();
                }

                return false;
            }

            [HarmonyPatch(typeof(HandheldTabletHH), nameof(HandheldTabletHH.setTabletMode))]
            [HarmonyPrefix]
            public static void HandheldTabletHH_setTabletMode(HandheldTabletHH __instance, ref int characterEquipmentMode)
            {
                if (!__instance.relatedCharacter.sessionOnly_isClientCharacter) return;

                const int newArraySize = 4;
                if (__instance.materialsByMode.Length < newArraySize)
                {
                    var materialsByMode = new Material[newArraySize];
                    var containersByMode = new GameObject[newArraySize];
                    for (int i = 0; i < 3; i++)
                    {
                        materialsByMode[i] = __instance.materialsByMode[i];
                        containersByMode[i] = __instance.containersByMode[i];
                    }
                    materialsByMode[3] = __instance.materialsByMode[0];
                    containersByMode[3] = __instance.containersByMode[0];
                    __instance.materialsByMode = materialsByMode;
                    __instance.containersByMode = containersByMode;
                }

                HandheldData data = GetHandheldData(__instance.relatedCharacter);

                if (data.currentlySetMode != characterEquipmentMode && data.currentlySetMode >= 4)
                {
                    customHandheldModes[data.currentlySetMode - 4].Exit();
                }

                data.currentlySetMode = characterEquipmentMode;
                SetHandheldData(__instance.relatedCharacter, data);

                characterEquipmentMode = (characterEquipmentMode < 4) ? characterEquipmentMode : 1;
            }

            [HarmonyPatch(typeof(HandheldTabletHH), nameof(HandheldTabletHH.initMode))]
            [HarmonyPrefix]
            public static bool HandheldTabletHH_initMode(HandheldTabletHH __instance)
            {
                if (!__instance.relatedCharacter.sessionOnly_isClientCharacter) return true;

                HandheldData data = GetHandheldData(__instance.relatedCharacter);
                if (data.currentlySetMode >= 4)
                {
                    blueprintModeActive = true;
                    __instance.currentlySetMode = 1;
                    __instance.relatedCharacter.clientData.equipmentMode = 1;
                    customHandheldModes[data.currentlySetMode - 4].Enter();
                    return false;
                }
                else
                {
                    blueprintModeActive = false;
                    return true;
                }
            }

            [HarmonyPatch(typeof(Character.ClientData), nameof(Character.ClientData.deselect))]
            [HarmonyPrefix]
            public static void ClientData_deselect()
            {
                CancelBlueprintMode();
            }

            [HarmonyPatch(typeof(Character.ClientData), nameof(Character.ClientData.setEquippedItemTemplate))]
            [HarmonyPrefix]
            public static void ClientData_setEquippedItemTemplate(Character.ClientData __instance, ItemTemplate itemTemplate)
            {
                if (itemTemplate != null) CancelBlueprintMode();
            }

            [HarmonyPatch(typeof(Character.ClientData), nameof(Character.ClientData.setEquipmentMode))]
            [HarmonyPrefix]
            public static bool ClientData_setEquipmentMode(Character.ClientData __instance, int modeToSet)
            {
                if (modeToSet <= 3)
                {
                    CancelBlueprintMode();
                    return true;
                }

                __instance.setBuildModeIntoCopyWithSettingsMode(null);
                if (modeToSet != 0 && __instance.equippedItem != null) __instance.setEquippedItemTemplate(null);
                __instance.equipmentMode = modeToSet;
                if (modeToSet != 0) GameRoot.characterEquipmentModeChangeUnlockCallback(modeToSet);
                IconShortcutHelper.singleton.RefreshIcons();

                var character = GameRoot.getTabletHH().relatedCharacter;
                Debug.Assert(character != null);
                Character.SaveSync_EquipmentMode syncEquipmentMode = new Character.SaveSync_EquipmentMode(character.usernameHash, modeToSet);

                GameRoot.addLockstepEvent(syncEquipmentMode);

                return false;
            }

            [HarmonyPatch(typeof(RenderCharacter), nameof(RenderCharacter.Update))]
            [HarmonyPrefix]
            public static void RenderCharacter_Update(RenderCharacter __instance)
            {
                if (!__instance.isClientControlled) return;

                if (Input.GetKeyDown(ToggleBlueprintToolKey) && IsKeyboardInputAllowed)
                {
                    Character.ClientData clientData = __instance.relatedCharacter.clientData;
                    HandheldData data = GetHandheldData(__instance.relatedCharacter);
                    if (data.currentlySetMode != 4)
                    {
                        clientData.setEquipmentMode(4);
                    }
                    else
                    {
                        clientData.setEquipmentMode(0);
                    }
                }
            }

            [HarmonyPatch(typeof(BuildEntityEvent), nameof(BuildEntityEvent.processEvent))]
            [HarmonyPrefix]
            public static void BuildEntityEvent_processEvent_prefix(BuildEntityEvent __instance)
            {
                if (__instance.characterHash != GameRoot.getClientCharacter().usernameHash) return;
                lastSpawnedBuildableWrapperEntityId = 0;
            }

            [HarmonyPatch(typeof(BuildEntityEvent), nameof(BuildEntityEvent.processEvent))]
            [HarmonyPostfix]
            public static void BuildEntityEvent_processEvent_postfix(BuildEntityEvent __instance)
            {
                if (__instance.characterHash != GameRoot.getClientCharacter().usernameHash) return;
                ActionManager.InvokeAndRemoveBuildEvent(__instance, lastSpawnedBuildableWrapperEntityId);
            }

            [HarmonyPatch(typeof(BuildingManager), nameof(BuildingManager.buildingManager_constructBuildableWrapper))]
            [HarmonyPostfix]
            public static void BuildingManager_buildingManager_constructBuildableWrapper(v3i pos, ulong buildableObjectTemplateId, ulong __result)
            {
                lastSpawnedBuildableWrapperEntityId = __result;
            }

            [HarmonyPatch(typeof(ResourceDB), nameof(ResourceDB.InitOnApplicationStart))]
            [HarmonyPostfix]
            public static void ResourceDB_InitOnApplicationStart()
            {
                BlueprintToolCHM.LoadIconSprites();
            }

            //[HarmonyPatch(typeof(GameRoot), nameof(GameRoot.addLockstepEvent))]
            //[HarmonyPostfix]
            //public static void GameRoot_addLockstepEvent(GameRoot.LockstepEvent e)
            //{
            //    log.LogInfo("====== GameRoot.addLockstepEvent ======");
            //    log.LogInfo(e.getDbgInfo());
            //}
        }
    }
}
