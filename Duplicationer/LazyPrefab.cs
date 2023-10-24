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
                prefab = DuplicationerPlugin.GetAsset<GameObject>(assetPath);
                if (prefab == null) throw new System.Exception(assetPath);
                return prefab;
            }
        }
    }
}
