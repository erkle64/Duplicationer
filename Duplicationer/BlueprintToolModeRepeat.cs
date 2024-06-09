using Unfoundry;
using UnityEngine;

namespace Duplicationer
{
    internal class BlueprintToolModeRepeat : BlueprintToolMode
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

        public BlueprintToolModeRepeat()
        {
        }

        public override string TabletTitle(BlueprintToolCHM tool) => "Place BLueprint - Repeat";

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
                    {
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
                                    tool.dragArrowMaterial = ResourceDB.material_glow_orange;
                                    TabletHelper.SetTabletTextQuickActions($@"{GameRoot.getHotkeyStringFromAction("Action")}: Drag X*{tool.CurrentBlueprint.SizeX}
{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate
Tap {GameRoot.getHotkeyStringFromAction("Modifier 2")}: Move Mode");
                                    break;

                                case 1:
                                    tool.dragArrowMaterial = ResourceDB.material_glow_orange;
                                    TabletHelper.SetTabletTextQuickActions($@"{GameRoot.getHotkeyStringFromAction("Action")}: Drag X*{tool.CurrentBlueprint.SizeX}
{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate
Tap {GameRoot.getHotkeyStringFromAction("Modifier 2")}: Move Mode");
                                    break;

                                case 2:
                                    tool.dragArrowMaterial = ResourceDB.material_glow_green;
                                    TabletHelper.SetTabletTextQuickActions($@"{GameRoot.getHotkeyStringFromAction("Action")}: Drag Y*{tool.CurrentBlueprint.SizeY}
{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate
Tap {GameRoot.getHotkeyStringFromAction("Modifier 2")}: Move Mode");
                                    break;

                                case 3:
                                    tool.dragArrowMaterial = ResourceDB.material_glow_green;
                                    TabletHelper.SetTabletTextQuickActions($@"{GameRoot.getHotkeyStringFromAction("Action")}: Drag Y*{tool.CurrentBlueprint.SizeY}
{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate
Tap {GameRoot.getHotkeyStringFromAction("Modifier 2")}: Move Mode");
                                    break;

                                case 4:
                                    tool.dragArrowMaterial = ResourceDB.material_glow_blue;
                                    TabletHelper.SetTabletTextQuickActions($@"{GameRoot.getHotkeyStringFromAction("Action")}: Drag Z*{tool.CurrentBlueprint.SizeZ}
{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate
Tap {GameRoot.getHotkeyStringFromAction("Modifier 2")}: Move Mode");
                                    break;

                                case 5:
                                    tool.dragArrowMaterial = ResourceDB.material_glow_blue;
                                    TabletHelper.SetTabletTextQuickActions($@"{GameRoot.getHotkeyStringFromAction("Action")}: Drag Z*{tool.CurrentBlueprint.SizeZ}
{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate
Tap {GameRoot.getHotkeyStringFromAction("Modifier 2")}: Move Mode");
                                    break;
                            }

                            if (InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
                            {
                                if (GlobalStateManager.getRewiredPlayer0().GetButtonDown(_idAction))
                                {
                                    mode = Mode.XPos + faceIndex;
                                    tool.dragFaceRay = new Ray(tool.dragFaceRay.origin, normal);
                                    tool.dragArrowOffset = 0.5f;
                                }
                                else if (GlobalStateManager.getRewiredPlayer0().GetButtonUp(_idModifier2))
                                {
                                    if (_altHeldTime < 0.5)
                                    {
                                        tool.SelectMode(tool.modeMove);
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
                            TabletHelper.SetTabletTextQuickActions($@"{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate
Tap {GameRoot.getHotkeyStringFromAction("Modifier 2")}: Move Mode");
                        }
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
                            bool positive = (direction & 0x1) == 0;
                            int axis = direction >> 1;
                            int dragStep = tool.CurrentBlueprintSize[axis];
                            var roundedOffset = Mathf.RoundToInt(offset / dragStep);
                            if (Mathf.Abs(roundedOffset) >= 1)
                            {
                                var offsetVector = CustomHandheldMode.faceNormals[direction] * roundedOffset;
                                var changed = false;

                                if (positive)
                                {
                                    var old = tool.repeatTo;
                                    tool.repeatTo = Vector3Int.Max(tool.repeatFrom, tool.repeatTo + offsetVector);
                                    if (tool.repeatTo != old) changed = true;
                                    else
                                    {
                                        mode = Mode.XPos + (direction ^ 0x1);
                                        tool.dragFaceRay = new Ray(tool.dragFaceRay.origin, -tool.dragFaceRay.direction);
                                    }
                                }
                                else
                                {
                                    var old = tool.repeatFrom;
                                    tool.repeatFrom = Vector3Int.Min(tool.repeatTo, tool.repeatFrom + offsetVector);
                                    if (tool.repeatFrom != old) changed = true;
                                    else
                                    {
                                        mode = Mode.XPos + (direction ^ 0x1);
                                        tool.dragFaceRay = new Ray(tool.dragFaceRay.origin, -tool.dragFaceRay.direction);
                                    }
                                }

                                tool.dragFaceRay.origin += CustomHandheldMode.faceNormals[direction] * (roundedOffset * dragStep);
                                if (changed) tool.RefreshBlueprint();
                            }
                        }
                    }
                    break;
            }
        }
    }
}