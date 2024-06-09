using Unfoundry;
using UnityEngine;

namespace Duplicationer
{
    internal class BlueprintToolModeMoveSideways : BlueprintToolMode
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

        private int _idAction = -1;
        private int _idModifier2 = -1;
        private double _altHeldTime;

        public BlueprintToolModeMoveSideways()
        {
        }

        public override string TabletTitle(BlueprintToolCHM tool) => "Place Blueprint - Move Sideways";

        public override void Enter(BlueprintToolCHM tool, BlueprintToolMode fromMode)
        {
            _idAction = InputHelpers.GetActionId("Action");
            _idModifier2 = InputHelpers.GetActionId("Modifier 2");

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
                    var distance = CustomHandheldMode.BoxRayIntersection(tool.RepeatBlueprintMin, tool.RepeatBlueprintMax + Vector3Int.one, lookRay, out var normal, out var faceIndex, out var isInternal);
                    if (distance >= 0.0f)
                    {
                        var point = lookRay.GetPoint(distance) + (Vector3)normal * (isInternal ? -0.5f : 0.5f);
                        tool.isDragArrowVisible = true;
                        tool.isDragArrowDouble = true;
                        tool.dragArrowOffset = 0.25f;
                        switch (faceIndex)
                        {
                            case 0:
                            case 1:
                                tool.dragArrowMaterial = ResourceDB.material_glow_purple;
                                tool.dragFaceRay = new Ray(point, Vector3.forward);
                                SetQuickActionsIdle("Z", tool.CurrentBlueprint.SizeZ);
                                break;

                            case 2:
                            case 3:
                                if (Mathf.Abs(lookRay.direction.z) > Mathf.Abs(lookRay.direction.x))
                                {
                                    tool.dragArrowMaterial = ResourceDB.material_glow_red;
                                    tool.dragFaceRay = new Ray(point, Vector3.right);
                                    SetQuickActionsIdle("X", tool.CurrentBlueprint.SizeX);
                                }
                                else
                                {
                                    tool.dragArrowMaterial = ResourceDB.material_glow_purple;
                                    tool.dragFaceRay = new Ray(point, Vector3.forward);
                                    SetQuickActionsIdle("Z", tool.CurrentBlueprint.SizeZ);
                                }
                                break;

                            case 4:
                            case 5:
                                tool.dragArrowMaterial = ResourceDB.material_glow_red;
                                tool.dragFaceRay = new Ray(point, Vector3.right);
                                SetQuickActionsIdle("X", tool.CurrentBlueprint.SizeX);
                                break;
                        }

                        if (InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
                        {
                            if (GlobalStateManager.getRewiredPlayer0().GetButtonDown(_idAction))
                            {
                                switch (faceIndex)
                                {
                                    case 0:
                                    case 1:
                                        mode = Mode.ZPos;
                                        break;

                                    case 2:
                                    case 3:
                                        mode = Mathf.Abs(lookRay.direction.z) > Mathf.Abs(lookRay.direction.x) ? Mode.XPos : Mode.ZPos;
                                        break;

                                    case 4:
                                    case 5:
                                        mode = Mode.XPos;
                                        break;
                                }
                            }
                            else if (GlobalStateManager.getRewiredPlayer0().GetButtonUp(_idModifier2))
                            {
                                if (_altHeldTime < 0.5)
                                {
                                    tool.SelectMode(tool.modeMoveVertical);
                                    AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);
                                    return;
                                }
                            }
                            else if (GlobalStateManager.getRewiredPlayer0().GetButton(_idModifier2))
                            {
                                _altHeldTime = GlobalStateManager.getRewiredPlayer0().GetButtonTimePressed(_idModifier2);
                            }
                        }
                    }
                    else
                    {
                        tool.isDragArrowVisible = false;
                        SetQuickActionsIdle();
                    }
                    break;

                case Mode.XPos:
                case Mode.XNeg:
                    SetQuickActionsIdle("X", tool.CurrentBlueprint.SizeX);
                    HandleMove(tool);
                    break;

                case Mode.YPos:
                case Mode.YNeg:
                    SetQuickActionsIdle("Y", tool.CurrentBlueprint.SizeY);
                    HandleMove(tool);
                    break;

                case Mode.ZPos:
                case Mode.ZNeg:
                    SetQuickActionsIdle("Z", tool.CurrentBlueprint.SizeZ);
                    HandleMove(tool);
                    break;
            }
        }

        private void HandleMove(BlueprintToolCHM tool)
        {
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
        }

        private static void SetQuickActionsIdle()
        {
            TabletHelper.SetTabletTextQuickActions($@"{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate
Tap {GameRoot.getHotkeyStringFromAction("Modifier 2")}: Move Vertical Mode");
        }

        private static void SetQuickActionsIdle(string axis, int size)
        {
            TabletHelper.SetTabletTextQuickActions($@"{GameRoot.getHotkeyStringFromAction("Action")}: Drag {axis}
Alt+{GameRoot.getHotkeyStringFromAction("Action")}: Drag {axis}*{size}
{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate
Tap {GameRoot.getHotkeyStringFromAction("Modifier 2")}: Move Vertical Mode");
        }

        private static void SetQuickActionsMoving(string axis, int size)
        {
            TabletHelper.SetTabletTextQuickActions($@"{GameRoot.getHotkeyStringFromAction("Action")}: Drag {axis}
Alt+{GameRoot.getHotkeyStringFromAction("Action")}: Drag {axis}*{size}");
        }
    }
}