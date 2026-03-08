using System;
using Cob.Data;

namespace Cob.Runtime
{
    [Serializable]
    public sealed class CobCardInstance
    {
        public int instanceId;
        public int cardId;

        public bool activated;
        public bool usedThisTurn;
        public bool playedThisTurn;

        public bool trashThis;

        public CobCardRecord Def { get; private set; }

        public CobCardInstance(int instanceId, CobCardRecord def)
        {
            this.instanceId = instanceId;
            Def = def ?? throw new ArgumentNullException(nameof(def));
            cardId = def.card_id;
        }

        public override string ToString() => $"{Def?.name ?? "?"}#{instanceId}";
    }
}

