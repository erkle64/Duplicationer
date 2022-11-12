using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Duplicationer
{
    internal class PlaceholderPattern
    {
        private static Dictionary<int, PlaceholderPattern> instances = new Dictionary<int, PlaceholderPattern>();

        public static PlaceholderPattern Instance(GameObject source)
        {
            PlaceholderPattern instance;
            int instanceId = source.GetInstanceID();
            if (instances.TryGetValue(instanceId, out instance)) return instance;

            return instances[instanceId] = new PlaceholderPattern(source);
        }

        public Entry[] Entries { get; private set; }

        private PlaceholderPattern(GameObject source)
        {
            UnhollowerBaseLib.Il2CppArrayBase<MeshFilter> meshFilters = source.GetComponentsInChildren<MeshFilter>(true);

            Entries = new Entry[meshFilters.Sum((meshFilter) => meshFilter.name == "Impostor" ? 0 : 1)];

            var localToWorldMatrix = source.transform.localToWorldMatrix;
            var worldToLocalMatrix = source.transform.worldToLocalMatrix;
            int index = 0;
            for (int i = 0; i < meshFilters.Count; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter.name == "Impostor") continue;

                var mesh = meshFilter.sharedMesh;
                var relativeTransform = worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
                Entries[index++] = new Entry(mesh, relativeTransform);
            }
        }

        public struct Entry
        {
            public Mesh mesh;
            public Matrix4x4 relativeTransform;

            public Entry(Mesh mesh, Matrix4x4 relativeTransform)
            {
                this.mesh = mesh;
                this.relativeTransform = relativeTransform;
            }
        }
    }
}
