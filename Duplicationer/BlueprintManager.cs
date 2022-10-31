using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using File = System.IO.File;
using Path = System.IO.Path;
using static Duplicationer.BepInExLoader;
using System;
using static Duplicationer.BlueprintManager.BlueprintData;
using static Duplicationer.BlueprintManager;
using static RenderChunk;
using static Il2CppSystem.Net.WebCompletionSource;
using static Duplicationer.BlueprintManager.BlueprintData.BuildableObjectData;

namespace Duplicationer
{
    internal static class BlueprintManager
    {
        public static bool isBlueprintLoaded { get; private set; } = false;
        public static BlueprintData CurrentBlueprint { get; private set; }
        public static string CurrentBlueprintStatusText { get; private set; } = "";
        public static Vector3Int CurrentBlueprintSize => CurrentBlueprint.blocks.Size;
        public static Vector3Int CurrentBlueprintAnchor => placeholderAnchorPosition;

        private static Dictionary<ulong, BlueprintShoppingListData> shoppingList = new Dictionary<ulong, BlueprintShoppingListData>();

        public static bool isPlaceholdersActive { get; private set; } = false;
        private static Vector3Int placeholderAnchorPosition = Vector3Int.zero;
        private static List<BlueprintPlaceholder> buildingPlaceholders = new List<BlueprintPlaceholder>();
        private static List<BlueprintPlaceholder> terrainPlaceholders = new List<BlueprintPlaceholder>();
        private static int buildingPlaceholderUpdateIndex = 0;
        private static int terrainPlaceholderUpdateIndex = 0;

        public delegate void BlueprintMovedDelegate(Vector3Int oldPosition, ref Vector3Int newPosition);
        public static event BlueprintMovedDelegate onBlueprintMoved;

        public delegate void BlueprintUpdatedDelegate(int countUntested, int countBlocked, int countClear, int countDone);
        public static event BlueprintUpdatedDelegate onBlueprintUpdated;

        private static List<ConstructionTaskGroup> activeConstructionTaskGroups = new List<ConstructionTaskGroup>();
        private static List<ConstructionTaskGroup.ConstructionTask> dependenciesTemp = new List<ConstructionTaskGroup.ConstructionTask>();
        private static Dictionary<ConstructionTaskGroup.ConstructionTask, List<ulong>> genericDependencies = new Dictionary<ConstructionTaskGroup.ConstructionTask, List<ulong>>();

        public delegate void PostBuildAction(ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task);
        private static List<PostBuildAction> postBuildActions = new List<PostBuildAction>();

        private static Il2CppSystem.Collections.Generic.List<BuildableObjectGO> bogoQueryResult = new Il2CppSystem.Collections.Generic.List<BuildableObjectGO>(0);

        private static ItemTemplate powerlineItemTemplate;
        public static ItemTemplate PowerlineItemTemplate
        {
            get => (powerlineItemTemplate == null) ? powerlineItemTemplate = ItemTemplateManager.getItemTemplate("_base_power_line_i") : powerlineItemTemplate;
        }

        public static bool isPlaceholdersHidden { get; private set; } = false;

        public static int MaxBuildingValidationsPerFrame { get; internal set; } = 4;
        public static int MaxTerrainValidationsPerFrame { get; internal set; } = 20;

        public static void Update()
        {
            ulong chunkIndex;
            uint blockIndex;

            if (isPlaceholdersActive)
            {
                var quadTree = StreamingSystem.getBuildableObjectGOQuadtreeArray();

                AABB3D aabb = ObjectPoolManager.aabb3ds.getObject();
                int count = Mathf.Min(MaxBuildingValidationsPerFrame, buildingPlaceholders.Count);
                if (buildingPlaceholderUpdateIndex >= buildingPlaceholders.Count) buildingPlaceholderUpdateIndex = 0;
                for (int i = 0; i < count; i++)
                {
                    var buildableObjectData = CurrentBlueprint.buildableObjects[buildingPlaceholderUpdateIndex];
                    var placeholder = buildingPlaceholders[buildingPlaceholderUpdateIndex];

                    var worldPos = new Vector3Int(buildableObjectData.worldX + placeholderAnchorPosition.x, buildableObjectData.worldY + placeholderAnchorPosition.y, buildableObjectData.worldZ + placeholderAnchorPosition.z);
                    int wx, wy, wz;
                    BuildingManager.getWidthFromOrientation(placeholder.bogo.template, (BuildingManager.BuildOrientation)buildableObjectData.orientationY, out wx, out wy, out wz);

                    byte errorCodeRaw = 0;
                    BuildingManager.buildingManager_validateConstruction_buildableEntityWrapper(new v3i(worldPos.x, worldPos.y, worldPos.z), buildableObjectData.orientationY, buildableObjectData.orientationUnlocked, buildableObjectData.templateId, ref errorCodeRaw, IOBool.iofalse);
                    var errorCode = (BuildingManager.CheckBuildableErrorCode)errorCodeRaw;

                    bool positionClear = false;
                    bool positionFilled = false;

                    switch (errorCode)
                    {
                        case BuildingManager.CheckBuildableErrorCode.Success:
                        case BuildingManager.CheckBuildableErrorCode.ModularBuilding_MissingModuleAttachmentPoint:
                        case BuildingManager.CheckBuildableErrorCode.ModularBuilding_ModuleValidationFailed:
                        case BuildingManager.CheckBuildableErrorCode.ModularBuilding_MissingFoundation:
                            positionClear = true;
                            break;
                    }

                    if (errorCode == BuildingManager.CheckBuildableErrorCode.BlockedByBuildableObject_Building)
                    {
                        aabb.reinitialize(worldPos.x, worldPos.y, worldPos.z, wx, wy, wz);
                        if (CheckIfBuildingExists(aabb, worldPos, buildableObjectData) > 0) positionFilled = true;
                    }

                    if (positionFilled)
                    {
                        placeholder.SetState(BlueprintPlaceholder.State.Done);
                    }
                    else if (positionClear)
                    {
                        placeholder.SetState(BlueprintPlaceholder.State.Clear);
                    }
                    else
                    {
                        placeholder.SetState(BlueprintPlaceholder.State.Blocked);
                    }

                    buildingPlaceholders[buildingPlaceholderUpdateIndex] = placeholder;

                    if (++buildingPlaceholderUpdateIndex >= buildingPlaceholders.Count) buildingPlaceholderUpdateIndex = 0;
                }
                ObjectPoolManager.aabb3ds.returnObject(aabb); aabb = null;

                count = Mathf.Min(MaxTerrainValidationsPerFrame, terrainPlaceholders.Count);
                if (terrainPlaceholderUpdateIndex >= terrainPlaceholders.Count) terrainPlaceholderUpdateIndex = 0;
                for (int i = 0; i < count; i++)
                {
                    var placeholder = terrainPlaceholders[terrainPlaceholderUpdateIndex];
                    var worldPos = Vector3Int.FloorToInt(placeholder.bogo.transform.position);
                    var localPos = worldPos - placeholderAnchorPosition;

                    bool positionClear = true;
                    bool positionFilled = false;

                    var queryResult = quadTree.queryPointXYZ(worldPos);
                    if (queryResult != null)
                    {
                        positionClear = false;
                    }
                    else
                    {
                        ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(worldPos.x, worldPos.y, worldPos.z, out chunkIndex, out blockIndex);

                        var blockId = CurrentBlueprint.blocks.ids[localPos.x + (localPos.y + localPos.z * CurrentBlueprint.blocks.sizeY) * CurrentBlueprint.blocks.sizeX];
                        var terrainData = ChunkManager.chunks_getTerrainData(chunkIndex, blockIndex);
                        if (terrainData == blockId)
                        {
                            positionFilled = true;
                        }
                        else if (terrainData > 0)
                        {
                            positionClear = false;
                        }
                    }

                    if (positionClear)
                    {
                        if (positionFilled)
                        {
                            placeholder.SetState(BlueprintPlaceholder.State.Done);
                        }
                        else
                        {
                            placeholder.SetState(BlueprintPlaceholder.State.Clear);
                        }
                    }
                    else
                    {
                        placeholder.SetState(BlueprintPlaceholder.State.Blocked);
                    }

                    terrainPlaceholders[terrainPlaceholderUpdateIndex] = placeholder;

                    if (++terrainPlaceholderUpdateIndex >= terrainPlaceholders.Count) terrainPlaceholderUpdateIndex = 0;
                }

                string text = "";

                int countUntested = BlueprintPlaceholder.GetStateCount(BlueprintPlaceholder.State.Untested);
                if (countUntested > 0) text += $"<color=#CCCCCC>Untested:</color> {countUntested}\n";

                int countClear = BlueprintPlaceholder.GetStateCount(BlueprintPlaceholder.State.Clear);
                if (countClear > 0) text += $"Ready: {BlueprintPlaceholder.GetStateCount(BlueprintPlaceholder.State.Clear)}\n";

                int countBlocked = BlueprintPlaceholder.GetStateCount(BlueprintPlaceholder.State.Blocked);
                if (countBlocked > 0) text += $"<color=\"red\">Blocked:</color> {BlueprintPlaceholder.GetStateCount(BlueprintPlaceholder.State.Blocked)}\n";

                int countDone = BlueprintPlaceholder.GetStateCount(BlueprintPlaceholder.State.Done);
                if (countDone > 0) text += $"<color=#AACCFF>Done:</color> {BlueprintPlaceholder.GetStateCount(BlueprintPlaceholder.State.Done)}\n";

                if (countUntested > 0 || countBlocked > 0 || countClear > 0 || countDone > 0)
                {
                    CurrentBlueprintStatusText = text + $"Total: {BlueprintPlaceholder.GetStateCountTotal()}";
                    onBlueprintUpdated?.Invoke(countUntested, countBlocked, countClear, countDone);
                }
                else
                {
                    CurrentBlueprintStatusText = "";
                    onBlueprintUpdated?.Invoke(countUntested, countBlocked, countClear, countDone);
                }

                foreach (var taskGroup in activeConstructionTaskGroups)
                {
                    CurrentBlueprintStatusText = $"ToDo:{taskGroup.Remaining} " + CurrentBlueprintStatusText;
                }
            }
        }

        internal static void CreateBlueprint(Vector3Int from, Vector3Int size)
        {
            var to = from + size;

            ulong chunkIndex;
            uint blockIndex;
            var buildings = new HashSet<BuildableObjectGO>(new BuildableObjectGOComparer());
            var blocks = new byte[size.x * size.y * size.z];
            var blocksIndex = 0;
            var quadTree = StreamingSystem.getBuildableObjectGOQuadtreeArray();
            for (int wz = from.z; wz < to.z; ++wz)
            {
                for (int wy = from.y; wy < to.y; ++wy)
                {
                    for (int wx = from.x; wx < to.x; ++wx)
                    {
                        ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(wx, wy, wz, out chunkIndex, out blockIndex);

                        var terrainData = ChunkManager.chunks_getTerrainData(chunkIndex, blockIndex);
                        blocks[blocksIndex++] = terrainData;

                        var bogo = quadTree.queryPointXYZ(new Vector3Int(wx, wy, wz));
                        if (bogo != null) { buildings.Add(bogo); }
                    }
                }
            }

            var buildingArray = buildings.ToArray();
            var buildingDataArray = new BuildableObjectData[buildingArray.Length];
            var customData = new List<BuildableObjectData.CustomData>();
            var powerGridBuildings = new HashSet<BuildableObjectGO>(new BuildableObjectGOComparer());
            for (int i = 0; i < buildingArray.Length; ++i)
            {
                var bogo = buildingArray[i];
                BuildableEntity.BuildableEntityGeneralData generalData = default;
                var hasGeneralData = BuildingManager.buildingManager_getBuildableEntityGeneralData(bogo.Id, ref generalData);
                Debug.Assert(hasGeneralData == IOBool.iotrue);

                buildingDataArray[i].originalEntityId = bogo.relatedEntityId;
                buildingDataArray[i].templateName = bogo.template.name;
                buildingDataArray[i].templateId = generalData.buildableObjectTempalteId;
                buildingDataArray[i].worldX = generalData.pos.x - from.x;
                buildingDataArray[i].worldY = generalData.pos.y - from.y;
                buildingDataArray[i].worldZ = generalData.pos.z - from.z;
                buildingDataArray[i].orientationY = bogo.template.canBeRotatedAroundXAxis ? (byte)0 : generalData.orientationY;
                buildingDataArray[i].itemMode = generalData.itemMode;

                if (bogo.template.canBeRotatedAroundXAxis)
                {
                    buildingDataArray[i].orientationUnlockedX = generalData.orientationUnlocked.x;
                    buildingDataArray[i].orientationUnlockedY = generalData.orientationUnlocked.y;
                    buildingDataArray[i].orientationUnlockedZ = generalData.orientationUnlocked.z;
                    buildingDataArray[i].orientationUnlockedW = generalData.orientationUnlocked.w;
                }
                else
                {
                    buildingDataArray[i].orientationUnlockedX = 0.0f;
                    buildingDataArray[i].orientationUnlockedY = 0.0f;
                    buildingDataArray[i].orientationUnlockedZ = 0.0f;
                    buildingDataArray[i].orientationUnlockedW = 1.0f;
                }

                var bogoType = bogo.GetIl2CppType();

                customData.Clear();
                if (UnhollowerRuntimeLib.Il2CppType.Of<ProducerGO>().IsAssignableFrom(bogoType))
                {
                    var assembler = bogo.Cast<ProducerGO>();
                    customData.Add(new BuildableObjectData.CustomData("craftingRecipeId", assembler.getLastPolledRecipeId()));
                }
                if (UnhollowerRuntimeLib.Il2CppType.Of<LoaderGO>().IsAssignableFrom(bogoType))
                {
                    var loader = bogo.Cast<LoaderGO>();
                    customData.Add(new BuildableObjectData.CustomData("isInputLoader", loader.isInputLoader() ? "true" : "false"));
                    if (bogo.template.loader_isFilter)
                    {
                        customData.Add(new BuildableObjectData.CustomData("loaderFilterTemplateId", loader._cache_lastSetFilterTemplateId));
                    }
                }
                if (UnhollowerRuntimeLib.Il2CppType.Of<PipeLoaderGO>().IsAssignableFrom(bogoType))
                {
                    var loader = bogo.Cast<PipeLoaderGO>();
                    customData.Add(new BuildableObjectData.CustomData("isInputLoader", loader.isInputLoader() ? "true" : "false"));
                    customData.Add(new BuildableObjectData.CustomData("pipeLoaderFilterTemplateId", loader._cache_lastSetFilterTemplateId));
                }
                if (UnhollowerRuntimeLib.Il2CppType.Of<ConveyorBalancerGO>().IsAssignableFrom(bogoType))
                {
                    var balancer = bogo.Cast<ConveyorBalancerGO>();
                    customData.Add(new BuildableObjectData.CustomData("balancerInputPriority", balancer.getInputPriority()));
                    customData.Add(new BuildableObjectData.CustomData("balancerOutputPriority", balancer.getOutputPriority()));
                }
                if (UnhollowerRuntimeLib.Il2CppType.Of<SignGO>().IsAssignableFrom(bogoType))
                {
                    var signTextLength = SignGO.signEntity_getSignTextLength(bogo.relatedEntityId);
                    var signText = new byte[signTextLength];
                    byte useAutoTextSize = 0;
                    float textMinSize = 0;
                    float textMaxSize = 0;
                    SignGO.signEntity_getSignText(bogo.relatedEntityId, signText, signTextLength, ref useAutoTextSize, ref textMinSize, ref textMaxSize);

                    customData.Add(new BuildableObjectData.CustomData("signText", System.Text.Encoding.Default.GetString(signText)));
                    customData.Add(new BuildableObjectData.CustomData("signUseAutoTextSize", useAutoTextSize));
                    customData.Add(new BuildableObjectData.CustomData("signTextMinSize", textMinSize));
                    customData.Add(new BuildableObjectData.CustomData("signTextMaxSize", textMaxSize));
                }
                if (UnhollowerRuntimeLib.Il2CppType.Of<BlastFurnaceBaseGO>().IsAssignableFrom(bogoType))
                {
                    BlastFurnacePollingUpdateData data = default;
                    if (BlastFurnaceBaseGO.blastFurnaceEntity_queryPollingData(bogo.relatedEntityId, ref data) == IOBool.iotrue)
                    {
                        customData.Add(new BuildableObjectData.CustomData("blastFurnaceModeTemplateId", data.modeTemplateId));
                    }
                }
                if (bogo.template.hasPoleGridConnection)
                {
                    if (!powerGridBuildings.Contains(bogo))
                    {
                        foreach (var powerGridBuilding in powerGridBuildings)
                        {
                            if (PowerLineHH.buildingManager_powerlineHandheld_checkIfAlreadyConnected(powerGridBuilding.relatedEntityId, bogo.relatedEntityId) == IOBool.iotrue)
                            {
                                customData.Add(new BuildableObjectData.CustomData("powerline", powerGridBuilding.relatedEntityId));
                            }
                        }
                        powerGridBuildings.Add(bogo);
                    }
                }

                void HandleParenting<T>(T module) where T : BuildableObjectGO
                {
                    var searchPos = (Vector3Int)generalData.pos;
                    var pbogo = quadTree.queryPointXYZ(BuildableEntity.getWorldPositionByLocalOffset(bogo.template, bogo.aabb, module.template.modularBuildingLocalSearchAnchor, bogo.buildOrientation, bogo.transform.rotation));
                    int nodeIndex = 0;
                    int matchIndex = -1;
                    foreach (var node in pbogo.template.modularBuildingConnectionNodes)
                    {
                        foreach (var nodeData in node.nodeData)
                        {
                            if (nodeData.botId == bogo.template.id)
                            {
                                var nodePos = BuildableEntity.getWorldPositionByLocalOffset(pbogo.template, pbogo.aabb, nodeData.positionData.offset, pbogo.buildOrientation, pbogo.transform.rotation);
                                if (nodePos == searchPos)
                                {
                                    matchIndex = nodeIndex;
                                    break;
                                }
                            }
                        }
                        if (matchIndex >= 0) break;
                        ++nodeIndex;
                    }
                    if (matchIndex >= 0)
                    {
                        customData.Add(new CustomData("modularNodeIndex", matchIndex));
                        customData.Add(new CustomData("modularParentId", pbogo.relatedEntityId));
                    }
                }

                if (UnhollowerRuntimeLib.Il2CppType.Of<ModularGO_Module>().IsAssignableFrom(bogoType))
                {
                    var modularModule = bogo.Cast<ModularGO_Module>();
                    HandleParenting(modularModule);
                }
                if (UnhollowerRuntimeLib.Il2CppType.Of<ModularGO_ModuleWithInteractable>().IsAssignableFrom(bogoType))
                {
                    var modularModule = bogo.Cast<ModularGO_ModuleWithInteractable>();
                    HandleParenting(modularModule);

                    int interactableCount = 0;
                    var interactables = modularModule.GetInteractableObjects(ref interactableCount);
                    if (interactableCount > 0)
                    {
                        customData.Add(new CustomData("interactableCount", interactableCount));
                        for (int j = 0; j < interactableCount; ++j)
                        {
                            var interactable = interactables[j];
                            if (interactable.eventType == InteractableObject.InteractableEventType.NoEvent)
                            {
                                ulong filterTemplateId = 0;
                                BuildableEntity.buildableEntity_getFilterTemplate(bogo.relatedEntityId, 0, ref filterTemplateId, interactable.filterIdx);
                                if (filterTemplateId > 0) customData.Add(new BuildableObjectData.CustomData("itemFilter", $"{interactable.filterIdx}={filterTemplateId}"));

                                BuildableEntity.buildableEntity_getFilterTemplate(bogo.relatedEntityId, 1, ref filterTemplateId, interactable.filterIdx);
                                if (filterTemplateId > 0) customData.Add(new BuildableObjectData.CustomData("elementFilter", $"{interactable.filterIdx}={filterTemplateId}"));
                            }
                        }
                    }
                }
                if (UnhollowerRuntimeLib.Il2CppType.Of<ModularGO_ModuleWithScreenPanel>().IsAssignableFrom(bogoType))
                {
                    var modularModule = bogo.Cast<ModularGO_ModuleWithScreenPanel>();
                    HandleParenting(modularModule);
                }

                buildingDataArray[i].customData = customData.ToArray();
            }

            BlueprintData blueprintData = new BlueprintData();
            blueprintData.blueprintVersion = 2;
            blueprintData.buildableObjects = buildingDataArray;
            blueprintData.blocks.sizeX = size.x;
            blueprintData.blocks.sizeY = size.y;
            blueprintData.blocks.sizeZ = size.z;
            blueprintData.blocks.ids = blocks;
            var json = JsonConvert.SerializeObject(blueprintData, Formatting.Indented);
            File.WriteAllText(Path.Combine(dataFolder, blueprintFilename), json);
            CurrentBlueprint = blueprintData;
            isBlueprintLoaded = true;
        }

        internal static void HideBlueprint()
        {
            if (isPlaceholdersActive)
            {
                isPlaceholdersActive = false;

                foreach (var placeholder in buildingPlaceholders)
                {
                    placeholder.SetState(BlueprintPlaceholder.State.Invalid);
                    BuildingManager.client_removePlaceholder(placeholder.bogo);
                }
                buildingPlaceholders.Clear();

                foreach (var placeholder in terrainPlaceholders)
                {
                    placeholder.SetState(BlueprintPlaceholder.State.Invalid);
                    BuildingManager.client_removePlaceholder(placeholder.bogo);
                }
                terrainPlaceholders.Clear();
            }
        }

        internal static void MoveBlueprint(Vector3Int newPosition)
        {
            if (!isPlaceholdersActive) return;
            onBlueprintMoved?.Invoke(placeholderAnchorPosition, ref newPosition);
            ShowBlueprint(newPosition);
        }

        internal static void PlaceBlueprintMultiple(Vector3Int targetPosition, Vector3Int repeatFrom, Vector3Int repeatTo, float delayInterval = 0.23f)
        {
            float delay = 0.0f;
            for (int y = repeatFrom.y; y <= repeatTo.y; ++y)
            {
                for (int z = repeatFrom.z; z <= repeatTo.z; ++z)
                {
                    for (int x = repeatFrom.x; x <= repeatTo.x; ++x)
                    {
                        PlaceBlueprint(targetPosition + new Vector3Int(x, y, z) * BlueprintManager.CurrentBlueprintSize, delay += delayInterval);
                    }
                }
            }
        }

        internal static void PlaceBlueprint(Vector3Int targetPosition, float delay)
        {
            ulong usernameHash = GameRoot.getClientCharacter().usernameHash;
            log.LogMessage(string.Format("Placing blueprint at {0}", targetPosition.ToString()));
            AABB3D aabb = ObjectPoolManager.aabb3ds.getObject();
            var modularBaseCoords = new Dictionary<ulong, Vector3Int>();
            var constructionTaskGroup = new ConstructionTaskGroup((ConstructionTaskGroup taskGroup) => { activeConstructionTaskGroups.Remove(taskGroup); });
            activeConstructionTaskGroups.Add(constructionTaskGroup);
            int buildingIndex = 0;
            foreach (var buildableObjectData in CurrentBlueprint.buildableObjects)
            {
                postBuildActions.Clear();

                var template = ItemTemplateManager.getBuildableObjectTemplate(buildableObjectData.templateId);
                Debug.Assert(template != null);

                var worldPos = new Vector3Int(buildableObjectData.worldX, buildableObjectData.worldY, buildableObjectData.worldZ) + targetPosition;

                int wx, wy, wz;
                BuildingManager.getWidthFromOrientation(template, (BuildingManager.BuildOrientation)buildableObjectData.orientationY, out wx, out wy, out wz);

                ulong additionalData_ulong_01 = 0ul;
                ulong additionalData_ulong_02 = 0ul;

                bool usePasteConfigSettings = false;
                ulong pasteConfigSettings_01 = 0ul;
                ulong pasteConfigSettings_02 = 0ul;

                var craftingRecipeId = GetCustomData<ulong>(buildableObjectData.customData, "craftingRecipeId");
                if (craftingRecipeId != 0)
                {
                    usePasteConfigSettings = true;
                    pasteConfigSettings_01 = craftingRecipeId;
                }

                if (HasCustomData(buildableObjectData.customData, "isInputLoader"))
                {
                    usePasteConfigSettings = true;
                    bool isInputLoader = GetCustomData<bool>(buildableObjectData.customData, "isInputLoader");
                    pasteConfigSettings_01 = isInputLoader ? 1u : 0u;

                    if (template.loader_isFilter)
                    {
                        if (HasCustomData(buildableObjectData.customData, "loaderFilterTemplateId"))
                        {
                            var loaderFilterTemplateId = GetCustomData<ulong>(buildableObjectData.customData, "loaderFilterTemplateId");
                            if (loaderFilterTemplateId > 0)
                            {
                                usePasteConfigSettings = true;
                                pasteConfigSettings_02 = loaderFilterTemplateId;
                            }
                        }
                    }

                    if (template.type == BuildableObjectTemplate.BuildableObjectType.PipeLoader)
                    {
                        if (HasCustomData(buildableObjectData.customData, "pipeLoaderFilterTemplateId"))
                        {
                            var pipeLoaderFilterTemplateId = GetCustomData<ulong>(buildableObjectData.customData, "pipeLoaderFilterTemplateId");
                            if (pipeLoaderFilterTemplateId > 0)
                            {
                                postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                                {
                                    if (task.entityId > 0)
                                    {
                                        GameRoot.addLockstepEvent(new SetPipeLoaderConfig(usernameHash, task.entityId, pipeLoaderFilterTemplateId, isInputLoader));
                                    }
                                });
                            }
                        }
                    }
                }

                if (HasCustomData(buildableObjectData.customData, "modularNodeIndex"))
                {
                    additionalData_ulong_01 = GetCustomData<ulong>(buildableObjectData.customData, "modularNodeIndex");
                }

                if (template.type == BuildableObjectTemplate.BuildableObjectType.ConveyorBalancer)
                {
                    var balancerInputPriority = GetCustomData<int>(buildableObjectData.customData, "balancerInputPriority");
                    var balancerOutputPriority = GetCustomData<int>(buildableObjectData.customData, "balancerOutputPriority");
                    postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                    {
                        if (task.entityId > 0)
                        {
                            GameRoot.addLockstepEvent(new SetConveyorBalancerConfig(usernameHash, task.entityId, balancerInputPriority, balancerOutputPriority));
                        }
                    });
                }

                if (template.type == BuildableObjectTemplate.BuildableObjectType.Sign)
                {
                    var signText = GetCustomData<string>(buildableObjectData.customData, "signText");
                    var signUseAutoTextSize = GetCustomData<byte>(buildableObjectData.customData, "signUseAutoTextSize");
                    var signTextMinSize = GetCustomData<float>(buildableObjectData.customData, "signTextMinSize");
                    var signTextMaxSize = GetCustomData<float>(buildableObjectData.customData, "signTextMaxSize");
                    postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                    {
                        if (task.entityId > 0)
                        {
                            GameRoot.addLockstepEvent(new SignSetTextEvent(usernameHash, task.entityId, signText, signUseAutoTextSize != 0, signTextMinSize, signTextMaxSize));
                        }
                    });
                }

                if (HasCustomData(buildableObjectData.customData, "blastFurnaceModeTemplateId"))
                {
                    var modeTemplateId = GetCustomData<ulong>(buildableObjectData.customData, "blastFurnaceModeTemplateId");
                    if (modeTemplateId > 0)
                    {
                        postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                        {
                            if (task.entityId > 0)
                            {
                                GameRoot.addLockstepEvent(new BlastFurnaceSetModeEvent(usernameHash, task.entityId, modeTemplateId));
                            }
                        });
                    }
                }

                if (HasCustomData(buildableObjectData.customData, "interactableCount"))
                {
                    var elementFilters = new List<string>();
                    GetCustomDataList(buildableObjectData.customData, "elementFilter", elementFilters);
                    foreach (var elementFilter in elementFilters)
                    {
                        var parts = elementFilter.Split('=');
                        if (parts.Length == 2)
                        {
                            var filterIdx = Convert.ToUInt32(parts[0]);
                            var elementFilterTemplateId = Convert.ToUInt64(parts[1]);
                            if (string.IsNullOrEmpty(template.modularBuildingPipeConnectionData[(int)filterIdx].modularBuildingFixedElementTemplateFilter))
                            {
                                postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                                {
                                    if (task.entityId > 0)
                                    {
                                        GameRoot.addLockstepEvent(new SetFilterEvent(usernameHash, task.entityId, elementFilterTemplateId, 1, filterIdx));
                                    }
                                });
                            }
                        }
                    }
                }

                var powerlineEntityIds = new List<ulong>();
                GetCustomDataList(buildableObjectData.customData, "powerline", powerlineEntityIds);
                foreach (var powerlineEntityId in powerlineEntityIds)
                {
                    var powerlineIndex = FindEntityIndex(powerlineEntityId);
                    if (powerlineIndex >= 0)
                    {
                        var fromPos = worldPos;
                        var toBuildableObjectData = CurrentBlueprint.buildableObjects[powerlineIndex];
                        var toPos = new Vector3Int(toBuildableObjectData.worldX, toBuildableObjectData.worldY, toBuildableObjectData.worldZ) + targetPosition;
                        postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                        {
                            //var fromBogo = StreamingSystem.getBuildableObjectGOQuadtreeArray().queryPointXYZ(fromPos);
                            var fromBogo = StreamingSystem.getBuildableObjectGOByEntityId(task.entityId);
                            var toBogo = StreamingSystem.getBuildableObjectGOQuadtreeArray().queryPointXYZ(toPos);
                            if (fromBogo != null && toBogo != null && PowerLineHH.buildingManager_powerlineHandheld_checkIfAlreadyConnected(fromBogo.relatedEntityId, toBogo.relatedEntityId) == IOBool.iofalse)
                            {
                                GameRoot.addLockstepEvent(new PoleConnectionEvent(usernameHash, PowerlineItemTemplate.id, fromBogo.Id, toBogo.Id));
                            }
                        });
                    }
                }

                aabb.reinitialize(worldPos.x, worldPos.y, worldPos.z, wx, wy, wz);
                var existingEntityId = CheckIfBuildingExists(aabb, worldPos, buildableObjectData);
                if (existingEntityId > 0)
                {
                    var postBuildActionsArray = postBuildActions.ToArray();
                    constructionTaskGroup.AddTask(buildableObjectData.originalEntityId, (ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) => {
                        ActionManager.AddQueuedEvent(() =>
                        {
                            task.entityId = existingEntityId;
                            foreach (var action in postBuildActionsArray) action.Invoke(taskGroup, task);
                            taskGroup.InvokeNextTask();
                        });
                    });
                }
                else
                {
                    var postBuildActionsArray = postBuildActions.ToArray();
                    constructionTaskGroup.AddTask(buildableObjectData.originalEntityId, (ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) => {
                        var buildEntityEvent = new BuildEntityEvent(
                            usernameHash,
                            template.parentItemTemplate.id,
                            buildableObjectData.itemMode,
                            worldPos,
                            buildableObjectData.orientationY,
                            buildableObjectData.orientationUnlocked,
                            template.modularBuildingModule_amountItemCost > 1 ? (int)template.modularBuildingModule_amountItemCost : 1,
                            0,
                            additionalData_ulong_01: additionalData_ulong_01,
                            additionalData_ulong_02: additionalData_ulong_02,
                            playSound: true,
                            usePasteConfigSettings: usePasteConfigSettings,
                            pasteConfigSettings_01: pasteConfigSettings_01,
                            pasteConfigSettings_02: pasteConfigSettings_02
                        );

                        ActionManager.AddQueuedEvent(() =>
                        {
                            ActionManager.AddBuildEvent(buildEntityEvent, (ulong entityId) =>
                            {
                                ActionManager.AddQueuedEvent(() =>
                                {
                                    task.entityId = entityId;
                                    foreach (var action in postBuildActionsArray) action.Invoke(taskGroup, task);
                                    taskGroup.InvokeNextTask();
                                });
                            });
                            GameRoot.addLockstepEvent(buildEntityEvent);
                        });
                    });
                }

                ++buildingIndex;
            }
            ObjectPoolManager.aabb3ds.returnObject(aabb); aabb = null;

            foreach (var buildableObjectData in CurrentBlueprint.buildableObjects)
            {
                dependenciesTemp.Clear();
                if (HasCustomData(buildableObjectData.customData, "modularParentId"))
                {
                    ulong parentId = GetCustomData<ulong>(buildableObjectData.customData, "modularParentId");

                    var dependency = constructionTaskGroup.GetTask(parentId);
                    if (dependency != null) dependenciesTemp.Add(dependency);
                    else log.LogWarning((string)$"Entity id {parentId} not found in blueprint");
                }

                var powerlineEntityIds = new List<ulong>();
                GetCustomDataList(buildableObjectData.customData, "powerline", powerlineEntityIds);
                foreach (var powerlineEntityId in powerlineEntityIds)
                {
                    var dependency = constructionTaskGroup.GetTask(powerlineEntityId);
                    if (dependency != null) dependenciesTemp.Add(dependency);
                    else log.LogWarning((string)$"Entity id {powerlineEntityId} not found in blueprint");
                }

                if (dependenciesTemp.Count > 0)
                {
                    var task = constructionTaskGroup.GetTask(buildableObjectData.originalEntityId);
                    if (task != null) task.dependencies = dependenciesTemp.ToArray();
                }
            }

            Debug.Assert(CurrentBlueprint.blocks.ids != null);

            var quadTreeArray = StreamingSystem.getBuildableObjectGOQuadtreeArray();
            int blockIndex = 0;
            for (int z = 0; z < CurrentBlueprint.blocks.sizeZ; z++)
            {
                for (int y = 0; y < CurrentBlueprint.blocks.sizeY; y++)
                {
                    for (int x = 0; x < CurrentBlueprint.blocks.sizeX; x++)
                    {
                        //var blockId = currentBlueprint.blocks.ids[x + (y + z * currentBlueprint.blocks.sizeY) * currentBlueprint.blocks.sizeX];
                        var blockId = CurrentBlueprint.blocks.ids[blockIndex++];
                        if (blockId > 0)
                        {
                            var worldPos = new Vector3Int(x, y, z) + targetPosition;
                            ulong worldChunkIndex;
                            uint worldBlockIndex;
                            ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(worldPos.x, worldPos.y, worldPos.z, out worldChunkIndex, out worldBlockIndex);
                            var terrainData = ChunkManager.chunks_getTerrainData(worldChunkIndex, worldBlockIndex);

                            if (terrainData == 0 && quadTreeArray.queryPointXYZ(worldPos) == null)
                            {
                                if (blockId >= GameRoot.BUILDING_PART_ARRAY_IDX_START)
                                {
                                    var partTemplate = ItemTemplateManager.getBuildingPartTemplate(GameRoot.BuildingPartIdxLookupTable.table[blockId]);
                                    if (partTemplate != null && partTemplate.parentItemTemplate != null)
                                    {
                                        //log.LogInfo((string)$"Place building part {partTemplate.parentItemTemplate.name} at ({worldPos.x}, {worldPos.y}, {worldPos.z})");

                                        ActionManager.AddQueuedEvent(() =>
                                        {
                                            //BuildingManager.client_tryBuild(worldPos, BuildingManager.BuildOrientation.xPos, partTemplate.parentItemTemplate);
                                            GameRoot.addLockstepEvent(new BuildEntityEvent(usernameHash, partTemplate.parentItemTemplate.id, 0, worldPos, 0, Quaternion.identity, 1, 0, false));
                                        });
                                    }
                                }
                                else
                                {
                                    var blockTemplate = ItemTemplateManager.getTerrainBlockTemplateByByteIdx(blockId);
                                    if (blockTemplate != null && blockTemplate.parentBOT != null)
                                    {
                                        var itemTemplate = blockTemplate.parentBOT.parentItemTemplate;
                                        if (itemTemplate != null)
                                        {
                                            //log.LogInfo((string)$"Place terrain {itemTemplate.name} at ({worldPos.x}, {worldPos.y}, {worldPos.z})");

                                            ActionManager.AddQueuedEvent(() =>
                                            {
                                                GameRoot.addLockstepEvent(new BuildEntityEvent(usernameHash, itemTemplate.id, 0, worldPos, 0, Quaternion.identity, 1, 0, false));
                                            });
                                        }
                                        else
                                        {
                                            log.LogWarning((string)$"No item template for terrain index {blockId}");
                                        }
                                    }
                                    else
                                    {
                                        log.LogWarning((string)$"No block template for terrain index {blockId}");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            constructionTaskGroup.SortTasks();
            ActionManager.AddQueuedEvent(() => constructionTaskGroup.InvokeNextTask());
        }

        private static int FindEntityIndex(ulong entityId)
        {
            for (int i = 0; i < CurrentBlueprint.buildableObjects.Length; ++i)
            {
                if (CurrentBlueprint.buildableObjects[i].originalEntityId == entityId) return i;
            }
            return -1;
        }

        private static int CountModularParents(ulong parentId)
        {
            var parentIndex = FindEntityIndex(parentId);
            if (parentIndex < 0) return 0;

            if (HasCustomData(CurrentBlueprint.buildableObjects[parentIndex].customData, "modularParentId"))
            {
                var grandparentId = GetCustomData<ulong>(CurrentBlueprint.buildableObjects[parentIndex].customData, "modularParentId");
                return CountModularParents(grandparentId) + 1;
            }

            return 1;
        }

        internal static void ShowBlueprint(Vector3Int targetPosition)
        {
            if (!isPlaceholdersActive)
            {
                isPlaceholdersActive = true;

                foreach (var buildableObjectData in CurrentBlueprint.buildableObjects)
                {
                    var template = ItemTemplateManager.getBuildableObjectTemplate(buildableObjectData.templateId);

                    GameRoot.createBuildingPlaceholder(template.parentItemTemplate.id, buildableObjectData.itemMode);
                    var bogo = GameRoot.singleton.placeholder_buildable;
                    if (bogo != null)
                    {
                        int wx, wy, wz;
                        BuildingManager.getWidthFromOrientation(template, (BuildingManager.BuildOrientation)buildableObjectData.orientationY, out wx, out wy, out wz);
                        var worldPos = new Vector3(buildableObjectData.worldX + wx * 0.5f, buildableObjectData.worldY + (template.canBeRotatedAroundXAxis ? wy * 0.5f : 0.0f), buildableObjectData.worldZ + wz * 0.5f) + targetPosition;
                        bogo.transform.position = worldPos;
                        bogo.transform.rotation = template.canBeRotatedAroundXAxis ? buildableObjectData.orientationUnlocked : Quaternion.EulerRotation(0, buildableObjectData.orientationY * Mathf.PI * 0.5f, 0.0f);

                        buildingPlaceholders.Add(new BlueprintPlaceholder(bogo));

                        GameRoot.singleton.placeholder_buildable = null;
                        GameRoot.singleton.placeholder_currentItemMode = 0;
                        GameRoot.singleton.placeholder_currentItemTemplateId = 0;

                        bogo.gameObject.SetActive(!isPlaceholdersHidden);
                    }
                    else
                    {
                        log.LogWarning((string)$"Failed to create placeholder for {template.name} at ({buildableObjectData.worldX}, {buildableObjectData.worldY}, {buildableObjectData.worldZ}).");
                    }
                }

                int blockIndex = 0;
                for (int z = 0; z < CurrentBlueprint.blocks.sizeZ; ++z)
                {
                    for (int y = 0; y < CurrentBlueprint.blocks.sizeY; ++y)
                    {
                        for (int x = 0; x < CurrentBlueprint.blocks.sizeX; ++x)
                        {
                            var id = CurrentBlueprint.blocks.ids[blockIndex++];
                            if (id > 0)
                            {
                                var worldPos = new Vector3(x + targetPosition.x + 0.5f, y + targetPosition.y, z + targetPosition.z + 0.5f);

                                BuildableObjectTemplate template = null;
                                if (id < GameRoot.MAX_TERRAIN_COUNT)
                                {
                                    var tbt = ItemTemplateManager.getTerrainBlockTemplateByByteIdx(id);
                                    if (tbt != null) template = tbt.parentBOT;
                                }
                                else
                                {
                                    template = ItemTemplateManager.getBuildingPartTemplate(GameRoot.BuildingPartIdxLookupTable.table[id]);
                                    if (template == null) log.LogWarning((string)$"Template not found for terrain index {id}-{GameRoot.BUILDING_PART_ARRAY_IDX_START} with id {GameRoot.BuildingPartIdxLookupTable.table[id]} at ({worldPos.x}, {worldPos.y}, {worldPos.z})");
                                }

                                if (template != null)
                                {
                                    GameRoot.createBuildingPlaceholder(template.parentItemTemplate.id, 0);
                                    var bogo = GameRoot.singleton.placeholder_buildable;
                                    if (bogo != null)
                                    {
                                        bogo.transform.position = worldPos;

                                        terrainPlaceholders.Add(new BlueprintPlaceholder(bogo));

                                        GameRoot.singleton.placeholder_buildable = null;
                                        GameRoot.singleton.placeholder_currentItemMode = 0;
                                        GameRoot.singleton.placeholder_currentItemTemplateId = 0;

                                        bogo.gameObject.SetActive(!isPlaceholdersHidden);

                                        //log.LogInfo((string)$"Terrain placeholder {template.name} created at ({worldPos.x}, {worldPos.y}, {worldPos.z}).");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else if (targetPosition != placeholderAnchorPosition)
            {
                var offset = targetPosition - placeholderAnchorPosition;
                foreach (var placeholder in buildingPlaceholders) placeholder.bogo.transform.position += offset;
                foreach (var placeholder in terrainPlaceholders) placeholder.bogo.transform.position += offset;
            }

            placeholderAnchorPosition = targetPosition;
        }

        public static void HidePlaceholders()
        {
            if (isPlaceholdersHidden) return;
            isPlaceholdersHidden = true;
            foreach (var buildingPlaceholder in buildingPlaceholders) buildingPlaceholder.bogo.gameObject.SetActive(false);
            foreach (var terrainPlaceholder in terrainPlaceholders) terrainPlaceholder.bogo.gameObject.SetActive(false);
        }

        public static void ShowPlaceholders()
        {
            if (!isPlaceholdersHidden) return;
            isPlaceholdersHidden = false;
            foreach (var buildingPlaceholder in buildingPlaceholders) buildingPlaceholder.bogo.gameObject.SetActive(true);
            foreach (var terrainPlaceholder in terrainPlaceholders) terrainPlaceholder.bogo.gameObject.SetActive(true);
        }

        internal static void LoadBlueprint(string blueprint)
        {
            CurrentBlueprint = JsonConvert.DeserializeObject<BlueprintData>(blueprint);
            isBlueprintLoaded = true;

            shoppingList.Clear();
            foreach (var buildingData in CurrentBlueprint.buildableObjects)
            {
                var buildingTemplate = ItemTemplateManager.getBuildableObjectTemplate(buildingData.templateId);
                if (buildingTemplate != null && buildingTemplate.parentItemTemplate != null)
                {
                    BlueprintShoppingListData shoppingListEntry;
                    if (!shoppingList.TryGetValue(buildingTemplate.parentItemTemplate.id, out shoppingListEntry))
                    {
                        shoppingListEntry.name = buildingTemplate.parentItemTemplate.name;
                        shoppingListEntry.count = 0;
                    }
                    ++shoppingListEntry.count;
                    shoppingList[buildingTemplate.parentItemTemplate.id] = shoppingListEntry;
                }
            }

            int blockIndex = 0;
            for(int z = 0; z < CurrentBlueprint.blocks.sizeZ; ++z)
            {
                for (int y = 0; y < CurrentBlueprint.blocks.sizeY; ++y)
                {
                    for (int x = 0; x < CurrentBlueprint.blocks.sizeX; ++x)
                    {
                        var blockId = CurrentBlueprint.blocks.ids[blockIndex++];
                        if(blockId >= GameRoot.BUILDING_PART_ARRAY_IDX_START)
                        {

                        }
                        else if (blockId > 0)
                        {
                            var blockTemplate = ItemTemplateManager.getTerrainBlockTemplateByByteIdx(blockId);
                            if (blockTemplate != null && blockTemplate.parentBOT != null)
                            {
                                var itemTemplate = blockTemplate.parentBOT.parentItemTemplate;
                                if (itemTemplate != null)
                                {
                                    BlueprintShoppingListData shoppingListEntry;
                                    if (!shoppingList.TryGetValue(itemTemplate.id, out shoppingListEntry))
                                    {
                                        shoppingListEntry.name = itemTemplate.name;
                                        shoppingListEntry.count = 0;
                                    }
                                    ++shoppingListEntry.count;
                                    shoppingList[itemTemplate.id] = shoppingListEntry;
                                }
                            }
                        }
                    }
                }
            }
        }

        private static ulong CheckIfBuildingExists(AABB3D aabb, Vector3Int worldPos, BuildableObjectData buildableObjectData)
        {
            bogoQueryResult.Clear();
            StreamingSystem.getBuildableObjectGOQuadtreeArray().queryAABB3D(aabb, bogoQueryResult, false);
            if (bogoQueryResult.Count > 0)
            {
                var template = ItemTemplateManager.getBuildableObjectTemplate(buildableObjectData.templateId);
                foreach (var wbogo in bogoQueryResult)
                {
                    if (wbogo.renderMode != 1)
                    {
                        bool match = true;

                        BuildableEntity.BuildableEntityGeneralData generalData = default;
                        if (wbogo.template != template)
                        {
                            match = false;
                        }
                        else if (BuildingManager.buildingManager_getBuildableEntityGeneralData(wbogo.relatedEntityId, ref generalData) == IOBool.iotrue)
                        {
                            if (generalData.pos != worldPos) match = false;

                            if (template.canBeRotatedAroundXAxis)
                            {
                                if (generalData.orientationUnlocked != buildableObjectData.orientationUnlocked) match = false;
                            }
                            else
                            {
                                if (generalData.orientationY != buildableObjectData.orientationY) match = false;
                            }
                        }
                        else
                        {
                            log.LogWarning("data not found");
                            match = false;
                        }

                        if (match) return wbogo.relatedEntityId;
                    }
                }
            }

            return 0ul;
        }

        internal static void OnGameInitializationDone()
        {
            CurrentBlueprintStatusText = "";

            isPlaceholdersActive = false;
            placeholderAnchorPosition = Vector3Int.zero;
            buildingPlaceholders.Clear();
            terrainPlaceholders.Clear();
            buildingPlaceholderUpdateIndex = 0;
            terrainPlaceholderUpdateIndex = 0;

            if (onBlueprintMoved != null) foreach(var dg in onBlueprintMoved.GetInvocationList()) onBlueprintMoved -= (BlueprintMovedDelegate)dg;
            if (onBlueprintUpdated != null) foreach (var dg in onBlueprintUpdated.GetInvocationList()) onBlueprintUpdated -= (BlueprintUpdatedDelegate)dg;

            activeConstructionTaskGroups.Clear();
            dependenciesTemp.Clear();
            genericDependencies.Clear();

            postBuildActions.Clear();

            bogoQueryResult.Clear();

            shoppingList.Clear();
        }

        public struct BlueprintPlaceholder {
            public BuildableObjectGO bogo { get; private set; }
            public State state { get; private set; }
            private static int[] stateCounts = new int[Enum.GetValues(typeof(State)).Length - 1];
            private static Dictionary<ulong, int[]> stateCountsByTemplateId = new Dictionary<ulong, int[]>();

            private static Color[] stateColours = new Color[] {
                new Color(1.0f, 0.0f, 1.0f, 1.0f),
                new Color(1.0f, 1.0f, 1.0f, 1.0f),
                new Color(0.8f, 0.9f, 1.0f, 1.0f),
                new Color(1.0f, 0.2f, 0.1f, 1.0f),
                new Color(0.0f, 0.0f, 0.0f, 0.0f)
            };
            private static Color[] stateBorderColours = new Color[] {
                new Color(1.0f, 0.0f, 1.0f, 1.0f),
                new Color(1.0f, 1.0f, 1.0f, 1.0f),
                new Color(0.8f, 0.9f, 1.0f, 1.0f),
                new Color(1.0f, 0.2f, 0.1f, 1.0f),
                new Color(0.35f, 0.5f, 1.0f, 1.0f)
            };

            public BlueprintPlaceholder(BuildableObjectGO bogo, State state = State.Untested)
            {
                this.bogo = bogo;
                this.state = State.Invalid;
                SetState(state);
            }

            public static IEnumerable<KeyValuePair<ulong, int[]>> GetStateCounts()
            {
                yield return new KeyValuePair<ulong, int[]>(0, stateCounts);
                foreach(var kv in stateCountsByTemplateId)
                {
                    yield return kv;
                }
            }

            public static IEnumerable<KeyValuePair<string, int[]>> GetNamedStateCounts()
            {
                yield return new KeyValuePair<string, int[]>("Total", stateCounts);
                foreach(var kv in stateCountsByTemplateId)
                {
                    var template = ItemTemplateManager.getBuildableObjectTemplate(kv.Key);
                    if(template != null) yield return new KeyValuePair<string, int[]>(template.name, kv.Value);
                }
            }

            public void SetState(State state)
            {
                if (state == this.state) return;

                var counts = (bogo.template != null && bogo.template.parentItemTemplate != null) ? ForceStateCount(bogo.template.parentItemTemplate.id) : null;

                if (this.state != State.Invalid)
                {
                    stateCounts[(int)this.state - 1]--;
                    if (counts != null) counts[(int)this.state - 1]--;
                }

                this.state = state;

                if (this.state != State.Invalid)
                {
                    bogo.setBuildableTint(stateColours[(int)this.state], stateBorderColours[(int)this.state]);

                    stateCounts[(int)this.state - 1]++;
                    if (counts != null) counts[(int)this.state - 1]++;
                }
            }

            public static int GetStateCount(State state)
            {
                return (state == State.Invalid) ? 0 : stateCounts[(int)state - 1];
            }

            public static int GetStateCount(ulong templateId, State state)
            {
                if (state == State.Invalid) return 0;

                var counts = GetStateCounts(templateId);
                if (counts == null) return 0;

                return counts[(int)state - 1];
            }

            public static int GetStateCountTotal()
            {
                return stateCounts.Sum();
            }

            public static int GetStateCountTotal(ulong templateId)
            {
                var counts = GetStateCounts(templateId);
                if (counts == null) return 0;

                return counts.Sum();
            }

            private static int[] GetStateCounts(ulong templateId)
            {
                int[] counts;
                return stateCountsByTemplateId.TryGetValue(templateId, out counts) ? counts : null;
            }

            private static int[] ForceStateCount(ulong templateId)
            {
                int[] counts;
                return stateCountsByTemplateId.TryGetValue(templateId, out counts) ? counts : (stateCountsByTemplateId[templateId] = new int[Enum.GetValues(typeof(State)).Length - 1]);
            }

            public enum State
            {
                Invalid,
                Untested,
                Clear,
                Blocked,
                Done
            }
        }

        public struct BlueprintData
        {
            public struct BuildableObjectData
            {
                public struct CustomData
                {
                    public string identifier;
                    public string value;

                    public CustomData(string identifier, object value)
                    {
                        this.identifier = identifier;
                        this.value = value.ToString();
                    }
                }

                public ulong originalEntityId;
                public string templateName;
                public ulong templateId;
                public int worldX;
                public int worldY;
                public int worldZ;
                public float orientationUnlockedX;
                public float orientationUnlockedY;
                public float orientationUnlockedZ;
                public float orientationUnlockedW;
                public byte orientationY;
                public byte itemMode;
                public CustomData[] customData;

                [JsonIgnore] public Quaternion orientationUnlocked => new Quaternion(orientationUnlockedX, orientationUnlockedY, orientationUnlockedZ, orientationUnlockedW);
                [JsonIgnore] public Vector3Int worldPos => new Vector3Int(worldX, worldY, worldZ);
            }

            public struct BlockData
            {
                public int sizeX;
                public int sizeY;
                public int sizeZ;
                public byte[] ids;

                public Vector3Int Size => new Vector3Int(sizeX, sizeY, sizeZ);
            }

            public uint blueprintVersion;
            public BuildableObjectData[] buildableObjects;
            public BlockData blocks;
        }

        public struct BlueprintShoppingListData
        {
            public string name;
            public int count;

            public BlueprintShoppingListData(string name, int count)
            {
                this.name = name;
                this.count = count;
            }
        }

        public class BuildableObjectGOComparer : IEqualityComparer<BuildableObjectGO>
        {
            public bool Equals(BuildableObjectGO x, BuildableObjectGO y)
            {
                return x.GetInstanceID() == y.GetInstanceID();
            }

            public int GetHashCode(BuildableObjectGO obj)
            {
                return obj.GetInstanceID();
            }
        }
    }
}