using System;
using System.IO;
using UnityEngine;

namespace Duplicationer
{
    public class LazyPrefab
    {
        private string assetPath;
        private GameObject prefab = null;

        public LazyPrefab(string assetPath)
        {
            this.assetPath = assetPath;
        }

        public GameObject Prefab
        {
            get
            {
                if (prefab != null) return prefab;
                if (Plugin.bundleMain == null) throw new ArgumentNullException(nameof(Plugin.bundleMain));
                prefab = Plugin.bundleMain.LoadAsset<GameObject>(assetPath);
                if (prefab == null) throw new FileLoadException(assetPath);
                return prefab;
            }
        }
    }
}
