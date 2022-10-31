using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Duplicationer.BepInExLoader;
using static Duplicationer.BlueprintManager;

namespace Duplicationer
{
    internal class BlueprintToolCHM : CustomHandheldMode
    {
        private static CustomRadialMenuStateControl menuStateControl = null;
        private static Mode mode = Mode.Place;

        private static BoxMode boxMode = BoxMode.None;
        private static Vector3Int BlueprintMin => CurrentBlueprintAnchor;
        private static Vector3Int BlueprintMax => CurrentBlueprintAnchor + CurrentBlueprintSize - Vector3Int.one;

        private static Vector3Int repeatFrom = Vector3Int.zero;
        private static Vector3Int repeatTo = Vector3Int.zero;
        private static Vector3Int RepeatCount => repeatTo - repeatFrom + Vector3Int.one;
        private static Vector3Int RepeatBlueprintMin => BlueprintMin + CurrentBlueprintSize * repeatFrom;
        private static Vector3Int RepeatBlueprintMax => BlueprintMax + CurrentBlueprintSize * repeatTo;

        private static Plane dragPlane = default;
        private static Vector3Int selectionFrom = Vector3Int.zero;
        private static Vector3Int selectionTo = Vector3Int.zero;

        private static Vector3Int DragMin => Vector3Int.Min(selectionFrom, selectionTo);
        private static Vector3Int DragMax => Vector3Int.Max(selectionFrom, selectionTo);
        private static Vector3Int DragSize => DragMax - DragMin + Vector3Int.one;

        private static bool isDragArrowVisible = false;
        private static Ray dragFaceRay = default;
        private Material dragArrowMaterial = null;
        private float dragArrowScale = 1.0f;
        private float dragArrowOffset = 0.5f;

        private static GameObject duplicationerFrame = null;
        private static TextMeshProUGUI textMaterialReport = null;
        private static TextMeshProUGUI textPositionX = null;
        private static TextMeshProUGUI textPositionY = null;
        private static TextMeshProUGUI textPositionZ = null;
        private static float nextUpdateTimeCountTexts = 0.0f;

        private static int NudgeX => GlobalStateManager.checkForKeyboardModifier(0, 1) ? CurrentBlueprint.blocks.sizeX : 1;
        private static int NudgeY => GlobalStateManager.checkForKeyboardModifier(0, 1) ? CurrentBlueprint.blocks.sizeY : 1;
        private static int NudgeZ => GlobalStateManager.checkForKeyboardModifier(0, 1) ? CurrentBlueprint.blocks.sizeZ : 1;

        private static Sprite iconCopy = null;
        private static Sprite iconMoveVertical = null;
        private static Sprite iconMove = null;
        private static Sprite iconPanel = null;
        private static Sprite iconPaste = null;
        private static Sprite iconPlace = null;
        private static Sprite iconRepeat = null;
        private static Sprite iconResizeVertical = null;
        private static Sprite iconResize = null;
        private static Sprite iconSelectArea = null;

        private static LazyMaterial materialDragBox = new LazyMaterial(() =>
        {
            var material = new Material(ResourceDB.material_placeholder_green);
            material.renderQueue = 3001;
            material.SetFloat("_Opacity", 0.25f);
            material.SetColor("_Color", Color.blue.AlphaMultiplied(0.25f));
            return material;
        });

        private static LazyMaterial materialDragBoxEdge = new LazyMaterial(() =>
        {
            var material = new Material(ResourceDB.material_glow_blue);
            material.SetColor("_Color", new Color(0.0f, 0.0f, 0.8f, 1.0f));
            return material;
        });

        public BlueprintToolCHM()
        {
            dragArrowScale = 1.0f;
            dragArrowOffset = 0.5f;
        }

        public override void Enter()
        {
            switch (mode)
            {
                case Mode.Place:
                    TabletHelper.SetTabletTextQuickActions("LMB: Place Blueprint");
                    break;

                case Mode.MoveIdle:
                case Mode.VerticalMoveIdle:
                case Mode.RepeatIdle:
                    TabletHelper.SetTabletTextQuickActions("");
                    ShowBlueprint(CurrentBlueprintAnchor);
                    break;

                case Mode.DragAreaIdle:
                    TabletHelper.SetTabletTextQuickActions("LMB: Select Area");
                    break;

                case Mode.DragFacesIdle:
                case Mode.DragFacesVerticalIdle:
                    TabletHelper.SetTabletTextQuickActions("");
                    boxMode = BoxMode.Selection;
                    break;
            }

            onBlueprintMoved += OnBlueprintMoved;
            onBlueprintUpdated += OnBlueprintUpdated;
        }

        public override void Exit()
        {
            HideBlueprint();
            boxMode = BoxMode.None;
            isDragArrowVisible = false;
            HideBlueprintFrame();
            onBlueprintMoved -= OnBlueprintMoved;
        }

        public override void HideMenu(int selected)
        {
        }

        public override void ShowMenu()
        {
            if (menuStateControl == null)
            {
                menuStateControl = new CustomRadialMenuStateControl(
                    new CustomRadialMenu.CustomOption("Confirm Copy", iconCopy, false, "", () =>
                    {
                        CreateBlueprint(DragMin, DragSize);
                        isDragArrowVisible = false;
                        mode = Mode.Place;
                        boxMode = BoxMode.None;
                    }, () => boxMode == BoxMode.Selection),
                    new CustomRadialMenu.CustomOption("Confirm Paste", iconPaste, false, "", () =>
                    {
                        isDragArrowVisible = false;
                        PlaceBlueprintMultiple(CurrentBlueprintAnchor, repeatFrom, repeatTo);
                    }, () => isBlueprintLoaded && isPlaceholdersActive),
                    new CustomRadialMenu.CustomOption("Place", iconPlace, false, "", () =>
                    {
                        isDragArrowVisible = false;
                        mode = Mode.Place;
                        boxMode = BoxMode.None;
                    }, () => isBlueprintLoaded),
                    new CustomRadialMenu.CustomOption("Select Area", iconSelectArea, false, "", () =>
                    {
                        mode = Mode.DragAreaIdle;
                        boxMode = BoxMode.None;
                        HideBlueprint();
                    }),
                    new CustomRadialMenu.CustomOption("Move", iconMove, false, "", () =>
                    {
                        isDragArrowVisible = false;
                        mode = Mode.MoveIdle;
                    }, () => isBlueprintLoaded && isPlaceholdersActive),
                    new CustomRadialMenu.CustomOption("Move Vertical", iconMoveVertical, false, "", () =>
                    {
                        isDragArrowVisible = false;
                        mode = Mode.VerticalMoveIdle;
                    }, () => isBlueprintLoaded && isPlaceholdersActive),
                    new CustomRadialMenu.CustomOption("Resize", iconResize, false, "", () => { mode = Mode.DragFacesIdle; }, () => boxMode == BoxMode.Selection),
                    new CustomRadialMenu.CustomOption("Resize Vertical", iconResizeVertical, false, "", () => { mode = Mode.DragFacesVerticalIdle; }, () => boxMode == BoxMode.Selection),
                    new CustomRadialMenu.CustomOption("Repeat", iconRepeat, false, "", () =>
                    {
                        isDragArrowVisible = false;
                        mode = Mode.RepeatIdle;
                    }, () => isBlueprintLoaded && isPlaceholdersActive),
                    new CustomRadialMenu.CustomOption("Show Placeholders", ResourceDB.sprite_waypointShow, false, "", () => { ShowPlaceholders(); }, () => isPlaceholdersHidden),
                    new CustomRadialMenu.CustomOption("Hide Placeholders", ResourceDB.sprite_waypointHide, false, "", () => { HidePlaceholders(); }, () => !isPlaceholdersHidden),
                    new CustomRadialMenu.CustomOption("Open Panel", iconPanel, false, "", () =>
                    {
                        ShowBlueprintFrame();
                    })
                );
            }

            CustomRadialMenu.ShowMenu(menuStateControl.GetMenuOptions());
        }

        public override void UpdateBehavoir()
        {
            switch (mode)
            {
                case Mode.Place:
                    if (Input.GetKeyDown(KeyCode.Mouse0))
                    {
                        boxMode = BoxMode.None;
                        HideBlueprint();
                    }
                    if (Input.GetKey(KeyCode.Mouse0))
                    {
                        repeatFrom = repeatTo = Vector3Int.zero;
                        boxMode = BoxMode.Blueprint;
                        Vector3 targetPoint;
                        Vector3Int targetCoord, targetNormal;
                        if (GetTargetCube(0.01f, out targetPoint, out targetCoord, out targetNormal))
                        {
                            ShowBlueprint(targetCoord - new Vector3Int(CurrentBlueprint.blocks.sizeX / 2, 0, CurrentBlueprint.blocks.sizeZ / 2));
                        }
                    }
                    if (Input.GetKeyUp(KeyCode.Mouse0) && isPlaceholdersActive)
                    {
                        mode = Mode.MoveIdle;
                        TabletHelper.SetTabletTextQuickActions("");
                    }
                    break;

                case Mode.MoveIdle:
                    {
                        boxMode = BoxMode.Blueprint;
                        var lookRay = GetLookRay();
                        Vector3Int normal;
                        int faceIndex;
                        float distance = BoxRayIntersection(RepeatBlueprintMin, RepeatBlueprintMax + Vector3Int.one, lookRay, out normal, out faceIndex);
                        if (distance >= 0.0f)
                        {
                            var point = lookRay.GetPoint(distance);
                            isDragArrowVisible = true;
                            dragFaceRay = new Ray(point, normal);
                            dragArrowOffset = 0.5f;
                            switch (faceIndex)
                            {
                                case 0:
                                    dragArrowMaterial = ResourceDB.material_glow_red;
                                    TabletHelper.SetTabletTextQuickActions($"LMB: Drag X\nAlt+LMB: Drag X*{CurrentBlueprint.blocks.sizeX}");
                                    break;

                                case 1:
                                    dragArrowMaterial = ResourceDB.material_glow_red;
                                    TabletHelper.SetTabletTextQuickActions($"LMB: Drag X\nAlt+LMB: Drag X*{CurrentBlueprint.blocks.sizeX}");
                                    break;

                                case 2:
                                    dragArrowMaterial = ResourceDB.material_glow_yellow;
                                    TabletHelper.SetTabletTextQuickActions($"LMB: Drag Y\nAlt+LMB: Drag Y*{CurrentBlueprint.blocks.sizeY}");
                                    break;

                                case 3:
                                    dragArrowMaterial = ResourceDB.material_glow_yellow;
                                    TabletHelper.SetTabletTextQuickActions($"LMB: Drag Y\nAlt+LMB: Drag Y*{CurrentBlueprint.blocks.sizeY}");
                                    break;

                                case 4:
                                    dragArrowMaterial = ResourceDB.material_glow_purple;
                                    TabletHelper.SetTabletTextQuickActions($"LMB: Drag Z\nAlt+LMB: Drag Z*{CurrentBlueprint.blocks.sizeZ}");
                                    break;

                                case 5:
                                    dragArrowMaterial = ResourceDB.material_glow_purple;
                                    TabletHelper.SetTabletTextQuickActions($"LMB: Drag Z\nAlt+LMB: Drag Z*{CurrentBlueprint.blocks.sizeZ}");
                                    break;
                            }

                            if (Input.GetKeyDown(KeyCode.Mouse0))
                            {
                                mode = Mode.MoveXPos + faceIndex;
                            }
                        }
                        else
                        {
                            isDragArrowVisible = false;
                            TabletHelper.SetTabletTextQuickActions("");
                        }
                    }
                    break;

                case Mode.MoveXPos:
                case Mode.MoveXNeg:
                case Mode.MoveYPos:
                case Mode.MoveYNeg:
                case Mode.MoveZPos:
                case Mode.MoveZNeg:
                    if (!Input.GetKey(KeyCode.Mouse0))
                    {
                        mode = Mode.MoveIdle;
                    }
                    else
                    {
                        float offset;
                        if (TryGetAxialDragOffset(dragFaceRay, GetLookRay(), out offset))
                        {
                            int direction = mode - Mode.MoveXPos;
                            int axis = direction >> 1;
                            int dragStep =  IsAltHeld ? CurrentBlueprintSize[axis] : 1;
                            var roundedOffset = Mathf.RoundToInt(offset / dragStep) * dragStep;
                            if (Mathf.Abs(roundedOffset) >= dragStep)
                            {
                                var offsetVector = faceNormals[direction] * roundedOffset;
                                ShowBlueprint(CurrentBlueprintAnchor + offsetVector);

                                dragFaceRay.origin += faceNormals[direction] * roundedOffset;
                            }
                        }
                    }
                    break;

                case Mode.VerticalMoveIdle:
                    {
                        boxMode = BoxMode.Blueprint;
                        var lookRay = GetLookRay();
                        Vector3Int normal;
                        int faceIndex;
                        float distance = BoxRayIntersection(RepeatBlueprintMin, RepeatBlueprintMax + Vector3Int.one, lookRay, out normal, out faceIndex);
                        if (distance >= 0.0f)
                        {
                            var point = lookRay.GetPoint(distance) + (Vector3)normal * 0.5f;
                            isDragArrowVisible = true;
                            dragFaceRay = new Ray(point, Vector3.up);
                            dragArrowMaterial = ResourceDB.material_glow_yellow;
                            dragArrowOffset = 0.0f;
                            TabletHelper.SetTabletTextQuickActions($"LMB: Drag Y\nAlt+LMB: Drag Y*{CurrentBlueprint.blocks.sizeY}");

                            if (Input.GetKeyDown(KeyCode.Mouse0)) mode = Mode.VerticalMove;
                        }
                        else
                        {
                            isDragArrowVisible = false;
                            TabletHelper.SetTabletTextQuickActions("");
                        }
                    }
                    break;

                case Mode.VerticalMove:
                    if (!Input.GetKey(KeyCode.Mouse0))
                    {
                        mode = Mode.VerticalMoveIdle;
                    }
                    else
                    {
                        float offset;
                        if (TryGetAxialDragOffset(dragFaceRay, GetLookRay(), out offset))
                        {
                            int dragStep = IsAltHeld ? CurrentBlueprintSize.y : 1;
                            var roundedOffset = Mathf.RoundToInt(offset / dragStep) * dragStep;
                            if (Mathf.Abs(roundedOffset) >= dragStep)
                            {
                                var offsetVector = Vector3Int.up * roundedOffset;
                                ShowBlueprint(CurrentBlueprintAnchor + offsetVector);

                                dragFaceRay.origin += Vector3Int.up * roundedOffset;
                            }
                        }
                    }
                    break;

                case Mode.RepeatIdle:
                    {
                        boxMode = BoxMode.Blueprint;
                        var lookRay = GetLookRay();
                        Vector3Int normal;
                        int faceIndex;
                        float distance = BoxRayIntersection(RepeatBlueprintMin, RepeatBlueprintMax + Vector3Int.one, lookRay, out normal, out faceIndex);
                        if (distance >= 0.0f)
                        {
                            var point = lookRay.GetPoint(distance);
                            isDragArrowVisible = true;
                            dragFaceRay = new Ray(point, normal);
                            dragArrowOffset = 0.5f;
                            switch (faceIndex)
                            {
                                case 0:
                                    dragArrowMaterial = ResourceDB.material_glow_red;
                                    TabletHelper.SetTabletTextQuickActions($"LMB: Drag +X*{CurrentBlueprint.blocks.sizeX}");
                                    break;

                                case 1:
                                    dragArrowMaterial = ResourceDB.material_glow_red;
                                    TabletHelper.SetTabletTextQuickActions($"LMB: Drag -X*{CurrentBlueprint.blocks.sizeX}");
                                    break;

                                case 2:
                                    dragArrowMaterial = ResourceDB.material_glow_yellow;
                                    TabletHelper.SetTabletTextQuickActions($"LMB: Drag +Y*{CurrentBlueprint.blocks.sizeY}");
                                    break;

                                case 3:
                                    dragArrowMaterial = ResourceDB.material_glow_yellow;
                                    TabletHelper.SetTabletTextQuickActions($"LMB: Drag -Y*{CurrentBlueprint.blocks.sizeY}");
                                    break;

                                case 4:
                                    dragArrowMaterial = ResourceDB.material_glow_purple;
                                    TabletHelper.SetTabletTextQuickActions($"LMB: Drag +Z*{CurrentBlueprint.blocks.sizeZ}");
                                    break;

                                case 5:
                                    dragArrowMaterial = ResourceDB.material_glow_purple;
                                    TabletHelper.SetTabletTextQuickActions($"LMB: Drag -Z*{CurrentBlueprint.blocks.sizeZ}");
                                    break;
                            }

                            if (Input.GetKeyDown(KeyCode.Mouse0))
                            {
                                mode = Mode.RepeatXPos + faceIndex;
                            }
                        }
                        else
                        {
                            isDragArrowVisible = false;
                            TabletHelper.SetTabletTextQuickActions("");
                        }
                    }
                    break;

                case Mode.RepeatXPos:
                case Mode.RepeatXNeg:
                case Mode.RepeatYPos:
                case Mode.RepeatYNeg:
                case Mode.RepeatZPos:
                case Mode.RepeatZNeg:
                    if (!Input.GetKey(KeyCode.Mouse0))
                    {
                        mode = Mode.RepeatIdle;
                    }
                    else
                    {
                        float offset;
                        if (TryGetAxialDragOffset(dragFaceRay, GetLookRay(), out offset))
                        {
                            int direction = mode - Mode.RepeatXPos;
                            bool positive = (direction & 0x1) == 0;
                            int axis = direction >> 1;
                            int dragStep = CurrentBlueprintSize[axis];
                            var roundedOffset = Mathf.RoundToInt(offset / dragStep);
                            if (Mathf.Abs(roundedOffset) >= 1)
                            {
                                var offsetVector = faceNormals[direction] * roundedOffset;

                                if (positive)
                                {
                                    repeatTo = Vector3Int.Max(Vector3Int.zero, repeatTo + offsetVector);
                                }
                                else
                                {
                                    repeatFrom = Vector3Int.Min(Vector3Int.zero, repeatFrom + offsetVector);
                                }

                                dragFaceRay.origin += faceNormals[direction] * (roundedOffset * dragStep);
                            }
                        }
                    }
                    break;

                case Mode.DragAreaIdle:
                    isDragArrowVisible = false;
                    if (Input.GetKeyDown(KeyCode.Mouse0))
                    {
                        Vector3 targetPoint;
                        Vector3Int targetCoord, targetNormal;
                        if (GetTargetCube(-0.01f, out targetPoint, out targetCoord, out targetNormal))
                        {
                            boxMode = BoxMode.Selection;
                            selectionFrom = selectionTo = targetCoord;
                            dragPlane = new Plane(Vector3.up, targetPoint);

                            mode = Mode.DragAreaStart;
                            TabletHelper.SetTabletTextQuickActions("Release LMB: Select Other Corner");
                        }
                    }
                    break;

                case Mode.DragAreaStart:
                    if (Input.GetKeyDown(KeyCode.Mouse0) || Input.GetKeyUp(KeyCode.Mouse0) && selectionFrom != selectionTo)
                    {
                        var lookRay = GetLookRay();
                        float distance;
                        if (dragPlane.Raycast(lookRay, out distance))
                        {
                            var point = lookRay.GetPoint(distance);
                            isDragArrowVisible = true;
                            dragFaceRay = new Ray(point, Vector3.up);
                            dragArrowMaterial = ResourceDB.material_glow_yellow;

                            mode = Mode.DragAreaVertical;
                            TabletHelper.SetTabletTextQuickActions("LMB: Select Height");
                        }
                        else
                        {
                            mode = Mode.DragAreaIdle;
                            TabletHelper.SetTabletTextQuickActions("LMB: Select Area");
                        }
                    }
                    else
                    {
                        if (Input.GetKeyUp(KeyCode.Mouse0))
                        {
                            TabletHelper.SetTabletTextQuickActions("LMB: Select Other Corner");
                        }

                        var lookRay = GetLookRay();
                        float distance;
                        if (dragPlane.Raycast(lookRay, out distance))
                        {
                            var point = lookRay.GetPoint(distance);
                            selectionTo = new Vector3Int(Mathf.FloorToInt(point.x - dragPlane.normal.x * 0.01f), Mathf.FloorToInt(point.y - dragPlane.normal.y * 0.01f), Mathf.FloorToInt(point.z - dragPlane.normal.z * 0.01f));
                        }
                    }
                    break;

                case Mode.DragAreaVertical:
                    if (Input.GetKeyDown(KeyCode.Mouse0))
                    {
                        mode = Mode.DragFacesIdle;
                        TabletHelper.SetTabletTextQuickActions("");
                    }
                    else
                    {
                        float offset;
                        if (TryGetAxialDragOffset(dragFaceRay, GetLookRay(), out offset))
                        {
                            var min = DragMin;
                            var max = DragMax;
                            var roundedOffset = Mathf.Max(min.y - max.y, Mathf.RoundToInt(offset));
                            if (Mathf.Abs(roundedOffset) >= 1)
                            {
                                max.y += roundedOffset;

                                dragFaceRay.origin += Vector3.up * roundedOffset;
                                selectionFrom = min;
                                selectionTo = max;
                            }
                        }
                    }
                    break;

                case Mode.DragFacesIdle:
                    {
                        var lookRay = GetLookRay();
                        Vector3Int normal;
                        int faceIndex;
                        float distance = BoxRayIntersection(DragMin, DragMax + Vector3Int.one, lookRay, out normal, out faceIndex);
                        if (distance >= 0.0f)
                        {
                            var point = lookRay.GetPoint(distance);
                            isDragArrowVisible = true;
                            if (IsAltHeld)
                            {
                                mode = Mode.DragFacesXPos + (faceIndex ^ 1);
                                dragFaceRay = new Ray(point, -normal);
                                dragArrowOffset = -0.5f;
                            }
                            else
                            {
                                mode = Mode.DragFacesXPos + faceIndex;
                                dragFaceRay = new Ray(point, normal);
                                dragArrowOffset = 0.5f;
                            }
                            switch (faceIndex)
                            {
                                case 0:
                                    dragArrowMaterial = ResourceDB.material_glow_red;
                                    TabletHelper.SetTabletTextQuickActions("LMB: Drag +X\nAlt+LMB: Drag -X");
                                    break;

                                case 1:
                                    dragArrowMaterial = ResourceDB.material_glow_red;
                                    TabletHelper.SetTabletTextQuickActions("LMB: Drag -X\nAlt+LMB: Drag +X");
                                    break;

                                case 2:
                                    dragArrowMaterial = ResourceDB.material_glow_yellow;
                                    TabletHelper.SetTabletTextQuickActions("LMB: Drag +Y\nAlt+LMB: Drag -Y");
                                    break;

                                case 3:
                                    dragArrowMaterial = ResourceDB.material_glow_yellow;
                                    TabletHelper.SetTabletTextQuickActions("LMB: Drag -Y\nAlt+LMB: Drag +Y");
                                    break;

                                case 4:
                                    dragArrowMaterial = ResourceDB.material_glow_purple;
                                    TabletHelper.SetTabletTextQuickActions("LMB: Drag +Z\nAlt+LMB: Drag -Z");
                                    break;

                                case 5:
                                    dragArrowMaterial = ResourceDB.material_glow_purple;
                                    TabletHelper.SetTabletTextQuickActions("LMB: Drag -Z\nAlt+LMB: Drag +Z");
                                    break;
                            }

                            if (Input.GetKeyDown(KeyCode.Mouse0))
                            {
                                if (IsAltHeld)
                                {
                                    mode = Mode.DragFacesXPos + (faceIndex ^ 1);
                                    dragFaceRay.direction = -normal;
                                    dragArrowOffset = -0.5f;
                                }
                                else
                                {
                                    mode = Mode.DragFacesXPos + faceIndex;
                                    dragFaceRay.direction = normal;
                                    dragArrowOffset = 0.5f;
                                }
                            }
                        }
                        else
                        {
                            isDragArrowVisible = false;
                            TabletHelper.SetTabletTextQuickActions("");
                        }
                    }
                    break;

                case Mode.DragFacesXPos:
                case Mode.DragFacesXNeg:
                case Mode.DragFacesYPos:
                case Mode.DragFacesYNeg:
                case Mode.DragFacesZPos:
                case Mode.DragFacesZNeg:
                    if (!Input.GetKey(KeyCode.Mouse0))
                    {
                        mode = Mode.DragFacesIdle;
                    }
                    else
                    {
                        float offset;
                        if (TryGetAxialDragOffset(dragFaceRay, GetLookRay(), out offset))
                        {
                            int direction = mode - Mode.DragFacesXPos;
                            int axis = direction >> 1;
                            bool positive = (direction & 0x1) == 0;
                            var min = DragMin;
                            var max = DragMax;
                            var roundedOffset = Mathf.Max(min[axis] - max[axis], Mathf.RoundToInt(offset));
                            if (Mathf.Abs(roundedOffset) >= 1)
                            {
                                if (positive) max[axis] += roundedOffset;
                                else min[axis] -= roundedOffset;

                                dragFaceRay.origin += faceNormals[direction] * roundedOffset;
                                selectionFrom = min;
                                selectionTo = max;
                            }
                        }
                    }
                    break;

                case Mode.DragFacesVerticalIdle:
                    {
                        var lookRay = GetLookRay();
                        Vector3Int normal;
                        int faceIndex;
                        float distance = BoxRayIntersection(DragMin, DragMax + Vector3Int.one, lookRay, out normal, out faceIndex);
                        if (distance >= 0.0f)
                        {
                            var point = lookRay.GetPoint(distance) + (Vector3)normal * 0.5f;
                            isDragArrowVisible = true;
                            dragFaceRay = new Ray(point, IsAltHeld ? Vector3.down : Vector3.up);
                            dragArrowMaterial = ResourceDB.material_glow_yellow;
                            dragArrowOffset = 0.0f;
                            TabletHelper.SetTabletTextQuickActions("LMB: Drag +Y\nAlt+LMB: Drag -Y");

                            if (Input.GetKeyDown(KeyCode.Mouse0))
                            {
                                if (IsAltHeld)
                                {
                                    mode = Mode.DragFacesVerticalNeg;
                                    dragFaceRay = new Ray(point, Vector3.down);
                                }
                                else
                                {
                                    mode = Mode.DragFacesVerticalPos;
                                    dragFaceRay = new Ray(point, Vector3.up);
                                }
                            }
                        }
                        else
                        {
                            isDragArrowVisible = false;
                            TabletHelper.SetTabletTextQuickActions("");
                        }
                    }
                    break;

                case Mode.DragFacesVerticalPos:
                case Mode.DragFacesVerticalNeg:
                    if (!Input.GetKey(KeyCode.Mouse0))
                    {
                        mode = Mode.DragFacesVerticalIdle;
                    }
                    else
                    {
                        float offset;
                        if (TryGetAxialDragOffset(dragFaceRay, GetLookRay(), out offset))
                        {
                            bool positive = mode == Mode.DragFacesVerticalPos;
                            var min = DragMin;
                            var max = DragMax;
                            var roundedOffset = Mathf.Max(min.y - max.y, Mathf.RoundToInt(offset));
                            if (Mathf.Abs(roundedOffset) >= 1)
                            {
                                if (positive) max.y += roundedOffset;
                                else min.y -= roundedOffset;

                                dragFaceRay.origin += faceNormals[positive ? 2 : 3] * roundedOffset;
                                selectionFrom = min;
                                selectionTo = max;
                            }
                        }
                    }
                    break;
            }

            TabletHelper.SetTabletTextAnalyzer(GetTabletTitle());
            TabletHelper.SetTabletTextLastCopiedConfig(ActionManager.StatusText + CurrentBlueprintStatusText.Replace('\n', ' '));

            switch (boxMode)
            {
                case BoxMode.None:
                    GameRoot.setHighVisibilityInfoText("");
                    break;

                case BoxMode.Blueprint:
                    DrawBoxWithEdges(RepeatBlueprintMin, RepeatBlueprintMax + Vector3.one, 0.015f, 0.04f, materialDragBox.Material, materialDragBoxEdge.Material);
                    var repeatCount = RepeatCount;
                    GameRoot.setHighVisibilityInfoText((repeatCount != Vector3Int.one) ? $"Repeat: {repeatCount.x}x{repeatCount.y}x{repeatCount.z}\n{CurrentBlueprintStatusText}" : CurrentBlueprintStatusText);
                    break;

                case BoxMode.Selection:
                    DrawBoxWithEdges(DragMin, DragMax + Vector3.one, 0.015f, 0.04f, materialDragBox.Material, materialDragBoxEdge.Material);
                    GameRoot.setHighVisibilityInfoText($"{DragSize.x}x{DragSize.y}x{DragSize.z}");
                    break;
            }

            if (isDragArrowVisible)
            {
                DrawArrow(dragFaceRay.origin, dragFaceRay.direction, dragArrowMaterial, dragArrowScale, dragArrowOffset);
            }
        }

        private void OnBlueprintMoved(Vector3Int oldPosition, ref Vector3Int newPosition)
        {
            UpdateBlueprintPositionText();
        }

        private void OnBlueprintUpdated(int countUntested, int countBlocked, int countClear, int countDone)
        {
            if (textMaterialReport != null && Time.time >= nextUpdateTimeCountTexts)
            {
                nextUpdateTimeCountTexts = Time.time + 0.5f;

                var materialReportBuilder = new System.Text.StringBuilder();
                foreach (var kv in BlueprintPlaceholder.GetStateCounts())
                {
                    var total = kv.Value[0] + kv.Value[1] + kv.Value[2] + kv.Value[3];
                    if (total > 0)
                    {
                        var template = (kv.Key > 0) ? ItemTemplateManager.getItemTemplate(kv.Key) : null;
                        var name = (template != null) ? template.name : "Total";
                        var untested = (kv.Value[0] > 0) ? $"<color=#AAAAAA>{kv.Value[0]}</color>/" : "";
                        var clear = (kv.Value[1] > 0) ? $"{kv.Value[1]}/" : "";
                        var blocked = (kv.Value[2] > 0) ? $"<color=#FF2F1F>{kv.Value[2]}</color>/" : "";
                        var done = (kv.Value[3] > 0) ? $"<color=#AACCFF>{kv.Value[3]}</color>/" : "";
                        if (template != null)
                        {
                            ulong inventoryPtr = (GameRoot.getClientCharacter().inventoryId != 0) ? InventoryManager.inventoryManager_getInventoryPtr(GameRoot.getClientCharacter().inventoryId) : 0;
                            if (inventoryPtr != 0)
                            {
                                var inventoryCount = InventoryManager.inventoryManagerPtr_countByItemTemplate(inventoryPtr, template.id, IOBool.iotrue);

                                materialReportBuilder.AppendLine($"<color=#CCCCCC>{name}:</color> {untested}{clear}{blocked}{done}{total} <color=#FFFFAA>({inventoryCount})</color>");
                            }
                            else
                            {
                                materialReportBuilder.AppendLine($"<color=#CCCCCC>{name}:</color> {untested}{clear}{blocked}{done}{total} <color=#FFFFAA>(###)</color>");
                            }
                        }
                        else
                        {
                            materialReportBuilder.AppendLine($"<color=#CCCCCC>{name}:</color> {untested}{clear}{blocked}{done}{total}");
                        }
                    }
                }
                textMaterialReport.text = materialReportBuilder.ToString();
            }
        }

        private string GetTabletTitle()
        {
            switch (mode)
            {
                case Mode.Place:
                    return "Place Blueprint";

                case Mode.MoveIdle:
                case Mode.MoveXPos:
                case Mode.MoveXNeg:
                case Mode.MoveYPos:
                case Mode.MoveYNeg:
                case Mode.MoveZPos:
                case Mode.MoveZNeg:
                    return "Place Blueprint - Move";

                case Mode.VerticalMoveIdle:
                case Mode.VerticalMove:
                    return "Place Blueprint - Move Vertical";

                case Mode.RepeatIdle:
                case Mode.RepeatXPos:
                case Mode.RepeatXNeg:
                case Mode.RepeatYPos:
                case Mode.RepeatYNeg:
                case Mode.RepeatZPos:
                case Mode.RepeatZNeg:
                    return "Place Blueprint - Repeat";

                case Mode.DragAreaIdle:
                case Mode.DragAreaStart:
                case Mode.DragAreaVertical:
                    return "Create Blueprint - Select";

                case Mode.DragFacesIdle:
                case Mode.DragFacesXPos:
                case Mode.DragFacesXNeg:
                case Mode.DragFacesYPos:
                case Mode.DragFacesYNeg:
                case Mode.DragFacesZPos:
                case Mode.DragFacesZNeg:
                    return "Create Blueprint - Resize";

                case Mode.DragFacesVerticalIdle:
                case Mode.DragFacesVerticalPos:
                case Mode.DragFacesVerticalNeg:
                    return "Create Blueprint - Resize Vertical";

                default: throw new ArgumentOutOfRangeException();
            }
        }

        internal static void HideBlueprintFrame()
        {
            if (duplicationerFrame == null || !duplicationerFrame.activeSelf) return;

            duplicationerFrame.SetActive(false);
            GlobalStateManager.removeCursorRequirement();
        }

        internal static void ShowBlueprintFrame()
        {
            if (duplicationerFrame != null && duplicationerFrame.activeSelf) return;

            if (duplicationerFrame == null)
            {
                ulong usernameHash = GameRoot.getClientCharacter().usernameHash;
                UIBuilder.BeginWith(DefaultCanvasGO)
                    .Panel("DuplicationerFrame", "corner_cut_outline", new Color(0.133f, 0.133f, 0.133f, 1.0f), new Vector4(13, 10, 8, 13))
                        .Keep(ref duplicationerFrame)
                        .SetVerticalLayout(new RectOffset(0, 0, 0, 0), 0.0f, TextAnchor.UpperLeft, false, true, true, true, false, false, false)
                        .SetRectTransform(-420.0f, 120.0f, -60.0f, 220.0f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f)
                        .Header("HeaderBar", "corner_cut_outline", new Color(0.0f, 0.6f, 1.0f, 1.0f), new Vector4(13, 3, 8, 13))
                            .SetRectTransform(0.0f, -60.0f, 599.0f, 0.0f, 0.5f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f)
                            .Layout()
                                .MinHeight(60)
                            .Done
                            .Element("Heading")
                                .SetRectTransform(0.0f, 0.0f, -60.0f, 0.0f, 0.0f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                .Element_Text("Duplicationer", "OpenSansSemibold SDF", 34.0f, Color.white)
                            .Done
                            .Button("Button Close", "corner_cut_fully_inset", Color.white, new Vector4(13.0f, 1.0f, 4.0f, 13.0f))
                                .SetOnClick(new Action(() => { HideBlueprintFrame(); }))
                                .SetRectTransform(-60.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.5f, 1.0f, 0.0f, 1.0f, 1.0f)
                                .SetTransitionColors(new Color(1.0f, 1.0f, 1.0f, 1.0f), new Color(1.0f, 0.25f, 0.0f, 1.0f), new Color(1.0f, 0.0f, 0.0f, 1.0f), new Color(1.0f, 0.25f, 0.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                .Element("Image")
                                    .SetRectTransform(5.0f, 5.0f, -5.0f, -5.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                    .Element_Image("cross", Color.white, Vector4.zero, Image.Type.Sliced)
                                .Done
                            .Done
                        .Done
                        .Element("Content")
                            .SetRectTransform(0.0f, -855.0f, 599.0f, -60.0f, 0.5f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f)
                            .SetVerticalLayout(new RectOffset(10, 10, 10, 10), 0.0f, TextAnchor.UpperLeft, false, true, true, true, false, false, false)
                            .Element("Padding")
                                .SetRectTransform(10.0f, -785.0f, 589.0f, -10.0f, 0.5f, 0.5f, 0.0f, 1.0f, 0.0f, 1.0f)
                                .SetVerticalLayout(new RectOffset(0, 0, 0, 0), 10.0f, TextAnchor.UpperLeft, false, true, true, true, false, false, false)
                                .Element("Material Report")
                                    .AutoSize(ContentSizeFitter.FitMode.Unconstrained, ContentSizeFitter.FitMode.PreferredSize)
                                    .Element_Text("", "OpenSansSemibold SDF", 14.0f, Color.white, TextAlignmentOptions.TopLeft)
                                        .Keep(ref textMaterialReport)
                                    .Done
                                .Done
                                .Element("Row Position X")
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Element("Position Display X")
                                        .Layout()
                                        .MinWidth(100)
                                        .FlexibleWidth(1)
                                    .Done
                                    .Element_Text("X: 0", "OpenSansSemibold SDF", 18.0f, Color.white, TextAlignmentOptions.MidlineLeft)
                                        .Keep(ref textPositionX)
                                    .Done
                                    .Button("Button Decrease", "corner_cut", Color.white, new Vector4(10.0f, 1.0f, 2.0f, 10.0f))
                                        .SetOnClick(new Action(() => { MoveBlueprint(CurrentBlueprintAnchor + Vector3Int.left * NudgeX); }))
                                        .SetTransitionColors(new Color(0.2f, 0.2f, 0.2f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.222f, 0.667f, 1.0f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                        .Layout()
                                            .MinWidth(40)
                                            .MinHeight(40)
                                            .PreferredWidth(40)
                                            .PreferredHeight(40)
                                        .Done
                                        .Element("Image")
                                            .SetRectTransform(5.0f, 5.0f, -5.0f, -5.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                            .SetRotation(90.0f)
                                            .Element_Image("icons8-chevron-left-filled-100_white", Color.white, Vector4.zero, Image.Type.Sliced)
                                        .Done
                                    .Done
                                    .Button("Button Increase", "corner_cut", Color.white, new Vector4(10.0f, 1.0f, 2.0f, 10.0f))
                                        .SetOnClick(new Action(() => { MoveBlueprint(CurrentBlueprintAnchor + Vector3Int.right * NudgeX); }))
                                        .SetTransitionColors(new Color(0.2f, 0.2f, 0.2f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.222f, 0.667f, 1.0f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                        .Layout()
                                            .MinWidth(40)
                                            .MinHeight(40)
                                            .PreferredWidth(40)
                                            .PreferredHeight(40)
                                        .Done
                                        .Element("Image")
                                            .SetRectTransform(5.0f, 5.0f, -5.0f, -5.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                            .SetRotation(270.0f)
                                            .Element_Image("icons8-chevron-left-filled-100_white", Color.white, Vector4.zero, Image.Type.Sliced)
                                        .Done
                                    .Done
                                .Done
                                .Element("Row Position Y")
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Element("Position Display Y")
                                        .Layout()
                                        .MinWidth(100)
                                        .FlexibleWidth(1)
                                    .Done
                                    .Element_Text("Y: 0", "OpenSansSemibold SDF", 18.0f, Color.white, TextAlignmentOptions.MidlineLeft)
                                        .Keep(ref textPositionY)
                                    .Done
                                    .Button("Button Decrease", "corner_cut", Color.white, new Vector4(10.0f, 1.0f, 2.0f, 10.0f))
                                        .SetOnClick(new Action(() => { MoveBlueprint(CurrentBlueprintAnchor + Vector3Int.down * NudgeY); }))
                                        .SetTransitionColors(new Color(0.2f, 0.2f, 0.2f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.222f, 0.667f, 1.0f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                        .Layout()
                                            .MinWidth(40)
                                            .MinHeight(40)
                                            .PreferredWidth(40)
                                            .PreferredHeight(40)
                                        .Done
                                        .Element("Image")
                                            .SetRectTransform(5.0f, 5.0f, -5.0f, -5.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                            .SetRotation(90.0f)
                                            .Element_Image("icons8-chevron-left-filled-100_white", Color.white, Vector4.zero, Image.Type.Sliced)
                                        .Done
                                    .Done
                                    .Button("Button Increase", "corner_cut", Color.white, new Vector4(10.0f, 1.0f, 2.0f, 10.0f))
                                        .SetOnClick(new Action(() => { MoveBlueprint(CurrentBlueprintAnchor + Vector3Int.up * NudgeY); }))
                                        .SetTransitionColors(new Color(0.2f, 0.2f, 0.2f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.222f, 0.667f, 1.0f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                        .Layout()
                                            .MinWidth(40)
                                            .MinHeight(40)
                                            .PreferredWidth(40)
                                            .PreferredHeight(40)
                                        .Done
                                        .Element("Image")
                                            .SetRectTransform(5.0f, 5.0f, -5.0f, -5.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                            .SetRotation(270.0f)
                                            .Element_Image("icons8-chevron-left-filled-100_white", Color.white, Vector4.zero, Image.Type.Sliced)
                                        .Done
                                    .Done
                                .Done
                                .Element("Row Position Z")
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Element("Position Display Z")
                                        .Layout()
                                        .MinWidth(100)
                                        .FlexibleWidth(1)
                                    .Done
                                    .Element_Text("Z: 0", "OpenSansSemibold SDF", 18.0f, Color.white, TextAlignmentOptions.MidlineLeft)
                                        .Keep(ref textPositionZ)
                                    .Done
                                    .Button("Button Decrease", "corner_cut", Color.white, new Vector4(10.0f, 1.0f, 2.0f, 10.0f))
                                        .SetOnClick(new Action(() => { MoveBlueprint(CurrentBlueprintAnchor + Vector3Int.back * NudgeZ); }))
                                        .SetTransitionColors(new Color(0.2f, 0.2f, 0.2f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.222f, 0.667f, 1.0f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                        .Layout()
                                            .MinWidth(40)
                                            .MinHeight(40)
                                            .PreferredWidth(40)
                                            .PreferredHeight(40)
                                        .Done
                                        .Element("Image")
                                            .SetRectTransform(5.0f, 5.0f, -5.0f, -5.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                            .SetRotation(90.0f)
                                            .Element_Image("icons8-chevron-left-filled-100_white", Color.white, Vector4.zero, Image.Type.Sliced)
                                        .Done
                                    .Done
                                    .Button("Button Increase", "corner_cut", Color.white, new Vector4(10.0f, 1.0f, 2.0f, 10.0f))
                                        .SetOnClick(new Action(() => { MoveBlueprint(CurrentBlueprintAnchor + Vector3Int.forward * NudgeZ); }))
                                        .SetTransitionColors(new Color(0.2f, 0.2f, 0.2f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.222f, 0.667f, 1.0f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                        .Layout()
                                            .MinWidth(40)
                                            .MinHeight(40)
                                            .PreferredWidth(40)
                                            .PreferredHeight(40)
                                        .Done
                                        .Element("Image")
                                            .SetRectTransform(5.0f, 5.0f, -5.0f, -5.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                            .SetRotation(270.0f)
                                            .Element_Image("icons8-chevron-left-filled-100_white", Color.white, Vector4.zero, Image.Type.Sliced)
                                        .Done
                                    .Done
                                .Done
                                .Element("Row Buttons")
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Button("Button Build", "corner_cut", Color.white, new Vector4(10.0f, 1.0f, 2.0f, 10.0f))
                                        .SetOnClick(new Action(() =>
                                        {
                                            PlaceBlueprintMultiple(CurrentBlueprintAnchor, repeatFrom, repeatTo);
                                        }))
                                        .SetTransitionColors(new Color(0.2f, 0.2f, 0.2f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.222f, 0.667f, 1.0f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                        .Layout()
                                            .MinWidth(40)
                                            .MinHeight(40)
                                            .FlexibleWidth(1)
                                            .FlexibleHeight(1)
                                        .Done
                                        .Element("Text")
                                            .SetRectTransform(5.0f, 5.0f, -5.0f, -5.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                            .Element_Text("Confirm/Paste", "OpenSansSemibold SDF", 22.0f, Color.white, TextAlignmentOptions.Center)
                                        .Done
                                    .Done
                                .Done
                            .Done
                        .Done
                    .End();
            }

            duplicationerFrame.SetActive(true);
            GlobalStateManager.addCursorRequirement();

            UpdateBlueprintPositionText();
        }

        internal static void UpdateBlueprintPositionText()
        {
            if (textPositionX != null) textPositionX.text = string.Format("Position X: {0}", CurrentBlueprintAnchor.x);
            if (textPositionY != null) textPositionY.text = string.Format("Position Y: {0}", CurrentBlueprintAnchor.y);
            if (textPositionZ != null) textPositionZ.text = string.Format("Position Z: {0}", CurrentBlueprintAnchor.z);
        }

        internal static void LoadIconSprites()
        {
            iconCopy = ResourceExt.LoadSprite("copy.png");
            iconMoveVertical = ResourceExt.LoadSprite("move-vertical.png");
            iconMove = ResourceExt.LoadSprite("move.png");
            iconPanel = ResourceExt.LoadSprite("panel.png");
            iconPaste = ResourceExt.LoadSprite("paste.png");
            iconPlace = ResourceExt.LoadSprite("place.png");
            iconRepeat = ResourceExt.LoadSprite("repeat.png");
            iconResizeVertical = ResourceExt.LoadSprite("resize-vertical.png");
            iconResize = ResourceExt.LoadSprite("resize.png");
            iconSelectArea = ResourceExt.LoadSprite("select-area.png");
        }

        private enum Mode
        {
            Place,
            MoveIdle,
            MoveXPos,
            MoveXNeg,
            MoveYPos,
            MoveYNeg,
            MoveZPos,
            MoveZNeg,
            VerticalMoveIdle,
            VerticalMove,
            RepeatIdle,
            RepeatXPos,
            RepeatXNeg,
            RepeatYPos,
            RepeatYNeg,
            RepeatZPos,
            RepeatZNeg,
            DragAreaIdle,
            DragAreaStart,
            DragAreaVertical,
            DragFacesIdle,
            DragFacesXPos,
            DragFacesXNeg,
            DragFacesYPos,
            DragFacesYNeg,
            DragFacesZPos,
            DragFacesZNeg,
            DragFacesVerticalIdle,
            DragFacesVerticalPos,
            DragFacesVerticalNeg
        }

        private enum BoxMode
        {
            None,
            Selection,
            Blueprint
        }
    }
}