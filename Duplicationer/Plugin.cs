using HarmonyLib;
using UnityEngine;
using System.IO;
using Unfoundry;
using System.Collections.Generic;
using C3.ModKit;
using System.Reflection;

namespace Duplicationer
{
    [UnfoundryMod(GUID)]
    public class DuplicationerPlugin : UnfoundryPlugin
    {
        public const string
            MODNAME = "duplicationer",
            AUTHOR = "erkle64",
            GUID = AUTHOR + "." + MODNAME,
            VERSION = "0.4.11";

        public static LogSource log;

        public static string BlueprintFolder;

        public const string BlueprintExtension = "ebp";

        public static TypedConfigEntry<int> configMaxBuildingValidationsPerFrame;
        public static TypedConfigEntry<int> configMaxTerrainValidationsPerFrame;
        public static TypedConfigEntry<float> configPreviewAlpha;
        public static TypedConfigEntry<KeyCode> configToggleBlueprintToolKey;
        public static TypedConfigEntry<KeyCode> configPasteBlueprintKey;
        public static TypedConfigEntry<KeyCode> configTogglePanelKey;
        public static TypedConfigEntry<KeyCode> configSaveBlueprintKey;
        public static TypedConfigEntry<KeyCode> configLoadBlueprintKey;
        public static TypedConfigEntry<bool> configCheatModeAllowed;
        public static TypedConfigEntry<bool> configCheatModeEnabled;
        public static TypedConfigEntry<bool> configAllowUnresearchedRecipes;

        internal static Dictionary<string, UnityEngine.Object> bundleMainAssets;

        private static BlueprintToolCHM blueprintTool;

        public static int BlueprintToolModeIndex { get; private set; }

        private static ulong lastSpawnedBuildableWrapperEntityId = 0;

        public static T GetAsset<T>(string name) where T : UnityEngine.Object
        {
            if (!bundleMainAssets.TryGetValue(name, out var asset))
            {
                log.Log($"Missing asset '{name}'");
                return null;
            }

            return (T)asset;
        }

        public DuplicationerPlugin()
        {
            log = new LogSource(MODNAME);

            new Config(GUID)
                .Group("Events")
                    .Entry(out configMaxBuildingValidationsPerFrame, "MaxBuildingValidationsPerFrame", 4)
                    .Entry(out configMaxTerrainValidationsPerFrame, "MaxTerrainValidationsPerFrame", 20)
                .EndGroup()
                .Group("Preview")
                    .Entry(out configPreviewAlpha, "Opacity", 0.5f, "Opacity of preview models.", "0.0 = transparent/invisible.", "1.0 = opaque.")
                .EndGroup()
                .Group("Cheats")
                    .Entry(out configCheatModeAllowed, "CheatModeAllowed", false, "Enable the cheat mode button.")
                    .Entry(out configCheatModeEnabled, "CheatModeEnabled", false, "Enable cheat mode if allowed.")
                    .Entry(out configAllowUnresearchedRecipes, "AllowUnresearchedRecipes", false, "Allow setting unresearched recipes on production machines.")
                .EndGroup()
                .Group("Input",
                    "Key Codes: Backspace, Tab, Clear, Return, Pause, Escape, Space, Exclaim,",
                    "DoubleQuote, Hash, Dollar, Percent, Ampersand, Quote, LeftParen, RightParen,",
                    "Asterisk, Plus, Comma, Minus, Period, Slash,",
                    "Alpha0, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9,",
                    "Colon, Semicolon, Less, Equals, Greater, Question, At,",
                    "LeftBracket, Backslash, RightBracket, Caret, Underscore, BackQuote,",
                    "A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,",
                    "LeftCurlyBracket, Pipe, RightCurlyBracket, Tilde, Delete,",
                    "Keypad0, Keypad1, Keypad2, Keypad3, Keypad4, Keypad5, Keypad6, Keypad7, Keypad8, Keypad9,",
                    "KeypadPeriod, KeypadDivide, KeypadMultiply, KeypadMinus, KeypadPlus, KeypadEnter, KeypadEquals,",
                    "UpArrow, DownArrow, RightArrow, LeftArrow, Insert, Home, End, PageUp, PageDown,",
                    "F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12, F13, F14, F15,",
                    "Numlock, CapsLock, ScrollLock,",
                    "RightShift, LeftShift, RightControl, LeftControl, RightAlt, LeftAlt, RightApple, RightApple,",
                    "LeftCommand, LeftCommand, LeftWindows, RightWindows, AltGr,",
                    "Help, Print, SysReq, Break, Menu,",
                    "Mouse0, Mouse1, Mouse2, Mouse3, Mouse4, Mouse5, Mouse6")
                    .Entry(out configToggleBlueprintToolKey, "ToggleBlueprintToolKey", KeyCode.K, "Keyboard shortcut for toggling the blueprint tool.")
                    .Entry(out configPasteBlueprintKey, "PasteBlueprintKey", KeyCode.J, "Keyboard shortcut key for confirm paste.")
                    .Entry(out configTogglePanelKey, "TogglePanelKey", KeyCode.N, "Keyboard shortcut key to open the blueprint panel.")
                    .Entry(out configSaveBlueprintKey, "SaveBlueprintKey", KeyCode.Period, "Keyboard shortcut key to open the save blueprint panel.")
                    .Entry(out configLoadBlueprintKey, "LoadBlueprintKey", KeyCode.Comma, "Keyboard shortcut key to open the load blueprint panel.")
                .EndGroup()
                .Load()
                .Save();
        }

        public override void Load(Mod mod)
        {
            BlueprintFolder = Path.Combine(Application.persistentDataPath, MODNAME.ToLower());

            log.Log("Loading Duplicationer");
            log.Log($"blueprintFolder: {BlueprintFolder}");

            if (!Directory.Exists(BlueprintFolder)) Directory.CreateDirectory(BlueprintFolder);

            bundleMainAssets = typeof(Mod).GetField("assets", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mod) as Dictionary<string, UnityEngine.Object>;

            blueprintTool = new BlueprintToolCHM();
            blueprintTool.LoadIconSprites();
            CommonEvents.OnDeselectTool += CustomHandheldModeManager.ExitCurrentMode2;
            CommonEvents.OnUpdate += OnUpdate;

            BlueprintToolModeIndex = CustomHandheldModeManager.RegisterMode(blueprintTool);
        }

        private static void OnUpdate()
        {
            var clientCharacter = GameRoot.getClientCharacter();
            if (clientCharacter == null) return;

            if (Input.GetKeyDown(configToggleBlueprintToolKey.Get()) && InputHelpers.IsKeyboardInputAllowed)
            {
                CustomHandheldModeManager.ToggleMode(clientCharacter, BlueprintToolModeIndex);
            }
        }

        public static bool IsCheatModeEnabled => configCheatModeAllowed.Get() && configCheatModeEnabled.Get();
    }
}
