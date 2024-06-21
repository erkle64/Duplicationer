using UnityEngine;

namespace Duplicationer
{
    public class BlueprintRequest : IEvent
    {
        public Vector3Int size;
        public Building[] buildings;
        public byte[] blocks;

        public struct Building
        {
            public ulong templateId;
            public Vector3Int anchorPosition;
            public BuildingManager.BuildOrientation orientationY;
            public Quaternion orientationUnlocked;
            public byte itemMode;
            public (string, object)[] customData;
        }
    }
}