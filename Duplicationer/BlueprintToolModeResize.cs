using Unfoundry;
using UnityEngine;

namespace Duplicationer
{
    internal class BlueprintToolModeResize : BlueprintToolMode
    {
        private enum Mode
        {
            Idle,
            XPos,
            XNeg,
            YPos,
            YNeg,
            ZPos,
            ZNeg
        }

        private Mode mode = Mode.Idle;

        public BlueprintToolModeResize()
        {
        }

        public override string TabletTitle(BlueprintToolCHM tool) => "Select Area - Resize";

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
                    Vector3Int normal;
                    int faceIndex;
                    float distance = CustomHandheldMode.BoxRayIntersection(tool.DragMin, tool.DragMax + Vector3Int.one, lookRay, out normal, out faceIndex);
                    if (distance >= 0.0f)
                    {
                        var point = lookRay.GetPoint(distance);
                        tool.isDragArrowVisible = true;
                        if (InputHelpers.IsAltHeld)
                        {
                            tool.dragFaceRay = new Ray(point, -normal);
                            tool.dragArrowOffset = -0.5f;
                        }
                        else
                        {
                            tool.dragFaceRay = new Ray(point, normal);
                            tool.dragArrowOffset = 0.5f;
                        }
                        switch (faceIndex)
                        {
                            case 0:
                                tool.dragArrowMaterial = ResourceDB.material_glow_red;
                                TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Drag +X\nAlt+{GameRoot.getHotkeyStringFromAction("Action")}: Drag -X");
                                break;

                            case 1:
                                tool.dragArrowMaterial = ResourceDB.material_glow_red;
                                TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Drag -X\nAlt+{GameRoot.getHotkeyStringFromAction("Action")}: Drag +X");
                                break;

                            case 2:
                                tool.dragArrowMaterial = ResourceDB.material_glow_yellow;
                                TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Drag +Y\nAlt+{GameRoot.getHotkeyStringFromAction("Action")}: Drag -Y");
                                break;

                            case 3:
                                tool.dragArrowMaterial = ResourceDB.material_glow_yellow;
                                TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Drag -Y\nAlt+{GameRoot.getHotkeyStringFromAction("Action")}: Drag +Y");
                                break;

                            case 4:
                                tool.dragArrowMaterial = ResourceDB.material_glow_purple;
                                TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Drag +Z\nAlt+{GameRoot.getHotkeyStringFromAction("Action")}: Drag -Z");
                                break;

                            case 5:
                                tool.dragArrowMaterial = ResourceDB.material_glow_purple;
                                TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Drag -Z\nAlt+{GameRoot.getHotkeyStringFromAction("Action")}: Drag +Z");
                                break;
                        }

                        if (GlobalStateManager.getRewiredPlayer0().GetButtonDown("Action") && InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
                        {
                            if (InputHelpers.IsAltHeld)
                            {
                                mode = Mode.XPos + (faceIndex ^ 1);
                                tool.dragFaceRay = new Ray(tool.dragFaceRay.origin, -normal);
                                tool.dragArrowOffset = -0.5f;
                            }
                            else
                            {
                                mode = Mode.XPos + faceIndex;
                                tool.dragFaceRay = new Ray(tool.dragFaceRay.origin, normal);
                                tool.dragArrowOffset = 0.5f;
                            }
                        }
                    }
                    else
                    {
                        tool.isDragArrowVisible = false;
                        TabletHelper.SetTabletTextQuickActions("");
                    }
                    break;

                case Mode.XPos:
                case Mode.XNeg:
                case Mode.YPos:
                case Mode.YNeg:
                case Mode.ZPos:
                case Mode.ZNeg:
                    if (!GlobalStateManager.getRewiredPlayer0().GetButton("Action"))
                    {
                        mode = Mode.Idle;
                    }
                    else
                    {
                        float offset;
                        if (CustomHandheldMode.TryGetAxialDragOffset(tool.dragFaceRay, CustomHandheldMode.GetLookRay(), out offset))
                        {
                            int direction = mode - Mode.XPos;
                            int axis = direction >> 1;
                            bool positive = (direction & 0x1) == 0;
                            var min = tool.DragMin;
                            var max = tool.DragMax;
                            var roundedOffset = Mathf.Max(min[axis] - max[axis], Mathf.RoundToInt(offset));
                            if (Mathf.Abs(roundedOffset) >= 1)
                            {
                                if (positive) max[axis] += roundedOffset;
                                else min[axis] -= roundedOffset;

                                tool.dragFaceRay.origin += CustomHandheldMode.faceNormals[direction] * roundedOffset;
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