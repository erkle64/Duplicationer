using UnityEngine;
using UnityEngine.Rendering;
using static Duplicationer.BepInExLoader;

namespace Duplicationer
{
    public abstract class CustomHandheldMode
    {
        public abstract void UpdateBehavoir();
        public abstract void Enter();
        public abstract void Exit();
        public abstract void HideMenu(int selected);
        public abstract void ShowMenu();
        public abstract bool OnRotateY();

        protected static void DrawBox(Vector3 from, Vector3 to, Material material)
        {
            var matrix = Matrix4x4.TRS((from + to) * 0.5f, Quaternion.identity, to - from);
            Graphics.DrawMesh(ResourceDB.mesh_cubeCenterPivot, matrix, material, GlobalStaticCache.s_Layer_DragHelper, Camera.main, 0, null, false, false, false);
        }

        protected static void DrawBox(Vector3 from, Vector3 to, Vector3 expand, Material material)
        {
            DrawBox(from - expand, to + expand, material);
        }

        private static Matrix4x4[] edgeMatrices = new Matrix4x4[12];
        protected static void DrawBoxWithEdges(Vector3 from, Vector3 to, float faceOffset, float edgeSize, Material faceMaterial, Material edgeMaterial)
        {
            DrawBox(from - new Vector3(faceOffset, faceOffset, faceOffset), to + new Vector3(faceOffset, faceOffset, faceOffset), faceMaterial);

            var expand = Vector3.one * edgeSize;
            Matrix4x4 buildEdgeMatrix(Vector3 min, Vector3 max)
            {
                return Matrix4x4.TRS((min + max) * 0.5f, Quaternion.identity, max - min + expand);
            }

            Vector3 v1 = new Vector3(from.x, from.y, from.z);
            Vector3 v2 = new Vector3(to.x, from.y, from.z);
            Vector3 v3 = new Vector3(from.x, to.y, from.z);
            Vector3 v4 = new Vector3(to.x, to.y, from.z);
            Vector3 v5 = new Vector3(from.x, from.y, to.z);
            Vector3 v6 = new Vector3(to.x, from.y, to.z);
            Vector3 v7 = new Vector3(from.x, to.y, to.z);
            Vector3 v8 = new Vector3(to.x, to.y, to.z);
            edgeMatrices[0] = buildEdgeMatrix(v1, v2);
            edgeMatrices[1] = buildEdgeMatrix(v3, v4);
            edgeMatrices[2] = buildEdgeMatrix(v5, v6);
            edgeMatrices[3] = buildEdgeMatrix(v7, v8);
            edgeMatrices[4] = buildEdgeMatrix(v1, v3);
            edgeMatrices[5] = buildEdgeMatrix(v2, v4);
            edgeMatrices[6] = buildEdgeMatrix(v5, v7);
            edgeMatrices[7] = buildEdgeMatrix(v6, v8);
            edgeMatrices[8] = buildEdgeMatrix(v1, v5);
            edgeMatrices[9] = buildEdgeMatrix(v2, v6);
            edgeMatrices[10] = buildEdgeMatrix(v3, v7);
            edgeMatrices[11] = buildEdgeMatrix(v4, v8);
            Graphics.DrawMeshInstanced(ResourceDB.mesh_cubeCenterPivot, 0, edgeMaterial, edgeMatrices, 12, null, ShadowCastingMode.Off, false, GlobalStaticCache.s_Layer_DragHelper, Camera.main, LightProbeUsage.Off);
        }

        protected static void DrawArrow(Vector3 origin, Vector3 direction, Material material, float scale = 1.0f, float offset = 0.5f)
        {
            Matrix4x4 matrix = Matrix4x4.TRS(origin + direction * offset, Quaternion.LookRotation(direction) * Quaternion.EulerRotation(0.0f, Mathf.PI * 0.5f, 0.0f), Vector3.one * scale);
            Graphics.DrawMesh(ResourceDB.resourceLinker.mesh_arrow_centerPivot, matrix, material, GlobalStaticCache.s_Layer_DragHelper, Camera.main, 0, null, false, false, false);
        }

        protected static Ray GetLookRay()
        {
            return GameRoot.getClientRenderCharacter().getLookRay();
        }

        protected static bool GetTargetCube(float offset, out Vector3 targetPoint, out Vector3Int targetCoord, out Vector3Int targetNormal)
        {
            var lookRay = GetLookRay();
            RaycastHit hitInfo;
            if (Physics.Raycast(lookRay, out hitInfo, 300.0f, GlobalStaticCache.s_LayerMask_Terrain | GlobalStaticCache.s_LayerMask_TerrainTileCollider | GlobalStaticCache.s_LayerMask_BuildableObjectFullSize | GlobalStaticCache.s_LayerMask_BuildableObjectPartialSize))
            {
                targetPoint = hitInfo.point;
                var normal = GameRoot.SnappedToNearestAxis(hitInfo.normal);
                targetCoord = Vector3Int.FloorToInt(targetPoint + normal * offset);
                targetNormal = new Vector3Int(Mathf.RoundToInt(normal.x), Mathf.RoundToInt(normal.y), Mathf.RoundToInt(normal.z));
                return true;
            }

            targetPoint = Vector3.zero;
            targetCoord = Vector3Int.zero;
            targetNormal = Vector3Int.up;
            return false;
        }

        protected static bool GetTargetCube(float offset, out Vector3Int targetCoord)
        {
            var lookRay = GetLookRay();
            RaycastHit hitInfo;
            if (Physics.Raycast(lookRay, out hitInfo, 30.0f, GlobalStaticCache.s_LayerMask_Terrain | GlobalStaticCache.s_LayerMask_TerrainTileCollider | GlobalStaticCache.s_LayerMask_BuildableObjectFullSize | GlobalStaticCache.s_LayerMask_BuildableObjectPartialSize))
            {
                var normal = GameRoot.SnappedToNearestAxis(hitInfo.normal);
                targetCoord = Vector3Int.FloorToInt(hitInfo.point + normal * offset);
                return true;
            }

            targetCoord = Vector3Int.zero;
            return false;
        }

        protected static readonly Vector3Int[] faceNormals = new Vector3Int[6]
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1)
        };
        protected static float BoxRayIntersection(Vector3 from, Vector3 to, Ray ray, out Vector3Int normal, out int faceIndex)
        {
            float dist;
            if (Bounds.IntersectRayAABB(ray, new Bounds((from + to) * 0.5f, to - from), out dist))
            {
                float maxDistance = -1.0f;
                normal = Vector3Int.zero;
                faceIndex = -1;
                for (int i = 0; i < 6; ++i)
                {
                    if (Vector3.Dot(faceNormals[i], ray.direction) < 0.0f)
                    {
                        float distance;
                        if (new Plane(faceNormals[i], ((i & 1) == 0) ? to : from).Raycast(ray, out distance))
                        {
                            if (maxDistance < 0.0f || distance > maxDistance)
                            {
                                maxDistance = distance;
                                normal = faceNormals[i];
                                faceIndex = i;
                            }
                        }
                    }
                }

                return dist;
            }

            faceIndex = -1;
            normal = Vector3Int.up;
            return -1.0f;
        }

        protected static bool TryGetAxialDragOffset(Ray axisRay, Ray lookRay, out float offset)
        {
            offset = 0.0f;
            if (Vector3.Dot(lookRay.direction, axisRay.origin - lookRay.origin) < 0.0f) return false;

            Vector3 right = Vector3.Cross(lookRay.direction, axisRay.direction);
            Vector3 planeNormal = Vector3.Cross(right, axisRay.direction);
            var plane = new Plane(planeNormal, axisRay.origin);

            float distance;
            if (!plane.Raycast(lookRay, out distance)) return false;

            offset = Vector3.Dot(axisRay.direction, lookRay.GetPoint(distance) - axisRay.origin);

            return true;
        }

        protected static void DumpMaterial(Material material)
        {
            var shader = material.shader;
            int count = shader.GetPropertyCount();
            log.LogInfo((string)$"============== {shader.name} ===============");
            for (int i = 0; i < count; ++i)
            {
                string value;
                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color:
                        value = material.GetColor(shader.GetPropertyName(i)).ToString();
                        break;
                    case ShaderPropertyType.Vector:
                        value = material.GetVector(shader.GetPropertyName(i)).ToString();
                        break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        value = material.GetFloat(shader.GetPropertyName(i)).ToString();
                        break;
                    case ShaderPropertyType.Texture:
                        var texture = material.GetTexture(shader.GetPropertyName(i));
                        value = texture == null ? "null" : texture.dimension.ToString();
                        break;
                    default:
                        value = "<undefined>";
                        break;
                }
                log.LogInfo((string)$"{shader.GetPropertyType(i)} {shader.GetPropertyName(i)} = {value}");
            }
        }
    }
}
