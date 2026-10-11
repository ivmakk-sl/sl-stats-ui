using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;

namespace StatsUI
{
    // ItemManager.UpdateOwnerBagWeight runs on each change of the weight of a bag (an item added, removed, used, or
    // moved). Only the leading role's bag marks the weight part.
    [HarmonyPatch(typeof(ItemManager), "UpdateOwnerBagWeight")]
    internal static class WeightOnUpdateOwnerBagWeight
    {
        private static void Postfix(long ownerId)
        {
            var agents = GameWorld.Current?._AgentManager;
            if (agents != null && ownerId == agents.GetLeadingRoleId()) PageTick.Mark(DataParts.Part.Weight);
        }
    }

    // The weight limit changed with no item move: a buff or a talent changed the heavy load ratio or the flat bonus.
    // BagComponent.SyncBurdenBonusIfChanged (inlined into BagComponent.Update) sends UpdateHeavyLoadRatio, which ends
    // in this trigger of the item reducer. The build compares its JSON, so a mark for another owner sends nothing.
    [HarmonyPatch(typeof(GameCore.HotUpdate.ReduxUI.Reducer_Data_Item), "Trigger_RA_UpdateHeavyLoadRatio")]
    internal static class WeightOnUpdateHeavyLoadRatio
    {
        private static void Postfix() => PageTick.Mark(DataParts.Part.Weight);
    }

    // The weight limit changed by an effect that adds burden (AE_AddWeight).
    [HarmonyPatch(typeof(BagComponent), nameof(BagComponent.AddExtraBurden))]
    internal static class WeightOnAddExtraBurden
    {
        private static void Postfix() => PageTick.Mark(DataParts.Part.Weight);
    }
}
