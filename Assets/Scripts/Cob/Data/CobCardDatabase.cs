using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cob.Data
{
    [CreateAssetMenu(menuName = "CoB/Card Database", fileName = "CobCardDatabase")]
    public sealed class CobCardDatabase : ScriptableObject
    {
        [Header("Card groups (mirror JSON keys)")]
        public List<CobCardRecord> disciple = new List<CobCardRecord>();
        public List<CobCardRecord> starters = new List<CobCardRecord>();
        public List<CobCardRecord> attire = new List<CobCardRecord>();
        public List<CobCardRecord> monsters = new List<CobCardRecord>();
        public List<CobCardRecord> consumables = new List<CobCardRecord>();
        public List<CobCardRecord> events = new List<CobCardRecord>();

        [NonSerialized] private Dictionary<int, CobCardRecord> _byId;
        [NonSerialized] private List<CobCardRecord> _all;

        public IReadOnlyList<CobCardRecord> All
        {
            get
            {
                EnsureIndex();
                return _all;
            }
        }

        public bool TryGetById(int id, out CobCardRecord card)
        {
            EnsureIndex();
            return _byId.TryGetValue(id, out card);
        }

        public void ReplaceFrom(CobCardDatabaseRoot root)
        {
            disciple = root?.disciple ?? new List<CobCardRecord>();
            starters = root?.starters ?? new List<CobCardRecord>();
            attire = root?.attire ?? new List<CobCardRecord>();
            monsters = root?.monsters ?? new List<CobCardRecord>();
            consumables = root?.consumables ?? new List<CobCardRecord>();
            events = root?.events ?? new List<CobCardRecord>();
            RebuildIndex();
        }

        public void RebuildIndex()
        {
            _byId = null;
            _all = null;
            EnsureIndex();
        }

        private void OnEnable()
        {
            _byId = null;
            _all = null;
        }

        private void EnsureIndex()
        {
            if (_byId != null && _all != null) return;

            _all = new List<CobCardRecord>(
                (disciple?.Count ?? 0) +
                (starters?.Count ?? 0) +
                (attire?.Count ?? 0) +
                (monsters?.Count ?? 0) +
                (consumables?.Count ?? 0) +
                (events?.Count ?? 0)
            );

            void AddRange(List<CobCardRecord> src)
            {
                if (src == null) return;
                for (var i = 0; i < src.Count; i++)
                {
                    if (src[i] != null) _all.Add(src[i]);
                }
            }

            AddRange(disciple);
            AddRange(starters);
            AddRange(attire);
            AddRange(monsters);
            AddRange(consumables);
            AddRange(events);

            _byId = new Dictionary<int, CobCardRecord>(_all.Count);
            for (var i = 0; i < _all.Count; i++)
            {
                var c = _all[i];
                _byId[c.card_id] = c;
            }
        }
    }
}

