using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System;
using Directory = System.IO.Directory;
using Path = System.IO.Path;
using BepInEx.Configuration;
using Unfoundry;

namespace Duplicationer
{
    [BepInPlugin(GUID, MODNAME, VERSION)]
    [BepInDependency(Unfoundry.Plugin.GUID, BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BepInEx.IL2CPP.BasePlugin
    {
        public const string
            MODNAME = "Duplicationer",
            AUTHOR = "erkle64",
            GUID = "com." + AUTHOR + "." + MODNAME,
            VERSION = "0.2.0";

        public static BepInEx.Logging.ManualLogSource log;

        public static string PluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static string BlueprintFolder = Path.Combine(Application.persistentDataPath, MODNAME.ToLower());

        public const string BlueprintExtension = "ebp";

        private static ConfigEntry<string> configBlueprintLibraryPath;
        private static ConfigEntry<string> configCurrentBlueprintFile;

        private static ConfigEntry<int> configMaxBuildingValidationsPerFrame;
        private static ConfigEntry<int> configMaxTerrainValidationsPerFrame;

        public static ConfigEntry<float> configPreviewAlpha;

        private static ConfigEntry<string> configToggleBlueprintToolKey;
        public static KeyCode ToggleBlueprintToolKey { get; private set; }

        private static ConfigEntry<string> configPasteBlueprintKey;
        public static KeyCode PasteBlueprintKey { get; private set; }

        private static ConfigEntry<string> configTogglePanelKey;
        public static KeyCode TogglePanelKey { get; private set; }

        private static ConfigEntry<string> configSaveBlueprintKey;
        public static KeyCode SaveBlueprintKey { get; private set; }

        private static ConfigEntry<string> configLoadBlueprintKey;
        public static KeyCode LoadBlueprintKey { get; private set; }

        internal static AssetBundleProxy bundleMain = null;

        private BlueprintToolCHM blueprintTool;

        public static int BlueprintToolModeIndex { get; private set; }

        public Plugin()
        {
            log = Log;
        }

        public override void Load()
        {
            log.LogMessage("Loading Duplicationer");
            log.LogMessage((string)$"blueprintFolder: {BlueprintFolder}");

            configMaxBuildingValidationsPerFrame = Config.Bind("Events", "MaxBuildingValidationsPerFrame", 4, "");
            BlueprintToolCHM.MaxBuildingValidationsPerFrame = Mathf.Max(1, configMaxBuildingValidationsPerFrame.Value);

            configMaxTerrainValidationsPerFrame = Config.Bind("Events", "MaxTerrainValidationsPerFrame", 20, "");
            BlueprintToolCHM.MaxTerrainValidationsPerFrame = Mathf.Max(1, configMaxTerrainValidationsPerFrame.Value);

            configPreviewAlpha = Config.Bind("Preview", "Opacity", 0.5f, "Opacity of preview models.\n0.0 = transparent/invisible.\n1.0 = opaque.");

            configToggleBlueprintToolKey = Config.Bind("Input", "ToggleBlueprintToolKey", "K", "Keyboard key for toggling the blueprint tool.\nValid values: Backspace, Tab, Clear, Return, Pause, Escape, Space, Exclaim,\nDoubleQuote, Hash, Dollar, Percent, Ampersand, Quote, LeftParen, RightParen,\nAsterisk, Plus, Comma, Minus, Period, Slash,\nAlpha0, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9,\nColon, Semicolon, Less, Equals, Greater, Question, At,\nLeftBracket, Backslash, RightBracket, Caret, Underscore, BackQuote,\nA, B, C, D, E, F, G, H, I, J, K, L, M,\nN, O, P, Q, R, S, T, U, V, W, X, Y, Z,\nLeftCurlyBracket, Pipe, RightCurlyBracket, Tilde, Delete,\nKeypad0, Keypad1, Keypad2, Keypad3, Keypad4, Keypad5, Keypad6, Keypad7, Keypad8, Keypad9,\nKeypadPeriod, KeypadDivide, KeypadMultiply, KeypadMinus, KeypadPlus, KeypadEnter, KeypadEquals,\nUpArrow, DownArrow, RightArrow, LeftArrow,\nInsert, Home, End, PageUp, PageDown,\nF1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12, F13, F14, F15,\nNumlock, CapsLock, ScrollLock,\nRightShift, LeftShift, RightControl, LeftControl, RightAlt, LeftAlt, RightApple, RightApple,\nLeftCommand, LeftCommand, LeftWindows, RightWindows, AltGr,\nHelp, Print, SysReq, Break, Menu,\nMouse0, Mouse1, Mouse2, Mouse3, Mouse4, Mouse5, Mouse6");
            ToggleBlueprintToolKey = ParseKeyCode(configToggleBlueprintToolKey.Value, KeyCode.K);

            configPasteBlueprintKey = Config.Bind("Input", "PasteBlueprintKey", "J", "Keyboard shortcut key for confirm paste");
            PasteBlueprintKey = ParseKeyCode(configPasteBlueprintKey.Value, KeyCode.J);

            configTogglePanelKey = Config.Bind("Input", "TogglePanelKey", "N", "Keyboard shortcut key to toggle the control panel");
            TogglePanelKey = ParseKeyCode(configTogglePanelKey.Value, KeyCode.N);

            configSaveBlueprintKey = Config.Bind("Input", "SaveBlueprintKey", "Period", "Keyboard shortcut key to open the save blueprint panel");
            SaveBlueprintKey = ParseKeyCode(configSaveBlueprintKey.Value, KeyCode.Period);

            configLoadBlueprintKey = Config.Bind("Input", "LoadBlueprintKey", "Comma", "Keyboard shortcut key to open the load blueprint panel");
            LoadBlueprintKey = ParseKeyCode(configLoadBlueprintKey.Value, KeyCode.Comma);

            configBlueprintLibraryPath = Config.Bind("Library", "BlueprintLibraryPath", "", $"Path to blueprint library. Leave blank to use %AppData%\\..\\LocalLow\\MederDynamics\\Foundry\\{MODNAME.ToLower()}");
            configCurrentBlueprintFile = Config.Bind("Library", "CurrentBlueprintFile", "", "Filename of the currently selected blueprint");

            if (!string.IsNullOrWhiteSpace(configBlueprintLibraryPath.Value)) BlueprintFolder = configBlueprintLibraryPath.Value;

            if (!Directory.Exists(BlueprintFolder)) Directory.CreateDirectory(BlueprintFolder);

            try
            {
                var harmony = new Harmony(GUID);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                log.LogError(e.ToString());
            }

            Assembly assembly = Assembly.GetExecutingAssembly();
            var bundleStream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.Resources.duplicationer.bundle");
            var bundleData = new byte[bundleStream.Length];
            bundleStream.Read(bundleData, 0, bundleData.Length);
            bundleMain = AssetBundleProxy.LoadFromMemory(bundleData);

            blueprintTool = new BlueprintToolCHM();
            CommonEvents.OnGameInitializationDone += () => blueprintTool.LoadBlueprintFromFile(Path.Combine(configBlueprintLibraryPath.Value, configCurrentBlueprintFile.Value));
            CommonEvents.OnDeselectTool += CustomHandheldModeManager.ExitCurrentMode;
            CommonEvents.OnApplicationStart += () => blueprintTool.LoadIconSprites();
            CommonEvents.OnUpdate += OnUpdate;

            BlueprintToolModeIndex = CustomHandheldModeManager.RegisterMode(blueprintTool);
        }

        private void OnUpdate()
        {
            var clientCharacter = GameRoot.getClientCharacter();
            if (clientCharacter == null) return;

            if (Input.GetKeyDown(ToggleBlueprintToolKey) && InputHelpers.IsKeyboardInputAllowed)
            {
                CustomHandheldModeManager.ToggleMode(clientCharacter, BlueprintToolModeIndex);
            }
        }

        private static KeyCode ParseKeyCode(string keyName, KeyCode defaultKeyCode)
        {
            try
            {
                return (KeyCode)Enum.Parse(typeof(KeyCode), keyName, true);
            }
            catch (ArgumentException)
            {
                return defaultKeyCode;
            }
        }
    }
}
