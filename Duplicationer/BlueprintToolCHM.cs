using System.Collections.Generic;
using UnityEngine;
using Unfoundry;
using System.Linq;
using System.IO;

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
        public readonly BlueprintToolModePlace modePlace;
        public readonly BlueprintToolModeSelectArea modeSelectArea;
        public readonly BlueprintToolModeResize modeResize;
        public readonly BlueprintToolModeResizeVertical modeResizeVertical;
        public readonly BlueprintToolModeMove modeMove;
        public readonly BlueprintToolModeMoveSideways modeMoveSideways;
        public readonly BlueprintToolModeMoveVertical modeMoveVertical;
        public readonly BlueprintToolModeRepeat modeRepeat;
        private BlueprintToolMode[] _blueprintToolModes;

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
        internal bool isDragArrowDouble = false;

        private BlueprintFrame _blueprintFrame;
        private SaveFrame _saveFrame;
        private LibraryFrame _libraryFrame;
        private FolderFrame _folderFrame;
        private List<BaseFrame> _frames = new List<BaseFrame>();

        public bool IsBlueprintFrameOpen => _blueprintFrame.IsOpen;
        public bool IsSaveFrameOpen => _saveFrame.IsOpen;
        public bool IsLibraryFrameOpen => _libraryFrame.IsOpen;
        public bool IsFolderFrameOpen => _folderFrame.IsOpen;
        public bool IsAnyFrameOpen => _frames.Any(x => x.IsOpen);

        internal int NudgeX => CurrentBlueprint != null && InputHelpers.IsAltHeld ? CurrentBlueprint.SizeX : 1;
        internal int NudgeY => CurrentBlueprint != null && InputHelpers.IsAltHeld ? CurrentBlueprint.SizeY : 1;
        internal int NudgeZ => CurrentBlueprint != null && InputHelpers.IsAltHeld ? CurrentBlueprint.SizeZ : 1;

        private static List<BuildableObjectGO> _bogoQueryResult = new List<BuildableObjectGO>(0);

        private static List<ConstructionTaskGroup> _activeConstructionTaskGroups = new List<ConstructionTaskGroup>();

        private static List<bool> _terrainTypeRemovalMask = null;

        internal LazyPrefab prefabGridScrollView = new LazyPrefab("GridScrollView");
        internal LazyPrefab prefabBlueprintNameInputField = new LazyPrefab("BlueprintNameInputField");
        internal LazyPrefab prefabBlueprintButtonDefaultIcon = new LazyPrefab("BlueprintButtonDefaultIcon");
        internal LazyPrefab prefabBlueprintButtonIcon = new LazyPrefab("BlueprintButtonIcon");
        internal LazyPrefab prefabBlueprintButton1Icon = new LazyPrefab("BlueprintButton1Icon");
        internal LazyPrefab prefabBlueprintButton2Icon = new LazyPrefab("BlueprintButton2Icon");
        internal LazyPrefab prefabBlueprintButton3Icon = new LazyPrefab("BlueprintButton3Icon");
        internal LazyPrefab prefabBlueprintButton4Icon = new LazyPrefab("BlueprintButton4Icon");
        internal LazyPrefab prefabBlueprintButtonFolder = new LazyPrefab("BlueprintButtonFolder");
        internal LazyPrefab prefabBlueprintButtonFolderNew = new LazyPrefab("BlueprintButtonFolderNew");
        internal LazyPrefab prefabBlueprintButtonFolderBack = new LazyPrefab("BlueprintButtonFolderBack");

        private static Material _placeholderMaterial = null;
        private static Material _placeholderPrepassMaterial = null;
        private static Material _placeholderSolidMaterial = null;

        internal LazyIconSprite iconBlack = null;
        internal LazyIconSprite iconEmpty = null;
        internal LazyIconSprite iconCopy = null;
        internal LazyIconSprite iconCopyInPlace = null;
        internal LazyIconSprite iconMoveVertical = null;
        internal LazyIconSprite iconMove = null;
        internal LazyIconSprite iconMoveSideways = null;
        internal LazyIconSprite iconPanel = null;
        internal LazyIconSprite iconPaste = null;
        internal LazyIconSprite iconPlace = null;
        internal LazyIconSprite iconRepeat = null;
        internal LazyIconSprite iconResizeVertical = null;
        internal LazyIconSprite iconResize = null;
        internal LazyIconSprite iconSelectArea = null;
        internal LazyIconSprite iconMirror = null;

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
            _frames.Add(_blueprintFrame = new BlueprintFrame(this));
            _frames.Add(_saveFrame = new SaveFrame(this));
            _frames.Add(_libraryFrame = new LibraryFrame(this));
            _frames.Add(_folderFrame = new FolderFrame(this));

            dragArrowScale = 1.0f;
            dragArrowOffset = 0.5f;

            _blueprintToolModes = new BlueprintToolMode[]
            {
                modeSelectArea = new BlueprintToolModeSelectArea(),
                modeResize = new BlueprintToolModeResize(),
                modeResizeVertical = new BlueprintToolModeResizeVertical(),
                modePlace = new BlueprintToolModePlace(),
                modeMove = new BlueprintToolModeMove(),
                modeMoveSideways = new BlueprintToolModeMoveSideways(),
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
                        "Confirm Copy In Place", iconCopyInPlace.Sprite, "",
                        CopySelectionInPlace,
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
                        "Select Area", iconSelectArea.Sprite, "Clears current blueprint",
                        () => SelectMode(modeSelectArea)),

                    new CustomRadialMenuOption(
                        "Move", iconMove.Sprite, "",
                        () => SelectMode(modeMove),
                        () => CurrentMode != null && CurrentMode.AllowPaste(this)),

                    new CustomRadialMenuOption(
                        "Move Sideways", iconMoveSideways.Sprite, "",
                        () => SelectMode(modeMoveSideways),
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

        internal void CopySelection()
        {
            ClearBlueprintPlaceholders();
            CurrentBlueprint = Blueprint.Create(DragMin, DragSize);
            TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Place Blueprint");
            isDragArrowVisible = false;
            SelectMode(modePlace);
            boxMode = BoxMode.None;
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_recipeCopyTool_copy);
        }

        internal void CopySelectionInPlace()
        {
            ClearBlueprintPlaceholders();
            CurrentBlueprint = Blueprint.Create(DragMin, DragSize);
            isDragArrowVisible = false;
            SelectMode(modeRepeat);
            repeatFrom = repeatTo = Vector3Int.zero;
            boxMode = BoxMode.Blueprint;
            ShowBlueprint(DragMin);
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

                            _bogoQueryResult.Clear();
                            quadTree.queryAABB3D(aabb, _bogoQueryResult, true);
                            foreach (var bogo in _bogoQueryResult)
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
                _blueprintFrame.UpdateMaterialReport();

                int remainingTasks = 0;
                foreach (var group in _activeConstructionTaskGroups) remainingTasks += group.Remaining;
                if (remainingTasks > 0) CurrentBlueprintStatusText = $"ToDo: {remainingTasks}{(CurrentBlueprintStatusText.Length > 0 ? "\n" : "")}{CurrentBlueprintStatusText}";

                if (!IsPlaceholdersHidden)
                {
                    if (_placeholderMaterial == null)
                    {
                        _placeholderMaterial = DuplicationerPlugin.GetAsset<Material>("PlaceholderMaterial");
                        Object.DontDestroyOnLoad(_placeholderMaterial);
                    }

                    if (_placeholderPrepassMaterial == null)
                    {
                        _placeholderPrepassMaterial = DuplicationerPlugin.GetAsset<Material>("PlaceholderPrepassMaterial");
                        Object.DontDestroyOnLoad(_placeholderPrepassMaterial);
                    }

                    if (_placeholderSolidMaterial == null)
                    {
                        _placeholderSolidMaterial = DuplicationerPlugin.GetAsset<Material>("PlaceholderSolidMaterial");
                        Object.DontDestroyOnLoad(_placeholderSolidMaterial);
                    }

                    float alpha = DuplicationerPlugin.configPreviewAlpha.Get();
                    if (alpha > 0.0f)
                    {
                        if (alpha < 1.0f)
                        {
                            if (_placeholderMaterial != null && _placeholderPrepassMaterial != null)
                            {
                                placeholderRenderGroup.Render(_placeholderPrepassMaterial, _placeholderMaterial);
                            }
                        }
                        else
                        {
                            if (_placeholderSolidMaterial != null)
                            {
                                placeholderRenderGroup.Render(null, _placeholderSolidMaterial);
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
                if (isDragArrowDouble) DrawArrow(dragFaceRay.origin, -dragFaceRay.direction, dragArrowMaterial, dragArrowScale, dragArrowOffset);
            }

            foreach (var frame in _frames) frame.Update();
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
            var constructionTaskGroup = new ConstructionTaskGroup((ConstructionTaskGroup taskGroup) => { _activeConstructionTaskGroups.Remove(taskGroup); });
            _activeConstructionTaskGroups.Add(constructionTaskGroup);
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

            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_construction);
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
            _blueprintFrame.UpdateBlueprintPositionText();
        }

        private string GetTabletTitle()
        {
            return CurrentMode?.TabletTitle(this) ?? "";
        }

        internal void BeginSaveBlueprint()
        {
            HideBlueprintFrame(true);
            ShowSaveFrame();
        }

        internal void FinishSaveBlueprint()
        {
            if (CurrentBlueprint == null) throw new System.ArgumentNullException(nameof(CurrentBlueprint));

            string name = _saveFrame.BlueprintName;
            if (string.IsNullOrWhiteSpace(name)) return;

            string filenameBase = Path.Combine(Path.GetDirectoryName(name), PathHelpers.MakeValidFileName(Path.GetFileName(name)));
            string path = Path.Combine(DuplicationerPlugin.BlueprintFolder, $"{filenameBase}.{DuplicationerPlugin.BlueprintExtension}");
            if (File.Exists(path))
            {
                ConfirmationFrame.Show($"Overwrite '{name}'?", "Overwrite", () =>
                {
                    DuplicationerPlugin.log.Log($"Saving blueprint '{name}' to '{path}'");
                    CurrentBlueprint.Save(path, Path.GetFileName(name), _saveFrame.IconItemTemplates.Take(_saveFrame.IconCount).ToArray());

                    HideSaveFrame();
                });
            }
            else
            {
                DuplicationerPlugin.log.Log($"Saving blueprint '{name}' to '{path}'");
                CurrentBlueprint.Save(path, Path.GetFileName(name), _saveFrame.IconItemTemplates.Take(_saveFrame.IconCount).ToArray());

                HideSaveFrame();
            }
        }

        internal void BeginLoadBlueprint()
        {
            HideBlueprintFrame(true);
            ShowLibraryFrame();
        }

        internal void HideBlueprintFrame(bool silent = false)
        {
            _blueprintFrame.Hide(silent);
        }

        internal void ShowBlueprintFrame()
        {
            _blueprintFrame.Show();
        }

        internal void HideSaveFrame(bool silent = false)
        {
            _saveFrame.Hide(silent);
        }

        internal void ShowSaveFrame()
        {
            _saveFrame.Show();
        }

        internal void HideLibraryFrame(bool silent = false)
        {
            _libraryFrame.Hide(silent);
        }

        internal void ShowLibraryFrame(SaveFrame saveFrame = null)
        {
            _libraryFrame.Show(saveFrame);
        }

        internal void FillLibraryGrid(string relativePath, SaveFrame saveFrame = null)
        {
            _libraryFrame.FillLibraryGrid(relativePath, saveFrame);
        }

        internal void HideFolderFrame(bool silent = false)
        {
            _folderFrame.Hide(silent);
        }

        internal void ShowFolderFrame(string relativePath, string folderName)
        {
            _folderFrame.Show(relativePath, folderName);
        }

        private static List<bool> GetTerrainTypeRemovalMask()
        {
            if (_terrainTypeRemovalMask == null)
            {
                var terrainTypes = ItemTemplateManager.getAllTerrainTemplates();

                _terrainTypeRemovalMask = new List<bool>
                {
                    false, // Air
                    false // ???
                };

                foreach (var terrainType in terrainTypes)
                {
                    _terrainTypeRemovalMask.Add(terrainType.Value.destructible);
                }
            }

            return _terrainTypeRemovalMask;
        }

        internal void DestroyArea(bool doBuildings, bool doBlocks, bool doTerrain, bool doDecor)
        {
            if (TryGetSelectedArea(out Vector3Int from, out Vector3Int to))
            {
                ulong characterHash = GameRoot.getClientCharacter().usernameHash;

                if (doBuildings || doDecor)
                {
                    AABB3D aabb = ObjectPoolManager.aabb3ds.getObject();
                    aabb.reinitialize(from.x, from.y, from.z, to.x - from.x + 1, to.y - from.y + 1, to.z - from.z + 1);
                    _bogoQueryResult.Clear();
                    StreamingSystem.getBuildableObjectGOQuadtreeArray().queryAABB3D(aabb, _bogoQueryResult, true);
                    if (_bogoQueryResult.Count > 0)
                    {
                        foreach (var bogo in _bogoQueryResult)
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

        internal void DemolishArea(bool doBuildings, bool doBlocks, bool doTerrain, bool doDecor)
        {
            if (TryGetSelectedArea(out Vector3Int from, out Vector3Int to))
            {
                ulong characterHash = GameRoot.getClientCharacter().usernameHash;

                if (doBuildings || doDecor)
                {
                    AABB3D aabb = ObjectPoolManager.aabb3ds.getObject();
                    aabb.reinitialize(from.x, from.y, from.z, to.x - from.x + 1, to.y - from.y + 1, to.z - from.z + 1);
                    _bogoQueryResult.Clear();
                    StreamingSystem.getBuildableObjectGOQuadtreeArray().queryAABB3D(aabb, _bogoQueryResult, true);
                    if (_bogoQueryResult.Count > 0)
                    {
                        foreach (var bogo in _bogoQueryResult)
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

        internal void LoadIconSprites()
        {
            iconBlack = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "black");
            iconEmpty = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "empty");
            iconCopy = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "copy");
            iconCopyInPlace = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "copy-in-place");
            iconMove = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "move");
            iconMoveSideways = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "move-sideways");
            iconMoveVertical = new LazyIconSprite(DuplicationerPlugin.bundleMainAssets, "move-vertical");
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

            _activeConstructionTaskGroups.Clear();

            _bogoQueryResult.Clear();
        }

        public void SetPlaceholderOpacity(float alpha)
        {
            placeholderRenderGroup.SetAlpha(alpha);

            for (int i = 0; i < BlueprintPlaceholder.stateColours.Length; i++)
            {
                BlueprintPlaceholder.stateColours[i].a = alpha;
            }
        }

        internal void ClearBlueprint()
        {
            ClearBlueprintPlaceholders();
            CurrentBlueprint = null;
            IsBlueprintActive = false;
        }

        internal void LoadBlueprintFromFile(string path)
        {
            CurrentBlueprint = Blueprint.LoadFromFile(path);
        }

        internal void ClearBlueprintRecipes()
        {
            if (CurrentBlueprint == null) return;

            CurrentBlueprint.ClearRecipes();
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);
        }

        internal void RemoveItemFromBlueprint(ItemTemplate template)
        {
            if (CurrentBlueprint == null) return;

            CurrentBlueprint.RemoveItem(template);
            ClearBlueprintPlaceholders();
            ShowBlueprint(CurrentBlueprintAnchor);
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_bulkDemolishObjects);
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
}
