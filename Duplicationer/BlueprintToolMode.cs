﻿using Unfoundry;
using UnityEngine;

namespace Duplicationer
{
    internal abstract class BlueprintToolMode
    {
        public abstract bool AllowCopy(BlueprintToolCHM tool);
        public abstract bool AllowPaste(BlueprintToolCHM tool);
        public abstract bool AllowRotate(BlueprintToolCHM tool);
        public abstract bool AllowMirror(BlueprintToolCHM tool);

        public virtual void Enter(BlueprintToolCHM tool, BlueprintToolMode fromMode)
        {
        }

        public virtual void Exit(BlueprintToolCHM tool)
        {
            TabletHelper.SetTabletTextQuickActions("");
            tool.HideBlueprint();
            tool.boxMode = BlueprintToolCHM.BoxMode.None;
            tool.isDragArrowVisible = false;
            tool.isDragArrowDouble = false;
            tool.HideBlueprintFrame();
        }

        public abstract void Update(BlueprintToolCHM tool);

        public abstract string TabletTitle(BlueprintToolCHM tool);

        public virtual bool GetSubText(BlueprintToolCHM tool, out string subText)
        {
            subText = "";
            return false;
        }
    }
}
