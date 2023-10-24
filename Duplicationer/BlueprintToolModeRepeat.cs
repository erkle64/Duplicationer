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

        public BlueprintToolModeRepeat()
        {
        }

        public override string TabletTitle(BlueprintToolCHM tool) => "Place BLueprint - Repeat";

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
                                    tool.dragArrowMaterial = ResourceDB.material_glow_red;
                                    TabletHelper.SetTabletTextQuickActions($"LMB: Drag +X*{tool.CurrentBlueprint.SizeX}");
                                    break;

                                case 1:
                                    tool.dragArrowMaterial = ResourceDB.material_glow_red;
                                    TabletHelper.SetTabletTextQuickActions($"LMB: Drag -X*{tool.CurrentBlueprint.SizeX}");
                                    break;

                                case 2:
                                    tool.dragArrowMaterial = ResourceDB.material_glow_yellow;
                                    TabletHelper.SetTabletTextQuickActions($"LMB: Drag +Y*{tool.CurrentBlueprint.SizeY}");
                                    break;

                                case 3:
                                    tool.dragArrowMaterial = ResourceDB.material_glow_yellow;
                                    TabletHelper.SetTabletTextQuickActions($"LMB: Drag -Y*{tool.CurrentBlueprint.SizeY}");
                                    break;

                                case 4:
                                    tool.dragArrowMaterial = ResourceDB.material_glow_purple;
                                    TabletHelper.SetTabletTextQuickActions($"LMB: Drag +Z*{tool.CurrentBlueprint.SizeZ}");
                                    break;

                                case 5:
                                    tool.dragArrowMaterial = ResourceDB.material_glow_purple;
                                    TabletHelper.SetTabletTextQuickActions($"LMB: Drag -Z*{tool.CurrentBlueprint.SizeZ}");
                                    break;
                            }

                            if (Input.GetKeyDown(KeyCode.Mouse0) && InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
                            {
                                mode = Mode.XPos + faceIndex;
                            }
                        }
                        else
                        {
                            tool.isDragArrowVisible = false;
                            TabletHelper.SetTabletTextQuickActions("");
                        }
                    }
                    break;

                case Mode.XPos:
                case Mode.XNeg:
                case Mode.YPos:
                case Mode.YNeg:
                case Mode.ZPos:
                case Mode.ZNeg:
                    if (!Input.GetKey(KeyCode.Mouse0))
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
                                    tool.repeatTo = Vector3Int.Max(Vector3Int.zero, tool.repeatTo + offsetVector);
                                    if (tool.repeatTo != old) changed = true;
                                }
                                else
                                {
                                    var old = tool.repeatFrom;
                                    tool.repeatFrom = Vector3Int.Min(Vector3Int.zero, tool.repeatFrom + offsetVector);
                                    if (tool.repeatFrom != old) changed = true;
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