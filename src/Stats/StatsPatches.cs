using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;

namespace StatsUI
{
    // A stat of the leading role changed: AttributeComponent.SyncAttr2UI ends in this trigger of the HUD reducer. A
    // buff can change a ratio with no change of a stat, so the buff refresh below marks the stats too.
    [HarmonyPatch(typeof(Reducer_Web_CoreUI1), "Trigger_RA_SyncAttr")]
    internal static class StatsOnSyncAttr
    {
        private static void Postfix() => PageTick.Mark(DataParts.Part.Stats);
    }

    // The buff list of the leading role changed (State_Data_Buff.LeadingRoleBuffList).
    [HarmonyPatch(typeof(Reducer_Data_Buff), "RA_RefreshLeadingRoleBuff")]
    internal static class StatsOnRefreshLeadingRoleBuff
    {
        private static void Postfix(State_Data_Buff __result)
        {
            PageTick.Mark(DataParts.Part.Stats);
            PageTick.Mark(DataParts.Part.Buffs);
        }
    }
}
