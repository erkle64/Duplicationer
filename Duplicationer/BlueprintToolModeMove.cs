using Unfoundry;
using UnityEngine;

namespace Duplicationer
{
    internal class BlueprintToolModeMove : BlueprintToolMode
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

        public BlueprintToolModeMove()
        {
        }

        public override string TabletTitle(BlueprintToolCHM tool) => "Place Blueprint - Move";

        public override void Enter(BlueprintToolCHM tool, BlueprintToolMode fromMode)
        {
            mode = Mode.Idle;
            tool.isDragArrowVisible = false;
            TabletHelper.SetTabletTextQuickActions("");
            tool.ShowBlueprint();
            tool.boxMode = BlueprintToolCHM.BoxMode.Blueprint;
        }

        public override bool AllowCopy(BlueprintToolCHM tool) => false;
        public override bool AllowPaste(BlueprintToolCHM tool) => mode == Mode.Idle;
        public override bool AllowRotate(BlueprintToolCHM tool) => mode == Mode.Idle;
        public override bool AllowMirror(BlueprintToolCHM tool) => mode == Mode.Idle;

        public override void Update(BlueprintToolCHM tool)
        {
            switch (mode)
            {
                case Mode.Idle:
                    tool.boxMode = BlueprintToolCHM.BoxMode.Blueprint;
                    var lookRay = CustomHandheldMode.GetLookRay();
                    Vector3Int normal;
                    int faceIndex;
                    float distance = CustomHandheldMode.BoxRayIntersection(tool.RepeatBlueprintMin, tool.RepeatBlueprintMax + Vector3Int.one, lookRay, out normal, out faceIndex);
                    if (distance >= 0.0f)
                    {
                        var point = lookRay.GetPoint(distance);
                        tool.isDragArrowVisible = true;
                        tool.dragFaceRay = new Ray(point, normal);
                        tool.dragArrowOffset = 0.5f;
                        switch (faceIndex)
                        {
                            case 0:
                                tool.dragArrowMaterial = ResourceDB.material_glow_red;
                                TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Drag X\nAlt+{GameRoot.getHotkeyStringFromAction("Action")}: Drag X*{tool.CurrentBlueprint.SizeX}\n{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate");
                                break;

                            case 1:
                                tool.dragArrowMaterial = ResourceDB.material_glow_red;
                                TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Drag X\nAlt+{GameRoot.getHotkeyStringFromAction("Action")}: Drag X*{tool.CurrentBlueprint.SizeX}\n{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate");
                                break;

                            case 2:
                                tool.dragArrowMaterial = ResourceDB.material_glow_yellow;
                                TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Drag Y\nAlt+{GameRoot.getHotkeyStringFromAction("Action")}: Drag Y*{tool.CurrentBlueprint.SizeY}\n{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate");
                                break;

                            case 3:
                                tool.dragArrowMaterial = ResourceDB.material_glow_yellow;
                                TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Drag Y\nAlt+{GameRoot.getHotkeyStringFromAction("Action")}: Drag Y*{tool.CurrentBlueprint.SizeY}\n{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate");
                                break;

                            case 4:
                                tool.dragArrowMaterial = ResourceDB.material_glow_purple;
                                TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Drag Z\nAlt+{GameRoot.getHotkeyStringFromAction("Action")}: Drag Z*{tool.CurrentBlueprint.SizeZ}\n{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate");
                                break;

                            case 5:
                                tool.dragArrowMaterial = ResourceDB.material_glow_purple;
                                TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Drag Z\nAlt+{GameRoot.getHotkeyStringFromAction("Action")}: Drag Z*{tool.CurrentBlueprint.SizeZ}\n{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate");
                                break;
                        }

                        if (GlobalStateManager.getRewiredPlayer0().GetButtonDown("Action") && InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
                        {
                            mode = Mode.XPos + faceIndex;
                        }
                    }
                    else
                    {
                        tool.isDragArrowVisible = false;
                        TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate");
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
                            int dragStep = InputHelpers.IsAltHeld ? tool.CurrentBlueprintSize[axis] : 1;
                            var roundedOffset = Mathf.RoundToInt(offset / dragStep) * dragStep;
                            if (Mathf.Abs(roundedOffset) >= dragStep)
                            {
                                var offsetVector = CustomHandheldMode.faceNormals[direction] * roundedOffset;
                                tool.MoveBlueprint(tool.CurrentBlueprintAnchor + offsetVector);

                                tool.dragFaceRay.origin += CustomHandheldMode.faceNormals[direction] * roundedOffset;
                            }
                        }
                    }
                    break;
            }
        }
    }
}