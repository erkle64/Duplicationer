using static Duplicationer.Blueprint;
using System.Collections.Generic;
using TinyJSON;
using Unfoundry;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace Duplicationer
{
    public class CDA_CraftingRecipe : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("craftingRecipeId");

        public override void Apply(
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
            Dictionary<ulong, ulong> entityIdMap)
        {
            var craftingRecipeId = customData.GetCustomData<ulong>("craftingRecipeId");
            if (craftingRecipeId != 0)
            {
                var recipe = ItemTemplateManager.getCraftingRecipeById(craftingRecipeId);
                if (recipe != null && (DuplicationerPlugin.configAllowUnresearchedRecipes.Get() || recipe.isResearched()))
                {
                    usePasteConfigSettings = true;
                    pasteConfigSettings_01 = craftingRecipeId;
                }
            }
        }
    }

    public class CDA_Loader : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("isInputLoader");

        public override void Apply(
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
            Dictionary<ulong, ulong> entityIdMap)
        {
            usePasteConfigSettings = true;
            bool isInputLoader = customData.GetCustomData<bool>("isInputLoader");
            pasteConfigSettings_01 = isInputLoader ? 1u : 0u;

            if (bot.loader_isFilter && customData.HasCustomData("loaderFilterTemplateId"))
            {
                var loaderFilterTemplateId = customData.GetCustomData<ulong>("loaderFilterTemplateId");
                if (loaderFilterTemplateId > 0)
                {
                    usePasteConfigSettings = true;
                    pasteConfigSettings_02 = loaderFilterTemplateId;
                }
            }
        }
    }

    public class CDA_ConveyorBalancer : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => bot.type == BuildableObjectTemplate.BuildableObjectType.ConveyorBalancer;

        public override void Apply(
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
            Dictionary<ulong, ulong> entityIdMap)
        {
            var balancerInputPriority = customData.GetCustomData<int>("balancerInputPriority");
            var balancerOutputPriority = customData.GetCustomData<int>("balancerOutputPriority");
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    GameRoot.addLockstepEvent(new SetConveyorBalancerConfig(usernameHash, task.entityId, balancerInputPriority, balancerOutputPriority));
                }
            });
        }
    }

    public class CDA_Sign : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => bot.type == BuildableObjectTemplate.BuildableObjectType.Sign;

        public override void Apply(
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
            Dictionary<ulong, ulong> entityIdMap)
        {
            var signText = customData.GetCustomData<string>("signText");
            var signUseAutoTextSize = customData.GetCustomData<byte>("signUseAutoTextSize");
            var signTextMinSize = customData.GetCustomData<float>("signTextMinSize");
            var signTextMaxSize = customData.GetCustomData<float>("signTextMaxSize");
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    GameRoot.addLockstepEvent(new SignSetTextEvent(usernameHash, task.entityId, signText, signUseAutoTextSize != 0, signTextMinSize, signTextMaxSize));
                }
            });
        }
    }

    public class CDA_BlastFurnaceMode : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("blastFurnaceModeTemplateId");

        public override void Apply(
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
            Dictionary<ulong, ulong> entityIdMap)
        {
            var modeTemplateId = customData.GetCustomData<ulong>("blastFurnaceModeTemplateId");
            if (modeTemplateId > 0)
            {
                postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                {
                    if (task.entityId > 0)
                    {
                        GameRoot.addLockstepEvent(new BlastFurnaceSetModeEvent(usernameHash, task.entityId, modeTemplateId));
                    }
                });
            }
        }
    }

    public class CDA_StorageLockedSlots : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => bot.type == BuildableObjectTemplate.BuildableObjectType.Storage && customData.HasCustomData("firstSoftLockedSlotIdx");

        public override void Apply(
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
            Dictionary<ulong, ulong> entityIdMap)
        {
            var firstSoftLockedSlotIdx = customData.GetCustomData<uint>("firstSoftLockedSlotIdx");
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    ulong inventoryId = 0UL;
                    if (BuildingManager.buildingManager_getInventoryAccessors(task.entityId, 0U, ref inventoryId) == IOBool.iotrue)
                    {
                        GameRoot.addLockstepEvent(new SetSoftLockForInventory(usernameHash, inventoryId, firstSoftLockedSlotIdx));
                    }
                    else
                    {
                        DuplicationerPlugin.log.LogWarning("Failed to get inventory accessor for storage");
                    }
                }
                else
                {
                    DuplicationerPlugin.log.LogWarning("Failed to get entity id for storage");
                }
            });
        }
    }

    public class CDA_DroneTransportConditions : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => bot.type == BuildableObjectTemplate.BuildableObjectType.DroneTransport && customData.HasCustomData("loadConditionFlags");

        public override void Apply(
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
            Dictionary<ulong, ulong> entityIdMap)
        {
            var loadConditionFlags = customData.GetCustomData<byte>("loadConditionFlags");
            var loadCondition_comparisonType = customData.GetCustomData<byte>("loadCondition_comparisonType");
            var loadCondition_fillRatePercentage = customData.GetCustomData<byte>("loadCondition_fillRatePercentage");
            var loadCondition_seconds = customData.GetCustomData<uint>("loadCondition_seconds");
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    GameRoot.addLockstepEvent(new DroneTransportLoadConditionEvent(usernameHash, task.entityId, loadConditionFlags, loadCondition_fillRatePercentage, loadCondition_seconds, loadCondition_comparisonType));
                }
                else
                {
                    DuplicationerPlugin.log.LogWarning("Failed to get entity id for drone transport");
                }
            });
        }
    }

    public class CDA_DroneTransportName : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => bot.type == BuildableObjectTemplate.BuildableObjectType.DroneTransport && customData.HasCustomData("stationName");

        public override void Apply(
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
            Dictionary<ulong, ulong> entityIdMap)
        {
            var stationName = customData.GetCustomData<string>("stationName");
            var stationType = customData.GetCustomData<byte>("stationType");
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    GameRoot.addLockstepEvent(new DroneTransportSetNameEvent(usernameHash, stationName, task.entityId, stationType));
                }
                else
                {
                    DuplicationerPlugin.log.LogWarning("Failed to get entity id for drone transport");
                }
            });
        }
    }

    public class CDA_ModularBuildingData : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("modularBuildingData");

        public override void Apply(
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
            Dictionary<ulong, ulong> entityIdMap)
        {
            var modularBuildingDataJSON = customData.GetCustomData<string>("modularBuildingData");
            var modularBuildingData = JSON.Load(modularBuildingDataJSON).Make<ModularBuildingData>();
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    byte out_mbState = 0;
                    byte out_isEnabled = 0;
                    byte out_canBeEnabled = 0;
                    byte out_constructionIsDismantle = 0;
                    uint out_assignedConstructionDronePorts = 0;
                    ModularBuildingManagerFrame.modularEntityBase_getGenericData(task.entityId, ref out_mbState, ref out_isEnabled, ref out_canBeEnabled, ref out_constructionIsDismantle, ref out_assignedConstructionDronePorts);
                    if (out_mbState == (byte)ModularBuildingManagerFrame.MBState.ConstructionSiteInactive)
                    {
                        var moduleCount = ModularBuildingManagerFrame.modularEntityBase_getTotalModuleCount(task.entityId, 0);
                        if (moduleCount <= 1)
                        {
                            var mbmfData = modularBuildingData.BuildMBMFData();
                            GameRoot.addLockstepEvent(new SetModularEntityConstructionStateDataEvent(usernameHash, 0U, task.entityId, mbmfData));
                        }
                    }
                }
            });
        }
    }

    public class CDA_PowerLines : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => true;

        public override void Apply(
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
            Dictionary<ulong, ulong> entityIdMap)
        {
            var powerlineEntityIds = new List<ulong>();
            customData.GetCustomDataList("powerline", powerlineEntityIds);
            foreach (var powerlineEntityId in powerlineEntityIds)
            {
                var powerlineIndex = blueprintData.FindEntityIndex(powerlineEntityId);
                if (powerlineIndex >= 0)
                {
                    var toBuildableObjectData = blueprintData.buildableObjects[powerlineIndex];
                    postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                    {
                        if (entityIdMap.TryGetValue(toBuildableObjectData.originalEntityId, out var toEntityId))
                        {
                            if (PowerLineHH.buildingManager_powerlineHandheld_checkIfAlreadyConnected(task.entityId, toEntityId) == IOBool.iofalse)
                            {
                                GameRoot.addLockstepEvent(new PoleConnectionEvent(usernameHash, PowerlineItemTemplate.id, task.entityId, toEntityId));
                            }
                        }
                    });
                }
            }
        }
    }

    public class CDA_ConfiguredItemTemplateId : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("configuredItemTemplateId");

        public override void Apply(
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
            Dictionary<ulong, ulong> entityIdMap)
        {
            var itemTemplateId = customData.GetCustomData<ulong>("configuredItemTemplateId");
            if (itemTemplateId > 0)
            {
                postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                {
                    if (task.entityId > 0)
                    {
                        GameRoot.addLockstepEvent(new SalesWarehouseConfigEvent(task.entityId, usernameHash, itemTemplateId));
                    }
                });
            }
        }
    }

    public class CDA_AL_Start : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("alotId");

        public override void Apply(
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
            Dictionary<ulong, ulong> entityIdMap)
        {
            var alotId = customData.GetCustomData<ulong>("alotId");
            if (alotId > 0)
            {
                postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                {
                    if (task.entityId > 0)
                    {
                        GameRoot.addLockstepEvent(new AL_StartSetAlotEvent(usernameHash, task.entityId, alotId));
                    }
                });
            }
        }
    }

    public class CDA_AL_Producer : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("actionTemplateId");

        public override void Apply(
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
            Dictionary<ulong, ulong> entityIdMap)
        {
            var actionTemplateId = customData.GetCustomData<ulong>("actionTemplateId");
            if (actionTemplateId > 0)
            {
                postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                {
                    if (task.entityId > 0)
                    {
                        GameRoot.addLockstepEvent(new AL_ProducerSetActionEvent(usernameHash, task.entityId, actionTemplateId));
                    }
                });
            }
        }
    }

    public class CDA_AL_Painter : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("painterAlotId");

        public override void Apply(
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
            Dictionary<ulong, ulong> entityIdMap)
        {
            var painterAlotId = customData.GetCustomData<ulong>("painterAlotId");
            if (painterAlotId > 0)
            {
                postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                {
                    if (task.entityId > 0)
                    {
                        var evt = new AL_ProducerSetPaintingActionEvent(usernameHash, task.entityId, painterAlotId);
                        if (customData.HasCustomData("colorVariants"))
                        {
                            var colorVariants = customData.GetCustomData<string>("colorVariants").Split("|")
                                .Select(x => x.Split("=").Select(y => Convert.ToUInt64(y)).ToArray());
                            foreach (var colorVariant in colorVariants)
                            {
                                var objectPartId = colorVariant[0];
                                var colorVariantId = colorVariant[1];
                                evt.addColorVariant(objectPartId, colorVariantId);
                            }
                        }
                        GameRoot.addLockstepEvent(evt);
                    }
                });
            }
        }
    }

    public class CDA_AL_Splitter : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("priorityIdx_output01")
                && customData.HasCustomData("priorityIdx_output02")
                && customData.HasCustomData("priorityIdx_output03");

        public override void Apply(
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
            Dictionary<ulong, ulong> entityIdMap)
        {
            var priorityIdx_output01 = GetToggleCount(customData.GetCustomData<byte>("priorityIdx_output01"));
            var priorityIdx_output02 = GetToggleCount(customData.GetCustomData<byte>("priorityIdx_output02"));
            var priorityIdx_output03 = GetToggleCount(customData.GetCustomData<byte>("priorityIdx_output03"));
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    for (int i = 0; i < priorityIdx_output01; i++)
                    {
                        GameRoot.addLockstepEvent(new AL_SplitterTogglePriorityEvent(usernameHash, task.entityId, 0));
                    }
                    for (int i = 0; i < priorityIdx_output02; i++)
                    {
                        GameRoot.addLockstepEvent(new AL_SplitterTogglePriorityEvent(usernameHash, task.entityId, 1));
                    }
                    for (int i = 0; i < priorityIdx_output03; i++)
                    {
                        GameRoot.addLockstepEvent(new AL_SplitterTogglePriorityEvent(usernameHash, task.entityId, 2));
                    }
                }
            });
        }

        private int GetToggleCount(byte priorityIdx) => (priorityIdx + 2) % 3;
    }

    public class CDA_AL_Merger : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("priorityIdx_input01")
                && customData.HasCustomData("priorityIdx_input02")
                && customData.HasCustomData("priorityIdx_input03");

        public override void Apply(
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
            Dictionary<ulong, ulong> entityIdMap)
        {
            var priorityIdx_input01 = GetToggleCount(customData.GetCustomData<byte>("priorityIdx_input01"));
            var priorityIdx_input02 = GetToggleCount(customData.GetCustomData<byte>("priorityIdx_input02"));
            var priorityIdx_input03 = GetToggleCount(customData.GetCustomData<byte>("priorityIdx_input03"));
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    for (int i = 0; i < priorityIdx_input01; i++)
                    {
                        GameRoot.addLockstepEvent(new AL_MergerTogglePriorityEvent(usernameHash, task.entityId, 0));
                    }
                    for (int i = 0; i < priorityIdx_input02; i++)
                    {
                        GameRoot.addLockstepEvent(new AL_MergerTogglePriorityEvent(usernameHash, task.entityId, 1));
                    }
                    for (int i = 0; i < priorityIdx_input03; i++)
                    {
                        GameRoot.addLockstepEvent(new AL_MergerTogglePriorityEvent(usernameHash, task.entityId, 2));
                    }
                }
            });
        }

        private int GetToggleCount(byte priorityIdx) => (priorityIdx + 2) % 3;
    }
}
