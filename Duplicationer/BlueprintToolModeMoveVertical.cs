using Unfoundry;
using UnityEngine;

namespace Duplicationer
{
    internal class BlueprintToolModeMoveVertical : BlueprintToolMode
    {
        private enum Mode
        {
            Idle,
            Move
        }

        private Mode mode = Mode.Idle;

        private int _idAction = -1;
        private int _idModifier2 = -1;
        private double _altHeldTime;

        public BlueprintToolModeMoveVertical()
        {
        }

        public override string TabletTitle(BlueprintToolCHM tool) => "Place BLueprint - Move Vertical";

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
                    Vector3Int normal;
                    int faceIndex;
                    float distance = CustomHandheldMode.BoxRayIntersection(tool.RepeatBlueprintMin, tool.RepeatBlueprintMax + Vector3Int.one, lookRay, out normal, out faceIndex);
                    if (distance >= 0.0f)
                    {
                        var point = lookRay.GetPoint(distance) + (Vector3)normal * 0.5f;
                        tool.isDragArrowVisible = true;
                        tool.isDragArrowDouble = faceIndex != 2 && faceIndex != 3;
                        tool.dragFaceRay = new Ray(point, faceIndex != 3 ? Vector3.up : Vector3.down);
                        tool.dragArrowMaterial = ResourceDB.material_glow_yellow;
                        tool.dragArrowOffset = tool.isDragArrowDouble ? 0.25f : 0.0f;
                        TabletHelper.SetTabletTextQuickActions($@"{GameRoot.getHotkeyStringFromAction("Action")}: Drag Y
Alt+{GameRoot.getHotkeyStringFromAction("Action")}: Drag Y*{tool.CurrentBlueprint.SizeY}
{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate
Tap {GameRoot.getHotkeyStringFromAction("Modifier 2")}: Move Mode");

                        if (InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
                        {
                            if (GlobalStateManager.getRewiredPlayer0().GetButtonDown(_idAction))
                            {
                                mode = Mode.Move;
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
                    break;

                case Mode.Move:
                    if (!GlobalStateManager.getRewiredPlayer0().GetButton(_idAction))
                    {
                        mode = Mode.Idle;
                    }
                    else
                    {
                        TabletHelper.SetTabletTextQuickActions($@"{GameRoot.getHotkeyStringFromAction("Action")}: Drag Y
Alt+{GameRoot.getHotkeyStringFromAction("Action")}: Drag Y*{tool.CurrentBlueprint.SizeY}");
                        float offset;
                        if (CustomHandheldMode.TryGetAxialDragOffset(new Ray(tool.dragFaceRay.origin, Vector3.up), CustomHandheldMode.GetLookRay(), out offset))
                        {
                            int dragStep = InputHelpers.IsAltHeld ? tool.CurrentBlueprintSize.y : 1;
                            var roundedOffset = Mathf.RoundToInt(offset / dragStep) * dragStep;
                            if (Mathf.Abs(roundedOffset) >= dragStep)
                            {
                                var offsetVector = new Vector3Int(0, 1, 0) * roundedOffset;
                                tool.MoveBlueprint(tool.CurrentBlueprintAnchor + offsetVector);

                                tool.dragFaceRay.origin += new Vector3Int(0, 1, 0) * roundedOffset;
                            }
                        }
                    }
                    break;
            }
        }
    }
}