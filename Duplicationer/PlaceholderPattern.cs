using System.Collections.Generic;
using UnityEngine;

namespace Duplicationer
{
    internal class PlaceholderPattern
    {
        private static Dictionary<int, PlaceholderPattern> instances = new Dictionary<int, PlaceholderPattern>();

        public static PlaceholderPattern Instance(GameObject source, BuildableObjectTemplate template)
        {
            PlaceholderPattern instance;
            int instanceId = source.GetInstanceID();
            if (instances.TryGetValue(instanceId, out instance)) return instance;

            return instances[instanceId] = new PlaceholderPattern(source, template);
        }

        public Entry[] Entries { get; private set; }

        private PlaceholderPattern(GameObject source, BuildableObjectTemplate template)
        {
            var meshFilters = source.GetComponentsInChildren<MeshFilter>(true);

            var entryCount = 0;
            foreach (var meshFilter in meshFilters) if (IsValidMeshFilter(meshFilter, source)) entryCount++;
            if (source.TryGetComponent<ConveyorGO>(out var conveyorGO))
            {
                Matrix4x4 relativeTransform;
                if (template.conveyor_isSlope)
                {
                    if (template.identifier.Contains("down"))
                    {
                        relativeTransform = Matrix4x4.TRS(new Vector3(0.0f, 1.0f, 0.0f), Quaternion.Euler(0.0f, 180.0f, 26.0f), Vector3.one);
                    }
                    else
                    {
                        relativeTransform = Matrix4x4.TRS(new Vector3(0.0f, 1.0f, 0.0f), Quaternion.Euler(0.0f, 180.0f, -26.0f), Vector3.one);
                    }
                }
                else
                {
                    relativeTransform = Matrix4x4.TRS(new Vector3(0.0f, 0.5f, 0.0f), Quaternion.Euler(0.0f, 180.0f, 0.0f), Vector3.one);
                }

                Entries = new Entry[entryCount + 1];
                Entries[Entries.Length - 1] = new Entry(
                    ResourceDB.resourceLinker.mesh_arrow_centerPivot,
                    relativeTransform
                    );
            }
            else
            {
                Entries = new Entry[entryCount];
            }

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
