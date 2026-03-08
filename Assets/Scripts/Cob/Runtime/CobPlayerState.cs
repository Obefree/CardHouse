using System;
using System.Collections.Generic;

namespace Cob.Runtime
{
    [Serializable]
    public sealed class CobPlayerState
    {
        public string name;

        public int hp;
        public int poison;
        public int bleed;

        public readonly List<CobCardInstance> deck = new List<CobCardInstance>();
        public readonly List<CobCardInstance> hand = new List<CobCardInstance>();
        public readonly List<CobCardInstance> discard = new List<CobCardInstance>();
        public readonly List<CobCardInstance> trash = new List<CobCardInstance>();
        public readonly List<CobCardInstance> played = new List<CobCardInstance>();
        public readonly List<CobCardInstance> attire = new List<CobCardInstance>();

        public int blessingThisTurn;
        public int spentBlessing;

        public int manaThisTurn;
        public int spentMana;

        public int damageThisTurn;
        public int healThisTurn;
        public int cardsDrawnThisTurn;

        public bool ignoreAttireDefenseThisTurn;

        public bool nextAcquireToHandThisTurn;
        public bool nextAcquireToTopdeckThisTurn;

        public CobPlayerState(string name, int hp)
        {
            this.name = name;
            this.hp = hp;
        }

        public int AvailableBlessing => Math.Max(0, blessingThisTurn - spentBlessing);
        public int AvailableMana => Math.Max(0, manaThisTurn - spentMana);
    }
}

