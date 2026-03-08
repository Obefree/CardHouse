using System;
using System.Collections.Generic;

namespace Cob.Data
{
    [Serializable]
    public sealed class CobCardDatabaseRoot
    {
        public List<CobCardRecord> disciple;
        public List<CobCardRecord> starters;
        public List<CobCardRecord> attire;
        public List<CobCardRecord> monsters;
        public List<CobCardRecord> consumables;
        public List<CobCardRecord> events;
    }
}

