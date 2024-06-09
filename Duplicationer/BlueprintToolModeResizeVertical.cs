using Unfoundry;
using UnityEngine;

namespace Duplicationer
{
    internal class BlueprintToolModeResizeVertical : BlueprintToolMode
    {
        private enum Mode
        {
            Idle,
            Negative,
            Positive
        }

        private Mode mode = Mode.Idle;

        public BlueprintToolModeResizeVertical()
        {
        }

        public override string TabletTitle(BlueprintToolCHM tool) => "Select Area - Resize Vertical";

        public override void Enter(BlueprintToolCHM tool, BlueprintToolMode fromMode)
        {
            mode = Mode.Idle;
            TabletHelper.SetTabletTextQuickActions("");
            tool.boxMode = BlueprintToolCHM.BoxMode.Selection;
        }

        public override bool AllowCopy(BlueprintToolCHM tool) => mode == Mode.Idle;
        public override bool AllowPaste(BlueprintToolCHM tool) => false;
        public override bool AllowRotate(BlueprintToolCHM tool) => false;
        public override bool AllowMirror(BlueprintToolCHM tool) => false;

        public override void Update(BlueprintToolCHM tool)
        {
            switch (mode)
            {
                case Mode.Idle:
                    var lookRay = CustomHandheldMode.GetLookRay();
                    var distance = CustomHandheldMode.BoxRayIntersection(tool.DragMin, tool.DragMax + Vector3Int.one, lookRay, out var normal, out var faceIndex, out var isInternal);
                    if (distance >= 0.0f)
                    {
                        var point = lookRay.GetPoint(distance) + (Vector3)normal * (isInternal ? -0.5f : 0.5f);
                        tool.isDragArrowVisible = true;
                        tool.dragFaceRay = new Ray(point, InputHelpers.IsAltHeld ? Vector3.down : Vector3.up);
                        tool.dragArrowMaterial = ResourceDB.material_glow_yellow;
                        tool.dragArrowOffset = 0.0f;
                        TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Drag +Y\nAlt+{GameRoot.getHotkeyStringFromAction("Action")}: Drag -Y");

                        if (GlobalStateManager.getRewiredPlayer0().GetButtonDown("Action") && InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
                        {
                            if (InputHelpers.IsAltHeld)
                            {
                                mode = Mode.Negative;
                                tool.dragFaceRay = new Ray(point, Vector3.down);
                            }
                            else
                            {
                                mode = Mode.Positive;
                                tool.dragFaceRay = new Ray(point, Vector3.up);
                            }
                        }
                    }
                    else
                    {
                        tool.isDragArrowVisible = false;
                        TabletHelper.SetTabletTextQuickActions("");
                    }
                    break;

                case Mode.Positive:
                case Mode.Negative:
                    if (!GlobalStateManager.getRewiredPlayer0().GetButton("Action"))
                    {
                        mode = Mode.Idle;
                    }
                    else
                    {
                        float offset;
                        if (CustomHandheldMode.TryGetAxialDragOffset(tool.dragFaceRay, CustomHandheldMode.GetLookRay(), out offset))
                        {
                            bool positive = mode == Mode.Positive;
                            var min = tool.DragMin;
                            var max = tool.DragMax;
                            var roundedOffset = Mathf.Max(min.y - max.y, Mathf.RoundToInt(offset));
                            if (Mathf.Abs(roundedOffset) >= 1)
                            {
                                if (positive) max.y += roundedOffset;
                                else min.y -= roundedOffset;

                                tool.dragFaceRay.origin += CustomHandheldMode.faceNormals[positive ? 2 : 3] * roundedOffset;
                                tool.selectionFrom = min;
                                tool.selectionTo = max;
                            }
                        }
                    }
                    break;
            }
        }
    }
}