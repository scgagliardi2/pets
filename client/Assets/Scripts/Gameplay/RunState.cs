using System.Collections.Generic;

namespace Pets.Gameplay
{
    public enum GamePhase
    {
        Shop,
        Battle,
        RunOver,
    }

    public sealed class RunState
    {
        public int Gold;
        public int Lives;
        public int Round = 1;
        public GamePhase Phase = GamePhase.Shop;
        public bool Victory;
        public List<BoardCreature> Board = new List<BoardCreature>();
        public List<ShopSlot> ShopSlots = new List<ShopSlot>();
    }
}
