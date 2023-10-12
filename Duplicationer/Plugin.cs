using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System;
using Directory = System.IO.Directory;
using Path = System.IO.Path;
using Unfoundry;
using System.Collections.Generic;
using Channel3.ModKit;

namespace Duplicationer
{
    public class DuplicationerPlugin
    {
        public const string
            MODNAME = "Duplicationer",
            AUTHOR = "erkle64",
            GUID = "com." + AUTHOR + "." + MODNAME,
            VERSION = "0.2.1";

        public static string BlueprintFolder;

        public const string BlueprintExtension = "ebp";
        public static float configPreviewAlpha = 0.5f;

        public static KeyCode ToggleBlueprintToolKey { get; private set; }

        public static KeyCode PasteBlueprintKey { get; private set; }

        public static KeyCode TogglePanelKey { get; private set; }

        public static KeyCode SaveBlueprintKey { get; private set; }

        public static KeyCode LoadBlueprintKey { get; private set; }

        internal static Dictionary<string, UnityEngine.Object> bundleMainAssets;

        private static BlueprintToolCHM blueprintTool;

        public static int BlueprintToolModeIndex { get; private set; }

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

        private static ulong lastSpawnedBuildableWrapperEntityId = 0;

        public static T GetAsset<T>(string name) where T : UnityEngine.Object
        {
            if (!bundleMainAssets.TryGetValue(name, out var asset))
            {
                Debug.Log($"Missing asset '{name}'");
                return null;
            }

            return (T)asset;
        }

        [HarmonyPatch]
        public static class Patch
        {
            private static void OnUpdate()
            {
                var clientCharacter = GameRoot.getClientCharacter();
                if (clientCharacter == null) return;

                if (Input.GetKeyDown(ToggleBlueprintToolKey) && InputHelpers.IsKeyboardInputAllowed)
                {
                    CustomHandheldModeManager.ToggleMode(clientCharacter, BlueprintToolModeIndex);
                }
            }

            [HarmonyPatch(typeof(ObjectPoolManager), nameof(ObjectPoolManager.InitOnApplicationStart))]
            [HarmonyPrefix]
            private static void LoadPlugin(BuildEntityEvent __instance)
            {
                BlueprintFolder = Path.Combine(Application.persistentDataPath, MODNAME.ToLower());

                Debug.Log("Loading Duplicationer");
                Debug.Log((string)$"blueprintFolder: {BlueprintFolder}");

                BlueprintToolCHM.MaxBuildingValidationsPerFrame = 4;

                BlueprintToolCHM.MaxTerrainValidationsPerFrame = 20;

                ToggleBlueprintToolKey = KeyCode.K;
                PasteBlueprintKey = KeyCode.J;
                TogglePanelKey = KeyCode.N;
                SaveBlueprintKey = KeyCode.Period;
                LoadBlueprintKey = KeyCode.Comma;

                if (!Directory.Exists(BlueprintFolder)) Directory.CreateDirectory(BlueprintFolder);

                var allMods = ModManager.getAllMods();
                Mod thisMod = null;
                Debug.Log($"Duplicationer found {allMods.Count} mods.");
                foreach (var mod in allMods)
                {
                    Debug.Log($"Duplicationer found mod '{mod.modInfo.identifier}'.");
                    if (mod.modInfo.identifier == "erkle64.duplicationer")
                    {
                        thisMod = mod;
                        Debug.Log("Duplicationer found itself.");
                    }
                }
                if (thisMod == null)
                {
                    Debug.Log("Duplicationer failed to find itself.");
                }

                bundleMainAssets = thisMod.getAssets();
                foreach (var asset in bundleMainAssets)
                {
                    Debug.Log($"Duplicationer Asset: {asset.Key} {asset.Value.name}");
                }

                blueprintTool = new BlueprintToolCHM();
                blueprintTool.LoadIconSprites();
                //CommonEvents.OnGameInitializationDone += () => blueprintTool.LoadBlueprintFromFile(Path.Combine(BlueprintFolder, "blueprint.test"));
                CommonEvents.OnDeselectTool += CustomHandheldModeManager.ExitCurrentMode2;
                CommonEvents.OnUpdate += OnUpdate;

                BlueprintToolModeIndex = CustomHandheldModeManager.RegisterMode(blueprintTool);
            }

            [HarmonyPatch(typeof(BuildEntityEvent), nameof(BuildEntityEvent.processEvent))]
            [HarmonyPrefix]
            private static void BuildEntityEvent_processEvent_prefix(BuildEntityEvent __instance)
            {
                if (__instance.characterHash != GameRoot.getClientCharacter().usernameHash) return;
                lastSpawnedBuildableWrapperEntityId = 0;
            }

            [HarmonyPatch(typeof(BuildEntityEvent), nameof(BuildEntityEvent.processEvent))]
            [HarmonyPostfix]
            private static void BuildEntityEvent_processEvent_postfix(BuildEntityEvent __instance)
            {
                if (__instance.characterHash != GameRoot.getClientCharacter().usernameHash) return;
                ActionManager.InvokeAndRemoveBuildEvent(__instance, lastSpawnedBuildableWrapperEntityId);
            }

            [HarmonyPatch(typeof(BuildingManager), nameof(BuildingManager.buildingManager_constructBuildableWrapper))]
            [HarmonyPostfix]
            private static void BuildingManager_buildingManager_constructBuildableWrapper(v3i pos, ulong buildableObjectTemplateId, ulong __result)
            {
                lastSpawnedBuildableWrapperEntityId = __result;
            }

            [HarmonyPatch(typeof(Character.DemolishBuildingEvent), nameof(Character.DemolishBuildingEvent.processEvent))]
            [HarmonyPrefix]
            private static bool DemolishBuildingEvent_processEvent(Character.DemolishBuildingEvent __instance)
            {
                if (__instance.clientPlaceholderId == -2)
                {
                    __instance.clientPlaceholderId = 0;
                    BuildingManager.buildingManager_demolishBuildingEntityForDynamite(__instance.entityId);
                    return false;
                }

                return true;
            }

            [HarmonyPatch(typeof(Character.RemoveTerrainEvent), nameof(Character.RemoveTerrainEvent.processEvent))]
            [HarmonyPrefix]
            private static bool RemoveTerrainEvent_processEvent(Character.RemoveTerrainEvent __instance)
            {
                if (__instance.terrainRemovalPlaceholderId == ulong.MaxValue)
                {
                    __instance.terrainRemovalPlaceholderId = 0ul;

                    ulong chunkIndex;
                    uint blockIndex;
                    ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(__instance.worldPos.x, __instance.worldPos.y, __instance.worldPos.z, out chunkIndex, out blockIndex);

                    byte terrainType = 0;
                    ChunkManager.chunks_removeTerrainBlock(chunkIndex, blockIndex, ref terrainType);
                    ChunkManager.flagChunkVisualsAsDirty(chunkIndex, true, true);
                    return false;
                }

                return true;
            }

            //[HarmonyPatch(typeof(GameRoot), nameof(GameRoot.addLockstepEvent))]
            //[HarmonyPostfix]
            //private static void GameRoot_addLockstepEvent(GameRoot.LockstepEvent e)
            //{
            //    Debug.Log("====== GameRoot.addLockstepEvent ======");
            //    Debug.Log(e.getDbgInfo());
            //}
        }
    }
}
