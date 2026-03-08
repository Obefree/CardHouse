using System.Collections.Generic;
using CardHouse;
using Cob.Data;
using Cob.Presentation.CardHouse;
using Cob.Runtime;
using UnityEngine;

namespace Cob.DeckBuilder
{
    [DisallowMultipleComponent]
    public sealed class CobDeckBuilderController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CobDeckList targetDeck;
        [SerializeField] private bool includeStartersInCollection = false;

        [Header("Presentation")]
        [SerializeField] private Card templateCard;
        [SerializeField] private CardGroup collectionGroup;
        [SerializeField] private CardGroup deckGroup;

        private CobCardDatabase _db;
        private int _nextInstanceId = 1_000_000;

        private readonly Dictionary<int, CobCardRecord> _defById = new Dictionary<int, CobCardRecord>();
        private readonly Dictionary<int, int> _deckCounts = new Dictionary<int, int>();

        private void Start()
        {
            _db = Resources.Load<CobCardDatabase>("CoB/CobCardDatabase");
            if (_db == null)
            {
                Debug.LogError("[CoB] DeckBuilder: missing `Assets/Resources/CoB/CobCardDatabase.asset`.");
                return;
            }

            IndexDb();

            templateCard = ResolveTemplateCard(templateCard);
            if (templateCard == null)
            {
                Debug.LogError("[CoB] DeckBuilder: could not resolve a template `Card` (CcgCard.prefab).");
                return;
            }

            EnsureGroups();
            LoadDeckIntoRuntimeCounts();
            BuildCollection();
            RebuildDeckView();
        }

        private void IndexDb()
        {
            _defById.Clear();
            for (var i = 0; i < _db.All.Count; i++)
            {
                var c = _db.All[i];
                if (c == null) continue;
                _defById[c.card_id] = c;
            }
        }

        private void EnsureGroups()
        {
            if (collectionGroup == null)
            {
                collectionGroup = CreateOrFindGroup("CoB_Collection", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardGrid.prefab");
            }

            if (deckGroup == null)
            {
                deckGroup = CreateOrFindGroup("CoB_DeckBuild", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardStack.prefab");
            }

            if (collectionGroup != null) collectionGroup.transform.position = new Vector3(-3.2f, 0.0f, 0f);
            if (deckGroup != null) deckGroup.transform.position = new Vector3(3.2f, 0.0f, 0f);
        }

        private void BuildCollection()
        {
            if (collectionGroup == null) return;
            ClearGroup(collectionGroup);

            var seen = new HashSet<int>();
            AddAll(_db.disciple, seen);
            AddAll(_db.attire, seen);
            AddAll(_db.consumables, seen);
            AddAll(_db.monsters, seen);
            if (includeStartersInCollection) AddAll(_db.starters, seen);

            collectionGroup.ApplyStrategy();
        }

        private void AddAll(List<CobCardRecord> defs, HashSet<int> seen)
        {
            if (defs == null) return;
            for (var i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (def == null) continue;
                if (!seen.Add(def.card_id)) continue;

                var card = Instantiate(templateCard, collectionGroup.transform);
                var inst = new CobCardInstance(_nextInstanceId++, def);

                var view = card.GetComponent<CobCardHouseCardView>();
                if (view == null) view = card.gameObject.AddComponent<CobCardHouseCardView>();
                view.Bind(inst);

                var click = card.GetComponent<ClickDetector>();
                if (click == null) click = card.gameObject.AddComponent<ClickDetector>();
                click.SetIsActive(true);
                click.OnButtonClicked.RemoveAllListeners();
                click.OnButtonClicked.AddListener(() => AddOne(def.card_id));

                card.SetFacing(true);
                collectionGroup.Mount(card, instaFlip: true);
            }
        }

        private void AddOne(int cardId)
        {
            if (!_defById.TryGetValue(cardId, out _)) return;
            _deckCounts.TryGetValue(cardId, out var n);
            _deckCounts[cardId] = n + 1;
            SyncToAsset();
            RebuildDeckView();
        }

        private void RemoveOne(int cardId)
        {
            if (!_deckCounts.TryGetValue(cardId, out var n) || n <= 0) return;
            n -= 1;
            if (n == 0) _deckCounts.Remove(cardId);
            else _deckCounts[cardId] = n;
            SyncToAsset();
            RebuildDeckView();
        }

        private void RebuildDeckView()
        {
            if (deckGroup == null) return;
            ClearGroup(deckGroup);

            foreach (var kv in _deckCounts)
            {
                var cardId = kv.Key;
                var count = kv.Value;
                if (!_defById.TryGetValue(cardId, out var def) || def == null) continue;

                for (var i = 0; i < count; i++)
                {
                    var card = Instantiate(templateCard, deckGroup.transform);
                    var inst = new CobCardInstance(_nextInstanceId++, def);

                    var view = card.GetComponent<CobCardHouseCardView>();
                    if (view == null) view = card.gameObject.AddComponent<CobCardHouseCardView>();
                    view.Bind(inst);

                    var click = card.GetComponent<ClickDetector>();
                    if (click == null) click = card.gameObject.AddComponent<ClickDetector>();
                    click.SetIsActive(true);
                    click.OnButtonClicked.RemoveAllListeners();
                    click.OnButtonClicked.AddListener(() => RemoveOne(cardId));

                    card.SetFacing(true);
                    deckGroup.Mount(card, instaFlip: true);
                }
            }

            deckGroup.ApplyStrategy();
        }

        private void LoadDeckIntoRuntimeCounts()
        {
            _deckCounts.Clear();
            if (targetDeck == null) return;
            for (var i = 0; i < targetDeck.entries.Count; i++)
            {
                var e = targetDeck.entries[i];
                if (e == null) continue;
                if (e.cardId == 0) continue;
                var c = Mathf.Max(0, e.count);
                if (c == 0) continue;
                _deckCounts[e.cardId] = c;
            }
        }

        private void SyncToAsset()
        {
            if (targetDeck == null) return;
            targetDeck.entries.Clear();
            foreach (var kv in _deckCounts)
            {
                targetDeck.entries.Add(new CobDeckList.Entry { cardId = kv.Key, count = kv.Value });
            }
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(targetDeck);
#endif
        }

        private static void ClearGroup(CardGroup g)
        {
            for (var i = g.MountedCards.Count - 1; i >= 0; i--)
            {
                var c = g.MountedCards[i];
                g.UnMount(i);
                if (c != null) Object.Destroy(c.gameObject);
            }
        }

        private static Card ResolveTemplateCard(Card current)
        {
            if (current != null) return current;
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardHouse/SampleGames/CCG/Prefabs/CcgCard.prefab");
            if (prefab != null)
            {
                var card = prefab.GetComponent<Card>() ?? prefab.GetComponentInChildren<Card>();
                if (card != null) return card;
            }
#endif
            return Object.FindFirstObjectByType<Card>();
        }

        private static CardGroup CreateOrFindGroup(string name, string prefabPath)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
            {
                var cg = existing.GetComponent<CardGroup>();
                if (cg != null) return cg;
            }

#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return null;
            var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            return go.GetComponent<CardGroup>();
#else
            return null;
#endif
        }
    }
}

