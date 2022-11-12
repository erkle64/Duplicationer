using Newtonsoft.Json;
using UnityEngine;

// dotnet mpc -i E:\Development\CS\Duplicationer\Duplicationer\Duplicationer.csproj -o E:\Development\CS\Duplicationer\Duplicationer\Formatters.cs

namespace Duplicationer
{
    public struct BlueprintData
    {
        public BlueprintData(int buildingCount, Vector3Int size)
        {
            buildableObjects = new BuildableObjectData[buildingCount];
            blocks = new BlockData();
        }

        public struct BuildableObjectData
        {
            public struct CustomData
            {
                public string identifier;
                public string value;

                public CustomData(string identifier, object value)
                {
                    this.identifier = identifier;
                    this.value = value.ToString();
                }
            }

            public ulong originalEntityId;
            public string templateName;
            public ulong templateId;
            public int worldX;
            public int worldY;
            public int worldZ;
            public float orientationUnlockedX;
            public float orientationUnlockedY;
            public float orientationUnlockedZ;
            public float orientationUnlockedW;
            public byte orientationY;
            public byte itemMode;
            public CustomData[] customData;

            [JsonIgnore]
            public Quaternion orientationUnlocked
            {
                get => new Quaternion(orientationUnlockedX, orientationUnlockedY, orientationUnlockedZ, orientationUnlockedW);
                set
                {
                    orientationUnlockedX = value.x;
                    orientationUnlockedY = value.y;
                    orientationUnlockedZ = value.z;
                    orientationUnlockedW = value.w;
                }
            }

            [JsonIgnore]
            public Vector3Int worldPos
            {
                get => new Vector3Int(worldX, worldY, worldZ);
                set
                {
                    worldX = value.x;
                    worldY = value.y;
                    worldZ = value.z;
                }
            }
        }

        public struct BlockData
        {
            public int sizeX;
            public int sizeY;
            public int sizeZ;
            public byte[] ids;

            public BlockData(Vector3Int size)
            {
                sizeX = size.x;
                sizeY = size.y;
                sizeZ = size.z;
                ids = new byte[size.x * size.y * size.z];
            }

            public BlockData(Vector3Int size, byte[] ids)
            {
                sizeX = size.x;
                sizeY = size.y;
                sizeZ = size.z;
                this.ids = ids;
            }

            public Vector3Int Size => new Vector3Int(sizeX, sizeY, sizeZ);
        }

        public BuildableObjectData[] buildableObjects;
        public BlockData blocks;
    }
}
