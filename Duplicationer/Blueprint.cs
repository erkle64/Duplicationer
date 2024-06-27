using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TinyJSON;
using Unfoundry;
using UnityEngine;
using static Duplicationer.BlueprintData.BuildableObjectData;

namespace Duplicationer
{
    public class Blueprint
    {
        public const uint FileMagicNumber = 0x42649921u;
        public const uint LatestBlueprintVersion = 4u;

        public string Name => _name;
        public Vector3Int Size => _data.blocks.Size;
        public int SizeX => _data.blocks.sizeX;
        public int SizeY => _data.blocks.sizeY;
        public int SizeZ => _data.blocks.sizeZ;

        public ItemElementTemplate[] IconItemTemplates => _iconItemTemplates;
        private ItemElementTemplate[] _iconItemTemplates;

        public Dictionary<ulong, ShoppingListData> ShoppingList { get; private set; }

        private string _name;
        private BlueprintData _data;

        private bool _isMirrorable = false;
        public bool IsMirrorable => _isMirrorable;

        private bool _hasRecipes = false;
        public bool HasRecipes => _hasRecipes;

        private static List<BuildableObjectGO> _bogoQueryResult = new List<BuildableObjectGO>(0);
        private static List<ConstructionTaskGroup.ConstructionTask> _dependenciesTemp = new List<ConstructionTaskGroup.ConstructionTask>();
        private static Dictionary<ConstructionTaskGroup.ConstructionTask, List<ulong>> _genericDependencies = new Dictionary<ConstructionTaskGroup.ConstructionTask, List<ulong>>();

        public delegate void PostBuildAction(ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task);
        private static List<PostBuildAction> _postBuildActions = new List<PostBuildAction>();

        private static ItemTemplate _powerlineItemTemplate;

        public static ItemTemplate PowerlineItemTemplate
        {
            get => (_powerlineItemTemplate == null) ? _powerlineItemTemplate = ItemTemplateManager.getItemTemplate("_base_power_line_i") : _powerlineItemTemplate;
        }

        private struct PowerlineConnectionPair
        {
            public ulong fromEntityId;
            public ulong toEntityId;

            public PowerlineConnectionPair(ulong fromEntityId, ulong toEntityId)
            {
                this.fromEntityId = fromEntityId;
                this.toEntityId = toEntityId;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is PowerlineConnectionPair)) return false;
                var other = (PowerlineConnectionPair)obj;
                return fromEntityId == other.fromEntityId && toEntityId == other.toEntityId;
            }

            public override int GetHashCode()
            {
                return fromEntityId.GetHashCode() ^ toEntityId.GetHashCode();
            }
        }
        private static HashSet<PowerlineConnectionPair> _powerlineConnectionPairs = new HashSet<PowerlineConnectionPair>();

        public Blueprint(string name, BlueprintData data, Dictionary<ulong, ShoppingListData> shoppingList, ItemElementTemplate[] iconItemTemplates)
        {
            _name = name;
            _data = data;
            ShoppingList = shoppingList;
            _iconItemTemplates = iconItemTemplates;

            _hasRecipes = false;
            _isMirrorable = true;
            foreach (var buildableObjectData in data.buildableObjects)
            {
                var template = ItemTemplateManager.getBuildableObjectTemplate(buildableObjectData.templateId);
                if (template != null && template.canBeRotatedAroundXAxis)
                {
                    _isMirrorable = false;
                    break;
                }

                if (buildableObjectData.HasCustomData("craftingRecipeId")) _hasRecipes = true;
            }
        }

        public static Blueprint Create(Vector3Int from, Vector3Int size)
        {
            var to = from + size;

            var blocks = new byte[size.x * size.y * size.z];
            var blocksIndex = 0;
            for (int wz = from.z; wz < to.z; ++wz)
            {
                for (int wy = from.y; wy < to.y; ++wy)
                {
                    for (int wx = from.x; wx < to.x; ++wx)
                    {
                        ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(wx, wy, wz, out ulong chunkIndex, out uint blockIndex);

                        var blockId = ChunkManager.chunks_getTerrainData(chunkIndex, blockIndex);
                        blocks[blocksIndex++] = blockId;
                    }
                }
            }

            var buildings = new HashSet<BuildableObjectGO>(new BuildableObjectGOComparer());
            AABB3D aabb = ObjectPoolManager.aabb3ds.getObject();
            aabb.reinitialize(from.x, from.y, from.z, to.x - from.x, to.y - from.y, to.z - from.z);
            QuadtreeArray<BuildableObjectGO> quadTree = StreamingSystem.getBuildableObjectGOQuadtreeArray();
            _bogoQueryResult.Clear();
            quadTree.queryAABB3D(aabb, _bogoQueryResult, true);
            foreach (var bogo in _bogoQueryResult)
            {
                if (aabb.hasXYZIntersection(bogo._aabb))
                {
                    switch (bogo.template.type)
                    {
                        case BuildableObjectTemplate.BuildableObjectType.BuildingPart:
                        case BuildableObjectTemplate.BuildableObjectType.WorldDecorMineAble:
                        case BuildableObjectTemplate.BuildableObjectType.ModularEntityModule:
                            break;

                        default:
                            buildings.Add(bogo);
                            break;
                    }
                }
            }
            ObjectPoolManager.aabb3ds.returnObject(aabb); aabb = null;

            return Create(from, size, buildings, blocks);
        }

        public static Blueprint Create(Vector3Int from, Vector3Int size, IEnumerable<BuildableObjectGO> buildings, byte[] blocks)
        {
            var to = from + size;

            var shoppingList = new Dictionary<ulong, ShoppingListData>();

            for (int i = 0; i < blocks.Length; i++)
            {
                byte blockId = blocks[i];
                if (blockId >= GameRoot.BUILDING_PART_ARRAY_IDX_START)
                {
                    var partTemplate = ItemTemplateManager.getBuildingPartTemplate(GameRoot.BuildingPartIdxLookupTable.table[blockId]);
                    if (partTemplate.parentItemTemplate == null)
                    {
                        blocks[i] = 0;
                    }
                }
                else if (blockId > 0)
                {
                    var blockTemplate = ItemTemplateManager.getTerrainBlockTemplateByByteIdx(blockId);
                    if (blockTemplate == null || blockTemplate.parentBOT == null || blockTemplate.parentBOT.parentItemTemplate == null)
                    {
                        blocks[i] = 0;
                    }
                }
            }

            var buildingDataArray = new BlueprintData.BuildableObjectData[buildings.Count()];
            var customData = new List<BlueprintData.BuildableObjectData.CustomData>();
            var powerGridBuildings = new HashSet<BuildableObjectGO>(new BuildableObjectGOComparer());
            int buildingIndex = 0;
            foreach (var bogo in buildings)
            {
                BuildableEntity.BuildableEntityGeneralData generalData = default;
                var hasGeneralData = BuildingManager.buildingManager_getBuildableEntityGeneralData(bogo.Id, ref generalData);
                Debug.Assert(hasGeneralData == IOBool.iotrue, $"{bogo.Id} {bogo.template?.identifier}");

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
                var customDataWrapper = new CustomDataWrapper(customData);
                foreach (var gatherer in CustomDataGatherer.All)
                {
                    if (gatherer.ShouldGather(bogo, bogoType))
                    {
                        gatherer.Gather(bogo, customDataWrapper, powerGridBuildings);
                    }
                }

                buildingDataArray[buildingIndex].customData = customData.ToArray();

                buildingIndex++;
            }

            BlueprintData blueprintData = new BlueprintData();
            blueprintData.buildableObjects = buildingDataArray;
            blueprintData.blocks.sizeX = size.x;
            blueprintData.blocks.sizeY = size.y;
            blueprintData.blocks.sizeZ = size.z;
            blueprintData.blocks.ids = blocks;

            BuildShoppingList(blueprintData, shoppingList);

            return new Blueprint("new blueprint", blueprintData, shoppingList, new ItemElementTemplate[0]);
        }

        public static Blueprint Create(BlueprintRequest blueprintRequest)
        {
            var shoppingList = new Dictionary<ulong, ShoppingListData>();

            var blueprintData = new BlueprintData();
            blueprintData.buildableObjects = new BlueprintData.BuildableObjectData[blueprintRequest.buildings.Length];
            for (int i = 0; i < blueprintRequest.buildings.Length; i++)
            {
                var building = blueprintRequest.buildings[i];
                var template = ItemTemplateManager.getBuildableObjectTemplate(building.templateId);

                var customData = new BlueprintData.BuildableObjectData.CustomData[building.customData.Length];
                for (int j = 0; j < building.customData.Length; j++)
                {
                    var (identifier, value) = building.customData[j];
                    customData[j] = new BlueprintData.BuildableObjectData.CustomData(identifier, value);
                }

                blueprintData.buildableObjects[i] = new BlueprintData.BuildableObjectData
                {
                    originalEntityId = (ulong)(i + 1),
                    templateName = template.name,
                    templateId = building.templateId,
                    worldX = building.anchorPosition.x,
                    worldY = building.anchorPosition.y,
                    worldZ = building.anchorPosition.z,
                    orientationUnlockedX = building.orientationUnlocked.x,
                    orientationUnlockedY = building.orientationUnlocked.y,
                    orientationUnlockedZ = building.orientationUnlocked.z,
                    orientationUnlockedW = building.orientationUnlocked.w,
                    orientationY = (byte)building.orientationY,
                    itemMode = building.itemMode,
                    customData = customData
                };
            }
            blueprintData.blocks.sizeX = blueprintRequest.size.x;
            blueprintData.blocks.sizeY = blueprintRequest.size.y;
            blueprintData.blocks.sizeZ = blueprintRequest.size.z;
            blueprintData.blocks.ids = blueprintRequest.blocks;

            BuildShoppingList(blueprintData, shoppingList);

            return new Blueprint("new blueprint", blueprintData, shoppingList, new ItemElementTemplate[0]);
        }

        public int GetShoppingListEntry(ulong itemTemplateId, out string name)
        {
            if (!ShoppingList.TryGetValue(itemTemplateId, out ShoppingListData shoppingListEntry))
            {
                name = "";
                return 0;
            }

            name = shoppingListEntry.name;
            return shoppingListEntry.count;
        }

        private static void AddToShoppingList(Dictionary<ulong, ShoppingListData> shoppingList, ItemTemplate template, int count = 1)
        {
            if (template == null) throw new System.ArgumentNullException(nameof(template));

            if (!shoppingList.TryGetValue(template.id, out ShoppingListData shoppingListEntry))
            {
                shoppingListEntry = new ShoppingListData(template.id, template.name, 0);
            }
            shoppingListEntry.count += count;
            shoppingList[template.id] = shoppingListEntry;
        }

        public static bool TryLoadFileHeader(string path, out FileHeader header, out string name)
        {
            header = new FileHeader();
            name = "";

            var headerSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FileHeader));
            var allBytes = File.ReadAllBytes(path);
            if (allBytes.Length < headerSize) return false;

            var reader = new BinaryReader(new MemoryStream(allBytes, false));

            header.magic = reader.ReadUInt32();
            if (header.magic != FileMagicNumber) return false;

            header.version = reader.ReadUInt32();

            if (header.version >= 4u)
            {
                header.icon1 = reader.ReadString();
                header.icon2 = reader.ReadString();
                header.icon3 = reader.ReadString();
                header.icon4 = reader.ReadString();
            }
            else
            {
                string GetIcon(ulong id)
                {
                    var template = ItemTemplateManager.getItemTemplate(id);
                    if (template == null) return string.Empty;
                    return $"item:{template.identifier}";
                }

                header.icon1 = GetIcon(reader.ReadUInt64());
                header.icon2 = GetIcon(reader.ReadUInt64());
                header.icon3 = GetIcon(reader.ReadUInt64());
                header.icon4 = GetIcon(reader.ReadUInt64());
            }

            name = reader.ReadString();

            reader.Close();
            reader.Dispose();

            return true;
        }

        public static Blueprint LoadFromFile(string path)
        {
            if (!File.Exists(path)) return null;

            var shoppingList = new Dictionary<ulong, ShoppingListData>();

            var headerSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FileHeader));
            var allBytes = File.ReadAllBytes(path);
            if (allBytes.Length < headerSize) throw new FileLoadException(path);

            var reader = new BinaryReader(new MemoryStream(allBytes, false));

            var magic = reader.ReadUInt32();
            if (magic != FileMagicNumber) throw new FileLoadException(path);

            var version = reader.ReadUInt32();

            var iconItemTemplates = new List<ItemElementTemplate>();
            if (version >= 4u)
            {
                for (int i = 0; i < 4; ++i)
                {
                    var iconItemTemplateIdentifier = reader.ReadString();
                    if (!string.IsNullOrEmpty(iconItemTemplateIdentifier))
                    {
                        var template = ItemElementTemplate.Get(iconItemTemplateIdentifier);
                        if (template.isValid) iconItemTemplates.Add(template);
                    }
                }
            }
            else
            {
                for (int i = 0; i < 4; ++i)
                {
                    var iconItemTemplateId = reader.ReadUInt64();
                    if (iconItemTemplateId != 0)
                    {
                        var template = ItemTemplateManager.getItemTemplate(iconItemTemplateId);
                        if (template != null) iconItemTemplates.Add(new ItemElementTemplate(template));
                    }
                }
            }

            var name = reader.ReadString();

            ulong dataSize;
            var rawData = SaveManager.decompressByteArray(reader.ReadBytes(allBytes.Length - headerSize), out dataSize);
            var blueprintData = LoadDataFromString(Encoding.UTF8.GetString(rawData.Take((int)dataSize).ToArray()), shoppingList);

            reader.Close();
            reader.Dispose();

            return new Blueprint(name, blueprintData, shoppingList, iconItemTemplates.ToArray());
        }

        private static BlueprintData LoadDataFromString(string blueprint, Dictionary<ulong, ShoppingListData> shoppingList)
        {
            var blueprintData = JSON.Load(blueprint).Make<BlueprintData>();

            BuildShoppingList(blueprintData, shoppingList);

            return blueprintData;
        }

        public void Save(string path, string name, ItemElementTemplate[] iconItemTemplates)
        {
            _name = name;
            _iconItemTemplates = iconItemTemplates;

            var json = JSON.Dump(_data, EncodeOptions.PrettyPrint | EncodeOptions.NoTypeHints);

            var uncompressed = Encoding.UTF8.GetBytes(json);
            var compressed = SaveManager.compressByteArrayInPlace(uncompressed, out ulong dataSize);

            var writer = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write));

            writer.Write(FileMagicNumber);
            writer.Write(LatestBlueprintVersion);

            for (int i = 0; i < iconItemTemplates.Length; i++)
            {
                var template = iconItemTemplates[i];
                writer.Write(template.fullIdentifier);
            }
            for (int i = iconItemTemplates.Length; i < 4; i++)
            {
                writer.Write(string.Empty);
            }

            writer.Write(name);

            writer.Write(compressed.Take((int)dataSize).ToArray());

            writer.Close();
            writer.Dispose();
        }

        public void Place(Vector3Int anchorPosition, ConstructionTaskGroup constructionTaskGroup) => Place(GameRoot.getClientUsernameHash(), anchorPosition, constructionTaskGroup);
        public void Place(Character character, Vector3Int anchorPosition, ConstructionTaskGroup constructionTaskGroup) => Place(character.usernameHash, anchorPosition, constructionTaskGroup);
        public void Place(ulong usernameHash, Vector3Int anchorPosition, ConstructionTaskGroup constructionTaskGroup)
        {
            _powerlineConnectionPairs.Clear();
            var entityIdMap = new Dictionary<ulong, ulong>();

            AABB3D aabb = ObjectPoolManager.aabb3ds.getObject();

            if (_data.blocks.ids == null) throw new System.ArgumentNullException(nameof(_data.blocks.ids));

            var quadTreeArray = StreamingSystem.getBuildableObjectGOQuadtreeArray();
            int blockIndex = 0;
            for (int z = 0; z < _data.blocks.sizeZ; z++)
            {
                for (int y = 0; y < _data.blocks.sizeY; y++)
                {
                    for (int x = 0; x < _data.blocks.sizeX; x++)
                    {
                        var blockId = _data.blocks.ids[blockIndex++];
                        if (blockId > 0)
                        {
                            var worldPos = new Vector3Int(x, y, z) + anchorPosition;
                            ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(worldPos.x, worldPos.y, worldPos.z, out ulong worldChunkIndex, out uint worldBlockIndex);
                            var terrainData = ChunkManager.chunks_getTerrainData(worldChunkIndex, worldBlockIndex);

                            if (terrainData == 0 && quadTreeArray.queryPointXYZ(worldPos) == null)
                            {
                                if (blockId >= GameRoot.BUILDING_PART_ARRAY_IDX_START)
                                {
                                    var partTemplate = ItemTemplateManager.getBuildingPartTemplate(GameRoot.BuildingPartIdxLookupTable.table[blockId]);
                                    if (partTemplate != null && partTemplate.parentItemTemplate != null)
                                    {
                                        ActionManager.AddQueuedEvent(() =>
                                        {
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
                                            GameRoot.addLockstepEvent(new BuildEntityEvent(usernameHash, partTemplate.parentItemTemplate.id, mode, worldPos, 0, Quaternion.identity, DuplicationerPlugin.IsCheatModeEnabled ? 0 : 1, 0, false));
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
                                            GameRoot.addLockstepEvent(new BuildEntityEvent(usernameHash, blockTemplate.yieldItemOnDig_template.id, mode, worldPos, 0, Quaternion.identity, DuplicationerPlugin.IsCheatModeEnabled ? 0 : 1, 0, false));
                                        });
                                    }
                                    else if (blockTemplate != null && blockTemplate.parentBOT != null)
                                    {
                                        var itemTemplate = blockTemplate.parentBOT.parentItemTemplate;
                                        if (itemTemplate != null)
                                        {
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
                                                GameRoot.addLockstepEvent(new BuildEntityEvent(usernameHash, itemTemplate.id, mode, worldPos, 0, Quaternion.identity, DuplicationerPlugin.IsCheatModeEnabled ? 0 : 1, 0, false));
                                            });
                                        }
                                        else
                                        {
                                            DuplicationerPlugin.log.LogWarning((string)$"No item template for terrain index {blockId}");
                                        }
                                    }
                                    else
                                    {
                                        DuplicationerPlugin.log.LogWarning((string)$"No block template for terrain index {blockId}");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            int buildingIndex = 0;
            foreach (var buildableObjectData in _data.buildableObjects)
            {
                _postBuildActions.Clear();

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

                var customDataWrapper = new CustomDataWrapper(buildableObjectData.customData);
                foreach (var applier in CustomDataApplier.All)
                {
                    if (applier.ShouldApply(template, customDataWrapper))
                    {
                        applier.Apply(
                            template,
                            customDataWrapper,
                            _postBuildActions,
                            usernameHash,
                            ref usePasteConfigSettings,
                            ref pasteConfigSettings_01,
                            ref pasteConfigSettings_02,
                            ref additionalData_ulong_01,
                            ref additionalData_ulong_02,
                            ref _data,
                            entityIdMap);
                    }
                }

                aabb.reinitialize(worldPos.x, worldPos.y, worldPos.z, wx, wy, wz);
                var existingEntityId = CheckIfBuildingExists(aabb, worldPos, buildableObjectData);
                if (existingEntityId > 0)
                {
                    entityIdMap[buildableObjectData.originalEntityId] = existingEntityId;
                    var postBuildActionsArray = _postBuildActions.ToArray();
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
                    var postBuildActionsArray = _postBuildActions.ToArray();
                    var originalEntityId = buildableObjectData.originalEntityId;
                    constructionTaskGroup.AddTask(originalEntityId, (ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) => {
                        var buildEntityEvent = new BuildEntityEvent(
                            usernameHash,
                            template.parentItemTemplate.id,
                            buildableObjectData.itemMode,
                            worldPos,
                            buildableObjectData.orientationY,
                            buildableObjectData.orientationUnlocked,
                            DuplicationerPlugin.IsCheatModeEnabled
                                ? 0
                                : (template.modularBuildingModule_amountItemCost > 1 ? (int)template.modularBuildingModule_amountItemCost : 1),
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
                                entityIdMap[originalEntityId] = entityId;
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
            foreach (var buildableObjectData in _data.buildableObjects)
            {
                _dependenciesTemp.Clear();
                if (HasCustomData(buildingIndex, "modularParentId"))
                {
                    ulong parentId = GetCustomData<ulong>(buildingIndex, "modularParentId");

                    var dependency = constructionTaskGroup.GetTask(parentId);
                    if (dependency != null) _dependenciesTemp.Add(dependency);
                    else DuplicationerPlugin.log.LogWarning($"Entity id {parentId} not found in blueprint");
                }

                var powerlineEntityIds = new List<ulong>();
                GetCustomDataList(buildingIndex, "powerline", powerlineEntityIds);
                foreach (var powerlineEntityId in powerlineEntityIds)
                {
                    var dependency = constructionTaskGroup.GetTask(powerlineEntityId);
                    if (dependency != null) _dependenciesTemp.Add(dependency);
                    else DuplicationerPlugin.log.LogWarning($"Entity id {powerlineEntityId} not found in blueprint");
                }

                if (_dependenciesTemp.Count > 0)
                {
                    var task = constructionTaskGroup.GetTask(buildableObjectData.originalEntityId);
                    if (task != null) task.dependencies = _dependenciesTemp.ToArray();
                }

                buildingIndex++;
            }
        }

        private int CountModularParents(ulong parentId)
        {
            var parentIndex = _data.FindEntityIndex(parentId);
            if (parentIndex < 0) return 0;

            if (HasCustomData(parentIndex, "modularParentId"))
            {
                var grandparentId = GetCustomData<ulong>(parentIndex, "modularParentId");
                return CountModularParents(grandparentId) + 1;
            }

            return 1;
        }

        public bool HasCustomData(int index, string identifier) => HasCustomData(ref _data, index, identifier);
        public static bool HasCustomData(ref BlueprintData data, int index, string identifier)
        {
            foreach (var customDataEntry in data.buildableObjects[index].customData) if (customDataEntry.identifier == identifier) return true;
            return false;
        }

        public T GetCustomData<T>(int index, string identifier) => GetCustomData<T>(ref _data, index, identifier);
        public static T GetCustomData<T>(ref BlueprintData data, int index, string identifier)
        {
            foreach (var customDataEntry in data.buildableObjects[index].customData) if (customDataEntry.identifier == identifier) return (T)System.Convert.ChangeType(customDataEntry.value, typeof(T));
            return default;
        }

        public void GetCustomDataList<T>(int index, string identifier, List<T> list) => GetCustomDataList<T>(ref _data, index, identifier, list);
        public static void GetCustomDataList<T>(ref BlueprintData data, int index, string identifier, List<T> list)
        {
            foreach (var customDataEntry in data.buildableObjects[index].customData) if (customDataEntry.identifier == identifier) list.Add((T)System.Convert.ChangeType(customDataEntry.value, typeof(T)));
        }

        internal BlueprintData.BuildableObjectData GetBuildableObjectData(int index) => GetBuildableObjectData(ref _data, index);
        internal static BlueprintData.BuildableObjectData GetBuildableObjectData(ref BlueprintData data, int index)
        {
            if (index < 0 || index >= data.buildableObjects.Length) throw new System.IndexOutOfRangeException(nameof(index));

            return data.buildableObjects[index];
        }

        internal byte GetBlockId(int x, int y, int z) => GetBlockId(ref _data, x, y, z);
        internal static byte GetBlockId(ref BlueprintData data, int x, int y, int z)
        {
            if (x < 0 || x >= data.blocks.sizeX) throw new System.IndexOutOfRangeException(nameof(x));
            if (y < 0 || y >= data.blocks.sizeY) throw new System.IndexOutOfRangeException(nameof(y));
            if (z < 0 || z >= data.blocks.sizeZ) throw new System.IndexOutOfRangeException(nameof(z));

            return data.blocks.ids[x + (y + z * data.blocks.sizeY) * data.blocks.sizeX];
        }

        internal byte GetBlockId(int index) => GetBlockId(ref _data, index);
        internal static byte GetBlockId(ref BlueprintData data, int index)
        {
            if (index < 0 || index >= data.blocks.ids.Length) throw new System.IndexOutOfRangeException(nameof(index));

            return data.blocks.ids[index];
        }

        internal static ulong CheckIfBuildingExists(AABB3D aabb, Vector3Int worldPos, BlueprintData.BuildableObjectData buildableObjectData)
        {
            _bogoQueryResult.Clear();
            StreamingSystem.getBuildableObjectGOQuadtreeArray().queryAABB3D(aabb, _bogoQueryResult, false);
            if (_bogoQueryResult.Count > 0)
            {
                var template = ItemTemplateManager.getBuildableObjectTemplate(buildableObjectData.templateId);
                foreach (var wbogo in _bogoQueryResult)
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
                            DuplicationerPlugin.log.LogWarning("data not found");
                            match = false;
                        }

                        if (match) return wbogo.relatedEntityId;
                    }
                }
            }

            return 0ul;
        }

        public void Rotate()
        {
            var oldSize = Size;
            var newSize = new Vector3Int(oldSize.z, oldSize.y, oldSize.x);
            var oldCenter = ((Vector3)oldSize) / 2.0f;
            var newCenter = ((Vector3)newSize) / 2.0f;

            var rotatedData = new BlueprintData(_data.buildableObjects.Length, _data.blocks.Size);
            rotatedData.buildableObjects = new BlueprintData.BuildableObjectData[_data.buildableObjects.Length];
            rotatedData.blocks.ids = new byte[_data.blocks.ids.Length];
            for (int i = 0; i < _data.buildableObjects.Length; ++i)
            {
                var buildableObjectData = _data.buildableObjects[i];
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
                        BuildingManager.getWidthFromUnlockedOrientation(template, newOrientation, out _, out _, out int wz);
                        newZ -= wz;
                        buildableObjectData.orientationUnlocked = newOrientation;
                    }
                    else
                    {
                        var oldOrientation = buildableObjectData.orientationY;
                        var newOrientation = (byte)((oldOrientation + 1) & 0x3);
                        BuildingManager.getWidthFromOrientation(template, (BuildingManager.BuildOrientation)newOrientation, out _, out _, out int wz);
                        newZ -= wz;
                        buildableObjectData.orientationY = newOrientation;
                    }
                }

                buildableObjectData.worldX = newX;
                buildableObjectData.worldZ = newZ;
                rotatedData.buildableObjects[i] = buildableObjectData;
            }

            var newBlockIds = new byte[_data.blocks.ids.Length];
            int fromIndex = 0;
            for (int x = 0; x < newSize.x; x++)
            {
                for (int y = 0; y < newSize.y; y++)
                {
                    for (int z = newSize.z - 1; z >= 0; z--)
                    {
                        newBlockIds[x + (y + z * newSize.y) * newSize.x] = _data.blocks.ids[fromIndex++];
                    }
                }
            }

            rotatedData.blocks = new BlueprintData.BlockData(newSize, newBlockIds);

            _data = rotatedData;
        }

        public void Mirror()
        {
            var size = Size;

            var mirroredData = new BlueprintData(_data.buildableObjects.Length, _data.blocks.Size);
            mirroredData.buildableObjects = new BlueprintData.BuildableObjectData[_data.buildableObjects.Length];
            mirroredData.blocks.ids = new byte[_data.blocks.ids.Length];
            for (int i = 0; i < _data.buildableObjects.Length; ++i)
            {
                var buildableObjectData = _data.buildableObjects[i];
                var newX = size.x - buildableObjectData.worldX;

                var template = ItemTemplateManager.getBuildableObjectTemplate(buildableObjectData.templateId);
                if (template != null)
                {
                    var oldOrientation = buildableObjectData.orientationY;
                    var needsRotation = buildableObjectData.orientationY == 0 || buildableObjectData.orientationY == 2;
                    var newOrientation = needsRotation ? (byte)((oldOrientation + 2) & 0x3) : oldOrientation;
                    BuildingManager.getWidthFromOrientation(template, (BuildingManager.BuildOrientation)newOrientation, out var wx, out _, out _);
                    newX -= wx;
                    buildableObjectData.orientationY = newOrientation;

                    if (template.type == BuildableObjectTemplate.BuildableObjectType.ConveyorBalancer)
                    {
                        var inputPriority = GetCustomData<int>(i, "balancerInputPriority");
                        if (inputPriority < 2) inputPriority = 1 - inputPriority;
                        buildableObjectData.ReplaceCustomData("balancerInputPriority", inputPriority.ToString());

                        var outputPriority = GetCustomData<int>(i, "balancerOutputPriority");
                        if (outputPriority < 2) outputPriority = 1 - outputPriority;
                        buildableObjectData.ReplaceCustomData("balancerOutputPriority", outputPriority.ToString());
                    }
                }

                buildableObjectData.worldX = newX;
                mirroredData.buildableObjects[i] = buildableObjectData;
            }

            var newBlockIds = new byte[_data.blocks.ids.Length];
            int fromIndex = 0;
            for (int z = 0; z < size.z; z++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int x = size.x - 1; x >= 0; x--)
                    {
                        newBlockIds[x + (y + z * size.y) * size.x] = _data.blocks.ids[fromIndex++];
                    }
                }
            }

            mirroredData.blocks = new BlueprintData.BlockData(size, newBlockIds);

            _data = mirroredData;
        }

        internal void ClearRecipes()
        {
            _hasRecipes = false;
            for (int i = 0; i < _data.buildableObjects.Length; ++i)
            {
                var buildableObjectData = _data.buildableObjects[i];

                if (buildableObjectData.RemoveCustomData("craftingRecipeId"))
                {
                    _data.buildableObjects[i] = buildableObjectData;
                }
            }
        }

        internal void RemoveItem(ItemTemplate template)
        {
            if (template.id == PowerlineItemTemplate.id)
            {
                for (int i = 0; i < _data.buildableObjects.Length; ++i)
                {
                    var buildableObjectData = _data.buildableObjects[i];

                    if (buildableObjectData.RemoveCustomData("powerline"))
                    {
                        _data.buildableObjects[i] = buildableObjectData;
                    }
                }
            }
            else if (template.buildableObjectTemplate != null
                && template.buildableObjectTemplate.type == BuildableObjectTemplate.BuildableObjectType.BuildingPart)
            {
                var bot = template.buildableObjectTemplate;
                RemoveBuildPartBlocks(bot);

                foreach (var toggleableMode in template.toggleableModes)
                {
                    if (toggleableMode.buildableObjectTemplate != null
                        && toggleableMode.buildableObjectTemplate.type == BuildableObjectTemplate.BuildableObjectType.BuildingPart)
                    {
                        RemoveBuildPartBlocks(toggleableMode.buildableObjectTemplate);
                    }
                }
            }
            else if (template.buildableObjectTemplate != null
                && template.buildableObjectTemplate.type == BuildableObjectTemplate.BuildableObjectType.TerrainBlock)
            {
                if (template.buildableObjectTemplate.terrainBlock_tbt != null)
                {
                    RemoveTerrainBlocks(template.buildableObjectTemplate.terrainBlock_tbt);
                }

                foreach (var toggleableMode in template.toggleableModes)
                {
                    if (toggleableMode.buildableObjectTemplate != null
                        && toggleableMode.buildableObjectTemplate.type == BuildableObjectTemplate.BuildableObjectType.TerrainBlock
                        && toggleableMode.buildableObjectTemplate.terrainBlock_tbt != null)
                    {
                        RemoveTerrainBlocks(toggleableMode.buildableObjectTemplate.terrainBlock_tbt);
                    }
                }
            }
            else
            {
                var newBuildableObjects = new List<BlueprintData.BuildableObjectData>(_data.buildableObjects.Length);
                for (int i = 0; i < _data.buildableObjects.Length; ++i)
                {
                    var buildableObjectData = _data.buildableObjects[i];

                    var bot = ItemTemplateManager.getBuildableObjectTemplate(buildableObjectData.templateId);
                    if (bot == null || bot.parentItemTemplate != template)
                    {
                        newBuildableObjects.Add(buildableObjectData);
                    }
                }
                _data.buildableObjects = newBuildableObjects.ToArray();
            }

            _hasRecipes = false;
            foreach (var buildableObjectData in _data.buildableObjects)
            {
                if (buildableObjectData.HasCustomData("craftingRecipeId")) _hasRecipes = true;
            }

            BuildShoppingList(_data, ShoppingList);
        }

        private void RemoveBuildPartBlocks(BuildableObjectTemplate bot)
        {
            var index = (byte)GameRoot.BuildingPartIdxLookupTable.getKeyByValue(bot.id);
            for (int i = 0; i < _data.blocks.ids.Length; ++i)
            {
                if (_data.blocks.ids[i] == index) _data.blocks.ids[i] = 0;
            }
        }

        private void RemoveTerrainBlocks(TerrainBlockType template)
        {
            var index = (byte)GameRoot.TerrainIdxLookupTable.getKeyByValue(template.id);
            for (int i = 0; i < _data.blocks.ids.Length; ++i)
            {
                if (_data.blocks.ids[i] == index) _data.blocks.ids[i] = 0;
            }
        }

        public static void BuildShoppingList(BlueprintData blueprintData, Dictionary<ulong, ShoppingListData> shoppingList)
        {
            shoppingList.Clear();

            int blockIndex = 0;
            for (int z = 0; z < blueprintData.blocks.sizeZ; ++z)
            {
                for (int y = 0; y < blueprintData.blocks.sizeY; ++y)
                {
                    for (int x = 0; x < blueprintData.blocks.sizeX; ++x)
                    {
                        var blockId = blueprintData.blocks.ids[blockIndex++];
                        if (blockId >= GameRoot.BUILDING_PART_ARRAY_IDX_START)
                        {
                            var partTemplate = ItemTemplateManager.getBuildingPartTemplate(GameRoot.BuildingPartIdxLookupTable.table[blockId]);
                            if (partTemplate != null)
                            {
                                var itemTemplate = partTemplate.parentItemTemplate;
                                if (itemTemplate != null)
                                {
                                    AddToShoppingList(shoppingList, itemTemplate);
                                }
                            }
                        }
                        else if (blockId > 0)
                        {
                            var blockTemplate = ItemTemplateManager.getTerrainBlockTemplateByByteIdx(blockId);
                            if (blockTemplate != null && blockTemplate.parentBOT != null)
                            {
                                var itemTemplate = blockTemplate.parentBOT.parentItemTemplate;
                                if (itemTemplate != null)
                                {
                                    AddToShoppingList(shoppingList, itemTemplate);
                                }
                            }
                        }
                    }
                }
            }

            var powerlineEntityIds = new List<ulong>();
            int buildingIndex = 0;
            foreach (var buildingData in blueprintData.buildableObjects)
            {
                var buildingTemplate = ItemTemplateManager.getBuildableObjectTemplate(buildingData.templateId);
                if (buildingTemplate != null && buildingTemplate.parentItemTemplate != null)
                {
                    AddToShoppingList(shoppingList, buildingTemplate.parentItemTemplate);
                }

                powerlineEntityIds.Clear();
                GetCustomDataList(ref blueprintData, buildingIndex, "powerline", powerlineEntityIds);
                if (powerlineEntityIds.Count > 0) AddToShoppingList(shoppingList, PowerlineItemTemplate, powerlineEntityIds.Count);

                ++buildingIndex;
            }
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

                        for (int buildingIndex = 0; buildingIndex < _data.buildableObjects.Length; buildingIndex++)
                        {
                            var buildableObjectData = _data.buildableObjects[buildingIndex];
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

                            if (buildableObjectData.TryGetCustomData("modularBuildingData", out var modularBuildingDataJSON))
                            {
                                var pattern = PlaceholderPattern.Instance(template.placeholderPrefab, template);
                                var handles = new List<BatchRenderingHandle>(pattern.Entries.Length);
                                for (int i = 0; i < pattern.Entries.Length; i++)
                                {
                                    var entry = pattern.Entries[i];
                                    var transform = baseTransform * entry.relativeTransform;
                                    handles.Add(placeholderRenderGroup.AddSimplePlaceholderTransform(entry.mesh, transform, BlueprintPlaceholder.stateColours[1]));
                                }

                                var modularBuildingData = JSON.Load(modularBuildingDataJSON).Make<ModularBuildingData>();

                                var centerOffset = new Vector3(wx, 0.0f, wz) * -0.5f;

                                var extraBoundingBoxes = new List<BoundsInt>();

                                AABB3D aabb = ObjectPoolManager.aabb3ds.getObject();
                                aabb.reinitialize(0, 0, 0, wx, wy, wz);
                                BuildModularBuildPlaceholders(anchorPosition, handles, placeholderRenderGroup, buildingPlaceholders, repeatIndex, buildingIndex, template, baseTransform, position + centerOffset, orientation, aabb, modularBuildingData, extraBoundingBoxes);
                                ObjectPoolManager.aabb3ds.returnObject(aabb);

                                buildingPlaceholders.Add(new BlueprintPlaceholder(buildingIndex, repeatIndex, template, position, rotation, orientation, handles.ToArray(), extraBoundingBoxes.ToArray()));
                            }
                            else
                            {
                                var pattern = PlaceholderPattern.Instance(template.placeholderPrefab, template);
                                var handles = new BatchRenderingHandle[pattern.Entries.Length];
                                for (int i = 0; i < pattern.Entries.Length; i++)
                                {
                                    var entry = pattern.Entries[i];
                                    var transform = baseTransform * entry.relativeTransform;
                                    handles[i] = placeholderRenderGroup.AddSimplePlaceholderTransform(entry.mesh, transform, BlueprintPlaceholder.stateColours[1]);
                                }

                                buildingPlaceholders.Add(new BlueprintPlaceholder(buildingIndex, repeatIndex, template, position, rotation, orientation, handles));
                            }
                        }

                        int blockIndex = 0;
                        for (int z = 0; z < _data.blocks.sizeZ; ++z)
                        {
                            for (int y = 0; y < _data.blocks.sizeY; ++y)
                            {
                                for (int x = 0; x < _data.blocks.sizeX; ++x)
                                {
                                    var id = _data.blocks.ids[blockIndex];
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
                                            if (template == null) DuplicationerPlugin.log.LogWarning((string)$"Template not found for terrain index {id}-{GameRoot.BUILDING_PART_ARRAY_IDX_START} with id {GameRoot.BuildingPartIdxLookupTable.table[id]} at ({worldPos.x}, {worldPos.y}, {worldPos.z})");
                                        }

                                        if (template != null)
                                        {
                                            var baseTransform = Matrix4x4.Translate(worldPos);

                                            var pattern = PlaceholderPattern.Instance(template.placeholderPrefab, template);
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

        private static Vector3 getWorldPositionByLocalOffsetOrientationY(AABB3D aabb, int orientation, Vector3 localOffset)
        {
            Vector3 result = new Vector3(aabb.x0, aabb.y0 + localOffset.y, aabb.z0);
            switch (orientation)
            {
                case 0:
                    result.x += localOffset.x;
                    result.z += localOffset.z;
                    break;
                case 1:
                    result.z += aabb.wz;
                    result.x += localOffset.z;
                    result.z -= localOffset.x;
                    break;
                case 2:
                    result.x += aabb.wx;
                    result.z += aabb.wz;
                    result.x -= localOffset.x;
                    result.z -= localOffset.z;
                    break;
                case 3:
                    result.x += aabb.wx;
                    result.x -= localOffset.z;
                    result.z += localOffset.x;
                    break;
            }

            return result;
        }


        private static void BuildModularBuildPlaceholders(Vector3Int anchorPosition, List<BatchRenderingHandle> handles, BatchRenderingGroup placeholderRenderGroup, List<BlueprintPlaceholder> buildingPlaceholders, Vector3Int repeatIndex, int buildingIndex, BuildableObjectTemplate template, Matrix4x4 baseTransform, Vector3 position, BuildingManager.BuildOrientation orientation, AABB3D aabb, ModularBuildingData modularBuildingData, List<BoundsInt> extraBoundingBoxes)
        {
            for (int attachmentIndex = 0; attachmentIndex < modularBuildingData.attachments.Length; attachmentIndex++)
            {
                var attachment = modularBuildingData.attachments[attachmentIndex];
                if (attachment == null) continue;

                foreach (var node in template.modularBuildingConnectionNodes[attachmentIndex].nodeData)
                {
                    if (node.botId == attachment.templateId)
                    {
                        var attachmentTemplate = ItemTemplateManager.getBuildableObjectTemplate(attachment.templateId);

                        BuildingManager.getWidthFromOrientation(attachmentTemplate, node.positionData.orientation, out var wx, out var wy, out var wz);

                        var offsetPosition = getWorldPositionByLocalOffsetOrientationY(aabb, (int)orientation, node.positionData.offset + new Vector3(wx, 0.0f, wz) * 0.5f);
                        var attachmentOrientation = (BuildingManager.BuildOrientation)(((int)node.positionData.orientation + (int)orientation) % 4);

                        var attachmentPosition = position + offsetPosition;
                        var attachmentRotation = Quaternion.Euler(0.0f, (int)attachmentOrientation * 90.0f, 0.0f);
                        var attachmentTransform = Matrix4x4.TRS(attachmentPosition, attachmentRotation, Vector3.one);

                        var attachmentPattern = PlaceholderPattern.Instance(attachmentTemplate.placeholderPrefab, attachmentTemplate);
                        foreach (var entry in attachmentPattern.Entries)
                        {
                            var transform = attachmentTransform * entry.relativeTransform;
                            handles.Add(placeholderRenderGroup.AddSimplePlaceholderTransform(entry.mesh, transform, BlueprintPlaceholder.stateColours[1]));
                        }

                        BuildingManager.getWidthFromOrientation(attachmentTemplate, attachmentOrientation, out wx, out wy, out wz);
                        var centerOffset = new Vector3(wx, 0.0f, wz) * -0.5f;

                        AABB3D attachmentAabb = ObjectPoolManager.aabb3ds.getObject();
                        attachmentAabb.reinitialize(0, 0, 0, wx, wy, wz);
                        BuildModularBuildPlaceholders(anchorPosition, handles, placeholderRenderGroup, buildingPlaceholders, repeatIndex, buildingIndex, attachmentTemplate, baseTransform, attachmentPosition + centerOffset, attachmentOrientation, attachmentAabb, attachment, extraBoundingBoxes);
                        ObjectPoolManager.aabb3ds.returnObject(attachmentAabb);

                        extraBoundingBoxes.Add(new BoundsInt(Vector3Int.RoundToInt(attachmentPosition + centerOffset) - anchorPosition, new Vector3Int(wx, wy, wz)));

                        break;
                    }
                }
            }
        }

        [System.Serializable]
        public class ModularBuildingData
        {
            public uint id;
            public ulong templateId;
            public ModularBuildingData[] attachments;

            public ModularBuildingData()
            {
                id = 0;
                templateId = 0;
                attachments = new ModularBuildingData[0];
            }

            public ModularBuildingData(BuildableObjectTemplate template, uint id)
            {
                this.id = id;
                templateId = template.id;
                attachments = new ModularBuildingData[template.modularBuildingConnectionNodes.Length];
            }

            public MBMFData_BuildingNode BuildMBMFData()
            {
                var attachmentPoints = new MBMFData_BuildingNode[attachments.Length];
                for (int i = 0; i < attachments.Length; i++)
                {
                    var attachment = attachments[i];
                    if (attachment != null) attachmentPoints[i] = attachment.BuildMBMFData();
                }
                return new MBMFData_BuildingNode(templateId, attachmentPoints, id);
            }

            public ModularBuildingData FindModularBuildingNodeById(uint id)
            {
                if (this.id == id) return this;
                for (uint index = 0; index < attachments.Length; ++index)
                {
                    if (attachments[(int)index] != null)
                    {
                        var nodeById = attachments[(int)index].FindModularBuildingNodeById(id);
                        if (nodeById != null) return nodeById;
                    }
                }
                return null;
            }
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
            public string icon1;
            public string icon2;
            public string icon3;
            public string icon4;
        }
    }
}
