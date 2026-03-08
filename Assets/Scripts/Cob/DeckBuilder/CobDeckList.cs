using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cob.DeckBuilder
{
    [CreateAssetMenu(menuName = "CoB/Deck Builder/Deck List", fileName = "CobDeckList")]
    public sealed class CobDeckList : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public int cardId;
            public int count = 1;
        }

        public List<Entry> entries = new List<Entry>();
    }
}

