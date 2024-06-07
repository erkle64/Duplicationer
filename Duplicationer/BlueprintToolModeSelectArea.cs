using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Unfoundry;
using UnityEngine;

namespace Duplicationer
{
    internal class BlueprintToolModeSelectArea : BlueprintToolMode
    {
        private enum Mode
        {
            Idle,
            Start,
            Up,
            Down,
            QuickCopy
        };

        private Mode mode = Mode.Idle;
        private float _timeOfStart = 0.0f;

        public BlueprintToolMode NextMode { get; private set; } = null;

        public override string TabletTitle(BlueprintToolCHM tool) => "Select Area";

        public BlueprintToolModeSelectArea()
        {
        }

        public override void Enter(BlueprintToolCHM tool, BlueprintToolMode fromMode)
        {
            mode = Mode.Idle;
            TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Select Start\nAlt+{GameRoot.getHotkeyStringFromAction("Action")}: Select Block with Offset\nDouble click {GameRoot.getHotkeyStringFromAction("Action")} to copy machine and loaders.");
            tool.boxMode = BlueprintToolCHM.BoxMode.None;
            tool.HideBlueprint();
        }

        public override bool AllowCopy(BlueprintToolCHM tool) => false;
        public override bool AllowPaste(BlueprintToolCHM tool) => false;
        public override bool AllowRotate(BlueprintToolCHM tool) => false;
        public override bool AllowMirror(BlueprintToolCHM tool) => false;

        public override void Update(BlueprintToolCHM tool)
        {
            if (GlobalStateManager.getRewiredPlayer0().GetButtonDown("Action"))
                DuplicationerPlugin.log.Log($"{mode}: {Time.time} {_timeOfStart}");

            switch (mode)
            {
                case Mode.Idle:
                    tool.isDragArrowVisible = false;

                    Vector3 targetPoint;
                    Vector3Int targetCoord, targetNormal;
                    if (CustomHandheldMode.GetTargetCube(-0.01f, out targetPoint, out targetCoord, out targetNormal))
                    {
                        if (InputHelpers.IsAltHeld) targetCoord += Vector3Int.RoundToInt(Plugin.SnappedToNearestAxis(targetNormal));

                        tool.boxMode = BlueprintToolCHM.BoxMode.Selection;
                        tool.selectionFrom = tool.selectionTo = targetCoord;

                        if (GlobalStateManager.getRewiredPlayer0().GetButtonDown("Action") && InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
                        {
                            tool.dragPlane = new Plane(Vector3.up, targetPoint);

                            mode = Mode.Start;
                            TabletHelper.SetTabletTextQuickActions($"Release {GameRoot.getHotkeyStringFromAction("Action")}: Confirm Start");
                            _timeOfStart = Time.time;
                        }
                    }
                    else
                    {
                        tool.boxMode = BlueprintToolCHM.BoxMode.None;
                    }
                    break;

                case Mode.Start:
                    if (GlobalStateManager.getRewiredPlayer0().GetButtonDown("Action") && Time.time < _timeOfStart + 0.5f && InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
                    {
                        mode = Mode.QuickCopy;
                        break;
                    }


                    if ((GlobalStateManager.getRewiredPlayer0().GetButtonDown("Action")
                        || GlobalStateManager.getRewiredPlayer0().GetButtonUp("Action")
                            && tool.selectionFrom != tool.selectionTo)
                            && InputHelpers.IsMouseInputAllowed
                            && !tool.IsAnyFrameOpen)
                    {
                        var lookRay = CustomHandheldMode.GetLookRay();
                        float distance;
                        if (tool.dragPlane.Raycast(lookRay, out distance))
                        {
                            var point = lookRay.GetPoint(distance);
                            point.y = tool.DragMax.y + 1;
                            tool.isDragArrowVisible = true;
                            tool.dragFaceRay = new Ray(point, Vector3.up);
                            tool.dragArrowMaterial = ResourceDB.material_glow_yellow;

                            mode = Mode.Up;
                            TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Select Height");
                        }
                        else
                        {
                            mode = Mode.Idle;
                            TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Select Start\nAlt+{GameRoot.getHotkeyStringFromAction("Action")}: Select Block with Offset\nDouble click {GameRoot.getHotkeyStringFromAction("Action")} to copy machine and loaders.");
                        }
                    }
                    else
                    {
                        if (GlobalStateManager.getRewiredPlayer0().GetButtonUp("Action"))
                        {
                            TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Select Other Corner");
                        }

                        var lookRay = CustomHandheldMode.GetLookRay();
                        float distance;
                        if (tool.dragPlane.Raycast(lookRay, out distance))
                        {
                            var point = lookRay.GetPoint(distance);
                            tool.selectionTo = new Vector3Int(Mathf.FloorToInt(point.x - tool.dragPlane.normal.x * 0.01f), tool.selectionFrom.y, Mathf.FloorToInt(point.z - tool.dragPlane.normal.z * 0.01f));

                            if (tool.selectionFrom == tool.selectionTo) TabletHelper.SetTabletTextQuickActions($"Release {GameRoot.getHotkeyStringFromAction("Action")}: Confirm Start");
                            else TabletHelper.SetTabletTextQuickActions($"Release {GameRoot.getHotkeyStringFromAction("Action")}: Select Other Corner");
                        }
                    }
                    break;

                case Mode.Up:
                    if (GlobalStateManager.getRewiredPlayer0().GetButtonDown("Action") && InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
                    {
                        tool.SelectMode(NextMode);
                    }
                    else
                    {
                        float offset;
                        if (CustomHandheldMode.TryGetAxialDragOffset(tool.dragFaceRay, CustomHandheldMode.GetLookRay(), out offset))
                        {
                            var min = tool.DragMin;
                            var max = tool.DragMax;
                            int roundedOffset = Mathf.RoundToInt(offset);
                            var clampedOffset = Mathf.Max(min.y - max.y, roundedOffset);
                            if (roundedOffset < clampedOffset)
                            {
                                mode = Mode.Down;
                                tool.dragFaceRay = new Ray(tool.dragFaceRay.origin, -tool.dragFaceRay.direction);
                                tool.dragFaceRay.origin += Vector3.up * (clampedOffset - 1);

                                max.y += clampedOffset;
                                tool.selectionFrom = min;
                                tool.selectionTo = max;
                            }
                            else if (Mathf.Abs(clampedOffset) >= 1)
                            {
                                tool.dragFaceRay.origin += Vector3.up * clampedOffset;

                                max.y += clampedOffset;
                                tool.selectionFrom = min;
                                tool.selectionTo = max;
                            }
                        }
                    }
                    break;

                case Mode.Down:
                    if (GlobalStateManager.getRewiredPlayer0().GetButtonDown("Action") && InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
                    {
                        tool.SelectMode(NextMode);
                    }
                    else
                    {
                        float offset;
                        if (CustomHandheldMode.TryGetAxialDragOffset(tool.dragFaceRay, CustomHandheldMode.GetLookRay(), out offset))
                        {
                            var min = tool.DragMin;
                            var max = tool.DragMax;
                            int roundedOffset = Mathf.RoundToInt(offset);
                            var clampedOffset = Mathf.Max(min.y - max.y, roundedOffset);
                            if (roundedOffset < clampedOffset)
                            {
                                mode = Mode.Up;
                                tool.dragFaceRay = new Ray(tool.dragFaceRay.origin, -tool.dragFaceRay.direction);
                                tool.dragFaceRay.origin += Vector3.down * (clampedOffset - 1);

                                min.y -= clampedOffset;
                                tool.selectionFrom = min;
                                tool.selectionTo = max;
                            }
                            else if (Mathf.Abs(clampedOffset) >= 1)
                            {
                                tool.dragFaceRay.origin += Vector3.down * clampedOffset;

                                min.y -= clampedOffset;
                                tool.selectionFrom = min;
                                tool.selectionTo = max;
                            }
                        }
                    }
                    break;

                case Mode.QuickCopy:
                    if (!GlobalStateManager.getRewiredPlayer0().GetButton("Action"))
                    {
                        var bogo = StreamingSystem.getBuildableObjectGOQuadtreeArray().queryPointXYZ(tool.selectionFrom);
                        if (bogo != null)
                        {
                            var bot = bogo.template;

                            var invalidTarget = false;
                            if (bogo.gameObject.CompareTag("BUILDING_PLACEHOLDER_DISSOLVE")) invalidTarget = true;
                            if (bot.type == BuildableObjectTemplate.BuildableObjectType.WorldDecorMineAble) invalidTarget = true;
                            if (!bot.canBeCopiedByTablet) invalidTarget = true;

                            if (!invalidTarget)
                            {
                                var position = new Vector3Int(bogo.aabb.x0, bogo.aabb.y0, bogo.aabb.z0);
                                var size = new Vector3Int(bogo.aabb.wx, bogo.aabb.wy, bogo.aabb.wz);
                                var blocks = new byte[size.x * size.y * size.z];
                                var buildings = new List<BuildableObjectGO>
                                    {
                                        bogo
                                    };

                                if (!bot.disableLoaders)
                                {
                                    void AddLoaderIfNotBlocked(Vector3Int localOffset, BuildingManager.BuildOrientation localOrientation)
                                    {
                                        if (bot.blockedLoaderPositions.Any(x => x == localOffset)) return;

                                        var loader = FindLoader(
                                            BuildableEntity.getWorldPositionByLocalOffset(
                                                bot,
                                                bogo.aabb,
                                                localOffset,
                                                bogo.buildOrientation,
                                                bogo.transform.rotation),
                                            (BuildingManager.BuildOrientation)(((int)bogo.buildOrientation + (int)localOrientation) % 4)
                                            );
                                        if (loader != null) buildings.Add(loader);
                                    }

                                    for (int x = 0; x < bot.size.x; x++)
                                    {
                                        AddLoaderIfNotBlocked(new Vector3Int(x, bot.loaderLevel, -1), BuildingManager.BuildOrientation.zNeg);
                                        AddLoaderIfNotBlocked(new Vector3Int(x, bot.loaderLevel, bot.size.z), BuildingManager.BuildOrientation.zPos);
                                    }

                                    for (int z = 0; z < bot.size.z; z++)
                                    {
                                        AddLoaderIfNotBlocked(new Vector3Int(-1, bot.loaderLevel, z), BuildingManager.BuildOrientation.xNeg);
                                        AddLoaderIfNotBlocked(new Vector3Int(bot.size.x, bot.loaderLevel, z), BuildingManager.BuildOrientation.xPos);
                                    }

                                    tool.CopyCustomSelection(position, size, buildings, blocks);
                                }
                            }
                        }

                        mode = Mode.Idle;
                    }
                    break;
            }
        }

        private LoaderGO FindLoader(Vector3Int worldPos, BuildingManager.BuildOrientation buildOrientation)
        {
            var bogo = StreamingSystem.getBuildableObjectGOQuadtreeArray().queryPointXYZ(worldPos);
            return bogo is LoaderGO loader && loader.buildOrientation == buildOrientation ? loader : null;
        }

        internal void Connect(BlueprintToolMode nextMode)
        {
            NextMode = nextMode;
        }
    }
}