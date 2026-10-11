using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;

namespace StatsUI
{
    // The battle world of the game, or null before the game creates it. BaseSingleton.Instance creates the world when
    // it does not exist yet, so a read of Instance from a frame hook at the title screen built a world outside the
    // game's flow, again on each frame while its constructor failed. Read the world only through this.
    internal static class GameWorld
    {
        internal static BattleLogicWorld Current =>
            BaseSingleton<BattleLogicWorld>.IsInstanceCreated ? BaseSingleton<BattleLogicWorld>.Instance : null;
    }
}
