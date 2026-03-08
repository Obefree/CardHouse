using System;
using System.Collections.Generic;

namespace Cob.Runtime.Effects
{
    [Serializable]
    public sealed class CobEffectParseResult
    {
        public string original;
        public string normalized;

        public bool hasTrigger;
        public string triggerCondition;
        public string triggerEffect;

        public bool hasChain;
        public char chainColor; // r/w/b/g

        public bool hasTo;
        public string costPart;
        public string effectPart;

        public bool hasOr;
        public List<string> orOptions;

        public bool hasTrashThis;

        public List<string> tokens;
    }
}

