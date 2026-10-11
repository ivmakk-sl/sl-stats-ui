using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;

namespace StatsUI
{
    // Reads the carried weight, the weight limit, and the Encumbered line of the leading role's bag, in grams, into
    // the weight part of the page data.
    internal static class WeightReader
    {
        internal static string Json()
        {
            var role = GameWorld.Current?._AgentManager?.GetLeadingRole();
            var bag = role != null ? AgentTools.GetAgentComponent<BagComponent>(role) : null;
            if (bag == null)
            {
                Plugin.WarnOnce("Stats UI: the bag of the leading role cannot be read; the weight readout shows nothing.");
                return PageJson.EmptyWeight;
            }
            int carried = bag.GetBagWeight();
            int limit = bag.GetMaxBurden();
            int line = bag.GetOverweightThreshold();
            Plugin.Debug($"Stats UI weight: carried={carried} limit={limit} line={line}");
            return PageJson.WeightJson(carried, limit, line);
        }
    }
}
