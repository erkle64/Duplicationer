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

        public BlueprintToolModeMoveVertical()
        {
        }

        public override string TabletTitle(BlueprintToolCHM tool) => "Place BLueprint - Move Vertical";

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
                        var point = lookRay.GetPoint(distance) + (Vector3)normal * 0.5f;
                        tool.isDragArrowVisible = true;
                        tool.dragFaceRay = new Ray(point, Vector3.up);
                        tool.dragArrowMaterial = ResourceDB.material_glow_yellow;
                        tool.dragArrowOffset = 0.0f;
                        TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Drag Y\nAlt+{GameRoot.getHotkeyStringFromAction("Action")}: Drag Y*{tool.CurrentBlueprint.SizeY}\n{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate");

                        if (GlobalStateManager.getRewiredPlayer0().GetButtonDown("Action") && InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen) mode = Mode.Move;
                    }
                    else
                    {
                        tool.isDragArrowVisible = false;
                        TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate");
                    }
                    break;

                case Mode.Move:
                    if (!GlobalStateManager.getRewiredPlayer0().GetButton("Action"))
                    {
                        mode = Mode.Idle;
                    }
                    else
                    {
                        float offset;
                        if (CustomHandheldMode.TryGetAxialDragOffset(tool.dragFaceRay, CustomHandheldMode.GetLookRay(), out offset))
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