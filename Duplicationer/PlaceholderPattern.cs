using System.Collections.Generic;
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
            var meshFilters = source.GetComponentsInChildren<MeshFilter>(true);

            var entryCount = 0;
            foreach (var meshFilter in meshFilters) if (IsValidMeshFilter(meshFilter, source)) entryCount++;
            Entries = new Entry[entryCount];

            var localToWorldMatrix = source.transform.localToWorldMatrix;
            var worldToLocalMatrix = source.transform.worldToLocalMatrix;
            int index = 0;
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (!IsValidMeshFilter(meshFilter, source)) continue;

                var mesh = meshFilter.sharedMesh;
                var relativeTransform = worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
                Entries[index++] = new Entry(mesh, relativeTransform);
            }
        }

        private bool IsValidMeshFilter(MeshFilter meshFilter, GameObject root)
        {
            if (meshFilter.gameObject == root) return true;
            if (meshFilter.name == "Impostor" || meshFilter.sharedMesh == null || !meshFilter.gameObject.activeSelf) return false;
            var transform = meshFilter.transform;
            while (transform != null)
            {
                if (transform == root.transform) return true;
                if (!transform.gameObject.activeSelf) return false;
                transform = transform.parent;
            }
            return false;
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
