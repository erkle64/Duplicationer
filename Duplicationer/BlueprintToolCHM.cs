using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unfoundry;
using UnityEngine.Events;
using HarmonyLib;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using System.CodeDom;

namespace Duplicationer
{
    internal class BlueprintToolCHM : CustomHandheldMode
    {
        public bool IsBlueprintLoaded => CurrentBlueprint != null;
        public Blueprint CurrentBlueprint { get; private set; } = null;
        public string CurrentBlueprintStatusText { get; private set; } = "";
        public Vector3Int CurrentBlueprintSize => CurrentBlueprint == null ? Vector3Int.zero : CurrentBlueprint.Size;
        public Vector3Int CurrentBlueprintAnchor { get; private set; } = Vector3Int.zero;

        public bool IsBlueprintActive { get; private set; } = false;
        public bool IsPlaceholdersHidden { get; private set; } = false;
        private BatchRenderingGroup placeholderRenderGroup = new BatchRenderingGroup();
        private List<BlueprintPlaceholder> buildingPlaceholders = new List<BlueprintPlaceholder>();
        private List<BlueprintPlaceholder> terrainPlaceholders = new List<BlueprintPlaceholder>();
        private int buildingPlaceholderUpdateIndex = 0;
        private int terrainPlaceholderUpdateIndex = 0;

        private CustomRadialMenuStateControl menuStateControl = null;
        public BlueprintToolMode CurrentMode { get; private set; } = null;
        private BlueprintToolModePlace modePlace;
        private BlueprintToolModeSelectArea modeSelectArea;
        private BlueprintToolModeResize modeResize;
        private BlueprintToolModeResizeVertical modeResizeVertical;
        private BlueprintToolModeMove modeMove;
        private BlueprintToolModeMoveVertical modeMoveVertical;
        private BlueprintToolModeRepeat modeRepeat;
        private BlueprintToolMode[] blueprintToolModes;

        public bool HasMinecartDepots => CurrentBlueprint?.HasMinecartDepots ?? false;
        private static List<MinecartDepotGO> existingMinecartDepots = new List<MinecartDepotGO>();

        internal BoxMode boxMode = BoxMode.None;
        public Vector3Int BlueprintMin => CurrentBlueprintAnchor;
        public Vector3Int BlueprintMax => CurrentBlueprintAnchor + CurrentBlueprintSize - Vector3Int.one;

        internal Vector3Int repeatFrom = Vector3Int.zero;
        internal Vector3Int repeatTo = Vector3Int.zero;
        public Vector3Int RepeatCount => repeatTo - repeatFrom + Vector3Int.one;
        public Vector3Int RepeatBlueprintMin => BlueprintMin + new Vector3Int(CurrentBlueprintSize.x * repeatFrom.x, CurrentBlueprintSize.y * repeatFrom.y, CurrentBlueprintSize.z * repeatFrom.z);
        public Vector3Int RepeatBlueprintMax => BlueprintMax + new Vector3Int(CurrentBlueprintSize.x * repeatTo.x, CurrentBlueprintSize.y * repeatTo.y, CurrentBlueprintSize.z * repeatTo.z);

        internal Plane dragPlane = default;
        internal Vector3Int selectionFrom = Vector3Int.zero;
        internal Vector3Int selectionTo = Vector3Int.zero;

        public Vector3Int DragMin => Vector3Int.Min(selectionFrom, selectionTo);
        public Vector3Int DragMax => Vector3Int.Max(selectionFrom, selectionTo);
        public Vector3Int DragSize => DragMax - DragMin + Vector3Int.one;

        internal bool isDragArrowVisible = false;
        internal Ray dragFaceRay = default;
        internal Material dragArrowMaterial = null;
        internal float dragArrowScale = 1.0f;
        internal float dragArrowOffset = 0.5f;

        public bool IsBlueprintFrameOpen => duplicationerFrame != null && duplicationerFrame.activeSelf;
        private GameObject duplicationerFrame = null;
        private TextMeshProUGUI textMaterialReport = null;
        private TextMeshProUGUI textPositionX = null;
        private TextMeshProUGUI textPositionY = null;
        private TextMeshProUGUI textPositionZ = null;
        private float nextUpdateTimeCountTexts = 0.0f;
        private EscapeCloseProxy blueprintFrameEscapeCloseProxy = null;

        private GameObject rowCheats;
        private Button buttonCheatMode;

        public bool IsSaveFrameOpen => saveFrame != null && saveFrame.activeSelf;
        private GameObject saveFrame = null;
        private GameObject saveGridObject = null;
        private GameObject saveFramePreviewContainer = null;
        private Image[] saveFrameIconImages = new Image[4] { null, null, null, null };
        private Image[] saveFramePreviewIconImages = new Image[4] { null, null, null, null };
        private TMP_InputField saveFrameNameInputField = null;
        private TextMeshProUGUI saveFramePreviewLabel = null;
        private TextMeshProUGUI saveFrameMaterialReportText = null;
        private ItemTemplate[] saveFrameIconItemTemplates = new ItemTemplate[4] { null, null, null, null };
        private int saveFrameIconCount = 0;
        private EscapeCloseProxy saveFrameEscapeCloseProxy = null;

        public bool IsLibraryFrameOpen => libraryFrame != null && libraryFrame.activeSelf;
        private GameObject libraryFrame = null;
        private TextMeshProUGUI libraryFrameHeading = null;
        private GameObject libraryGridObject = null;
        private string lastLibraryRelativePath = "";
        private EscapeCloseProxy libraryFrameEscapeCloseProxy = null;

        public bool IsFolderFrameOpen => folderFrame != null && folderFrame.activeSelf;
        private GameObject folderFrame = null;
        private GameObject folderGridObject = null;
        private GameObject folderFramePreviewContainer = null;
        private Image folderFrameIconImage = null;
        private Image folderFramePreviewIconImage = null;
        private TMP_InputField folderFrameNameInputField = null;
        private TextMeshProUGUI folderFramePreviewLabel = null;
        private ItemTemplate folderFrameIconItemTemplate = null;
        private EscapeCloseProxy folderFrameEscapeCloseProxy = null;

        public bool IsAnyFrameOpen => IsBlueprintFrameOpen || IsSaveFrameOpen || IsLibraryFrameOpen || IsFolderFrameOpen;

        private int NudgeX => CurrentBlueprint != null && InputHelpers.IsAltHeld ? CurrentBlueprint.SizeX : 1;
        private int NudgeY => CurrentBlueprint != null && InputHelpers.IsAltHeld ? CurrentBlueprint.SizeY : 1;
        private int NudgeZ => CurrentBlueprint != null && InputHelpers.IsAltHeld ? CurrentBlueprint.SizeZ : 1;

        private static List<BuildableObjectGO> bogoQueryResult = new List<BuildableObjectGO>(0);

        private List<UIBuilder.GenericUpdateDelegate> guiUpdaters = new List<UIBuilder.GenericUpdateDelegate>();

        private static List<ConstructionTaskGroup> activeConstructionTaskGroups = new List<ConstructionTaskGroup>();

        private static List<bool> terrainTypeRemovalMask = null;

        private LazyPrefab prefabGridScrollView = new LazyPrefab("GridScrollView");
        private LazyPrefab prefabBlueprintNameInputField = new LazyPrefab("BlueprintNameInputField");
        private LazyPrefab prefabBlueprintButtonDefaultIcon = new LazyPrefab("BlueprintButtonDefaultIcon");
        private LazyPrefab prefabBlueprintButtonIcon = new LazyPrefab("BlueprintButtonIcon");
        private LazyPrefab prefabBlueprintButton1Icon = new LazyPrefab("BlueprintButton1Icon");
        private LazyPrefab prefabBlueprintButton2Icon = new LazyPrefab("BlueprintButton2Icon");
        private LazyPrefab prefabBlueprintButton3Icon = new LazyPrefab("BlueprintButton3Icon");
        private LazyPrefab prefabBlueprintButton4Icon = new LazyPrefab("BlueprintButton4Icon");
        private LazyPrefab prefabBlueprintButtonFolder = new LazyPrefab("BlueprintButtonFolder");
        private LazyPrefab prefabBlueprintButtonFolderNew = new LazyPrefab("BlueprintButtonFolderNew");
        private LazyPrefab prefabBlueprintButtonFolderBack = new LazyPrefab("BlueprintButtonFolderBack");

        private static Material placeholderMaterial = null;
        private static Material placeholderPrepassMaterial = null;
        private static Material placeholderSolidMaterial = null;

        private LazyIconSprite iconBlack = null;
        private LazyIconSprite iconEmpty = null;
        private LazyIconSprite iconCopy = null;
        private LazyIconSprite iconMoveVertical = null;
        private LazyIconSprite iconMove = null;
        private LazyIconSprite iconPanel = null;
        private LazyIconSprite iconPaste = null;
        private LazyIconSprite iconPlace = null;
        private LazyIconSprite iconRepeat = null;
        private LazyIconSprite iconResizeVertical = null;
        private LazyIconSprite iconResize = null;
        private LazyIconSprite iconSelectArea = null;
        private LazyIconSprite iconMirror = null;

        private LazyMaterial materialDragBox = new LazyMaterial(() =>
        {
            var material = new Material(ResourceDB.material_placeholder_green);
            material.renderQueue = 3001;
            material.SetFloat("_Opacity", 0.25f);
            material.SetColor("_Color", new Color(0.0f, 0.0f, 1.0f, 0.25f));
            return material;
        });

        private LazyMaterial materialDragBoxEdge = new LazyMaterial(() =>
        {
            var material = new Material(ResourceDB.material_glow_blue);
            material.SetColor("_Color", new Color(0.0f, 0.0f, 0.8f, 1.0f));
            return material;
        });

        public BlueprintToolCHM()
        {
            dragArrowScale = 1.0f;
            dragArrowOffset = 0.5f;

            blueprintToolModes = new BlueprintToolMode[]
            {
                modeSelectArea = new BlueprintToolModeSelectArea(),
                modeResize = new BlueprintToolModeResize(),
                modeResizeVertical = new BlueprintToolModeResizeVertical(),
                modePlace = new BlueprintToolModePlace(),
                modeMove = new BlueprintToolModeMove(),
                modeMoveVertical = new BlueprintToolModeMoveVertical(),
                modeRepeat = new BlueprintToolModeRepeat()
            };

            modePlace.Connect(modeSelectArea, modeMove);
            modeSelectArea.Connect(modeResize);

            SetPlaceholderOpacity(DuplicationerPlugin.configPreviewAlpha.Get());
        }

        public override void Enter()
        {
            if (CurrentMode == null) SelectMode(modePlace);

            CurrentMode?.Enter(this, null);
        }

        public override void Exit()
        {
            CurrentMode?.Exit(this);
        }

        public override void ShowMenu()
        {
            if (IsAnyFrameOpen) return;

            if (menuStateControl == null)
            {
                menuStateControl = new CustomRadialMenuStateControl(
                    new CustomRadialMenuOption(
                        "Confirm Copy", iconCopy.Sprite, "",
                        CopySelection,
                        () => CurrentMode != null && CurrentMode.AllowCopy(this)),

                    new CustomRadialMenuOption(
                        "Confirm Paste", iconPaste.Sprite, "",
                        () =>
                        {
                            isDragArrowVisible = false;
                            PlaceBlueprintMultiple(CurrentBlueprintAnchor, repeatFrom, repeatTo);
                            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_recipeCopyTool_paste);
                        },
                        () => CurrentMode != null && CurrentMode.AllowPaste(this)),

                    new CustomRadialMenuOption(
                        "Place", iconPlace.Sprite, "",
                        () => SelectMode(modePlace),
                        () => IsBlueprintLoaded && CurrentMode != modePlace),

                    new CustomRadialMenuOption(
                        "Select Area", iconSelectArea.Sprite, "",
                        () => SelectMode(modeSelectArea)),

                    new CustomRadialMenuOption(
                        "Move", iconMove.Sprite, "",
                        () => SelectMode(modeMove),
                        () => CurrentMode != null && CurrentMode.AllowPaste(this)),

                    new CustomRadialMenuOption(
                        "Move Vertical", iconMoveVertical.Sprite, "",
                        () => SelectMode(modeMoveVertical),
                        () => CurrentMode != null && CurrentMode.AllowPaste(this)),

                    new CustomRadialMenuOption(
                        "Resize", iconResize.Sprite, "",
                        () => SelectMode(modeResize),
                        () => CurrentMode != null && CurrentMode.AllowCopy(this)),

                    new CustomRadialMenuOption(
                        "Resize Vertical", iconResizeVertical.Sprite, "",
                        () => SelectMode(modeResizeVertical),
                        () => CurrentMode != null && CurrentMode.AllowCopy(this)),

                    new CustomRadialMenuOption(
                        "Repeat", iconRepeat.Sprite, "",
                        () => SelectMode(modeRepeat),
                        () => CurrentMode != null && CurrentMode.AllowPaste(this)),

                    new CustomRadialMenuOption(
                        "Mirror", iconMirror.Sprite, "",
                        () => MirrorBlueprint(),
                        () => CurrentMode != null && CurrentMode.AllowMirror(this) && CurrentBlueprint != null && CurrentBlueprint.IsMirrorable),

                    new CustomRadialMenuOption(
                        "Open Panel", iconPanel.Sprite, "",
                        ShowBlueprintFrame)
                );
            }

            if (menuStateControl != null)
            {
                CustomRadialMenuManager.ShowMenu(menuStateControl.GetMenuOptions());
            }
        }

        internal void SelectMode(BlueprintToolMode mode)
        {
            var fromMode = CurrentMode;
            CurrentMode?.Exit(this);
            CurrentMode = mode;
            CurrentMode?.Enter(this, fromMode);
        }

        private void CopySelection()
        {
            ClearBlueprintPlaceholders();
            CurrentBlueprint = Blueprint.Create(DragMin, DragSize);
            TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Place Blueprint");
            isDragArrowVisible = false;
            SelectMode(modePlace);
            boxMode = BoxMode.None;
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_recipeCopyTool_copy);
        }

        public void CopyCustomSelection(Vector3Int from, Vector3Int size, IEnumerable<BuildableObjectGO> buildings, byte[] blocks)
        {
            ClearBlueprintPlaceholders();
            CurrentBlueprint = Blueprint.Create(from, size, buildings, blocks);
            TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Place Blueprint");
            isDragArrowVisible = false;
            SelectMode(modePlace);
            boxMode = BoxMode.None;
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_recipeCopyTool_copy);
        }

        public override void UpdateBehavoir()
        {
            if (IsBlueprintActive)
            {
                var quadTree = StreamingSystem.getBuildableObjectGOQuadtreeArray();

                AABB3D aabb = ObjectPoolManager.aabb3ds.getObject();
                int count = Mathf.Min(DuplicationerPlugin.configMaxBuildingValidationsPerFrame.Get(), buildingPlaceholders.Count);
                if (buildingPlaceholderUpdateIndex >= buildingPlaceholders.Count) buildingPlaceholderUpdateIndex = 0;
                for (int i = 0; i < count; i++)
                {
                    var placeholder = buildingPlaceholders[buildingPlaceholderUpdateIndex];
                    var buildableObjectData = CurrentBlueprint.GetBuildableObjectData(placeholder.Index);

                    var repeatOffset = new Vector3Int(placeholder.RepeatIndex.x * CurrentBlueprintSize.x, placeholder.RepeatIndex.y * CurrentBlueprintSize.y, placeholder.RepeatIndex.z * CurrentBlueprintSize.z);
                    var worldPos = new Vector3Int(buildableObjectData.worldX + CurrentBlueprintAnchor.x + repeatOffset.x, buildableObjectData.worldY + CurrentBlueprintAnchor.y + repeatOffset.y, buildableObjectData.worldZ + CurrentBlueprintAnchor.z + repeatOffset.z);
                    int wx, wy, wz;
                    if (placeholder.Template.canBeRotatedAroundXAxis)
                        BuildingManager.getWidthFromUnlockedOrientation(placeholder.Template, buildableObjectData.orientationUnlocked, out wx, out wy, out wz);
                    else
                        BuildingManager.getWidthFromOrientation(placeholder.Template, (BuildingManager.BuildOrientation)buildableObjectData.orientationY, out wx, out wy, out wz);

                    byte errorCodeRaw = 0;
                    BuildingManager.buildingManager_validateConstruction_buildableEntityWrapper(new v3i(worldPos.x, worldPos.y, worldPos.z), buildableObjectData.orientationY, buildableObjectData.orientationUnlocked, buildableObjectData.templateId, ref errorCodeRaw, IOBool.iofalse);
                    var errorCode = (BuildingManager.CheckBuildableErrorCode)errorCodeRaw;

                    if (placeholder.ExtraBoundingBoxes != null)
                    {
                        foreach (var extraBox in placeholder.ExtraBoundingBoxes)
                        {
                            aabb.reinitialize(
                                extraBox.position.x + CurrentBlueprintAnchor.x,
                                extraBox.position.y + CurrentBlueprintAnchor.y,
                                extraBox.position.z + CurrentBlueprintAnchor.z,
                                extraBox.size.x,
                                extraBox.size.y,
                                extraBox.size.z);

                            if (aabb.y0 < 0 || aabb.y1 >= 256)
                            {
                                errorCode = BuildingManager.CheckBuildableErrorCode.BlockedByReservedArea;
                                break;
                            }

                            bogoQueryResult.Clear();
                            quadTree.queryAABB3D(aabb, bogoQueryResult, true);
                            foreach (var bogo in bogoQueryResult)
                            {
                                if (aabb.hasXYZIntersection(bogo._aabb))
                                {
                                    errorCode = BuildingManager.CheckBuildableErrorCode.BlockedByReservedArea;
                                    break;
                                }
                            }

                            var from = new Vector3Int(aabb.x0, aabb.y0, aabb.z0);
                            var to = new Vector3Int(aabb.x0 + aabb.wx - 1, aabb.y0 + aabb.wy - 1, aabb.z0 + aabb.wz - 1);
                            for (int bz = from.z; bz < to.z; ++bz)
                            {
                                for (int by = from.y; by < to.y; ++by)
                                {
                                    for (int bx = from.x; bx < to.x; ++bx)
                                    {
                                        ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(bx, by, bz, out ulong chunkIndex, out uint blockIndex);
                                        var blockId = ChunkManager.chunks_getTerrainData(chunkIndex, blockIndex);
                                        if (blockId > 0)
                                        {
                                            errorCode = BuildingManager.CheckBuildableErrorCode.BlockedByReservedArea;
                                            break;
                                        }
                                    }
                                    if (errorCode == BuildingManager.CheckBuildableErrorCode.BlockedByReservedArea) break;
                                }
                                if (errorCode == BuildingManager.CheckBuildableErrorCode.BlockedByReservedArea) break;
                            }

                            if (errorCode == BuildingManager.CheckBuildableErrorCode.BlockedByReservedArea) break;
                        }
                    }

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

                        case BuildingManager.CheckBuildableErrorCode.BlockedByBuildableObject_Building:
                            aabb.reinitialize(worldPos.x, worldPos.y, worldPos.z, wx, wy, wz);
                            if (Blueprint.CheckIfBuildingExists(aabb, worldPos, buildableObjectData) > 0) positionFilled = true;
                            break;
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

                count = Mathf.Min(DuplicationerPlugin.configMaxTerrainValidationsPerFrame.Get(), terrainPlaceholders.Count);
                if (terrainPlaceholderUpdateIndex >= terrainPlaceholders.Count) terrainPlaceholderUpdateIndex = 0;
                for (int i = 0; i < count; i++)
                {
                    var placeholder = terrainPlaceholders[terrainPlaceholderUpdateIndex];
                    var worldPos = new Vector3Int(Mathf.FloorToInt(placeholder.Position.x), Mathf.FloorToInt(placeholder.Position.y), Mathf.FloorToInt(placeholder.Position.z));

                    bool positionClear = true;
                    bool positionFilled = false;

                    var queryResult = quadTree.queryPointXYZ(worldPos);
                    if (queryResult != null)
                    {
                        positionClear = false;
                    }
                    else
                    {
                        ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(worldPos.x, worldPos.y, worldPos.z, out ulong chunkIndex, out uint blockIndex);

                        var blockId = CurrentBlueprint.GetBlockId(placeholder.Index);
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

                int countReady;
                int countMissing;

                if (DuplicationerPlugin.IsCheatModeEnabled)
                {
                    countReady = 0;
                    countMissing = 0;

                    foreach (var kv in BlueprintPlaceholder.GetStateCounts(BlueprintPlaceholder.State.Clear))
                    {
                        if (kv.Key != 0)
                        {
                            var clear = kv.Value;
                            if (clear > 0) countReady += clear;
                        }
                    }
                }
                else
                {
                    ulong inventoryId = GameRoot.getClientCharacter().inventoryId;
                    ulong inventoryPtr = inventoryId != 0 ? InventoryManager.inventoryManager_getInventoryPtr(inventoryId) : 0;
                    if (inventoryPtr != 0)
                    {
                        countReady = 0;
                        countMissing = 0;

                        foreach (var kv in BlueprintPlaceholder.GetStateCounts(BlueprintPlaceholder.State.Clear))
                        {
                            if (kv.Key != 0)
                            {
                                var clear = kv.Value;
                                if (clear > 0)
                                {
                                    var inventoryCount = InventoryManager.inventoryManager_countByItemTemplateByPtr(inventoryPtr, kv.Key, IOBool.iotrue);
                                    if (inventoryCount >= clear)
                                    {
                                        countReady += clear;
                                    }
                                    else
                                    {
                                        countReady += (int)inventoryCount;
                                        countMissing += clear - (int)inventoryCount;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        countMissing = BlueprintPlaceholder.GetStateCount(BlueprintPlaceholder.State.Clear);
                        countReady = 0;
                    }
                }
                if (countMissing > 0) text += $"Missing: {countMissing}\n";
                if (countReady > 0) text += $"Ready: {countReady}\n";

                int countBlocked = BlueprintPlaceholder.GetStateCount(BlueprintPlaceholder.State.Blocked);
                if (countBlocked > 0) text += $"<color=\"red\">Blocked:</color> {BlueprintPlaceholder.GetStateCount(BlueprintPlaceholder.State.Blocked)}\n";

                int countDone = BlueprintPlaceholder.GetStateCount(BlueprintPlaceholder.State.Done);
                if (countDone > 0) text += $"<color=#AACCFF>Done:</color> {BlueprintPlaceholder.GetStateCount(BlueprintPlaceholder.State.Done)}\n";

                if (countUntested > 0 || countBlocked > 0 || countMissing > 0 || countReady > 0 || countDone > 0)
                {
                    CurrentBlueprintStatusText = text + $"Total: {BlueprintPlaceholder.GetStateCountTotal()}";
                }
                else
                {
                    CurrentBlueprintStatusText = "";
                }
                UpdateMaterialReport();

                int remainingTasks = 0;
                foreach (var group in activeConstructionTaskGroups) remainingTasks += group.Remaining;
                if (remainingTasks > 0) CurrentBlueprintStatusText = $"ToDo: {remainingTasks}{(CurrentBlueprintStatusText.Length > 0 ? "\n" : "")}{CurrentBlueprintStatusText}";

                if (!IsPlaceholdersHidden)
                {
                    if (placeholderMaterial == null)
                    {
                        placeholderMaterial = DuplicationerPlugin.GetAsset<Material>("PlaceholderMaterial");
                        UnityEngine.Object.DontDestroyOnLoad(placeholderMaterial);
                    }

                    if (placeholderPrepassMaterial == null)
                    {
                        placeholderPrepassMaterial = DuplicationerPlugin.GetAsset<Material>("PlaceholderPrepassMaterial");
                        UnityEngine.Object.DontDestroyOnLoad(placeholderPrepassMaterial);
                    }

                    if (placeholderSolidMaterial == null)
                    {
                        placeholderSolidMaterial = DuplicationerPlugin.GetAsset<Material>("PlaceholderSolidMaterial");
                        UnityEngine.Object.DontDestroyOnLoad(placeholderSolidMaterial);
                    }

                    float alpha = DuplicationerPlugin.configPreviewAlpha.Get();
                    if (alpha > 0.0f)
                    {
                        if (alpha < 1.0f)
                        {
                            if (placeholderMaterial != null && placeholderPrepassMaterial != null)
                            {
                                placeholderRenderGroup.Render(placeholderPrepassMaterial, placeholderMaterial);
                            }
                        }
                        else
                        {
                            if (placeholderSolidMaterial != null)
                            {
                                placeholderRenderGroup.Render(null, placeholderSolidMaterial);
                            }
                        }
                    }
                }
            }

            if (IsBlueprintLoaded && IsBlueprintActive && Input.GetKeyDown(DuplicationerPlugin.configPasteBlueprintKey.Get()) && InputHelpers.IsKeyboardInputAllowed)
            {
                PlaceBlueprintMultiple(CurrentBlueprintAnchor, repeatFrom, repeatTo);
                AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_recipeCopyTool_paste);
            }

            if (Input.GetKeyDown(DuplicationerPlugin.configTogglePanelKey.Get()) && InputHelpers.IsKeyboardInputAllowed)
            {
                if (IsBlueprintFrameOpen) HideBlueprintFrame();
                else ShowBlueprintFrame();
            }

            if (Input.GetKeyDown(DuplicationerPlugin.configSaveBlueprintKey.Get()) && InputHelpers.IsKeyboardInputAllowed)
            {
                if (IsBlueprintLoaded) BeginSaveBlueprint();
            }

            if (Input.GetKeyDown(DuplicationerPlugin.configLoadBlueprintKey.Get()) && InputHelpers.IsKeyboardInputAllowed)
            {
                BeginLoadBlueprint();
            }

            CurrentMode?.Update(this);

            TabletHelper.SetTabletTextAnalyzer(GetTabletTitle());
            TabletHelper.SetTabletTextLastCopiedConfig(CurrentBlueprintStatusText.Replace('\n', ' '));

            switch (boxMode)
            {
                case BoxMode.None:
                    GameRoot.setHighVisibilityInfoText(ActionManager.StatusText);
                    break;

                case BoxMode.Blueprint:
                    DrawBoxWithEdges(RepeatBlueprintMin, RepeatBlueprintMax + Vector3.one, 0.015f, 0.04f, materialDragBox.Material, materialDragBoxEdge.Material);
                    var repeatCount = RepeatCount;
                    GameRoot.setHighVisibilityInfoText((repeatCount != Vector3Int.one) ? $"{ActionManager.StatusText} Repeat: {repeatCount.x}x{repeatCount.y}x{repeatCount.z}\n{CurrentBlueprintStatusText}" : $"{ActionManager.StatusText} {CurrentBlueprintStatusText}");
                    break;

                case BoxMode.Selection:
                    DrawBoxWithEdges(DragMin, DragMax + Vector3.one, 0.015f, 0.04f, materialDragBox.Material, materialDragBoxEdge.Material);
                    GameRoot.setHighVisibilityInfoText(DragSize == Vector3Int.one ? ActionManager.StatusText : $"{ActionManager.StatusText} {DragSize.x}x{DragSize.y}x{DragSize.z}");
                    break;
            }

            if (isDragArrowVisible)
            {
                DrawArrow(dragFaceRay.origin, dragFaceRay.direction, dragArrowMaterial, dragArrowScale, dragArrowOffset);
            }

            foreach (var updater in guiUpdaters) updater();
        }

        public override bool OnRotateY()
        {
            if (!IsBlueprintLoaded || !IsBlueprintActive) return true;
            if (CurrentMode == null || !CurrentMode.AllowRotate(this)) return false;

            RotateBlueprint();
            ClearBlueprintPlaceholders();
            ShowBlueprint(CurrentBlueprintAnchor);

            return false;
        }

        internal void ClearBlueprintPlaceholders()
        {
            if (IsBlueprintActive)
            {
                IsBlueprintActive = false;

                foreach (var placeholder in buildingPlaceholders)
                {
                    placeholder.SetState(BlueprintPlaceholder.State.Invalid);
                }
                buildingPlaceholders.Clear();

                foreach (var placeholder in terrainPlaceholders)
                {
                    placeholder.SetState(BlueprintPlaceholder.State.Invalid);
                }
                terrainPlaceholders.Clear();

                placeholderRenderGroup.Clear();
            }
        }

        internal void MoveBlueprint(Vector3Int newPosition)
        {
            if (IsBlueprintActive) OnBlueprintMoved(CurrentBlueprintAnchor, ref newPosition);

            ShowBlueprint(newPosition);
        }

        internal void PlaceBlueprintMultiple(Vector3Int targetPosition, Vector3Int repeatFrom, Vector3Int repeatTo)
        {
            for (int y = repeatFrom.y; y <= repeatTo.y; ++y)
            {
                for (int z = repeatFrom.z; z <= repeatTo.z; ++z)
                {
                    for (int x = repeatFrom.x; x <= repeatTo.x; ++x)
                    {
                        PlaceBlueprint(targetPosition + new Vector3Int(x * CurrentBlueprintSize.x, y * CurrentBlueprintSize.y, z * CurrentBlueprintSize.z));
                    }
                }
            }
        }

        internal void PlaceBlueprint(Vector3Int targetPosition)
        {
            if (CurrentBlueprint == null) throw new System.ArgumentNullException(nameof(CurrentBlueprint));

            ulong usernameHash = GameRoot.getClientCharacter().usernameHash;
            DuplicationerPlugin.log.Log(string.Format("Placing blueprint at {0}", targetPosition.ToString()));
            AABB3D aabb = ObjectPoolManager.aabb3ds.getObject();
            var modularBaseCoords = new Dictionary<ulong, Vector3Int>();
            var constructionTaskGroup = new ConstructionTaskGroup((ConstructionTaskGroup taskGroup) => { activeConstructionTaskGroups.Remove(taskGroup); });
            activeConstructionTaskGroups.Add(constructionTaskGroup);
            ObjectPoolManager.aabb3ds.returnObject(aabb); aabb = null;

            CurrentBlueprint.Place(targetPosition, constructionTaskGroup);

            constructionTaskGroup.SortTasks();
            ActionManager.AddQueuedEvent(() => constructionTaskGroup.InvokeNextTask());
        }

        internal void RotateBlueprint()
        {
            CurrentBlueprint?.Rotate();
        }

        internal void MirrorBlueprint()
        {
            if (!IsBlueprintLoaded || !IsBlueprintActive) ;
            if (CurrentMode == null || !CurrentMode.AllowRotate(this)) ;

            CurrentBlueprint?.Mirror();
            ClearBlueprintPlaceholders();
            ShowBlueprint(CurrentBlueprintAnchor);
        }

        internal void HideBlueprint()
        {
            IsPlaceholdersHidden = true;
        }

        internal void ShowBlueprint()
        {
            ShowBlueprint(CurrentBlueprintAnchor);
        }

        internal void ShowBlueprint(Vector3Int targetPosition)
        {
            if (!IsBlueprintLoaded) return;

            IsPlaceholdersHidden = false;

            if (!IsBlueprintActive)
            {
                IsBlueprintActive = true;
                placeholderRenderGroup.Clear();

                CurrentBlueprint?.Show(targetPosition, repeatFrom, repeatTo, CurrentBlueprintSize, placeholderRenderGroup, buildingPlaceholders, terrainPlaceholders);
            }
            else if (targetPosition != CurrentBlueprintAnchor)
            {
                var offset = targetPosition - CurrentBlueprintAnchor;
                placeholderRenderGroup.Move(offset);
                foreach (var placeholder in buildingPlaceholders) placeholder.Moved(offset);
                foreach (var placeholder in terrainPlaceholders) placeholder.Moved(offset);
            }

            CurrentBlueprintAnchor = targetPosition;
        }

        internal void RefreshBlueprint()
        {
            ClearBlueprintPlaceholders();
            ShowBlueprint();
        }

        private void OnBlueprintMoved(Vector3Int oldPosition, ref Vector3Int newPosition)
        {
            UpdateBlueprintPositionText();
        }

        private void UpdateMaterialReport()
        {
            if (textMaterialReport != null && Time.time >= nextUpdateTimeCountTexts)
            {
                nextUpdateTimeCountTexts = Time.time + 0.5f;

                int repeatCount = RepeatCount.x * RepeatCount.y * RepeatCount.z;
                ulong inventoryId = GameRoot.getClientCharacter().inventoryId;
                ulong inventoryPtr = inventoryId != 0 ? InventoryManager.inventoryManager_getInventoryPtr(inventoryId) : 0;

                int totalItemCount = 0;
                int totalDoneCount = 0;
                var materialReportBuilder = new System.Text.StringBuilder();
                foreach (var kv in CurrentBlueprint.ShoppingList)
                {
                    var itemCount = kv.Value.count * repeatCount;
                    if (itemCount > 0)
                    {
                        totalItemCount += itemCount;

                        var name = kv.Value.name;
                        var templateId = kv.Value.itemTemplateId;
                        if (templateId != 0)
                        {
                            var doneCount = BlueprintPlaceholder.GetStateCount(templateId, BlueprintPlaceholder.State.Done);
                            totalDoneCount += doneCount;

                            if (inventoryPtr != 0)
                            {
                                var inventoryCount = InventoryManager.inventoryManager_countByItemTemplateByPtr(inventoryPtr, templateId, IOBool.iotrue);

                                if (doneCount > 0)
                                {
                                    materialReportBuilder.AppendLine($"<color=#CCCCCC>{name}:</color> {itemCount - doneCount} <color=#FFFFAA>({inventoryCount})</color> (<color=#AACCFF>{doneCount}</color>/{itemCount})");
                                }
                                else
                                {
                                    materialReportBuilder.AppendLine($"<color=#CCCCCC>{name}:</color> {itemCount} <color=#FFFFAA>({inventoryCount})</color>");
                                }
                            }
                            else
                            {
                                materialReportBuilder.AppendLine($"<color=#CCCCCC>{name}:</color> {itemCount} <color=#FFFFAA>(###)</color>");
                            }
                        }
                        else
                        {
                            materialReportBuilder.AppendLine($"<color=#CCCCCC>{name}:</color> {itemCount}");
                        }
                    }
                }

                if (totalItemCount > 0)
                {
                    if (totalDoneCount > 0)
                    {
                        materialReportBuilder.AppendLine($"<color=#CCCCCC>Total:</color> {totalItemCount - totalDoneCount} (<color=#AACCFF>{totalDoneCount}</color>/{totalItemCount})");
                    }
                    else
                    {
                        materialReportBuilder.AppendLine($"<color=#CCCCCC>Total:</color> {totalItemCount}");
                    }
                }

                textMaterialReport.text = materialReportBuilder.ToString();
            }
        }

        private string GetTabletTitle()
        {
            return CurrentMode?.TabletTitle(this) ?? "";
        }

        internal void HideBlueprintFrame()
        {
            if (blueprintFrameEscapeCloseProxy != null)
            {
                blueprintFrameEscapeCloseProxy.Dispose();
                blueprintFrameEscapeCloseProxy = null;
            }

            if (duplicationerFrame == null || !duplicationerFrame.activeSelf) return;

            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIClose);

            duplicationerFrame.SetActive(false);
            GlobalStateManager.removeCursorRequirement();
        }

        internal void ShowBlueprintFrame()
        {
            if (duplicationerFrame != null && duplicationerFrame.activeSelf) return;

            if (duplicationerFrame == null)
            {
                var graphics = Traverse.Create(typeof(UIRaycastTooltipManager))?.Field("singleton")?.GetValue<UIRaycastTooltipManager>()?.tooltipRectTransform?.GetComponentsInChildren<Graphic>();
                if (graphics != null)
                {
                    foreach (var graphic in graphics)
                    {
                        graphic.raycastTarget = false;
                    }
                }

                ulong usernameHash = GameRoot.getClientCharacter().usernameHash;
                UIBuilder.BeginWith(GameRoot.getDefaultCanvas())
                    .Element_PanelAutoSize("DuplicationerFrame", "corner_cut_outline", new Color(0.133f, 0.133f, 0.133f, 1.0f), new Vector4(13, 10, 8, 13))
                        .Keep(out duplicationerFrame)
                        .SetVerticalLayout(new RectOffset(0, 0, 0, 0), 0.0f, TextAnchor.UpperLeft, false, true, true, true, false, false, false)
                        .SetRectTransform(-420.0f, 120.0f, -60.0f, 220.0f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f)
                        .Element_Header("HeaderBar", "corner_cut_outline", new Color(0.0f, 0.6f, 1.0f, 1.0f), new Vector4(13, 3, 8, 13))
                            .SetRectTransform(0.0f, -60.0f, 599.0f, 0.0f, 0.5f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f)
                            .Layout()
                                .MinWidth(340)
                                .MinHeight(60)
                            .Done
                            .Element("Heading")
                                .SetRectTransform(0.0f, 0.0f, -60.0f, 0.0f, 0.0f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                .Component_Text("Duplicationer", "OpenSansSemibold SDF", 34.0f, Color.white)
                            .Done
                            .Element_Button("Button Close", "corner_cut_fully_inset", Color.white, new Vector4(13.0f, 1.0f, 4.0f, 13.0f))
                                .SetOnClick(HideBlueprintFrame)
                                .SetRectTransform(-60.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.5f, 1.0f, 0.0f, 1.0f, 1.0f)
                                .SetTransitionColors(new Color(1.0f, 1.0f, 1.0f, 1.0f), new Color(1.0f, 0.25f, 0.0f, 1.0f), new Color(1.0f, 0.0f, 0.0f, 1.0f), new Color(1.0f, 0.25f, 0.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                .Element("Image")
                                    .SetRectTransform(5.0f, 5.0f, -5.0f, -5.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                    .Component_Image("cross", Color.white, Image.Type.Sliced, Vector4.zero)
                                .Done
                            .Done
                        .Done
                        .Element("Content")
                            .SetRectTransform(0.0f, -855.0f, 599.0f, -60.0f, 0.5f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f)
                            .SetVerticalLayout(new RectOffset(10, 10, 10, 10), 0.0f, TextAnchor.UpperLeft, false, true, true, true, false, false, false)
                            .Element("Padding")
                                .SetRectTransform(10.0f, -785.0f, 589.0f, -10.0f, 0.5f, 0.5f, 0.0f, 1.0f, 0.0f, 1.0f)
                                .SetVerticalLayout(new RectOffset(0, 0, 0, 0), 10.0f, TextAnchor.UpperLeft, false, true, true, true, false, false, false)
                                .Element_ScrollBox("Material Report ScrollBox", builder =>
                                    {
                                        builder = builder
                                            .SetVerticalLayout(new RectOffset(5, 5, 5, 5), 10.0f, TextAnchor.UpperLeft, false, true, true, false, false, false, false)
                                            .Element("Material Report")
                                                .SetRectTransform(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f)
                                                .AutoSize(ContentSizeFitter.FitMode.PreferredSize, ContentSizeFitter.FitMode.PreferredSize)
                                                .Component_Text("", "OpenSansSemibold SDF", 14.0f, Color.white, TextAlignmentOptions.TopLeft)
                                                .Keep(out textMaterialReport)
                                            .Done;
                                    })
                                    .Layout()
                                        .PreferredHeight(200)
                                    .Done
                                .Done
                                .Element("Demolish Row")
                                    .Updater(guiUpdaters, () => boxMode != BoxMode.None && CurrentMode != modeSelectArea)
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Element_Label("Demolish Label", "Demolish ", 100, 1)
                                    .Done
                                    .Element_ImageButton("Button Demolish Buildings", "assembler_iii")
                                        .Component_Tooltip("Demolish\nBuildings")
                                        .SetOnClick(() => DemolishArea(true, false, false, false))
                                    .Done
                                    .Element_ImageButton("Button Demolish Blocks", "floor")
                                        .Component_Tooltip("Demolish\nBlocks")
                                        .SetOnClick(() => DemolishArea(false, true, false, false))
                                    .Done
                                    .Element_ImageButton("Button Demolish Terrain", "dirt")
                                        .Component_Tooltip("Demolish\nTerrain")
                                        .SetOnClick(() => DemolishArea(false, false, true, false))
                                    .Done
                                    .Element_ImageButton("Button Demolish Decor", "biomass")
                                        .Component_Tooltip("Demolish\nDecor")
                                        .SetOnClick(() => DemolishArea(false, false, false, true))
                                    .Done
                                    .Element_ImageButton("Button Demolish All", "icons8-error-100")
                                        .Component_Tooltip("Demolish\nAll")
                                        .SetOnClick(() => DemolishArea(true, true, true, true))
                                    .Done
                                .Done
                                .Element("Destroy Row")
                                    .Updater(guiUpdaters, () => boxMode != BoxMode.None && CurrentMode != modeSelectArea)
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Element_Label("Destroy Label", "Destroy ", 100, 1)
                                    .Done
                                    //.Element_ImageButton("Button Destroy Buildings", "assembler_iii")
                                    //    .Component_Tooltip("Destroy\nBuildings")
                                    //    .SetOnClick(() => ConfirmationFrame.Show("Permanently destroy buildings in selection?", () => DestroyArea(true, false, false, false)))
                                    //.Done
                                    //.Element_ImageButton("Button Destroy Blocks", "floor")
                                    //    .Component_Tooltip("Destroy\nBlocks")
                                    //    .SetOnClick(() => ConfirmationFrame.Show("Permanently destroy foundation blocks in selection?", () => DestroyArea(false, true, false, false)))
                                    //.Done
                                    .Element_ImageButton("Button Destroy Terrain", "dirt")
                                        .Component_Tooltip("Destroy\nTerrain")
                                        .SetOnClick(() => ConfirmationFrame.Show("Permanently destroy terrain in selection?", () => DestroyArea(false, false, true, false)))
                                    .Done
                                    .Element_ImageButton("Button Destroy Decor", "biomass")
                                        .Component_Tooltip("Destroy\nDecor")
                                        .SetOnClick(() => ConfirmationFrame.Show("Permanently destroy plants in selection?", () => DestroyArea(false, false, false, true)))
                                    .Done
                                    .Element_ImageButton("Button Destroy All", "icons8-error-100")
                                        .Component_Tooltip("Destroy\nAll")
                                        .SetOnClick(() => ConfirmationFrame.Show("Permanently destroy everything in selection?", () => DestroyArea(true, true, true, true)))
                                    .Done
                                .Done
                                .Element("Position Row")
                                    .Updater(guiUpdaters, () => boxMode == BoxMode.Blueprint)
                                    .AutoSize(ContentSizeFitter.FitMode.Unconstrained, ContentSizeFitter.FitMode.PreferredSize)
                                    .SetVerticalLayout(new RectOffset(0, 0, 0, 0), 10.0f, TextAnchor.UpperLeft, false, true, true, true, false, false, false)
                                    .Element("Row Position X")
                                        .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                        .Element("Position Display X")
                                            .Layout()
                                                .MinWidth(100)
                                                .FlexibleWidth(1)
                                            .Done
                                            .Component_Text("X: 0", "OpenSansSemibold SDF", 18.0f, Color.white, TextAlignmentOptions.MidlineLeft)
                                            .Keep(out textPositionX)
                                        .Done
                                        .Element_ImageButton("Button Decrease", "icons8-chevron-left-filled-100_white", 28, 28, 90.0f)
                                            .SetOnClick(() => { MoveBlueprint(CurrentBlueprintAnchor + new Vector3Int(-1, 0, 0) * NudgeX); })
                                        .Done
                                        .Element_ImageButton("Button Increase", "icons8-chevron-left-filled-100_white", 28, 28, 270.0f)
                                            .SetOnClick(() => { MoveBlueprint(CurrentBlueprintAnchor + new Vector3Int(1, 0, 0) * NudgeX); })
                                        .Done
                                    .Done
                                    .Element("Row Position Y")
                                        .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                        .Element("Position Display Y")
                                            .Layout()
                                                .MinWidth(100)
                                                .FlexibleWidth(1)
                                            .Done
                                            .Component_Text("Y: 0", "OpenSansSemibold SDF", 18.0f, Color.white, TextAlignmentOptions.MidlineLeft)
                                            .Keep(out textPositionY)
                                        .Done
                                        .Element_ImageButton("Button Decrease", "icons8-chevron-left-filled-100_white", 28, 28, 90.0f)
                                            .SetOnClick(() => { MoveBlueprint(CurrentBlueprintAnchor + new Vector3Int(0, -1, 0) * NudgeY); })
                                        .Done
                                        .Element_ImageButton("Button Increase", "icons8-chevron-left-filled-100_white", 28, 28, 270.0f)
                                            .SetOnClick(() => { MoveBlueprint(CurrentBlueprintAnchor + new Vector3Int(0, 1, 0) * NudgeY); })
                                        .Done
                                    .Done
                                    .Element("Row Position Z")
                                        .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                        .Element("Position Display Z")
                                            .Layout()
                                                .MinWidth(100)
                                                .FlexibleWidth(1)
                                            .Done
                                            .Component_Text("Z: 0", "OpenSansSemibold SDF", 18.0f, Color.white, TextAlignmentOptions.MidlineLeft)
                                            .Keep(out textPositionZ)
                                        .Done
                                        .Element_ImageButton("Button Decrease", "icons8-chevron-left-filled-100_white", 28, 28, 90.0f)
                                            .SetOnClick(() => { MoveBlueprint(CurrentBlueprintAnchor + new Vector3Int(0, 0, -1) * NudgeZ); })
                                        .Done
                                        .Element_ImageButton("Button Increase", "icons8-chevron-left-filled-100_white", 28, 28, 270.0f)
                                            .SetOnClick(() => { MoveBlueprint(CurrentBlueprintAnchor + new Vector3Int(0, 0, 1) * NudgeZ); })
                                        .Done
                                    .Done
                                .Done
                                .Element("Preview Opacity Row")
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Element_Label("Preview Opacity Label", "Preview Opacity", 150, 1)
                                    .Done
                                    .Element_Slider("Preview Opacity Slider", DuplicationerPlugin.configPreviewAlpha.Get(), 0.0f, 1.0f, (value) => { DuplicationerPlugin.configPreviewAlpha.Set(value); SetPlaceholderOpacity(value); })
                                        .Layout()
                                            .MinWidth(200)
                                            .MinHeight(40)
                                            .FlexibleWidth(1)
                                        .Done
                                    .Done
                                .Done
                                .Element("Row Cheats")
                                    .Keep(out rowCheats)
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Element_TextButton("Button Cheat Mode", CheatModeButtonText(), Color.white)
                                        .Keep(out buttonCheatMode)
                                        .Component_Tooltip("Toggle cheat mode")
                                        .SetOnClick(ToggleCheatMode)
                                    .Done
                                .Done
                                .Element("Row Files")
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Element_ImageTextButton("Button Save", "Save", "download", Color.white, 28, 28)
                                        .Component_Tooltip("Save current blueprint")
                                        .SetOnClick(BeginSaveBlueprint)
                                        .Updater<Button>(guiUpdaters, () => IsBlueprintLoaded)
                                    .Done
                                    .Element_ImageTextButton("Button Load", "Load", "upload", Color.white, 28, 28)
                                        .Component_Tooltip("Load blueprint from library")
                                        .SetOnClick(BeginLoadBlueprint)
                                    .Done
                                .Done
                                .Element("Row Confirm Buttons")
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Element_TextButton("Button Paste", "Confirm/Paste")
                                        .Updater<Button>(guiUpdaters, () => CurrentMode != null && CurrentMode.AllowPaste(this))
                                        .SetOnClick(() => { PlaceBlueprintMultiple(CurrentBlueprintAnchor, repeatFrom, repeatTo); })
                                    .Done
                                    .Element_TextButton("Button Copy", "Confirm/Copy")
                                        .Updater<Button>(guiUpdaters, () => CurrentMode != null && CurrentMode.AllowCopy(this))
                                        .SetOnClick(CopySelection)
                                    .Done
                                .Done
                            .Done
                        .Done
                    .Done
                .End();

                if (!DuplicationerPlugin.configCheatModeAllowed.Get() && rowCheats != null)
                {
                    rowCheats.SetActive(false);
                }
            }

            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIOpen);

            duplicationerFrame.SetActive(true);
            GlobalStateManager.addCursorRequirement();
            blueprintFrameEscapeCloseProxy = EscapeCloseProxy.Register(HideBlueprintFrame);

            UpdateBlueprintPositionText();
        }

        private string CheatModeButtonText() => DuplicationerPlugin.IsCheatModeEnabled ? "Disable Cheat Mode" : "Enable Cheat Mode";

        private void ToggleCheatMode()
        {
            if (buttonCheatMode == null) return;
            if (!DuplicationerPlugin.configCheatModeAllowed.Get()) return;
            DuplicationerPlugin.configCheatModeEnabled.Set(!DuplicationerPlugin.configCheatModeEnabled.Get());
            var textComponent = buttonCheatMode.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent == null) return;
            textComponent.text = CheatModeButtonText();
        }

        private void BeginSaveBlueprint()
        {
            HideBlueprintFrame();
            ShowSaveFrame();
        }

        private void GetInfoFromExistingBlueprint()
        {
            ShowLibraryFrame(true);
        }

        private void FinishSaveBlueprint()
        {
            if (CurrentBlueprint == null) throw new System.ArgumentNullException(nameof(CurrentBlueprint));

            string name = saveFrameNameInputField.text;
            if (string.IsNullOrWhiteSpace(name)) return;

            string filenameBase = Path.Combine(Path.GetDirectoryName(name), PathHelpers.MakeValidFileName(Path.GetFileName(name)));
            string path = Path.Combine(DuplicationerPlugin.BlueprintFolder, $"{filenameBase}.{DuplicationerPlugin.BlueprintExtension}");
            if (File.Exists(path))
            {
                ConfirmationFrame.Show($"Overwrite '{name}'?", "Overwrite", () =>
                {
                    DuplicationerPlugin.log.Log($"Saving blueprint '{name}' to '{path}'");
                    CurrentBlueprint.Save(path, Path.GetFileName(name), saveFrameIconItemTemplates.Take(saveFrameIconCount).ToArray());

                    HideSaveFrame();
                });
            }
            else
            {
                DuplicationerPlugin.log.Log($"Saving blueprint '{name}' to '{path}'");
                CurrentBlueprint.Save(path, Path.GetFileName(name), saveFrameIconItemTemplates.Take(saveFrameIconCount).ToArray());

                HideSaveFrame();
            }
        }

        internal void HideSaveFrame()
        {
            if (saveFrameEscapeCloseProxy != null)
            {
                saveFrameEscapeCloseProxy.Dispose();
                saveFrameEscapeCloseProxy = null;
            }

            if (saveFrame == null || !saveFrame.activeSelf) return;

            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIClose);

            saveFrame.SetActive(false);
            GlobalStateManager.removeCursorRequirement();
        }

        internal void ShowSaveFrame()
        {
            if (saveFrame != null && saveFrame.activeSelf) return;

            if (libraryFrame != null && libraryFrame.activeSelf) HideLibraryFrame();

            if (saveFrame == null)
            {
                ulong usernameHash = GameRoot.getClientCharacter().usernameHash;
                UIBuilder.BeginWith(GameRoot.getDefaultCanvas())
                    .Element_Panel("Save Frame", "corner_cut_outline", new Color(0.133f, 0.133f, 0.133f, 1.0f), new Vector4(13, 10, 8, 13))
                        .Keep(out saveFrame)
                        .SetRectTransform(100, 100, -100, -100, 0.5f, 0.5f, 0, 0, 1, 1)
                        .Element_Header("HeaderBar", "corner_cut_outline", new Color(0.0f, 0.6f, 1.0f, 1.0f), new Vector4(13, 3, 8, 13))
                            .SetRectTransform(0.0f, -60.0f, 0.0f, 0.0f, 0.5f, 1.0f, 0.0f, 1.0f, 1.0f, 1.0f)
                            .Element("Heading")
                                .SetRectTransform(0.0f, 0.0f, -60.0f, 0.0f, 0.0f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                .Component_Text("Save Blueprint", "OpenSansSemibold SDF", 34.0f, Color.white)
                            .Done
                            .Element_Button("Button Close", "corner_cut_fully_inset", Color.white, new Vector4(13.0f, 1.0f, 4.0f, 13.0f))
                                .SetOnClick(HideSaveFrame)
                                .SetRectTransform(-60.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.5f, 1.0f, 0.0f, 1.0f, 1.0f)
                                .SetTransitionColors(new Color(1.0f, 1.0f, 1.0f, 1.0f), new Color(1.0f, 0.25f, 0.0f, 1.0f), new Color(1.0f, 0.0f, 0.0f, 1.0f), new Color(1.0f, 0.25f, 0.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                .Element("Image")
                                    .SetRectTransform(5.0f, 5.0f, -5.0f, -5.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                    .Component_Image("cross", Color.white, Image.Type.Sliced, Vector4.zero)
                                .Done
                            .Done
                        .Done
                        .Element("Content")
                            .SetRectTransform(0.0f, 0.0f, 0.0f, -60.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                            .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 0, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                            .Element("ContentLeft")
                                .Layout()
                                    .FlexibleWidth(1)
                                .Done
                                .Element("Padding")
                                    .SetRectTransform(10.0f, 10.0f, -10.0f, -10.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                    .Do(builder =>
                                    {
                                        var gameObject = UnityEngine.Object.Instantiate(prefabGridScrollView.Prefab, builder.GameObject.transform);
                                        var grid = gameObject.GetComponentInChildren<GridLayoutGroup>();
                                        if (grid == null) throw new System.Exception("Grid not found.");
                                        saveGridObject = grid.gameObject;
                                        grid.cellSize = new Vector2(80.0f, 80.0f);
                                        grid.padding = new RectOffset(4, 4, 4, 4);
                                        grid.spacing = new Vector2(0.0f, 0.0f);
                                    })
                                .Done
                            .Done
                            .Element("ContentRight")
                                .Layout()
                                    .MinWidth(132 + 4 + 132 + 4 + 132 + 10)
                                    .FlexibleWidth(0)
                                .Done
                                .SetVerticalLayout(new RectOffset(0, 10, 10, 10), 10, TextAnchor.UpperLeft, false, true, true, true, false, false, false)
                                .Element("Icons Row")
                                    .Layout()
                                        .MinHeight(132 + 6 + 132)
                                        .FlexibleHeight(0)
                                    .Done
                                    .Element_Button("Icon 1 Button", iconBlack.Sprite, Color.white, Vector4.zero, Image.Type.Simple)
                                        .SetRectTransform(0, -132, 132, 0, 0, 1, 0, 1, 0, 1)
                                        .SetOnClick(() => SaveFrameRemoveIcon(0))
                                        .SetTransitionColors(new Color(0.2f, 0.2f, 0.2f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.222f, 0.667f, 1.0f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                        .Element("Image")
                                            .SetRectTransform(0, 0, 0, 0, 0.5f, 0.5f, 0, 0, 1, 1)
                                            .Component_Image(iconEmpty.Sprite, Color.white, Image.Type.Sliced, Vector4.zero)
                                            .Keep(out saveFrameIconImages[0])
                                        .Done
                                    .Done
                                    .Element_Button("Icon 2 Button", iconBlack.Sprite, Color.white, Vector4.zero, Image.Type.Simple)
                                        .SetRectTransform(132 + 4, -132, 132 + 4 + 132, 0, 0, 1, 0, 1, 0, 1)
                                        .SetOnClick(() => SaveFrameRemoveIcon(1))
                                        .SetTransitionColors(new Color(0.2f, 0.2f, 0.2f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.222f, 0.667f, 1.0f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                        .Element("Image")
                                            .SetRectTransform(0, 0, 0, 0, 0.5f, 0.5f, 0, 0, 1, 1)
                                            .Component_Image(iconEmpty.Sprite, Color.white, Image.Type.Sliced, Vector4.zero)
                                        .Keep(out saveFrameIconImages[1])
                                        .Done
                                    .Done
                                    .Element_Button("Icon 3 Button", iconBlack.Sprite, Color.white, Vector4.zero, Image.Type.Simple)
                                        .SetRectTransform(0, -(132 + 4 + 132), 132, -(132 + 4), 0, 1, 0, 1, 0, 1)
                                        .SetOnClick(() => SaveFrameRemoveIcon(2))
                                        .SetTransitionColors(new Color(0.2f, 0.2f, 0.2f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.222f, 0.667f, 1.0f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                        .Element("Image")
                                            .SetRectTransform(0, 0, 0, 0, 0.5f, 0.5f, 0, 0, 1, 1)
                                            .Component_Image(iconEmpty.Sprite, Color.white, Image.Type.Sliced, Vector4.zero)
                                            .Keep(out saveFrameIconImages[2])
                                        .Done
                                    .Done
                                    .Element_Button("Icon 4 Button", iconBlack.Sprite, Color.white, Vector4.zero, Image.Type.Simple)
                                        .SetRectTransform(132 + 4, -(132 + 4 + 132), 132 + 4 + 132, -(132 + 4), 0, 1, 0, 1, 0, 1)
                                        .SetOnClick(() => SaveFrameRemoveIcon(3))
                                        .SetTransitionColors(new Color(0.2f, 0.2f, 0.2f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.222f, 0.667f, 1.0f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                        .Element("Image")
                                            .SetRectTransform(0, 0, 0, 0, 0.5f, 0.5f, 0, 0, 1, 1)
                                            .Component_Image(iconEmpty.Sprite, Color.white, Image.Type.Sliced, Vector4.zero)
                                            .Keep(out saveFrameIconImages[3])
                                        .Done
                                    .Done
                                    .Element("Preview")
                                        .SetRectTransform(132 + 4 + 132 + 10 + 64 - 50, -(132 + 5 - 60), 132 + 4 + 132 + 10 + 64 - 50, -(132 + 5 - 60), 0, 1, 0, 1, 0, 1)
                                        .SetSizeDelta(100, 120)
                                        .Keep(out saveFramePreviewContainer)
                                    .Done
                                .Done
                                .Element("Name Row")
                                    .Layout()
                                        .MinHeight(40)
                                        .FlexibleHeight(0)
                                    .Done
                                    .Do(builder =>
                                    {
                                        var gameObject = Object.Instantiate(prefabBlueprintNameInputField.Prefab, builder.GameObject.transform);
                                        saveFrameNameInputField = gameObject.GetComponentInChildren<TMP_InputField>();
                                        if (saveFrameNameInputField == null) throw new System.Exception("TextMeshPro Input field not found.");
                                        saveFrameNameInputField.text = "";
                                        saveFrameNameInputField.onValueChanged.AddListener(new UnityAction<string>((string value) =>
                                        {
                                            if (saveFramePreviewLabel != null) saveFramePreviewLabel.text = Path.GetFileName(value);
                                        }));
                                        EventSystem.current.SetSelectedGameObject(saveFrameNameInputField.gameObject, null);
                                    })
                                .Done
                                .Element("Row Buttons")
                                    .Layout()
                                        .MinHeight(40)
                                        .FlexibleHeight(0)
                                    .Done
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Element_TextButton("Button Confirm", "Save Blueprint")
                                        .Updater<Button>(guiUpdaters, () => !string.IsNullOrWhiteSpace(saveFrameNameInputField?.text))
                                        .SetOnClick(FinishSaveBlueprint)
                                    .Done
                                    .Element_TextButton("Button Get Existing", "Get Existing")
                                        .SetOnClick(GetInfoFromExistingBlueprint)
                                    .Done
                                    .Element_TextButton("Button Cancel", "Cancel")
                                        .SetOnClick(HideSaveFrame)
                                    .Done
                                .Done
                                .Element_ScrollBox("Material Report ScrollBox", builder =>
                                    {
                                        builder = builder
                                            .SetVerticalLayout(new RectOffset(5, 5, 5, 5), 10.0f, TextAnchor.UpperLeft, false, true, true, false, false, false, false)
                                            .Element("Material Report")
                                                .SetRectTransform(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f)
                                                .AutoSize(ContentSizeFitter.FitMode.PreferredSize, ContentSizeFitter.FitMode.PreferredSize)
                                                .Component_Text("", "OpenSansSemibold SDF", 14.0f, Color.white, TextAlignmentOptions.TopLeft)
                                                .Keep(out saveFrameMaterialReportText)
                                            .Done;
                                    })
                                    .Layout()
                                        .PreferredHeight(400)
                                    .Done
                                .Done
                            .Done
                        .Done
                    .Done
                .End();

                FillSaveGrid();
            }

            if (CurrentBlueprint != null)
            {
                if (saveFrameNameInputField != null) saveFrameNameInputField.text = CurrentBlueprint.Name;

                for (int i = 0; i < 4; i++) saveFrameIconItemTemplates[i] = null;
                CurrentBlueprint.IconItemTemplates.CopyTo(saveFrameIconItemTemplates, 0);
                saveFrameIconCount = CurrentBlueprint.IconItemTemplates.Length;
            }

            FillSavePreview();
            FillSaveFrameIcons();
            FillSaveMaterialReport();

            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIOpen);

            saveFrame.SetActive(true);
            GlobalStateManager.addCursorRequirement();
            saveFrameEscapeCloseProxy = EscapeCloseProxy.Register(HideSaveFrame);
        }

        private void FillSaveMaterialReport()
        {
            int totalItemCount = 0;
            var materialReportBuilder = new System.Text.StringBuilder();
            foreach (var kv in CurrentBlueprint.ShoppingList)
            {
                var itemCount = kv.Value.count;
                if (itemCount > 0)
                {
                    totalItemCount += itemCount;
                    var name = kv.Value.name;
                    materialReportBuilder.AppendLine($"<color=#CCCCCC>{name}:</color> {itemCount}");
                }
            }

            if (totalItemCount > 0)
            {
                materialReportBuilder.AppendLine($"<color=#CCCCCC>Total:</color> {totalItemCount}");
            }

            saveFrameMaterialReportText.text = materialReportBuilder.ToString();
        }

        private void FillSavePreview()
        {
            saveFramePreviewIconImages[0] = saveFramePreviewIconImages[1] = saveFramePreviewIconImages[2] = saveFramePreviewIconImages[3] = null;

            switch (saveFrameIconCount)
            {
                case 0:
                    {
                        DestroyAllTransformChildren(saveFramePreviewContainer.transform);
                        var gameObject = UnityEngine.Object.Instantiate(prefabBlueprintButtonDefaultIcon.Prefab, saveFramePreviewContainer.transform);
                        var deleteButton = gameObject.transform.Find("DeleteButton")?.gameObject;
                        if (deleteButton != null) deleteButton.SetActive(false);
                        var renameButton = gameObject.transform.Find("RenameButton")?.gameObject;
                        if (renameButton != null) renameButton.SetActive(false);
                        saveFramePreviewLabel = gameObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (saveFramePreviewLabel == null) throw new System.ArgumentNullException(nameof(saveFramePreviewLabel));
                    }
                    break;

                case 1:
                    {
                        DestroyAllTransformChildren(saveFramePreviewContainer.transform);
                        var gameObject = UnityEngine.Object.Instantiate(prefabBlueprintButton1Icon.Prefab, saveFramePreviewContainer.transform);
                        var deleteButton = gameObject.transform.Find("DeleteButton")?.gameObject;
                        if (deleteButton != null) deleteButton.SetActive(false);
                        var renameButton = gameObject.transform.Find("RenameButton")?.gameObject;
                        if (renameButton != null) renameButton.SetActive(false);
                        saveFramePreviewLabel = gameObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (saveFramePreviewLabel == null) throw new System.ArgumentNullException(nameof(saveFramePreviewLabel));
                        saveFramePreviewIconImages[0] = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                    }
                    break;

                case 2:
                    {
                        DestroyAllTransformChildren(saveFramePreviewContainer.transform);
                        var gameObject = UnityEngine.Object.Instantiate(prefabBlueprintButton2Icon.Prefab, saveFramePreviewContainer.transform);
                        var deleteButton = gameObject.transform.Find("DeleteButton")?.gameObject;
                        if (deleteButton != null) deleteButton.SetActive(false);
                        var renameButton = gameObject.transform.Find("RenameButton")?.gameObject;
                        if (renameButton != null) renameButton.SetActive(false);
                        saveFramePreviewLabel = gameObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (saveFramePreviewLabel == null) throw new System.ArgumentNullException(nameof(saveFramePreviewLabel));
                        saveFramePreviewIconImages[0] = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                        saveFramePreviewIconImages[1] = gameObject.transform.Find("Icon2")?.GetComponent<Image>();
                    }
                    break;

                case 3:
                    {
                        DestroyAllTransformChildren(saveFramePreviewContainer.transform);
                        var gameObject = UnityEngine.Object.Instantiate(prefabBlueprintButton3Icon.Prefab, saveFramePreviewContainer.transform);
                        var deleteButton = gameObject.transform.Find("DeleteButton")?.gameObject;
                        if (deleteButton != null) deleteButton.SetActive(false);
                        var renameButton = gameObject.transform.Find("RenameButton")?.gameObject;
                        if (renameButton != null) renameButton.SetActive(false);
                        saveFramePreviewLabel = gameObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (saveFramePreviewLabel == null) throw new System.ArgumentNullException(nameof(saveFramePreviewLabel));
                        saveFramePreviewIconImages[0] = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                        saveFramePreviewIconImages[1] = gameObject.transform.Find("Icon2")?.GetComponent<Image>();
                        saveFramePreviewIconImages[2] = gameObject.transform.Find("Icon3")?.GetComponent<Image>();
                    }
                    break;

                case 4:
                    {
                        DestroyAllTransformChildren(saveFramePreviewContainer.transform);
                        var gameObject = UnityEngine.Object.Instantiate(prefabBlueprintButton4Icon.Prefab, saveFramePreviewContainer.transform);
                        saveFramePreviewLabel = gameObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (saveFramePreviewLabel == null) throw new System.ArgumentNullException(nameof(saveFramePreviewLabel));
                        var renameButton = gameObject.transform.Find("RenameButton")?.gameObject;
                        if (renameButton != null) renameButton.SetActive(false);
                        saveFramePreviewIconImages[0] = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                        saveFramePreviewIconImages[1] = gameObject.transform.Find("Icon2")?.GetComponent<Image>();
                        saveFramePreviewIconImages[2] = gameObject.transform.Find("Icon3")?.GetComponent<Image>();
                        saveFramePreviewIconImages[3] = gameObject.transform.Find("Icon4")?.GetComponent<Image>();
                    }
                    break;

                default:
                    break;
            }

            if (saveFramePreviewLabel != null && saveFrameNameInputField != null)
            {
                saveFramePreviewLabel.text = Path.GetFileName(saveFrameNameInputField.text);
            }

            for (int i = 0; i < saveFrameIconCount; i++)
            {
                if (saveFramePreviewIconImages[i] != null)
                {
                    saveFramePreviewIconImages[i].sprite = saveFrameIconItemTemplates[i]?.icon ?? iconEmpty.Sprite;
                }
            }
        }

        private void FillSaveGrid()
        {
            if (saveGridObject == null) return;

            DestroyAllTransformChildren(saveGridObject.transform);

            foreach (var kv in ItemTemplateManager.getAllItemTemplates())
            {
                var itemTemplate = kv.Value;
                if (itemTemplate.isHiddenItem) continue;

                var gameObject = UnityEngine.Object.Instantiate(prefabBlueprintButtonIcon.Prefab, saveGridObject.transform);

                var iconImage = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                if (iconImage != null) iconImage.sprite = itemTemplate.icon;

                var button = gameObject.GetComponentInChildren<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(new UnityAction(() => SaveFrameAddIcon(itemTemplate)));
                }

                var panel = gameObject.GetComponent<Image>();
                if (panel != null) panel.color = Color.clear;
            }
        }

        private void FillSaveFrameIcons()
        {
            for (int i = 0; i < saveFrameIconCount; i++)
            {
                if (saveFrameIconImages[i] != null)
                {
                    saveFrameIconImages[i].sprite = saveFrameIconItemTemplates[i]?.icon_256 ?? iconEmpty.Sprite;
                }
            }
            for (int i = saveFrameIconCount; i < 4; i++)
            {
                if (saveFrameIconImages[i] != null)
                {
                    saveFrameIconImages[i].sprite = iconEmpty.Sprite;
                }
            }
        }

        private void SaveFrameAddIcon(ItemTemplate itemTemplate)
        {
            if (itemTemplate == null) throw new System.ArgumentNullException(nameof(itemTemplate));
            if (saveFrameIconCount >= 4) return;

            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);

            saveFrameIconItemTemplates[saveFrameIconCount] = itemTemplate;
            saveFrameIconCount++;

            FillSavePreview();
            FillSaveFrameIcons();
        }

        private void SaveFrameRemoveIcon(int iconIndex)
        {
            if (iconIndex >= saveFrameIconCount) return;

            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);

            for (int i = iconIndex; i < 3; i++) saveFrameIconItemTemplates[i] = saveFrameIconItemTemplates[i + 1];
            saveFrameIconItemTemplates[3] = null;
            saveFrameIconCount--;

            FillSavePreview();
            FillSaveFrameIcons();
        }

        private static void DestroyAllTransformChildren(Transform transform)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                child.SetParent(null, false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        private void BeginLoadBlueprint()
        {
            HideBlueprintFrame();
            ShowLibraryFrame();
        }

        internal void HideLibraryFrame()
        {
            if (libraryFrameEscapeCloseProxy != null)
            {
                libraryFrameEscapeCloseProxy.Dispose();
                libraryFrameEscapeCloseProxy = null;
            }

            if (libraryFrame == null || !libraryFrame.activeSelf) return;

            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIClose);

            libraryFrame.SetActive(false);
            GlobalStateManager.removeCursorRequirement();
        }

        internal void ShowLibraryFrame(bool isForSaveInfo = false)
        {
            if (!isForSaveInfo && saveFrame != null && saveFrame.activeSelf) HideSaveFrame();

            if (libraryFrame != null)
            {
                if (libraryFrame.activeSelf) return;

                Object.Destroy(libraryFrame);
                libraryFrame = null;
            }

            var graphics = Traverse.Create(typeof(UIRaycastTooltipManager))?.Field("singleton")?.GetValue<UIRaycastTooltipManager>()?.tooltipRectTransform?.GetComponentsInChildren<Graphic>();
            if (graphics != null)
            {
                foreach (var graphic in graphics)
                {
                    graphic.raycastTarget = false;
                }
            }

            ulong usernameHash = GameRoot.getClientCharacter().usernameHash;
            UIBuilder.BeginWith(GameRoot.getDefaultCanvas())
                .Element_Panel("Library Frame", "corner_cut_outline", new Color(0.133f, 0.133f, 0.133f, 1.0f), new Vector4(13, 10, 8, 13))
                    .Keep(out libraryFrame)
                    .SetRectTransform(100, 100, -100, -100, 0.5f, 0.5f, 0, 0, 1, 1)
                    .Element_Header("HeaderBar", "corner_cut_outline", new Color(0.0f, 0.6f, 1.0f, 1.0f), new Vector4(13, 3, 8, 13))
                        .SetRectTransform(0.0f, -60.0f, 0.0f, 0.0f, 0.5f, 1.0f, 0.0f, 1.0f, 1.0f, 1.0f)
                        .Element("Heading")
                            .SetRectTransform(0.0f, 0.0f, -60.0f, 0.0f, 0.0f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                            .Component_Text("Blueprints", "OpenSansSemibold SDF", 34.0f, Color.white)
                            .Keep(out libraryFrameHeading)
                        .Done
                        .Element_Button("Button Close", "corner_cut_fully_inset", Color.white, new Vector4(13.0f, 1.0f, 4.0f, 13.0f))
                            .SetOnClick(HideLibraryFrame)
                            .SetRectTransform(-60.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.5f, 1.0f, 0.0f, 1.0f, 1.0f)
                            .SetTransitionColors(new Color(1.0f, 1.0f, 1.0f, 1.0f), new Color(1.0f, 0.25f, 0.0f, 1.0f), new Color(1.0f, 0.0f, 0.0f, 1.0f), new Color(1.0f, 0.25f, 0.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                            .Element("Image")
                                .SetRectTransform(5.0f, 5.0f, -5.0f, -5.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                .Component_Image("cross", Color.white, Image.Type.Sliced, Vector4.zero)
                            .Done
                        .Done
                    .Done
                    .Element("Content")
                        .SetRectTransform(0.0f, 0.0f, 0.0f, -60.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                        .Element("Padding")
                            .SetRectTransform(10.0f, 10.0f, -10.0f, -10.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                            .Do(builder =>
                            {
                                var gameObject = Object.Instantiate(prefabGridScrollView.Prefab, builder.GameObject.transform);
                                var grid = gameObject.GetComponentInChildren<GridLayoutGroup>();
                                if (grid == null) throw new System.Exception("Grid not found.");
                                libraryGridObject = grid.gameObject;
                            })
                        .Done
                    .Done
                .Done
            .End();

            FillLibraryGrid(lastLibraryRelativePath, isForSaveInfo);

            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIOpen);

            libraryFrame.SetActive(true);
            GlobalStateManager.addCursorRequirement();
            libraryFrameEscapeCloseProxy = EscapeCloseProxy.Register(HideLibraryFrame);
        }

        private void FillLibraryGrid(string relativePath, bool isForSaveInfo)
        {
            if (libraryGridObject == null) return;

            lastLibraryRelativePath = relativePath;

            DestroyAllTransformChildren(libraryGridObject.transform);

            libraryFrameHeading.text = string.IsNullOrEmpty(relativePath) ? "Blueprints" : $"Blueprints\\{relativePath}";

            var prefabs = new GameObject[5]
            {
                prefabBlueprintButtonDefaultIcon.Prefab, prefabBlueprintButton1Icon.Prefab, prefabBlueprintButton2Icon.Prefab, prefabBlueprintButton3Icon.Prefab, prefabBlueprintButton4Icon.Prefab
            };

            if (!string.IsNullOrEmpty(relativePath))
            {
                var backGameObject = Object.Instantiate(prefabBlueprintButtonFolderBack.Prefab, libraryGridObject.transform);
                var backButton = backGameObject.GetComponentInChildren<Button>();
                if (backButton != null)
                {
                    backButton.onClick.AddListener(new UnityAction(() =>
                    {
                        AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);

                        var backPath = Path.GetDirectoryName(relativePath);
                        FillLibraryGrid(backPath, isForSaveInfo);
                    }));
                }
            }

            if (!isForSaveInfo)
            {
                var newFolderGameObject = Object.Instantiate(prefabBlueprintButtonFolderNew.Prefab, libraryGridObject.transform);
                var newFolderButton = newFolderGameObject.GetComponentInChildren<Button>();
                if (newFolderButton != null)
                {
                    newFolderButton.onClick.AddListener(new UnityAction(() =>
                    {
                        ShowFolderFrame(relativePath, "");
                        //TextEntryFrame.Show("Enter folder name:", "", "Create Folder", (name) =>
                        //{
                        //    if (string.IsNullOrWhiteSpace(name)) return;
                        //    var path = Path.Combine(DuplicationerPlugin.BlueprintFolder, relativePath, name);
                        //    if (Directory.Exists(path)) return;
                        //    Directory.CreateDirectory(path);
                        //    FillLibraryGrid(Path.Combine(relativePath, name), false);
                        //});
                    }));
                }
            }

            var builder = UIBuilder.BeginWith(libraryGridObject);
            foreach (var path in Directory.GetDirectories(Path.Combine(DuplicationerPlugin.BlueprintFolder, relativePath)))
            {
                var name = Path.GetFileName(path);

                var gameObject = Object.Instantiate(prefabBlueprintButtonFolder.Prefab, libraryGridObject.transform);

                var label = gameObject.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                if (label != null) label.text = name;

                ItemTemplate iconItemTemplate = null;
                var iconNamePath = Path.Combine(path, "__folder_icon.txt");
                if (File.Exists(iconNamePath))
                {
                    var identifier = File.ReadAllText(iconNamePath).Trim();
                    var hash = ItemTemplate.generateStringHash(identifier);
                    iconItemTemplate = ItemTemplateManager.getItemTemplate(hash);
                }

                var iconImage = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                if (iconImage != null)
                {
                    iconImage.sprite = iconItemTemplate?.icon ?? iconEmpty.Sprite;
                }

                var button = gameObject.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(new UnityAction(() =>
                    {
                        ActionManager.AddQueuedEvent(() =>
                        {
                            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);

                            FillLibraryGrid(Path.Combine(relativePath, name), isForSaveInfo);
                        });
                    }));
                }

                var deleteButton = gameObject.transform.Find("DeleteButton")?.GetComponent<Button>();
                if (deleteButton != null)
                {
                    if (isForSaveInfo)
                    {
                        deleteButton.gameObject.SetActive(false);
                    }
                    else
                    {
                        var nameToDelete = name;
                        var pathToDelete = path;
                        deleteButton.onClick.AddListener(new UnityAction(() =>
                        {
                            ActionManager.AddQueuedEvent(() =>
                            {
                                ConfirmationFrame.Show($"Delete folder '{name}'", "Delete", () =>
                                {
                                    try
                                    {
                                        Directory.Delete(pathToDelete, true);
                                        FillLibraryGrid(relativePath, false);
                                    }
                                    catch (System.Exception) { }
                                });
                            });
                        }));
                    }

                    var renameButton = gameObject.transform.Find("RenameButton")?.GetComponent<Button>();
                    if (renameButton != null)
                    {
                        if (isForSaveInfo)
                        {
                            renameButton.gameObject.SetActive(false);
                        }
                        else
                        {
                            var nameToRename = name;
                            var pathToRename = path;
                            renameButton.onClick.AddListener(new UnityAction(() =>
                            {
                                ActionManager.AddQueuedEvent(() =>
                                {
                                    ShowFolderFrame(relativePath, nameToRename);
                                    //TextEntryFrame.Show($"Rename Folder", nameToRename, "Rename", (string newName) =>
                                    //{
                                    //    string filenameBase = Path.Combine(Path.GetDirectoryName(newName), PathHelpers.MakeValidFileName(Path.GetFileName(newName)));
                                    //    string newPath = Path.Combine(DuplicationerPlugin.BlueprintFolder, relativePath, filenameBase);
                                    //    if (!File.Exists(newPath))
                                    //    {
                                    //        try
                                    //        {
                                    //            DuplicationerPlugin.log.Log($"Renaming folder '{pathToRename}' to '{newPath}'");
                                    //            Directory.Move(pathToRename, newPath);
                                    //            FillLibraryGrid(relativePath, false);
                                    //        }
                                    //        catch (System.Exception) { }
                                    //    }
                                    //});
                                });
                            }));
                        }
                    }
                }
            }

            foreach (var path in Directory.GetFiles(Path.Combine(DuplicationerPlugin.BlueprintFolder, relativePath), $"*.{DuplicationerPlugin.BlueprintExtension}"))
            {
                if (Blueprint.TryLoadFileHeader(path, out var header, out var name))
                {
                    var iconItemTemplates = new List<ItemTemplate>();
                    if (header.icon1 != 0)
                    {
                        var template = ItemTemplateManager.getItemTemplate(header.icon1);
                        if (template != null && template.icon != null) iconItemTemplates.Add(template);
                    }
                    if (header.icon2 != 0)
                    {
                        var template = ItemTemplateManager.getItemTemplate(header.icon2);
                        if (template != null && template.icon != null) iconItemTemplates.Add(template);
                    }
                    if (header.icon3 != 0)
                    {
                        var template = ItemTemplateManager.getItemTemplate(header.icon3);
                        if (template != null && template.icon != null) iconItemTemplates.Add(template);
                    }
                    if (header.icon4 != 0)
                    {
                        var template = ItemTemplateManager.getItemTemplate(header.icon4);
                        if (template != null && template.icon != null) iconItemTemplates.Add(template);
                    }

                    int iconCount = iconItemTemplates.Count;

                    var gameObject = Object.Instantiate(prefabs[iconCount], libraryGridObject.transform);

                    var label = gameObject.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                    if (label != null) label.text = name;

                    var iconImages = new Image[] {
                        gameObject.transform.Find("Icon1")?.GetComponent<Image>(),
                        gameObject.transform.Find("Icon2")?.GetComponent<Image>(),
                        gameObject.transform.Find("Icon3")?.GetComponent<Image>(),
                        gameObject.transform.Find("Icon4")?.GetComponent<Image>()
                    };

                    for (int iconIndex = 0; iconIndex < iconCount; iconIndex++)
                    {
                        iconImages[iconIndex].sprite = iconItemTemplates[iconIndex].icon;
                    }

                    var button = gameObject.GetComponent<Button>();
                    if (button != null)
                    {
                        if (isForSaveInfo)
                        {
                            var nameForSaveInfo = Path.Combine(relativePath, Path.GetFileNameWithoutExtension(path));
                            button.onClick.AddListener(new UnityAction(() =>
                            {
                                ActionManager.AddQueuedEvent(() =>
                                {
                                    saveFrameNameInputField.text = nameForSaveInfo;
                                    saveFrameIconCount = iconCount;
                                    for (int i = 0; i < 4; i++)
                                    {
                                        saveFrameIconItemTemplates[i] = (i < iconCount) ? iconItemTemplates[i] : null;
                                    }
                                    FillSaveFrameIcons();
                                    FillSavePreview();
                                    HideLibraryFrame();
                                });
                            }));
                        }
                        else
                        {
                            button.onClick.AddListener(new UnityAction(() =>
                            {
                                ActionManager.AddQueuedEvent(() =>
                                {
                                    HideLibraryFrame();
                                    ClearBlueprintPlaceholders();
                                    LoadBlueprintFromFile(path);
                                    SelectMode(modePlace);
                                });
                            }));
                        }
                    }

                    var deleteButton = gameObject.transform.Find("DeleteButton")?.GetComponent<Button>();
                    if (deleteButton != null)
                    {
                        if (isForSaveInfo)
                        {
                            deleteButton.gameObject.SetActive(false);
                        }
                        else
                        {
                        var nameToDelete = name;
                        var pathToDelete = path;
                        deleteButton.onClick.AddListener(new UnityAction(() =>
                        {
                            ActionManager.AddQueuedEvent(() =>
                            {
                                ConfirmationFrame.Show($"Delete '{name}'", "Delete", () =>
                                {
                                    try
                                    {
                                        File.Delete(pathToDelete);
                                        FillLibraryGrid(relativePath, false);
                                    }
                                    catch (System.Exception) { }
                                });
                            });
                        }));
                    }

                    var renameButton = gameObject.transform.Find("RenameButton")?.GetComponent<Button>();
                        if (renameButton != null)
                        {
                            if (isForSaveInfo)
                            {
                                renameButton.gameObject.SetActive(false);
                            }
                            else
                            {
                                var nameToRename = name;
                                var pathToRename = path;
                                renameButton.onClick.AddListener(new UnityAction(() =>
                                {
                                    ActionManager.AddQueuedEvent(() =>
                                    {
                                        TextEntryFrame.Show($"Rename Blueprint", nameToRename, "Rename", (string newName) =>
                                        {
                                            string filenameBase = Path.Combine(Path.GetDirectoryName(newName), PathHelpers.MakeValidFileName(Path.GetFileName(newName)));
                                            string newPath = Path.Combine(DuplicationerPlugin.BlueprintFolder, relativePath, $"{filenameBase}.{DuplicationerPlugin.BlueprintExtension}");
                                            if (File.Exists(newPath))
                                            {
                                                ConfirmationFrame.Show($"Overwrite '{newName}'?", "Overwrite", () =>
                                                {
                                                    try
                                                    {
                                                        DuplicationerPlugin.log.Log($"Renaming blueprint '{nameToRename}' to '{newName}'");
                                                        File.Delete(newPath);
                                                        File.Move(pathToRename, newPath);
                                                        RenameBlueprint(newPath, Path.GetFileName(newName));
                                                        FillLibraryGrid(relativePath, false);
                                                    }
                                                    catch (System.Exception) { }
                                                });
                                            }
                                            else
                                            {
                                                try
                                                {
                                                    DuplicationerPlugin.log.Log($"Renaming blueprint '{nameToRename}' to '{newName}'");
                                                    File.Move(pathToRename, newPath);
                                                    RenameBlueprint(newPath, Path.GetFileName(newName));
                                                    FillLibraryGrid(relativePath, false);
                                                }
                                                catch (System.Exception) { }
                                            }
                                        });
                                    });
                                }));
                            }
                        }
                    }
                }
            }
        }

        private void RenameBlueprint(string path, string name)
        {
            var iconItemTemplateIds = new ulong[4];

            var reader = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read));

            var magic = reader.ReadUInt32();
            var version = reader.ReadUInt32();

            for (int i = 0; i < 4; i++) iconItemTemplateIds[i] = reader.ReadUInt64();

            var oldName = reader.ReadString();

            var data = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));

            reader.Close();
            reader.Dispose();

            var writer = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write));

            writer.Write(magic);
            writer.Write(version);

            for (int i = 0; i < 4; i++)
            {
                writer.Write(iconItemTemplateIds[i]);
            }

            writer.Write(name);

            writer.Write(data);

            writer.Close();
            writer.Dispose();
        }

        internal void HideFolderFrame()
        {
            if (folderFrameEscapeCloseProxy != null)
            {
                folderFrameEscapeCloseProxy.Dispose();
                folderFrameEscapeCloseProxy = null;
            }

            if (folderFrame == null || !folderFrame.activeSelf) return;

            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIClose);

            folderFrame.SetActive(false);
            GlobalStateManager.removeCursorRequirement();
        }

        internal void ShowFolderFrame(string relativePath, string folderName)
        {
            if (folderFrame != null && folderFrame.activeSelf) return;

            var originalPath = Path.Combine(DuplicationerPlugin.BlueprintFolder, relativePath, folderName);

            if (folderFrame != null)
            {
                Object.Destroy(folderFrame);
                folderFrame = null;
            }

            ulong usernameHash = GameRoot.getClientCharacter().usernameHash;
            UIBuilder.BeginWith(GameRoot.getDefaultCanvas())
                .Element_Panel("Folder Frame", "corner_cut_outline", new Color(0.133f, 0.133f, 0.133f, 1.0f), new Vector4(13, 10, 8, 13))
                    .Keep(out folderFrame)
                    .SetRectTransform(100, 100, -100, -100, 0.5f, 0.5f, 0, 0, 1, 1)
                    .Element_Header("HeaderBar", "corner_cut_outline", new Color(0.0f, 0.6f, 1.0f, 1.0f), new Vector4(13, 3, 8, 13))
                        .SetRectTransform(0.0f, -60.0f, 0.0f, 0.0f, 0.5f, 1.0f, 0.0f, 1.0f, 1.0f, 1.0f)
                        .Element("Heading")
                            .SetRectTransform(0.0f, 0.0f, -60.0f, 0.0f, 0.0f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                            .Component_Text($"Folder - /{relativePath}", "OpenSansSemibold SDF", 34.0f, Color.white)
                        .Done
                        .Element_Button("Button Close", "corner_cut_fully_inset", Color.white, new Vector4(13.0f, 1.0f, 4.0f, 13.0f))
                            .SetOnClick(HideFolderFrame)
                            .SetRectTransform(-60.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.5f, 1.0f, 0.0f, 1.0f, 1.0f)
                            .SetTransitionColors(new Color(1.0f, 1.0f, 1.0f, 1.0f), new Color(1.0f, 0.25f, 0.0f, 1.0f), new Color(1.0f, 0.0f, 0.0f, 1.0f), new Color(1.0f, 0.25f, 0.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                            .Element("Image")
                                .SetRectTransform(5.0f, 5.0f, -5.0f, -5.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                .Component_Image("cross", Color.white, Image.Type.Sliced, Vector4.zero)
                            .Done
                        .Done
                    .Done
                    .Element("Content")
                        .SetRectTransform(0.0f, 0.0f, 0.0f, -60.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                        .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 0, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                        .Element("ContentLeft")
                            .Layout()
                                .FlexibleWidth(1)
                            .Done
                            .Element("Padding")
                                .SetRectTransform(10.0f, 10.0f, -10.0f, -10.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                .Do(builder =>
                                {
                                    var gameObject = Object.Instantiate(prefabGridScrollView.Prefab, builder.GameObject.transform);
                                    var grid = gameObject.GetComponentInChildren<GridLayoutGroup>();
                                    if (grid == null) throw new System.Exception("Grid not found.");
                                    folderGridObject = grid.gameObject;
                                    grid.cellSize = new Vector2(80.0f, 80.0f);
                                    grid.padding = new RectOffset(4, 4, 4, 4);
                                    grid.spacing = new Vector2(0.0f, 0.0f);
                                })
                            .Done
                        .Done
                        .Element("ContentRight")
                            .Layout()
                                .MinWidth(132 + 4 + 132 + 4 + 132 + 10)
                                .FlexibleWidth(0)
                            .Done
                            .SetVerticalLayout(new RectOffset(0, 10, 10, 10), 10, TextAnchor.UpperLeft, false, true, true, true, false, false, false)
                            .Element("Icons Row")
                                .Layout()
                                    .MinHeight(132 + 6 + 132)
                                    .FlexibleHeight(0)
                                .Done
                                .Element_Button("Icon 1 Button", iconBlack.Sprite, Color.white, Vector4.zero, Image.Type.Simple)
                                    .SetRectTransform(0, -270, 270, 0, 0, 1, 0, 1, 0, 1)
                                    .SetOnClick(() => {
                                        folderFrameIconImage.sprite = iconEmpty.Sprite;
                                        folderFrameIconItemTemplate = null;
                                        FillFolderPreview();
                                        AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);
                                    })
                                    .SetTransitionColors(new Color(0.2f, 0.2f, 0.2f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.222f, 0.667f, 1.0f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                    .Element("Image")
                                        .SetRectTransform(0, 0, 0, 0, 0.5f, 0.5f, 0, 0, 1, 1)
                                        .Component_Image(iconEmpty.Sprite, Color.white, Image.Type.Sliced, Vector4.zero)
                                        .Keep(out folderFrameIconImage)
                                    .Done
                                .Done
                                .Element("Preview")
                                    .SetRectTransform(132 + 4 + 132 + 10 + 64 - 50, -(132 + 5 - 60), 132 + 4 + 132 + 10 + 64 - 50, -(132 + 5 - 60), 0, 1, 0, 1, 0, 1)
                                    .SetSizeDelta(100, 120)
                                    .Keep(out folderFramePreviewContainer)
                                .Done
                            .Done
                            .Element("Name Row")
                                .Layout()
                                    .MinHeight(40)
                                    .FlexibleHeight(0)
                                .Done
                                .Do(builder =>
                                {
                                    var gameObject = Object.Instantiate(prefabBlueprintNameInputField.Prefab, builder.GameObject.transform);
                                    folderFrameNameInputField = gameObject.GetComponentInChildren<TMP_InputField>();
                                    if (folderFrameNameInputField == null) throw new System.Exception("TextMeshPro Input field not found.");
                                    folderFrameNameInputField.text = "";
                                    folderFrameNameInputField.onValueChanged.AddListener(new UnityAction<string>((string value) =>
                                    {
                                        if (folderFramePreviewLabel != null) folderFramePreviewLabel.text = Path.GetFileName(value);
                                    }));
                                    EventSystem.current.SetSelectedGameObject(folderFrameNameInputField.gameObject, null);
                                })
                            .Done
                            .Element("Row Buttons")
                                .Layout()
                                    .MinHeight(40)
                                    .FlexibleHeight(0)
                                .Done
                                .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                .Element_TextButton("Button Confirm", "Confirm")
                                    .Updater<Button>(guiUpdaters, () => !string.IsNullOrWhiteSpace(folderFrameNameInputField?.text))
                                    .SetOnClick(() => { ConfirmFolderEdit(relativePath, folderName, folderFrameNameInputField?.text); })
                                .Done
                                .Element_TextButton("Button Cancel", "Cancel")
                                    .SetOnClick(HideFolderFrame)
                                .Done
                            .Done
                        .Done
                    .Done
                .Done
            .End();

            FillFolderGrid();

            if (folderFrameNameInputField != null) folderFrameNameInputField.text = folderName;

            folderFrameIconItemTemplate = null;

            if (!string.IsNullOrEmpty(folderName))
            {
                string iconNamePath = Path.Combine(originalPath, "__folder_icon.txt");
                if (File.Exists(iconNamePath))
                {
                    var iconName = File.ReadAllText(iconNamePath).Trim();
                    var iconHash = ItemTemplate.generateStringHash(iconName);
                    folderFrameIconItemTemplate = ItemTemplateManager.getItemTemplate(iconHash);
                }
            }

            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIOpen);

            FillFolderPreview();
            FillFolderFrameIcons();

            folderFrame.SetActive(true);
            GlobalStateManager.addCursorRequirement();
            folderFrameEscapeCloseProxy = EscapeCloseProxy.Register(HideFolderFrame);
        }

        private void ConfirmFolderEdit(string relativePath, string originalName, string newName)
        {
            var newPath = Path.Combine(DuplicationerPlugin.BlueprintFolder, relativePath, newName);
            var iconNamePath = Path.Combine(newPath, "__folder_icon.txt");
            if (string.IsNullOrWhiteSpace(originalName))
            {
                try
                {
                    Directory.CreateDirectory(newPath);
                    if (Directory.Exists(newPath))
                    {
                        if (folderFrameIconItemTemplate != null)
                        {
                            File.WriteAllText(iconNamePath, folderFrameIconItemTemplate.identifier);
                        }

                        HideFolderFrame();
                        FillLibraryGrid(relativePath, false);
                    }
                }
                catch { }
            }
            else
            {
                var originalPath = Path.Combine(DuplicationerPlugin.BlueprintFolder, relativePath, originalName);

                try
                {
                    if (originalPath != newPath && Directory.Exists(originalPath) && !Directory.Exists(newPath))
                    {
                        Directory.Move(originalPath, newPath);
                    }

                    if (File.Exists(iconNamePath)) File.Delete(iconNamePath);

                    if (folderFrameIconItemTemplate != null)
                    {
                        File.WriteAllText(iconNamePath, folderFrameIconItemTemplate.identifier);
                    }

                    HideFolderFrame();
                    FillLibraryGrid(relativePath, false);
                }
                catch { }
            }
        }

        private void FillFolderGrid()
        {
            if (folderGridObject == null) return;

            DestroyAllTransformChildren(folderGridObject.transform);

            foreach (var kv in ItemTemplateManager.getAllItemTemplates())
            {
                var itemTemplate = kv.Value;
                if (itemTemplate.isHiddenItem) continue;

                var gameObject = Object.Instantiate(prefabBlueprintButtonIcon.Prefab, folderGridObject.transform);

                var iconImage = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                if (iconImage != null) iconImage.sprite = itemTemplate.icon;

                var button = gameObject.GetComponentInChildren<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(new UnityAction(() => {
                        AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);
                        folderFrameIconItemTemplate = itemTemplate;
                        folderFrameIconImage.sprite = itemTemplate?.icon ?? iconEmpty.Sprite;
                        FillFolderPreview();
                    }));
                }

                var panel = gameObject.GetComponent<Image>();
                if (panel != null) panel.color = Color.clear;
            }
        }

        private void FillFolderPreview()
        {
            DestroyAllTransformChildren(folderFramePreviewContainer.transform);
            var gameObject = Object.Instantiate(prefabBlueprintButtonFolder.Prefab, folderFramePreviewContainer.transform);
            var deleteButton = gameObject.transform.Find("DeleteButton")?.gameObject;
            if (deleteButton != null) deleteButton.SetActive(false);
            var renameButton = gameObject.transform.Find("RenameButton")?.gameObject;
            if (renameButton != null) renameButton.SetActive(false);
            folderFramePreviewLabel = gameObject.GetComponentInChildren<TextMeshProUGUI>();
            folderFramePreviewIconImage = gameObject.transform.Find("Icon1")?.GetComponent<Image>();

            if (folderFramePreviewLabel != null && folderFrameNameInputField != null)
            {
                folderFramePreviewLabel.text = Path.GetFileName(folderFrameNameInputField.text);
            }

            if (folderFramePreviewIconImage != null)
            {
                folderFramePreviewIconImage.sprite = folderFrameIconItemTemplate?.icon ?? iconEmpty.Sprite;
            }
        }

        private void FillFolderFrameIcons()
        {
            if (folderFrameIconImage != null)
            {
                folderFrameIconImage.sprite = folderFrameIconItemTemplate?.icon_256 ?? iconEmpty.Sprite;
            }
        }

        private static List<bool> GetTerrainTypeRemovalMask()
        {
            if (terrainTypeRemovalMask == null)
            {
                var terrainTypes = ItemTemplateManager.getAllTerrainTemplates();

                terrainTypeRemovalMask = new List<bool>();
                terrainTypeRemovalMask.Add(false); // Air
                terrainTypeRemovalMask.Add(false); // ???

                foreach (var terrainType in terrainTypes)
                {
                    terrainTypeRemovalMask.Add(terrainType.Value.destructible);
                }
            }

            return terrainTypeRemovalMask;
        }

        private void DestroyArea(bool doBuildings, bool doBlocks, bool doTerrain, bool doDecor)
        {
            if (TryGetSelectedArea(out Vector3Int from, out Vector3Int to))
            {
                ulong characterHash = GameRoot.getClientCharacter().usernameHash;

                if (doBuildings || doDecor)
                {
                    AABB3D aabb = ObjectPoolManager.aabb3ds.getObject();
                    aabb.reinitialize(from.x, from.y, from.z, to.x - from.x + 1, to.y - from.y + 1, to.z - from.z + 1);
                    bogoQueryResult.Clear();
                    StreamingSystem.getBuildableObjectGOQuadtreeArray().queryAABB3D(aabb, bogoQueryResult, true);
                    if (bogoQueryResult.Count > 0)
                    {
                        foreach (var bogo in bogoQueryResult)
                        {
                            if (bogo.template.type == BuildableObjectTemplate.BuildableObjectType.WorldDecorMineAble)
                            {
                                if (doDecor)
                                {
                                    ActionManager.AddQueuedEvent(() => GameRoot.addLockstepEvent(new Character.DemolishBuildingEvent(characterHash, bogo.relatedEntityId, -2, 0)));
                                }
                            }
                            else if (doBuildings)
                            {
                                ActionManager.AddQueuedEvent(() => GameRoot.addLockstepEvent(new Character.DemolishBuildingEvent(characterHash, bogo.relatedEntityId, -2, 0)));
                            }
                        }
                    }
                    ObjectPoolManager.aabb3ds.returnObject(aabb); aabb = null;
                }

                if (doBlocks || doTerrain)
                {
                    var shouldRemove = GetTerrainTypeRemovalMask();

                    int blocksRemoved = 0;
                    for (int wz = from.z; wz <= to.z; ++wz)
                    {
                        for (int wy = from.y; wy <= to.y; ++wy)
                        {
                            for (int wx = from.x; wx <= to.x; ++wx)
                            {
                                ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(wx, wy, wz, out ulong chunkIndex, out uint blockIndex);
                                var terrainData = ChunkManager.chunks_getTerrainData(chunkIndex, blockIndex);

                                if (terrainData >= GameRoot.BUILDING_PART_ARRAY_IDX_START && doBlocks)
                                {
                                    ulong entityId = 0;
                                    ChunkManager.chunks_getBuildingPartBlock(chunkIndex, blockIndex, ref entityId);
                                    ActionManager.AddQueuedEvent(() => GameRoot.addLockstepEvent(new Character.DemolishBuildingEvent(characterHash, entityId, -2, 0)));
                                    ++blocksRemoved;
                                }
                                else if (doTerrain && terrainData > 0 && terrainData < GameRoot.BUILDING_PART_ARRAY_IDX_START && terrainData < shouldRemove.Count && shouldRemove[terrainData])
                                {
                                    var worldPos = new Vector3Int(wx, wy, wz);
                                    ActionManager.AddQueuedEvent(() => GameRoot.addLockstepEvent(new Character.RemoveTerrainEvent(characterHash, worldPos, ulong.MaxValue)));
                                    ++blocksRemoved;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void DemolishArea(bool doBuildings, bool doBlocks, bool doTerrain, bool doDecor)
        {
            if (TryGetSelectedArea(out Vector3Int from, out Vector3Int to))
            {
                ulong characterHash = GameRoot.getClientCharacter().usernameHash;

                if (doBuildings || doDecor)
                {
                    AABB3D aabb = ObjectPoolManager.aabb3ds.getObject();
                    aabb.reinitialize(from.x, from.y, from.z, to.x - from.x + 1, to.y - from.y + 1, to.z - from.z + 1);
                    bogoQueryResult.Clear();
                    StreamingSystem.getBuildableObjectGOQuadtreeArray().queryAABB3D(aabb, bogoQueryResult, true);
                    if (bogoQueryResult.Count > 0)
                    {
                        foreach (var bogo in bogoQueryResult)
                        {
                            if (bogo.template.type == BuildableObjectTemplate.BuildableObjectType.WorldDecorMineAble)
                            {
                                if (doDecor)
                                {
                                    ActionManager.AddQueuedEvent(() =>
                                    {
                                        GameRoot.addLockstepEvent(new Character.RemoveWorldDecorEvent(characterHash, bogo.relatedEntityId, 0));
                                    });
                                }
                            }
                            else if (doBuildings)
                            {
                                ActionManager.AddQueuedEvent(() =>
                                {
                                    GameRoot.addLockstepEvent(new Character.DemolishBuildingEvent(characterHash, bogo.relatedEntityId, bogo.placeholderId, 0));
                                });
                            }
                        }
                    }
                    ObjectPoolManager.aabb3ds.returnObject(aabb); aabb = null;
                }

                if (doBlocks || doTerrain)
                {
                    var shouldRemove = GetTerrainTypeRemovalMask();

                    int blocksRemoved = 0;
                    for (int wz = from.z; wz <= to.z; ++wz)
                    {
                        for (int wy = from.y; wy <= to.y; ++wy)
                        {
                            for (int wx = from.x; wx <= to.x; ++wx)
                            {
                                ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(wx, wy, wz, out ulong chunkIndex, out uint blockIndex);
                                var terrainData = ChunkManager.chunks_getTerrainData(chunkIndex, blockIndex);

                                if (terrainData >= GameRoot.BUILDING_PART_ARRAY_IDX_START && doBlocks)
                                {
                                    var worldPos = new Vector3Int(wx, wy, wz);
                                    ulong entityId = 0;
                                    ChunkManager.chunks_getBuildingPartBlock(chunkIndex, blockIndex, ref entityId);
                                    ActionManager.AddQueuedEvent(() => GameRoot.addLockstepEvent(new Character.DemolishBuildingEvent(characterHash, entityId, 0, 0)));
                                    ++blocksRemoved;
                                }
                                else if (doTerrain && terrainData > 0 && terrainData < GameRoot.BUILDING_PART_ARRAY_IDX_START && terrainData < shouldRemove.Count && shouldRemove[terrainData])
                                {
                                    var worldPos = new Vector3Int(wx, wy, wz);
                                    ActionManager.AddQueuedEvent(() => GameRoot.addLockstepEvent(new Character.RemoveTerrainEvent(characterHash, worldPos, 0)));
                                    ++blocksRemoved;
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool TryGetSelectedArea(out Vector3Int from, out Vector3Int to)
        {
            switch (boxMode)
            {
                case BoxMode.Selection:
                    from = DragMin;
                    to = DragMax;
                    return true;

                case BoxMode.Blueprint:
                    from = RepeatBlueprintMin;
                    to = RepeatBlueprintMax;
                    return true;
            }

            from = Vector3Int.zero;
            to = Vector3Int.zero;
            return false;
        }

        internal void UpdateBlueprintPositionText()
        {
            if (textPositionX != null) textPositionX.text = string.Format("Position X: {0}", CurrentBlueprintAnchor.x);
            if (textPositionY != null) textPositionY.text = string.Format("Position Y: {0}", CurrentBlueprintAnchor.y);
            if (textPositionZ != null) textPositionZ.text = string.Format("Position Z: {0}", CurrentBlueprintAnchor.z);
        }

        internal void LoadIconSprites()
        {
            iconBlack = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "black");
            iconEmpty = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "empty");
            iconCopy = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "copy");
            iconMoveVertical = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "move-vertical");
            iconMove = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "move");
            iconPanel = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "panel");
            iconPaste = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "paste");
            iconPlace = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "place");
            iconRepeat = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "repeat");
            iconResizeVertical = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "resize-vertical");
            iconResize = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "resize");
            iconSelectArea = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "select-area");
            iconMirror = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "mirror");
        }

        private void OnGameInitializationDone()
        {
            CurrentBlueprintStatusText = "";

            IsBlueprintActive = false;
            CurrentBlueprintAnchor = Vector3Int.zero;
            buildingPlaceholders.Clear();
            terrainPlaceholders.Clear();
            buildingPlaceholderUpdateIndex = 0;
            terrainPlaceholderUpdateIndex = 0;

            activeConstructionTaskGroups.Clear();

            bogoQueryResult.Clear();
        }

        internal List<MinecartDepotGO> GetExistingMinecartDepots(Vector3Int targetPosition, Vector3Int repeatFrom, Vector3Int repeatTo)
        {
            existingMinecartDepots.Clear();
            for (int y = repeatFrom.y; y <= repeatTo.y; ++y)
            {
                for (int z = repeatFrom.z; z <= repeatTo.z; ++z)
                {
                    for (int x = repeatFrom.x; x <= repeatTo.x; ++x)
                    {
                        CurrentBlueprint.GetExistingMinecartDepots(targetPosition + new Vector3Int(x * CurrentBlueprintSize.x, y * CurrentBlueprintSize.y, z * CurrentBlueprintSize.z), existingMinecartDepots);
                    }
                }
            }

            return existingMinecartDepots;
        }

        internal bool HasExistingMinecartDepots(Vector3Int targetPosition, Vector3Int repeatFrom, Vector3Int repeatTo)
        {
            for (int y = repeatFrom.y; y <= repeatTo.y; ++y)
            {
                for (int z = repeatFrom.z; z <= repeatTo.z; ++z)
                {
                    for (int x = repeatFrom.x; x <= repeatTo.x; ++x)
                    {
                        if (CurrentBlueprint.HasExistingMinecartDepots(targetPosition + new Vector3Int(x * CurrentBlueprintSize.x, y * CurrentBlueprintSize.y, z * CurrentBlueprintSize.z))) return true;
                    }
                }
            }

            return false;
        }

        public void SetPlaceholderOpacity(float alpha)
        {
            placeholderRenderGroup.SetAlpha(alpha);

            for (int i = 0; i < BlueprintPlaceholder.stateColours.Length; i++)
            {
                BlueprintPlaceholder.stateColours[i].a = alpha;
            }
        }

        internal void LoadBlueprintFromFile(string path)
        {
            CurrentBlueprint = Blueprint.LoadFromFile(path);
        }

        internal enum BoxMode
        {
            None,
            Selection,
            Blueprint
        }
    }

    public static class UIExtensions
    {
        public static UIBuilder SetSizeDelta(this UIBuilder builder, float width, float height)
        {
            RectTransform transform = (RectTransform)builder.GameObject.transform;
            transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            return builder;
        }
    }

    public class EscapeCloseProxy : IEscapeCloseable
    {
        public delegate void EscapeCloseDelegate();
        private readonly EscapeCloseDelegate _onClose;

        public EscapeCloseProxy(EscapeCloseDelegate onClose)
        {
            _onClose = onClose;
        }

        public void iec_triggerFrameClose()
        {
            _onClose?.Invoke();
        }

        public static EscapeCloseProxy Register(EscapeCloseDelegate onClose)
        {
            var iec = new EscapeCloseProxy(onClose);
            GlobalStateManager.registerEscapeCloseable(iec);
            return iec;
        }

        internal void Dispose()
        {
            GlobalStateManager.deRegisterEscapeCloseable(this);
        }
    }
}
