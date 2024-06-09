using System.Collections.Generic;
using Unfoundry;
using UnityEngine;

namespace Duplicationer
{
    internal abstract class BaseFrame : IEscapeCloseable
    {
        protected BlueprintToolCHM _tool;

        protected GameObject _frameRoot = null;

        protected List<UIBuilder.GenericUpdateDelegate> _guiUpdaters = new List<UIBuilder.GenericUpdateDelegate>();

        public bool IsOpen => _frameRoot != null && _frameRoot.activeSelf;

        public BaseFrame(BlueprintToolCHM tool)
        {
            _tool = tool;
        }

        protected void Shown()
        {
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIOpen);

            _frameRoot.SetActive(true);
            GlobalStateManager.addCursorRequirement();
            GlobalStateManager.registerEscapeCloseable(this);
        }

        public void Hide(bool silent = false)
        {
            if (_frameRoot == null || !_frameRoot.activeSelf) return;

            if (!silent) AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIClose);

            _frameRoot.SetActive(false);
            GlobalStateManager.removeCursorRequirement();
            GlobalStateManager.deRegisterEscapeCloseable(this);
        }

        internal void Update()
        {
            if (!IsOpen) return;

            foreach (var updater in _guiUpdaters) updater();
        }

        public void iec_triggerFrameClose()
        {
            Hide();
        }
    }
}
