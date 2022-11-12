using System;
using Unfoundry;
using UnityEngine;

namespace Duplicationer
{
    internal class BlueprintToolModePlace : BlueprintToolMode
    {
        public BlueprintToolMode FallbackMode { get; private set; } = null;
        public BlueprintToolMode NextMode { get; private set; } = null;

        public BlueprintToolModePlace()
        {
        }

        public override string TabletTitle(BlueprintToolCHM tool) => "Place Blueprint";

        public override void Enter(BlueprintToolCHM tool, BlueprintToolMode fromMode)
        {
            if (!tool.IsBlueprintLoaded)
            {
                tool.SelectMode(FallbackMode);
                return;
            }

            TabletHelper.SetTabletTextQuickActions("LMB: Place Blueprint");
            tool.HideBlueprint();
            tool.boxMode = BlueprintToolCHM.BoxMode.None;
        }

        public override bool AllowCopy(BlueprintToolCHM tool) => false;
        public override bool AllowPaste(BlueprintToolCHM tool) => false;

        public override void Update(BlueprintToolCHM tool)
        {
            if (Input.GetKeyDown(KeyCode.Mouse0) && InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
            {
                tool.boxMode = BlueprintToolCHM.BoxMode.None;
                tool.HideBlueprint();
            }
            if (Input.GetKey(KeyCode.Mouse0) && InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
            {
                tool.repeatFrom = tool.repeatTo = Vector3Int.zero;
                tool.boxMode = BlueprintToolCHM.BoxMode.Blueprint;
                Vector3 targetPoint;
                Vector3Int targetCoord, targetNormal;
                if (CustomHandheldMode.GetTargetCube(0.01f, out targetPoint, out targetCoord, out targetNormal))
                {
                    tool.ShowBlueprint(targetCoord - new Vector3Int(tool.CurrentBlueprint.SizeX / 2, 0, tool.CurrentBlueprint.SizeZ / 2));
                }
            }
            if (Input.GetKeyUp(KeyCode.Mouse0) && tool.IsBlueprintActive && InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
            {
                tool.SelectMode(NextMode);
                TabletHelper.SetTabletTextQuickActions("");
            }
        }

        internal void Connect(BlueprintToolMode fallbackMode, BlueprintToolMode nextMode)
        {
            FallbackMode = fallbackMode;
            NextMode = nextMode;
        }
    }
}