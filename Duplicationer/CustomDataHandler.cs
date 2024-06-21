using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TinyJSON;
using Unfoundry;
using static Duplicationer.Blueprint;

namespace Duplicationer
{
    public struct CustomDataWrapper
    {
        public readonly List<BlueprintData.BuildableObjectData.CustomData> customData;

        public CustomDataWrapper(List<BlueprintData.BuildableObjectData.CustomData> customData)
        {
            this.customData = customData;
        }

        public CustomDataWrapper(BlueprintData.BuildableObjectData.CustomData[] customData)
        {
            this.customData = new List<BlueprintData.BuildableObjectData.CustomData>(customData);
        }

        public void Add(in string identifier, in object value)
        {
            customData.Add(new BlueprintData.BuildableObjectData.CustomData(identifier, value));
        }

        public bool HasCustomData(in string identifier)
        {
            foreach (var customDataEntry in customData) if (customDataEntry.identifier == identifier) return true;
            return false;
        }

        public T GetCustomData<T>(in string identifier)
        {
            foreach (var customDataEntry in customData) if (customDataEntry.identifier == identifier) return (T)System.Convert.ChangeType(customDataEntry.value, typeof(T));
            return default;
        }

        public void GetCustomDataList<T>(in string identifier, List<T> list)
        {
            foreach (var customDataEntry in customData) if (customDataEntry.identifier == identifier) list.Add((T)System.Convert.ChangeType(customDataEntry.value, typeof(T)));
        }
    }

    public abstract class CustomDataGatherer
    {
        public abstract bool ShouldGather(BuildableObjectGO bogo, Type bogoType);
        public abstract void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings);

        private static CustomDataGatherer[] _gatherers = null;
        public static CustomDataGatherer[] All
        {
            get
            {
                if (_gatherers == null) {
                    _gatherers = System.Reflection.Assembly
                        .GetAssembly(typeof(CustomDataGatherer))
                        .GetTypes()
                        .Where(t => typeof(CustomDataGatherer).IsAssignableFrom(t) && !t.IsAbstract)
                        .Select(t => (CustomDataGatherer)Activator.CreateInstance(t))
                        .ToArray();
                }
                return _gatherers;
            }
        }
    }

    public abstract class TypedCustomDataGatherer<T> : CustomDataGatherer
    {
        public override bool ShouldGather(BuildableObjectGO bogo, Type bogoType)
            => typeof(T).IsAssignableFrom(bogoType);
    }

    public abstract class CustomDataApplier
    {
        public abstract bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData);
        public abstract void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap);

        private static CustomDataApplier[] _appliers = null;
        public static CustomDataApplier[] All
        {
            get
            {
                if (_appliers == null) {
                    _appliers = System.Reflection.Assembly
                        .GetAssembly(typeof(CustomDataApplier))
                        .GetTypes()
                        .Where(t => typeof(CustomDataApplier).IsAssignableFrom(t) && !t.IsAbstract)
                        .Select(t => (CustomDataApplier)Activator.CreateInstance(t))
                        .ToArray();
                }
                return _appliers;
            }
        }
    }
}
