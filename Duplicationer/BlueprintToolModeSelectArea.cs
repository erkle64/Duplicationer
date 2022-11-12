using System;
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
            Down
        };

        private Mode mode = Mode.Idle;

        public BlueprintToolMode NextMode { get; private set; } = null;

        public override string TabletTitle(BlueprintToolCHM tool) => "Select Area";

        public BlueprintToolModeSelectArea()
        {
        }

        public override void Enter(BlueprintToolCHM tool, BlueprintToolMode fromMode)
        {
            mode = Mode.Idle;
            TabletHelper.SetTabletTextQuickActions("LMB: Select Start\nAlt+LMB: Select Block with Offset");
            tool.boxMode = BlueprintToolCHM.BoxMode.None;
            tool.HideBlueprint();
        }

        public override bool AllowCopy(BlueprintToolCHM tool) => false;
        public override bool AllowPaste(BlueprintToolCHM tool) => false;

        public override void Update(BlueprintToolCHM tool)
        {
            switch (mode)
            {
                case Mode.Idle:
                    tool.isDragArrowVisible = false;
                    Vector3 targetPoint;
                    Vector3Int targetCoord, targetNormal;
                    if (CustomHandheldMode.GetTargetCube(-0.01f, out targetPoint, out targetCoord, out targetNormal))
                    {
                        if (InputHelpers.IsAltHeld) targetCoord += Vector3Int.RoundToInt(GameRoot.SnappedToNearestAxis(targetNormal));

                        tool.boxMode = BlueprintToolCHM.BoxMode.Selection;
                        tool.selectionFrom = tool.selectionTo = targetCoord;

                        if (Input.GetKeyDown(KeyCode.Mouse0) && InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
                        {
                            tool.dragPlane = new Plane(Vector3.up, targetPoint);

                            mode = Mode.Start;
                            TabletHelper.SetTabletTextQuickActions("Release LMB: Confirm Start");
                        }
                    }
                    else
                    {
                        tool.boxMode = BlueprintToolCHM.BoxMode.None;
                    }
                    break;

                case Mode.Start:
                    if ((Input.GetKeyDown(KeyCode.Mouse0) || Input.GetKeyUp(KeyCode.Mouse0) && tool.selectionFrom != tool.selectionTo) && InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
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
                            TabletHelper.SetTabletTextQuickActions("LMB: Select Height");
                        }
                        else
                        {
                            mode = Mode.Idle;
                            TabletHelper.SetTabletTextQuickActions("LMB: Select Start\nAlt+LMB: Select Start with Offset");
                        }
                    }
                    else
                    {
                        if (Input.GetKeyUp(KeyCode.Mouse0))
                        {
                            TabletHelper.SetTabletTextQuickActions("LMB: Select Other Corner");
                        }

                        var lookRay = CustomHandheldMode.GetLookRay();
                        float distance;
                        if (tool.dragPlane.Raycast(lookRay, out distance))
                        {
                            var point = lookRay.GetPoint(distance);
                            tool.selectionTo = new Vector3Int(Mathf.FloorToInt(point.x - tool.dragPlane.normal.x * 0.01f), tool.selectionFrom.y, Mathf.FloorToInt(point.z - tool.dragPlane.normal.z * 0.01f));

                            if (tool.selectionFrom == tool.selectionTo) TabletHelper.SetTabletTextQuickActions("Release LMB: Confirm Start");
                            else TabletHelper.SetTabletTextQuickActions("Release LMB: Select Other Corner");
                        }
                    }
                    break;

                case Mode.Up:
                    if (Input.GetKeyDown(KeyCode.Mouse0) && InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
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
                                tool.dragFaceRay.direction = -tool.dragFaceRay.direction;
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
                    if (Input.GetKeyDown(KeyCode.Mouse0) && InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
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
                                tool.dragFaceRay.direction = -tool.dragFaceRay.direction;
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
            }
        }

        internal void Connect(BlueprintToolMode nextMode)
        {
            NextMode = nextMode;
        }
    }
}