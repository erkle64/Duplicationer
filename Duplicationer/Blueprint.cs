using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unfoundry;
using UnityEngine;

namespace Duplicationer
{
    public class Blueprint
    {
        public const uint FileMagicNumber = 0x42649921u;
        public const uint LatestBlueprintVersion = 3;

        public string Name => name;
        public Vector3Int Size => data.blocks.Size;
        public int SizeX => data.blocks.sizeX;
        public int SizeY => data.blocks.sizeY;
        public int SizeZ => data.blocks.sizeZ;
        public bool HasMinecartDepots => minecartDepotIndices.IsNullOrEmpty();

        public ItemTemplate[] IconItemTemplates => iconItemTemplates;
        private ItemTemplate[] iconItemTemplates;

        public Dictionary<ulong, ShoppingListData> ShoppingList { get; private set; }

        private string name;
        private BlueprintData data;
        private int[] minecartDepotIndices;

        private static List<BuildableObjectGO> bogoQueryResult = new List<BuildableObjectGO>(0);
        private static List<ConstructionTaskGroup.ConstructionTask> dependenciesTemp = new List<ConstructionTaskGroup.ConstructionTask>();
        private static Dictionary<ConstructionTaskGroup.ConstructionTask, List<ulong>> genericDependencies = new Dictionary<ConstructionTaskGroup.ConstructionTask, List<ulong>>();

        public delegate void PostBuildAction(ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task);
        private static List<PostBuildAction> postBuildActions = new List<PostBuildAction>();

        private static ItemTemplate powerlineItemTemplate;

        private static ItemTemplate PowerlineItemTemplate
        {
            get => (powerlineItemTemplate == null) ? powerlineItemTemplate = ItemTemplateManager.getItemTemplate("_base_power_line_i") : powerlineItemTemplate;
        }

        public Blueprint(string name, BlueprintData data, int[] minecartDepotIndices, Dictionary<ulong, ShoppingListData> shoppingList, ItemTemplate[] iconItemTemplates)
        {
            this.name = name;
            this.data = data;
            this.minecartDepotIndices = minecartDepotIndices;
            this.ShoppingList = shoppingList;
            this.iconItemTemplates = iconItemTemplates;
        }

        public static Blueprint Create(Vector3Int from, Vector3Int size)
        {
            var to = from + size;

            var shoppingList = new Dictionary<ulong, ShoppingListData>();
            var minecartDepotIndices = new List<int>();
            ulong chunkIndex;
            uint blockIndex;
            var blocks = new byte[size.x * size.y * size.z];
            var blocksIndex = 0;
            for (int wz = from.z; wz < to.z; ++wz)
            {
                for (int wy = from.y; wy < to.y; ++wy)
                {
                    for (int wx = from.x; wx < to.x; ++wx)
                    {
                        ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(wx, wy, wz, out chunkIndex, out blockIndex);

                        var blockId = ChunkManager.chunks_getTerrainData(chunkIndex, blockIndex);
                        blocks[blocksIndex++] = blockId;

                        if (blockId >= GameRoot.BUILDING_PART_ARRAY_IDX_START)
                        {
                            var partTemplate = ItemTemplateManager.getBuildingPartTemplate(GameRoot.BuildingPartIdxLookupTable.table[blockId]);
                            if (partTemplate.parentItemTemplate != null)
                            {
                                AddToShoppingList(shoppingList, partTemplate.parentItemTemplate);
                            }
                        }
                        else if (blockId > 0)
                        {
                            var blockTemplate = ItemTemplateManager.getTerrainBlockTemplateByByteIdx(blockId);
                            if (blockTemplate != null && blockTemplate.parentBOT != null && blockTemplate.parentBOT.parentItemTemplate != null)
                            {
                                AddToShoppingList(shoppingList, blockTemplate.parentBOT.parentItemTemplate);
                            }
                        }
                    }
                }
            }

            var buildings = new HashSet<BuildableObjectGO>(new BuildableObjectGOComparer());
            AABB3D aabb = ObjectPoolManager.aabb3ds.getObject();
            aabb.reinitialize(from.x, from.y, from.z, to.x - from.x, to.y - from.y, to.z - from.z);
            QuadtreeArray<BuildableObjectGO> quadTree = StreamingSystem.getBuildableObjectGOQuadtreeArray();
            quadTree.queryAABB3D(aabb, bogoQueryResult, true);
            foreach (var bogo in bogoQueryResult)
            {
                if (aabb.hasXYZIntersection(bogo._aabb))
                {
                    switch (bogo.template.type)
                    {
                        case BuildableObjectTemplate.BuildableObjectType.BuildingPart:
                        case BuildableObjectTemplate.BuildableObjectType.WorldDecorMineAble:
                            break;

                        default:
                            buildings.Add(bogo);
                            break;
                    }
                }
            }
            ObjectPoolManager.aabb3ds.returnObject(aabb); aabb = null;

            var buildingDataArray = new BlueprintData.BuildableObjectData[buildings.Count];
            var customData = new List<BlueprintData.BuildableObjectData.CustomData>();
            var powerGridBuildings = new HashSet<BuildableObjectGO>(new BuildableObjectGOComparer());
            int buildingIndex = 0;
            foreach (var bogo in buildings)
            {
                BuildableEntity.BuildableEntityGeneralData generalData = default;
                var hasGeneralData = BuildingManager.buildingManager_getBuildableEntityGeneralData(bogo.Id, ref generalData);
                Debug.Assert(hasGeneralData == IOBool.iotrue);

                if (bogo.template.type == BuildableObjectTemplate.BuildableObjectType.MinecartDepot)
                {
                    minecartDepotIndices.Add(buildingIndex);
                }

                buildingDataArray[buildingIndex].originalEntityId = bogo.relatedEntityId;
                buildingDataArray[buildingIndex].templateName = bogo.template.name;
                buildingDataArray[buildingIndex].templateId = generalData.buildableObjectTemplateId;
                buildingDataArray[buildingIndex].worldX = generalData.pos.x - from.x;
                buildingDataArray[buildingIndex].worldY = generalData.pos.y - from.y;
                buildingDataArray[buildingIndex].worldZ = generalData.pos.z - from.z;
                buildingDataArray[buildingIndex].orientationY = bogo.template.canBeRotatedAroundXAxis ? (byte)0 : generalData.orientationY;
                buildingDataArray[buildingIndex].itemMode = generalData.itemMode;

                if (bogo.template.canBeRotatedAroundXAxis)
                {
                    buildingDataArray[buildingIndex].orientationUnlockedX = generalData.orientationUnlocked.x;
                    buildingDataArray[buildingIndex].orientationUnlockedY = generalData.orientationUnlocked.y;
                    buildingDataArray[buildingIndex].orientationUnlockedZ = generalData.orientationUnlocked.z;
                    buildingDataArray[buildingIndex].orientationUnlockedW = generalData.orientationUnlocked.w;
                }
                else
                {
                    buildingDataArray[buildingIndex].orientationUnlockedX = 0.0f;
                    buildingDataArray[buildingIndex].orientationUnlockedY = 0.0f;
                    buildingDataArray[buildingIndex].orientationUnlockedZ = 0.0f;
                    buildingDataArray[buildingIndex].orientationUnlockedW = 1.0f;
                }

                var bogoType = bogo.GetType();

                customData.Clear();
                if (typeof(ProducerGO).IsAssignableFrom(bogoType))
                {
                    var assembler = (ProducerGO)bogo;
                    customData.Add(new BlueprintData.BuildableObjectData.CustomData("craftingRecipeId", assembler.getLastPolledRecipeId()));
                }
                if (typeof(LoaderGO).IsAssignableFrom(bogoType))
                {
                    var loader = (LoaderGO)bogo;
                    customData.Add(new BlueprintData.BuildableObjectData.CustomData("isInputLoader", loader.isInputLoader() ? "true" : "false"));
                    if (bogo.template.loader_isFilter)
                    {
                        customData.Add(new BlueprintData.BuildableObjectData.CustomData("loaderFilterTemplateId", loader.getLastSetFilterTemplate()));
                    }
                }
                if (typeof(PipeLoaderGO).IsAssignableFrom(bogoType))
                {
                    var loader = (PipeLoaderGO)bogo;
                    customData.Add(new BlueprintData.BuildableObjectData.CustomData("isInputLoader", loader.isInputLoader() ? "true" : "false"));
                    customData.Add(new BlueprintData.BuildableObjectData.CustomData("pipeLoaderFilterTemplateId", loader.getLastSetFilterTemplate()));
                }
                if (typeof(ConveyorBalancerGO).IsAssignableFrom(bogoType))
                {
                    var balancer = (ConveyorBalancerGO)bogo;
                    customData.Add(new BlueprintData.BuildableObjectData.CustomData("balancerInputPriority", balancer.getInputPriority()));
                    customData.Add(new BlueprintData.BuildableObjectData.CustomData("balancerOutputPriority", balancer.getOutputPriority()));
                }
                if (typeof(SignGO).IsAssignableFrom(bogoType))
                {
                    var signTextLength = SignGO.signEntity_getSignTextLength(bogo.relatedEntityId);
                    var signText = new byte[signTextLength];
                    byte useAutoTextSize = 0;
                    float textMinSize = 0;
                    float textMaxSize = 0;
                    SignGO.signEntity_getSignText(bogo.relatedEntityId, signText, signTextLength, ref useAutoTextSize, ref textMinSize, ref textMaxSize);

                    customData.Add(new BlueprintData.BuildableObjectData.CustomData("signText", System.Text.Encoding.Default.GetString(signText)));
                    customData.Add(new BlueprintData.BuildableObjectData.CustomData("signUseAutoTextSize", useAutoTextSize));
                    customData.Add(new BlueprintData.BuildableObjectData.CustomData("signTextMinSize", textMinSize));
                    customData.Add(new BlueprintData.BuildableObjectData.CustomData("signTextMaxSize", textMaxSize));
                }
                if (typeof(BlastFurnaceBaseGO).IsAssignableFrom(bogoType))
                {
                    BlastFurnacePollingUpdateData data = default;
                    if (BlastFurnaceBaseGO.blastFurnaceEntity_queryPollingData(bogo.relatedEntityId, ref data) == IOBool.iotrue)
                    {
                        customData.Add(new BlueprintData.BuildableObjectData.CustomData("blastFurnaceModeTemplateId", data.modeTemplateId));
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
                                customData.Add(new BlueprintData.BuildableObjectData.CustomData("powerline", powerGridBuilding.relatedEntityId));
                                AddToShoppingList(shoppingList, PowerlineItemTemplate);
                            }
                        }
                        powerGridBuildings.Add(bogo);
                    }
                }

                //void HandleParenting<T>(T module) where T : BuildableObjectGO
                //{
                //    var searchPos = (Vector3Int)generalData.pos;
                //    var pbogo = quadTree.queryPointXYZ(BuildableEntity.getWorldPositionByLocalOffset(bogo.template, bogo.aabb, module.template.modularBuildingLocalSearchAnchor, bogo.buildOrientation, bogo.transform.rotation));
                //    int nodeIndex = 0;
                //    int matchIndex = -1;
                //    foreach (var node in pbogo.template.modularBuildingConnectionNodes)
                //    {
                //        foreach (var nodeData in node.nodeData)
                //        {
                //            if (nodeData.botId == bogo.template.id)
                //            {
                //                var nodePos = BuildableEntity.getWorldPositionByLocalOffset(pbogo.template, pbogo.aabb, nodeData.positionData.offset, pbogo.buildOrientation, pbogo.transform.rotation);
                //                if (nodePos == searchPos)
                //                {
                //                    matchIndex = nodeIndex;
                //                    break;
                //                }
                //            }
                //        }
                //        if (matchIndex >= 0) break;
                //        ++nodeIndex;
                //    }
                //    if (matchIndex >= 0)
                //    {
                //        customData.Add(new BlueprintData.BuildableObjectData.CustomData("modularNodeIndex", matchIndex));
                //        customData.Add(new BlueprintData.BuildableObjectData.CustomData("modularParentId", pbogo.relatedEntityId));
                //    }
                //}

                //if (typeof(ModularGO_Module).IsAssignableFrom(bogoType))
                //{
                //    var modularModule = bogo.Cast<ModularGO_Module>();
                //    HandleParenting(modularModule);
                //}
                //if (typeof(ModularGO_ModuleWithInteractable).IsAssignableFrom(bogoType))
                //{
                //    var modularModule = bogo.Cast<ModularGO_ModuleWithInteractable>();
                //    HandleParenting(modularModule);

                //    int interactableCount = 0;
                //    var interactables = modularModule.GetInteractableObjects(ref interactableCount);
                //    if (interactableCount > 0)
                //    {
                //        customData.Add(new BlueprintData.BuildableObjectData.CustomData("interactableCount", interactableCount));
                //        for (int j = 0; j < interactableCount; ++j)
                //        {
                //            var interactable = interactables[j];
                //            if (interactable.eventType == InteractableObject.InteractableEventType.NoEvent)
                //            {
                //                ulong filterTemplateId = 0;
                //                BuildableEntity.buildableEntity_getFilterTemplate(bogo.relatedEntityId, 0, ref filterTemplateId, interactable.filterIdx);
                //                if (filterTemplateId > 0) customData.Add(new BlueprintData.BuildableObjectData.CustomData("itemFilter", $"{interactable.filterIdx}={filterTemplateId}"));

                //                BuildableEntity.buildableEntity_getFilterTemplate(bogo.relatedEntityId, 1, ref filterTemplateId, interactable.filterIdx);
                //                if (filterTemplateId > 0) customData.Add(new BlueprintData.BuildableObjectData.CustomData("elementFilter", $"{interactable.filterIdx}={filterTemplateId}"));
                //            }
                //        }
                //    }
                //}
                //if (typeof(ModularGO_ModuleWithScreenPanel).IsAssignableFrom(bogoType))
                //{
                //    var modularModule = bogo.Cast<ModularGO_ModuleWithScreenPanel>();
                //    HandleParenting(modularModule);
                //}

                buildingDataArray[buildingIndex].customData = customData.ToArray();

                if (bogo.template.parentItemTemplate != null)
                {
                    AddToShoppingList(shoppingList, bogo.template.parentItemTemplate);
                }

                buildingIndex++;
            }

            BlueprintData blueprintData = new BlueprintData();
            blueprintData.buildableObjects = buildingDataArray;
            blueprintData.blocks.sizeX = size.x;
            blueprintData.blocks.sizeY = size.y;
            blueprintData.blocks.sizeZ = size.z;
            blueprintData.blocks.ids = blocks;

            return new Blueprint("new blueprint", blueprintData, minecartDepotIndices.ToArray(), shoppingList, new ItemTemplate[0]);
        }

        public int GetShoppingListEntry(ulong itemTemplateId, out string name)
        {
            ShoppingListData shoppingListEntry;
            if (!ShoppingList.TryGetValue(itemTemplateId, out shoppingListEntry))
            {
                name = "";
                return 0;
            }

            name = shoppingListEntry.name;
            return shoppingListEntry.count;
        }

        private static void AddToShoppingList(Dictionary<ulong, ShoppingListData> shoppingList, ItemTemplate template, int count = 1)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));

            ShoppingListData shoppingListEntry;
            if (!shoppingList.TryGetValue(template.id, out shoppingListEntry))
            {
                shoppingListEntry = new ShoppingListData(template.id, template.name, 0);
            }
            shoppingListEntry.count += count;
            shoppingList[template.id] = shoppingListEntry;
        }

        //public static bool TryLoadFileHeader(string path, out FileHeader header, out string name)
        //{
        //    header = new FileHeader();
        //    name = "";

        //    var headerSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FileHeader));
        //    var allBytes = File.ReadAllBytes(path);
        //    if (allBytes.Length < headerSize) return false;

        //    var reader = new BinaryReader(new MemoryStream(allBytes, false));

        //    header.magic = reader.ReadUInt32();
        //    if (header.magic != FileMagicNumber) return false;

        //    header.version = reader.ReadUInt32();

        //    header.icon1 = reader.ReadUInt64();
        //    header.icon2 = reader.ReadUInt64();
        //    header.icon3 = reader.ReadUInt64();
        //    header.icon4 = reader.ReadUInt64();

        //    name = reader.ReadString();

        //    reader.Close();
        //    reader.Dispose();

        //    return true;
        //}

        //public static Blueprint LoadFromFile(string path)
        //{
        //    if (!File.Exists(path)) return null;

        //    var shoppingList = new Dictionary<ulong, ShoppingListData>();
        //    var minecartDepotIndices = new List<int>();

        //    var headerSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FileHeader));
        //    var allBytes = File.ReadAllBytes(path);
        //    if (allBytes.Length < headerSize) throw new FileLoadException(path);

        //    var reader = new BinaryReader(new MemoryStream(allBytes, false));

        //    var magic = reader.ReadUInt32();
        //    if (magic != FileMagicNumber) throw new FileLoadException(path);

        //    var version = reader.ReadUInt32();

        //    var iconItemTemplates = new List<ItemTemplate>();
        //    for (int i = 0; i < 4; ++i)
        //    {
        //        var iconItemTemplateId = reader.ReadUInt64();
        //        if (iconItemTemplateId != 0)
        //        {
        //            var template = ItemTemplateManager.getItemTemplate(iconItemTemplateId);
        //            if (template != null) iconItemTemplates.Add(template);
        //        }
        //    }

        //    var name = reader.ReadString();

        //    ulong dataSize;
        //    var rawData = SaveManager.decompressByteArray(reader.ReadBytes(allBytes.Length - headerSize), out dataSize);
        //    var blueprintData = LoadDataFromString(Encoding.UTF8.GetString(rawData.Take((int)dataSize).ToArray()), shoppingList, minecartDepotIndices);

        //    reader.Close();
        //    reader.Dispose();

        //    return new Blueprint(name, blueprintData, minecartDepotIndices.ToArray(), shoppingList, iconItemTemplates.ToArray());
        //}

        //private static BlueprintData LoadDataFromString(string blueprint, Dictionary<ulong, ShoppingListData> shoppingList, List<int> minecartDepotIndices)
        //{
        //    var blueprintData = JsonConvert.DeserializeObject<BlueprintData>(blueprint);

        //    var powerlineEntityIds = new List<ulong>();
        //    int buildingIndex = 0;
        //    foreach (var buildingData in blueprintData.buildableObjects)
        //    {
        //        var buildingTemplate = ItemTemplateManager.getBuildableObjectTemplate(buildingData.templateId);
        //        if (buildingTemplate != null && buildingTemplate.parentItemTemplate != null)
        //        {
        //            if (buildingTemplate.type == BuildableObjectTemplate.BuildableObjectType.MinecartDepot)
        //            {
        //                minecartDepotIndices.Add(buildingIndex);
        //            }

        //            AddToShoppingList(shoppingList, buildingTemplate.parentItemTemplate);
        //        }

        //        powerlineEntityIds.Clear();
        //        GetCustomDataList(ref blueprintData, buildingIndex, "powerline", powerlineEntityIds);
        //        if (powerlineEntityIds.Count > 0) AddToShoppingList(shoppingList, PowerlineItemTemplate, powerlineEntityIds.Count);

        //        ++buildingIndex;
        //    }

        //    int blockIndex = 0;
        //    for (int z = 0; z < blueprintData.blocks.sizeZ; ++z)
        //    {
        //        for (int y = 0; y < blueprintData.blocks.sizeY; ++y)
        //        {
        //            for (int x = 0; x < blueprintData.blocks.sizeX; ++x)
        //            {
        //                var blockId = blueprintData.blocks.ids[blockIndex++];
        //                if (blockId >= GameRoot.BUILDING_PART_ARRAY_IDX_START)
        //                {
        //                    var partTemplate = ItemTemplateManager.getBuildingPartTemplate(GameRoot.BuildingPartIdxLookupTable.table[blockId]);
        //                    if (partTemplate != null)
        //                    {
        //                        var itemTemplate = partTemplate.parentItemTemplate;
        //                        if (itemTemplate != null)
        //                        {
        //                            AddToShoppingList(shoppingList, itemTemplate);
        //                        }
        //                    }
        //                }
        //                else if (blockId > 0)
        //                {
        //                    var blockTemplate = ItemTemplateManager.getTerrainBlockTemplateByByteIdx(blockId);
        //                    if (blockTemplate != null && blockTemplate.parentBOT != null)
        //                    {
        //                        var itemTemplate = blockTemplate.parentBOT.parentItemTemplate;
        //                        if (itemTemplate != null)
        //                        {
        //                            AddToShoppingList(shoppingList, itemTemplate);
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }

        //    return blueprintData;
        //}

        //public void Save(string path, string name, ItemTemplate[] iconItemTemplates)
        //{
        //    this.name = name;
        //    this.iconItemTemplates = iconItemTemplates;

        //    //var json = Utf8Json.JsonSerializer.Serialize(data);
        //    ////var json = JsonConvert.SerializeObject(data);

        //    //ulong dataSize;
        //    //var compressed = SaveManager.compressByteArray(json, out dataSize);
        //    ////var compressed = SaveManager.compressByteArray(Encoding.UTF8.GetBytes(json), out dataSize);
        //    ////File.WriteAllBytes(path, compressed.Take((int)dataSize).ToArray());

        //    //var writer = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write));

        //    //writer.Write(FileMagicNumber);
        //    //writer.Write(LatestBlueprintVersion);

        //    //for (int i = 0; i < iconItemTemplates.Length; i++)
        //    //{
        //    //    var template = iconItemTemplates[i];
        //    //    writer.Write(template.id);
        //    //}
        //    //for (int i = iconItemTemplates.Length; i < 4; i++)
        //    //{
        //    //    writer.Write(0ul);
        //    //}

        //    //writer.Write(name);

        //    //writer.Write(compressed.Take((int)dataSize).ToArray());

        //    //writer.Close();
        //    //writer.Dispose();
        //}

        public void Place(Vector3Int anchorPosition, ConstructionTaskGroup constructionTaskGroup) => Place(GameRoot.getClientUsernameHash(), anchorPosition, constructionTaskGroup);
        public void Place(Character character, Vector3Int anchorPosition, ConstructionTaskGroup constructionTaskGroup) => Place(character.usernameHash, anchorPosition, constructionTaskGroup);
        public void Place(ulong usernameHash, Vector3Int anchorPosition, ConstructionTaskGroup constructionTaskGroup)
        {
            AABB3D aabb = ObjectPoolManager.aabb3ds.getObject();
            int buildingIndex = 0;
            foreach (var buildableObjectData in data.buildableObjects)
            {
                postBuildActions.Clear();

                var template = ItemTemplateManager.getBuildableObjectTemplate(buildableObjectData.templateId);
                Debug.Assert(template != null);

                var worldPos = new Vector3Int(buildableObjectData.worldX, buildableObjectData.worldY, buildableObjectData.worldZ) + anchorPosition;

                int wx, wy, wz;
                if (template.canBeRotatedAroundXAxis)
                    BuildingManager.getWidthFromUnlockedOrientation(template, buildableObjectData.orientationUnlocked, out wx, out wy, out wz);
                else
                    BuildingManager.getWidthFromOrientation(template, (BuildingManager.BuildOrientation)buildableObjectData.orientationY, out wx, out wy, out wz);

                ulong additionalData_ulong_01 = 0ul;
                ulong additionalData_ulong_02 = 0ul;

                bool usePasteConfigSettings = false;
                ulong pasteConfigSettings_01 = 0ul;
                ulong pasteConfigSettings_02 = 0ul;

                var craftingRecipeId = GetCustomData<ulong>(buildingIndex, "craftingRecipeId");
                if (craftingRecipeId != 0)
                {
                    usePasteConfigSettings = true;
                    pasteConfigSettings_01 = craftingRecipeId;
                }

                if (HasCustomData(buildingIndex, "isInputLoader"))
                {
                    usePasteConfigSettings = true;
                    bool isInputLoader = GetCustomData<bool>(buildingIndex, "isInputLoader");
                    pasteConfigSettings_01 = isInputLoader ? 1u : 0u;

                    if (template.loader_isFilter)
                    {
                        if (HasCustomData(buildingIndex, "loaderFilterTemplateId"))
                        {
                            var loaderFilterTemplateId = GetCustomData<ulong>(buildingIndex, "loaderFilterTemplateId");
                            if (loaderFilterTemplateId > 0)
                            {
                                usePasteConfigSettings = true;
                                pasteConfigSettings_02 = loaderFilterTemplateId;
                            }
                        }
                    }

                    if (template.type == BuildableObjectTemplate.BuildableObjectType.PipeLoader)
                    {
                        if (HasCustomData(buildingIndex, "pipeLoaderFilterTemplateId"))
                        {
                            var pipeLoaderFilterTemplateId = GetCustomData<ulong>(buildingIndex, "pipeLoaderFilterTemplateId");
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

                if (HasCustomData(buildingIndex, "modularNodeIndex"))
                {
                    additionalData_ulong_01 = GetCustomData<ulong>(buildingIndex, "modularNodeIndex");
                }

                if (template.type == BuildableObjectTemplate.BuildableObjectType.ConveyorBalancer)
                {
                    var balancerInputPriority = GetCustomData<int>(buildingIndex, "balancerInputPriority");
                    var balancerOutputPriority = GetCustomData<int>(buildingIndex, "balancerOutputPriority");
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
                    var signText = GetCustomData<string>(buildingIndex, "signText");
                    var signUseAutoTextSize = GetCustomData<byte>(buildingIndex, "signUseAutoTextSize");
                    var signTextMinSize = GetCustomData<float>(buildingIndex, "signTextMinSize");
                    var signTextMaxSize = GetCustomData<float>(buildingIndex, "signTextMaxSize");
                    postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                    {
                        if (task.entityId > 0)
                        {
                            GameRoot.addLockstepEvent(new SignSetTextEvent(usernameHash, task.entityId, signText, signUseAutoTextSize != 0, signTextMinSize, signTextMaxSize));
                        }
                    });
                }

                if (HasCustomData(buildingIndex, "blastFurnaceModeTemplateId"))
                {
                    var modeTemplateId = GetCustomData<ulong>(buildingIndex, "blastFurnaceModeTemplateId");
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

                //if (HasCustomData(buildingIndex, "interactableCount"))
                //{
                //    var elementFilters = new List<string>();
                //    GetCustomDataList(buildingIndex, "elementFilter", elementFilters);
                //    foreach (var elementFilter in elementFilters)
                //    {
                //        var parts = elementFilter.Split('=');
                //        if (parts.Length == 2)
                //        {
                //            var filterIdx = Convert.ToUInt32(parts[0]);
                //            var elementFilterTemplateId = Convert.ToUInt64(parts[1]);
                //            if (string.IsNullOrEmpty(template.modularBuildingPipeConnectionData[(int)filterIdx].modularBuildingFixedElementTemplateFilter))
                //            {
                //                postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                //                {
                //                    if (task.entityId > 0)
                //                    {
                //                        GameRoot.addLockstepEvent(new SetFilterEvent(usernameHash, task.entityId, elementFilterTemplateId, 1, filterIdx));
                //                    }
                //                });
                //            }
                //        }
                //    }
                //}

                var powerlineEntityIds = new List<ulong>();
                GetCustomDataList(buildingIndex, "powerline", powerlineEntityIds);
                foreach (var powerlineEntityId in powerlineEntityIds)
                {
                    var powerlineIndex = FindEntityIndex(powerlineEntityId);
                    if (powerlineIndex >= 0)
                    {
                        var fromPos = worldPos;
                        var toBuildableObjectData = data.buildableObjects[powerlineIndex];
                        var toPos = new Vector3Int(toBuildableObjectData.worldX, toBuildableObjectData.worldY, toBuildableObjectData.worldZ) + anchorPosition;
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
                            while (taskGroup.InvokeNextTaskIfReady()) ;
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
                                    while (taskGroup.InvokeNextTaskIfReady()) ;
                                });
                            });
                            GameRoot.addLockstepEvent(buildEntityEvent);
                        });
                    });
                }

                ++buildingIndex;
            }
            ObjectPoolManager.aabb3ds.returnObject(aabb); aabb = null;


            buildingIndex = 0;
            foreach (var buildableObjectData in data.buildableObjects)
            {
                dependenciesTemp.Clear();
                if (HasCustomData(buildingIndex, "modularParentId"))
                {
                    ulong parentId = GetCustomData<ulong>(buildingIndex, "modularParentId");

                    var dependency = constructionTaskGroup.GetTask(parentId);
                    if (dependency != null) dependenciesTemp.Add(dependency);
                    else Debug.LogWarning((string)$"Entity id {parentId} not found in blueprint");
                }

                var powerlineEntityIds = new List<ulong>();
                GetCustomDataList(buildingIndex, "powerline", powerlineEntityIds);
                foreach (var powerlineEntityId in powerlineEntityIds)
                {
                    var dependency = constructionTaskGroup.GetTask(powerlineEntityId);
                    if (dependency != null) dependenciesTemp.Add(dependency);
                    else Debug.LogWarning((string)$"Entity id {powerlineEntityId} not found in blueprint");
                }

                if (dependenciesTemp.Count > 0)
                {
                    var task = constructionTaskGroup.GetTask(buildableObjectData.originalEntityId);
                    if (task != null) task.dependencies = dependenciesTemp.ToArray();
                }

                buildingIndex++;
            }

            if (data.blocks.ids == null) throw new ArgumentNullException(nameof(data.blocks.ids));

            var quadTreeArray = StreamingSystem.getBuildableObjectGOQuadtreeArray();
            int blockIndex = 0;
            for (int z = 0; z < data.blocks.sizeZ; z++)
            {
                for (int y = 0; y < data.blocks.sizeY; y++)
                {
                    for (int x = 0; x < data.blocks.sizeX; x++)
                    {
                        //var blockId = currentBlueprint.blocks.ids[x + (y + z * currentBlueprint.blocks.sizeY) * currentBlueprint.blocks.sizeX];
                        var blockId = data.blocks.ids[blockIndex++];
                        if (blockId > 0)
                        {
                            var worldPos = new Vector3Int(x, y, z) + anchorPosition;
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
                                        //Debug.Log((string)$"Place building part {partTemplate.parentItemTemplate.name} at ({worldPos.x}, {worldPos.y}, {worldPos.z})");

                                        ActionManager.AddQueuedEvent(() =>
                                        {
                                            //BuildingManager.client_tryBuild(worldPos, BuildingManager.BuildOrientation.xPos, partTemplate.parentItemTemplate);
                                            int mode = 0;
                                            if (partTemplate.parentItemTemplate.toggleableModes != null && partTemplate.parentItemTemplate.toggleableModes.Length != 0 && partTemplate.parentItemTemplate.toggleableModeType == ItemTemplate.ItemTemplateToggleableModeTypes.MultipleBuildings)
                                            {
                                                for (int index = 0; index < partTemplate.parentItemTemplate.toggleableModes.Length; ++index)
                                                {
                                                    if (partTemplate.parentItemTemplate.toggleableModes[index].buildableObjectTemplate == partTemplate)
                                                    {
                                                        mode = index;
                                                        break;
                                                    }
                                                }
                                            }
                                            GameRoot.addLockstepEvent(new BuildEntityEvent(usernameHash, partTemplate.parentItemTemplate.id, mode, worldPos, 0, Quaternion.identity, 1, 0, false));
                                        });
                                    }
                                }
                                else
                                {
                                    var blockTemplate = ItemTemplateManager.getTerrainBlockTemplateByByteIdx(blockId);
                                    if (blockTemplate != null && blockTemplate.yieldItemOnDig_template != null && blockTemplate.yieldItemOnDig_template.buildableObjectTemplate != null)
                                    {
                                        ActionManager.AddQueuedEvent(() =>
                                        {
                                            int mode = 0;
                                            if (blockTemplate.yieldItemOnDig_template.toggleableModes != null && blockTemplate.yieldItemOnDig_template.toggleableModes.Length != 0 && blockTemplate.yieldItemOnDig_template.toggleableModeType == ItemTemplate.ItemTemplateToggleableModeTypes.MultipleBuildings)
                                            {
                                                for (int index = 0; index < blockTemplate.yieldItemOnDig_template.toggleableModes.Length; ++index)
                                                {
                                                    if (blockTemplate.yieldItemOnDig_template.toggleableModes[index].buildableObjectTemplate == blockTemplate.parentBOT)
                                                    {
                                                        mode = index;
                                                        break;
                                                    }
                                                }
                                            }
                                            GameRoot.addLockstepEvent(new BuildEntityEvent(usernameHash, blockTemplate.yieldItemOnDig_template.id, mode, worldPos, 0, Quaternion.identity, 1, 0, false));
                                        });
                                    }
                                    else if (blockTemplate != null && blockTemplate.parentBOT != null)
                                    {
                                        var itemTemplate = blockTemplate.parentBOT.parentItemTemplate;
                                        if (itemTemplate != null)
                                        {
                                            //Debug.Log((string)$"Place terrain {itemTemplate.name} at ({worldPos.x}, {worldPos.y}, {worldPos.z})");

                                            ActionManager.AddQueuedEvent(() =>
                                            {
                                                int mode = 0;
                                                if (itemTemplate.toggleableModes != null && itemTemplate.toggleableModes.Length != 0 && itemTemplate.toggleableModeType == ItemTemplate.ItemTemplateToggleableModeTypes.MultipleBuildings)
                                                {
                                                    for (int index = 0; index < itemTemplate.toggleableModes.Length; ++index)
                                                    {
                                                        if (itemTemplate.toggleableModes[index].buildableObjectTemplate == blockTemplate.parentBOT)
                                                        {
                                                            mode = index;
                                                            break;
                                                        }
                                                    }
                                                }
                                                GameRoot.addLockstepEvent(new BuildEntityEvent(usernameHash, itemTemplate.id, mode, worldPos, 0, Quaternion.identity, 1, 0, false));
                                            });
                                        }
                                        else
                                        {
                                            Debug.LogWarning((string)$"No item template for terrain index {blockId}");
                                        }
                                    }
                                    else
                                    {
                                        Debug.LogWarning((string)$"No block template for terrain index {blockId}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private int FindEntityIndex(ulong entityId)
        {
            for (int i = 0; i < data.buildableObjects.Length; ++i)
            {
                if (data.buildableObjects[i].originalEntityId == entityId) return i;
            }
            return -1;
        }

        private int CountModularParents(ulong parentId)
        {
            var parentIndex = FindEntityIndex(parentId);
            if (parentIndex < 0) return 0;

            if (HasCustomData(parentIndex, "modularParentId"))
            {
                var grandparentId = GetCustomData<ulong>(parentIndex, "modularParentId");
                return CountModularParents(grandparentId) + 1;
            }

            return 1;
        }

        public bool HasCustomData(int index, string identifier) => HasCustomData(ref data, index, identifier);
        public static bool HasCustomData(ref BlueprintData data, int index, string identifier)
        {
            foreach (var customDataEntry in data.buildableObjects[index].customData) if (customDataEntry.identifier == identifier) return true;
            return false;
        }

        public T GetCustomData<T>(int index, string identifier) => GetCustomData<T>(ref data, index, identifier);
        public static T GetCustomData<T>(ref BlueprintData data, int index, string identifier)
        {
            foreach (var customDataEntry in data.buildableObjects[index].customData) if (customDataEntry.identifier == identifier) return (T)Convert.ChangeType(customDataEntry.value, typeof(T));
            return default;
        }

        public void GetCustomDataList<T>(int index, string identifier, List<T> list) => GetCustomDataList<T>(ref data, index, identifier, list);
        public static void GetCustomDataList<T>(ref BlueprintData data, int index, string identifier, List<T> list)
        {
            foreach (var customDataEntry in data.buildableObjects[index].customData) if (customDataEntry.identifier == identifier) list.Add((T)Convert.ChangeType(customDataEntry.value, typeof(T)));
        }

        internal BlueprintData.BuildableObjectData GetBuildableObjectData(int index) => GetBuildableObjectData(ref data, index);
        internal static BlueprintData.BuildableObjectData GetBuildableObjectData(ref BlueprintData data, int index)
        {
            if (index < 0 || index >= data.buildableObjects.Length) throw new IndexOutOfRangeException(nameof(index));

            return data.buildableObjects[index];
        }

        internal byte GetBlockId(int x, int y, int z) => GetBlockId(ref data, x, y, z);
        internal static byte GetBlockId(ref BlueprintData data, int x, int y, int z)
        {
            if (x < 0 || x >= data.blocks.sizeX) throw new IndexOutOfRangeException(nameof(x));
            if (y < 0 || y >= data.blocks.sizeY) throw new IndexOutOfRangeException(nameof(y));
            if (z < 0 || z >= data.blocks.sizeZ) throw new IndexOutOfRangeException(nameof(z));

            return data.blocks.ids[x + (y + z * data.blocks.sizeY) * data.blocks.sizeX];
        }

        internal byte GetBlockId(int index) => GetBlockId(ref data, index);
        internal static byte GetBlockId(ref BlueprintData data, int index)
        {
            if (index < 0 || index >= data.blocks.ids.Length) throw new IndexOutOfRangeException(nameof(index));

            return data.blocks.ids[index];
        }

        internal static ulong CheckIfBuildingExists(AABB3D aabb, Vector3Int worldPos, BlueprintData.BuildableObjectData buildableObjectData)
        {
            bogoQueryResult.Clear();
            StreamingSystem.getBuildableObjectGOQuadtreeArray().queryAABB3D(aabb, bogoQueryResult, false);
            if (bogoQueryResult.Count > 0)
            {
                var template = ItemTemplateManager.getBuildableObjectTemplate(buildableObjectData.templateId);
                foreach (var wbogo in bogoQueryResult)
                {
                    if (Traverse.Create(wbogo).Field("renderMode").GetValue<int>() != 1)
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
                            Debug.LogWarning("data not found");
                            match = false;
                        }

                        if (match) return wbogo.relatedEntityId;
                    }
                }
            }

            return 0ul;
        }

        internal void Rotate()
        {
            var oldSize = Size;
            var newSize = new Vector3Int(oldSize.z, oldSize.y, oldSize.x);
            var oldCenter = ((Vector3)oldSize) / 2.0f;
            var newCenter = ((Vector3)newSize) / 2.0f;

            BlueprintData rotatedData = new BlueprintData(data.buildableObjects.Length, data.blocks.Size);
            rotatedData.buildableObjects = new BlueprintData.BuildableObjectData[data.buildableObjects.Length];
            rotatedData.blocks.ids = new byte[data.blocks.ids.Length];
            for (int i = 0; i < data.buildableObjects.Length; ++i)
            {
                var buildableObjectData = data.buildableObjects[i];
                var offsetX = buildableObjectData.worldZ - oldCenter.z;
                var offsetZ = oldCenter.x - buildableObjectData.worldX;
                var newX = Mathf.RoundToInt(newCenter.x + offsetX);
                var newZ = Mathf.RoundToInt(newCenter.z + offsetZ);

                var template = ItemTemplateManager.getBuildableObjectTemplate(buildableObjectData.templateId);
                if (template != null)
                {
                    if (template.canBeRotatedAroundXAxis)
                    {
                        var oldOrientation = buildableObjectData.orientationUnlocked;
                        var newOrientation = Quaternion.Euler(0.0f, 90.0f, 0.0f) * oldOrientation;
                        int wx, wy, wz;
                        BuildingManager.getWidthFromUnlockedOrientation(template, newOrientation, out wx, out wy, out wz);
                        newZ -= wz;
                        buildableObjectData.orientationUnlocked = newOrientation;
                    }
                    else
                    {
                        var oldOrientation = buildableObjectData.orientationY;
                        var newOrientation = (byte)((oldOrientation + 1) & 0x3);
                        int wx, wy, wz;
                        BuildingManager.getWidthFromOrientation(template, (BuildingManager.BuildOrientation)newOrientation, out wx, out wy, out wz);
                        newZ -= wz;
                        buildableObjectData.orientationY = newOrientation;
                    }
                }

                buildableObjectData.worldX = newX;
                buildableObjectData.worldZ = newZ;
                rotatedData.buildableObjects[i] = buildableObjectData;
            }

            var newBlockIds = new byte[data.blocks.ids.Length];
            int fromIndex = 0;
            for (int x = 0; x < newSize.x; x++)
            {
                for (int y = 0; y < newSize.y; y++)
                {
                    for (int z = newSize.z - 1; z >= 0; z--)
                    {
                        newBlockIds[x + (y + z * newSize.y) * newSize.x] = data.blocks.ids[fromIndex++];
                    }
                }
            }

            rotatedData.blocks = new BlueprintData.BlockData(newSize, newBlockIds);

            data = rotatedData;
        }

        public void Show(Vector3Int anchorPosition, Vector3Int repeatFrom, Vector3Int repeatTo, Vector3Int repeatStepSize, BatchRenderingGroup placeholderRenderGroup, List<BlueprintPlaceholder> buildingPlaceholders, List<BlueprintPlaceholder> terrainPlaceholders)
        {
            for (int ry = repeatFrom.y; ry <= repeatTo.y; ++ry)
            {
                for (int rz = repeatFrom.z; rz <= repeatTo.z; ++rz)
                {
                    for (int rx = repeatFrom.x; rx <= repeatTo.x; ++rx)
                    {
                        var repeatIndex = new Vector3Int(rx, ry, rz);
                        var repeatAnchorPosition = anchorPosition + new Vector3Int(rx * repeatStepSize.x, ry * repeatStepSize.y, rz * repeatStepSize.z);

                        for (int buildingIndex = 0; buildingIndex < data.buildableObjects.Length; buildingIndex++)
                        {
                            var buildableObjectData = data.buildableObjects[buildingIndex];
                            var template = ItemTemplateManager.getBuildableObjectTemplate(buildableObjectData.templateId);

                            int wx, wy, wz;
                            if (template.canBeRotatedAroundXAxis)
                                BuildingManager.getWidthFromUnlockedOrientation(template, buildableObjectData.orientationUnlocked, out wx, out wy, out wz);
                            else
                                BuildingManager.getWidthFromOrientation(template, (BuildingManager.BuildOrientation)buildableObjectData.orientationY, out wx, out wy, out wz);

                            var position = new Vector3(buildableObjectData.worldX + wx * 0.5f, buildableObjectData.worldY + (template.canBeRotatedAroundXAxis ? wy * 0.5f : 0.0f), buildableObjectData.worldZ + wz * 0.5f) + repeatAnchorPosition;
                            var rotation = template.canBeRotatedAroundXAxis ? buildableObjectData.orientationUnlocked : Quaternion.Euler(0, buildableObjectData.orientationY * 90.0f, 0.0f);
                            var orientation = (BuildingManager.BuildOrientation)buildableObjectData.orientationY;

                            var baseTransform = Matrix4x4.TRS(position, rotation, Vector3.one);

                            var pattern = PlaceholderPattern.Instance(template.prefab);
                            var handles = new BatchRenderingHandle[pattern.Entries.Length];
                            for (int i = 0; i < pattern.Entries.Length; i++)
                            {
                                var entry = pattern.Entries[i];
                                var transform = baseTransform * entry.relativeTransform;
                                handles[i] = placeholderRenderGroup.AddSimplePlaceholderTransform(entry.mesh, transform, BlueprintPlaceholder.stateColours[1]);
                            }

                            buildingPlaceholders.Add(new BlueprintPlaceholder(buildingIndex, repeatIndex, template, position, rotation, orientation, handles));
                        }

                        int blockIndex = 0;
                        for (int z = 0; z < data.blocks.sizeZ; ++z)
                        {
                            for (int y = 0; y < data.blocks.sizeY; ++y)
                            {
                                for (int x = 0; x < data.blocks.sizeX; ++x)
                                {
                                    var id = data.blocks.ids[blockIndex];
                                    if (id > 0)
                                    {
                                        var worldPos = new Vector3(x + repeatAnchorPosition.x + 0.5f, y + repeatAnchorPosition.y, z + repeatAnchorPosition.z + 0.5f);

                                        BuildableObjectTemplate template = null;
                                        if (id < GameRoot.MAX_TERRAIN_COUNT)
                                        {
                                            var tbt = ItemTemplateManager.getTerrainBlockTemplateByByteIdx(id);
                                            if (tbt != null) template = tbt.parentBOT;
                                        }
                                        else
                                        {
                                            template = ItemTemplateManager.getBuildingPartTemplate(GameRoot.BuildingPartIdxLookupTable.table[id]);
                                            if (template == null) Debug.LogWarning((string)$"Template not found for terrain index {id}-{GameRoot.BUILDING_PART_ARRAY_IDX_START} with id {GameRoot.BuildingPartIdxLookupTable.table[id]} at ({worldPos.x}, {worldPos.y}, {worldPos.z})");
                                        }

                                        if (template != null)
                                        {
                                            var baseTransform = Matrix4x4.Translate(worldPos);

                                            var pattern = PlaceholderPattern.Instance(template.prefab);
                                            var handles = new BatchRenderingHandle[pattern.Entries.Length];
                                            for (int i = 0; i < pattern.Entries.Length; i++)
                                            {
                                                var entry = pattern.Entries[i];
                                                var transform = baseTransform * entry.relativeTransform;
                                                handles[i] = placeholderRenderGroup.AddSimplePlaceholderTransform(entry.mesh, transform, BlueprintPlaceholder.stateColours[1]);
                                            }

                                            terrainPlaceholders.Add(new BlueprintPlaceholder(blockIndex, repeatIndex, template, worldPos, Quaternion.identity, BuildingManager.BuildOrientation.xPos, handles));
                                        }
                                    }
                                    blockIndex++;
                                }
                            }
                        }
                    }
                }
            }
        }

        internal void GetExistingMinecartDepots(Vector3Int targetPosition, List<MinecartDepotGO> existingMinecartDepots)
        {
            AABB3D aabb = ObjectPoolManager.aabb3ds.getObject();
            foreach (var index in minecartDepotIndices)
            {
                if (index >= data.buildableObjects.Length) continue;

                var buildableObjectData = data.buildableObjects[index];
                var template = ItemTemplateManager.getBuildableObjectTemplate(buildableObjectData.templateId);
                if (template != null)
                {
                    var worldPos = new Vector3Int(buildableObjectData.worldX + targetPosition.x, buildableObjectData.worldY + targetPosition.y, buildableObjectData.worldZ + targetPosition.z);
                    int wx, wy, wz;
                    if (template.canBeRotatedAroundXAxis)
                        BuildingManager.getWidthFromUnlockedOrientation(template, buildableObjectData.orientationUnlocked, out wx, out wy, out wz);
                    else
                        BuildingManager.getWidthFromOrientation(template, (BuildingManager.BuildOrientation)buildableObjectData.orientationY, out wx, out wy, out wz);
                    aabb.reinitialize(worldPos.x, worldPos.y, worldPos.z, wx, wy, wz);

                    var depotEntityId = CheckIfBuildingExists(aabb, worldPos, buildableObjectData);
                    if (depotEntityId > 0)
                    {
                        var bogo = StreamingSystem.getBuildableObjectGOByEntityId(depotEntityId);
                        if (bogo != null)
                        {
                            var depot = (MinecartDepotGO)bogo;
                            if (depot != null) existingMinecartDepots.Add(depot);
                        }
                    }
                }
            }
            ObjectPoolManager.aabb3ds.returnObject(aabb); aabb = null;
        }

        internal bool HasExistingMinecartDepots(Vector3Int targetPosition)
        {
            AABB3D aabb = ObjectPoolManager.aabb3ds.getObject();
            foreach (var index in minecartDepotIndices)
            {
                if (index >= data.buildableObjects.Length) continue;

                var buildableObjectData = data.buildableObjects[index];
                var template = ItemTemplateManager.getBuildableObjectTemplate(buildableObjectData.templateId);
                if (template != null)
                {
                    var worldPos = new Vector3Int(buildableObjectData.worldX + targetPosition.x, buildableObjectData.worldY + targetPosition.y, buildableObjectData.worldZ + targetPosition.z);
                    int wx, wy, wz;
                    if (template.canBeRotatedAroundXAxis)
                        BuildingManager.getWidthFromUnlockedOrientation(template, buildableObjectData.orientationUnlocked, out wx, out wy, out wz);
                    else
                        BuildingManager.getWidthFromOrientation(template, (BuildingManager.BuildOrientation)buildableObjectData.orientationY, out wx, out wy, out wz);
                    aabb.reinitialize(worldPos.x, worldPos.y, worldPos.z, wx, wy, wz);

                    var depotEntityId = CheckIfBuildingExists(aabb, worldPos, buildableObjectData);
                    if (depotEntityId > 0)
                    {
                        var bogo = StreamingSystem.getBuildableObjectGOByEntityId(depotEntityId);
                        if (bogo != null)
                        {
                            var depot = (MinecartDepotGO)bogo;
                            if (depot != null) return true;
                        }
                    }
                }
            }
            ObjectPoolManager.aabb3ds.returnObject(aabb); aabb = null;

            return false;
        }

        public struct ShoppingListData
        {
            public ulong itemTemplateId;
            public string name;
            public int count;

            public ShoppingListData(ulong itemTemplateId, string name, int count)
            {
                this.itemTemplateId = itemTemplateId;
                this.name = name;
                this.count = count;
            }
        }

        public struct FileHeader
        {
            public uint magic;
            public uint version;
            public ulong icon1;
            public ulong icon2;
            public ulong icon3;
            public ulong icon4;
        }
    }
}
