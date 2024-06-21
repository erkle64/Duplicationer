using static Duplicationer.Blueprint;
using System.Collections.Generic;
using System.Text;
using TinyJSON;
using System;
using CubeInterOp;
using System.Linq;

namespace Duplicationer
{
    public class CDG_Producer : TypedCustomDataGatherer<ProducerGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var assembler = (ProducerGO)bogo;
            customData.Add("craftingRecipeId", assembler.getLastPolledRecipeId());
        }
    }

    public class CDG_Loader : TypedCustomDataGatherer<LoaderGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var loader = (LoaderGO)bogo;
            customData.Add("isInputLoader", loader.isInputLoader() ? "true" : "false");
            if (bogo.template.loader_isFilter)
            {
                customData.Add("loaderFilterTemplateId", loader.getLastSetFilterTemplate()?.id ?? ulong.MaxValue);
            }
        }
    }

    public class CDG_ConveyorBalancer : TypedCustomDataGatherer<ConveyorBalancerGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var balancer = (ConveyorBalancerGO)bogo;
            customData.Add("balancerInputPriority", balancer.getInputPriority());
            customData.Add("balancerOutputPriority", balancer.getOutputPriority());
        }
    }

    public class CDG_Sign : TypedCustomDataGatherer<SignGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var signTextLength = SignGO.signEntity_getSignTextLength(bogo.relatedEntityId);
            var signText = new byte[signTextLength];
            byte useAutoTextSize = 0;
            float textMinSize = 0;
            float textMaxSize = 0;
            SignGO.signEntity_getSignText(bogo.relatedEntityId, signText, signTextLength, ref useAutoTextSize, ref textMinSize, ref textMaxSize);

            customData.Add("signText", Encoding.Default.GetString(signText));
            customData.Add("signUseAutoTextSize", useAutoTextSize);
            customData.Add("signTextMinSize", textMinSize);
            customData.Add("signTextMaxSize", textMaxSize);
        }
    }

    public class CDG_BlastFurnaceBase : TypedCustomDataGatherer<BlastFurnaceBaseGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            BlastFurnacePollingUpdateData data = default;
            if (BlastFurnaceBaseGO.blastFurnaceEntity_queryPollingData(bogo.relatedEntityId, ref data) == IOBool.iotrue)
            {
                customData.Add("blastFurnaceModeTemplateId", data.modeTemplateId);
            }
        }
    }

    public class CDG_DroneTransport : TypedCustomDataGatherer<DroneTransportGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            DroneTransportPollingUpdateData data = default;
            if (DroneTransportGO.droneTransportEntity_queryPollingData(bogo.relatedEntityId, ref data, null, 0U) == IOBool.iotrue)
            {
                customData.Add("loadConditionFlags", data.loadConditionFlags);
                customData.Add("loadCondition_comparisonType", data.loadCondition_comparisonType);
                customData.Add("loadCondition_fillRatePercentage", data.loadCondition_fillRatePercentage);
                customData.Add("loadCondition_seconds", data.loadCondition_seconds);
            }

            byte[] stationName = new byte[128];
            uint stationNameLength = 0;
            byte stationType = (byte)(bogo.template.droneTransport_isStartStation ? 1 : 0);
            DroneTransportGO.droneTransportEntity_getStationName(bogo.relatedEntityId, stationType, stationName, (uint)stationName.Length, ref stationNameLength);
            customData.Add("stationName", Encoding.UTF8.GetString(stationName, 0, (int)stationNameLength));
            customData.Add("stationType", stationType);
        }
    }

    public class CDG_Chest : TypedCustomDataGatherer<ChestGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            ulong inventoryId = 0UL;
            if (BuildingManager.buildingManager_getInventoryAccessors(bogo.relatedEntityId, 0U, ref inventoryId) == IOBool.iotrue)
            {
                uint slotCount = 0;
                uint categoryLock = 0;
                uint firstSoftLockedSlotIdx = 0;
                InventoryManager.inventoryManager_getAuxiliaryDataById(inventoryId, ref slotCount, ref categoryLock, ref firstSoftLockedSlotIdx, IOBool.iofalse);

                customData.Add("firstSoftLockedSlotIdx", firstSoftLockedSlotIdx);
            }
        }
    }

    public class CDG_ModularEntityBase : TypedCustomDataGatherer<IHasModularEntityBaseManager>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            ModularBuildingData rootNode = null;
            uint totalModuleCount = ModularBuildingManagerFrame.modularEntityBase_getTotalModuleCount(bogo.relatedEntityId, 0U);
            for (uint id = 1; id <= totalModuleCount; ++id)
            {
                ulong botId = 0;
                uint parentId = 0;
                uint parentAttachmentPointIdx = 0;
                ModularBuildingManagerFrame.modularEntityBase_getModuleDataForModuleId(bogo.relatedEntityId, id, ref botId, ref parentId, ref parentAttachmentPointIdx, 0U);
                if (id == 1U)
                {
                    rootNode = new ModularBuildingData(bogo.template, id);
                }
                else
                {
                    var nodeById = rootNode.FindModularBuildingNodeById(parentId);
                    if (nodeById == null)
                    {
                        DuplicationerPlugin.log.LogError("parent node not found!");
                        break;
                    }
                    if (nodeById.attachments[(int)parentAttachmentPointIdx] != null)
                    {
                        DuplicationerPlugin.log.LogError("parent node attachment point is occupied!");
                        break;
                    }
                    var node = new ModularBuildingData(ItemTemplateManager.getBuildableObjectTemplate(botId), id);
                    nodeById.attachments[(int)parentAttachmentPointIdx] = node;
                }
            }
            if (rootNode != null)
            {
                var rootNodeJSON = JSON.Dump(rootNode, EncodeOptions.NoTypeHints);
                customData.Add("modularBuildingData", rootNodeJSON);
            }
        }
    }

    public class CDG_Powerline : CustomDataGatherer
    {
        public override bool ShouldGather(BuildableObjectGO bogo, Type bogoType)
            => bogo.template.hasPoleGridConnection;

        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            if (!powerGridBuildings.Contains(bogo))
            {
                foreach (var powerGridBuilding in powerGridBuildings)
                {
                    if (PowerLineHH.buildingManager_powerlineHandheld_checkIfAlreadyConnected(powerGridBuilding.relatedEntityId, bogo.relatedEntityId) == IOBool.iotrue)
                    {
                        customData.Add("powerline", powerGridBuilding.relatedEntityId);
                    }
                }
                powerGridBuildings.Add(bogo);
            }
        }
    }

    public class CDG_SalesCurrencyWarehouse : TypedCustomDataGatherer<SalesCurrencyWarehouseGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var data = new SalesCurrencyWarehousePollingUpdateData();
            if (SalesCurrencyWarehouseGO.salesCurrencyWarehouseEntity_queryPollingData(bogo.relatedEntityId, ref data) == IOBool.iofalse)
                return;

            if (data.configuredItemTemplateId != 0)
            {
                customData.Add("configuredItemTemplateId", data.configuredItemTemplateId);
            }
        }
    }

    public class CDG_SalesItemWarehouse : TypedCustomDataGatherer<SalesItemWarehouseGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var data = new SalesItemWarehousePollingUpdateData();
            if (SalesItemWarehouseGO.salesItemWarehouseEntity_queryPollingData(bogo.relatedEntityId, ref data) == IOBool.iofalse)
                return;

            if (data.configuredItemTemplateId != 0)
            {
                customData.Add("configuredItemTemplateId", data.configuredItemTemplateId);
            }
        }
    }

    public class CDG_AL_EndConsumer : TypedCustomDataGatherer<AL_EndConsumerGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var data = new AL_EndConsumerPollingUpdateData();
            if (AL_EndConsumerGO.alEndConsumer_queryPollingData(bogo.relatedEntityId, ref data) == IOBool.iofalse)
                return;

            if (data.configuredItemTemplateId != 0)
            {
                customData.Add("configuredItemTemplateId", data.configuredItemTemplateId);
            }
        }
    }

    public class CDG_AL_Start : TypedCustomDataGatherer<AL_StartGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var data = new AL_StartPollingUpdateData();
            if (AL_StartGO.alStartEntity_queryPollingData(bogo.relatedEntityId, ref data) == IOBool.iofalse)
                return;

            if (data.alotId != 0)
            {
                customData.Add("alotId", data.alotId);
            }
        }
    }

    public class CDG_AL_Producer : TypedCustomDataGatherer<AL_ProducerGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            if (AL_ProducerGO.alProducerEntity_getActionTemplateId(bogo.relatedEntityId, out var actionTemplateId) == IOBool.iofalse)
                return;

            if (actionTemplateId != 0)
            {
                customData.Add("actionTemplateId", actionTemplateId);
            }
        }
    }

    public class CDG_AL_Splitter : TypedCustomDataGatherer<AL_SplitterGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var data = new AL_SplitterUpdateData();
            if (AL_SplitterGO.alSplitterEntity_queryPollingData(bogo.relatedEntityId, ref data) == IOBool.iofalse)
                return;

            customData.Add("priorityIdx_output01", data.priorityIdx_output01);
            customData.Add("priorityIdx_output02", data.priorityIdx_output02);
            customData.Add("priorityIdx_output03", data.priorityIdx_output03);
        }
    }

    public class CDG_AL_Merger : TypedCustomDataGatherer<AL_MergerGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var data = new AL_MergerUpdateData();
            if (AL_MergerGO.alMergerEntity_queryPollingData(bogo.relatedEntityId, ref data) == IOBool.iofalse)
                return;

            customData.Add("priorityIdx_input01", data.priorityIdx_input01);
            customData.Add("priorityIdx_input02", data.priorityIdx_input02);
            customData.Add("priorityIdx_input03", data.priorityIdx_input03);
        }
    }

    public class CDG_AL_Painter : TypedCustomDataGatherer<AL_ProducerGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var data = new AL_ProducerPollingUpdateData();
            if (AL_ProducerGO.alProducerEntity_queryPollingData(bogo.relatedEntityId, ref data, null, 0U, null, 0U) == IOBool.iofalse || bogo.template.al_producer_machineType != BuildableObjectTemplate.AL_ProducerMachineType.Painter)
                return;

            var painterAlotId = data.painterAlotId;

            if (painterAlotId != 0)
            {
                customData.Add("painterAlotId", painterAlotId);

                var alot = ItemTemplateManager.getAssemblyLineObjectTemplate(painterAlotId);
                if (alot != null)
                {
                    var colorVariants = new Dictionary<ulong, ulong>();

                    foreach (var objectPart in alot.objectParts)
                    {
                        ulong colorVariantId = 0;
                        if (AL_ProducerGO.alProducerEntity_queryColorVariantByObjectPartId(bogo.relatedEntityId, objectPart.id, ref colorVariantId) != IOBool.iofalse)
                        {
                            colorVariants[objectPart.id] = colorVariantId;
                        }
                    }

                    customData.Add("colorVariants", string.Join("|", colorVariants.Select(x => $"{x.Key}={x.Value}")));
                }
            }
        }
    }
}
