using System;

namespace Cob.Runtime
{
    [Serializable]
    public sealed class CobGameState
    {
        public readonly CobPlayerState player;
        public readonly CobPlayerState ai;

        public int turn = 1;
        public CobPlayerId currentPlayer = CobPlayerId.Player;

        public CobGameState(int hpPlayer, int hpAi)
        {
            player = new CobPlayerState("Игрок", hpPlayer);
            ai = new CobPlayerState("ИИ", hpAi);
        }

        public CobPlayerState Get(CobPlayerId id) => id == CobPlayerId.Player ? player : ai;
        public CobPlayerState OpponentOf(CobPlayerId id) => id == CobPlayerId.Player ? ai : player;
    }
}

