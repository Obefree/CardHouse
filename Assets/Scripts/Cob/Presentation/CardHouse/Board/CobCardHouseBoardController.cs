using System;
using System.Collections.Generic;
using CardHouse;
using Cob.Data;
using Cob.Presentation.CardHouse;
using Cob.Runtime;
using UnityEngine;

namespace Cob.Presentation.CardHouse.Board
{
    [DisallowMultipleComponent]
    public sealed class CobCardHouseBoardController : MonoBehaviour
    {
        [Serializable]
        private sealed class SplayZoneTuning
        {
            public Vector2 areaScale = new Vector2(6.2f, 2.2f);
            [Range(0.05f, 1f)] public float cardScale = 0.22f;
            [Range(0f, 0.8f)] public float arcMargin = 0.10f;
            public float arcCenterYOffset = -4.0f;
        }

        [Serializable]
        private sealed class GridZoneTuning
        {
            public Vector2 areaScale = new Vector2(6.2f, 2.4f);
            [Range(0.05f, 1f)] public float cardScale = 0.22f;
            [Min(1)] public int cardsPerRow = 6;
        }

        [Serializable]
        private sealed class StackZoneTuning
        {
            public Vector2 areaScale = new Vector2(1.4f, 1.4f);
            [Range(0.05f, 1f)] public float cardScale = 0.18f;
        }

        [Serializable]
        private sealed class SlotZoneTuning
        {
            public Vector2 areaScale = new Vector2(2.8f, 2.4f);
            [Range(0.05f, 1f)] public float cardScale = 0.26f;
        }

        [Header("Data")]
        [SerializeField] private bool enableConsumables = true;
        [SerializeField] private int eventCount = 6;

        [Header("CardHouse")]
        [SerializeField] private Card templateCard;

        [Header("Layout (camera-normalized)")]
        [Tooltip("0..1 from bottom to top of camera view.")]
        [Range(0f, 1f)]
        [SerializeField] private float yHand = 0.12f;
        [Range(0f, 1f)]
        [SerializeField] private float yPlayed = 0.38f;
        [Range(0f, 1f)]
        [SerializeField] private float yMarket = 0.62f;
        [Range(0f, 1f)]
        [SerializeField] private float yMonsters = 0.86f;
        [Range(0f, 1f)]
        [SerializeField] private float yAi = 0.96f;

        [Header("Layout X (camera-normalized)")]
        [Tooltip("0..1 from left to right of camera view.")]
        [Range(0f, 1f)]
        [SerializeField] private float xHandCenter = 0.52f;
        [Range(0f, 1f)]
        [SerializeField] private float xPlayedCenter = 0.52f;
        [Range(0f, 1f)]
        [SerializeField] private float xMarketCenter = 0.34f;
        [Range(0f, 1f)]
        [SerializeField] private float xMonstersCenter = 0.78f;
        [Range(0f, 1f)]
        [SerializeField] private float xAttire = 0.12f;

        [Range(0f, 1f)]
        [SerializeField] private float xLeft = 0.12f;
        [Range(0f, 1f)]
        [SerializeField] private float xRight = 0.88f;
        [SerializeField] private float zPlane = 0f;
        [SerializeField] private float safeMargin = 0.7f;

        [Header("Layout offsets (world units)")]
        [SerializeField] private float altarMonsterGapX = 2.9f;

        [Header("Zones")]
        [SerializeField] private CardGroup playerDeck;
        [SerializeField] private CardGroup playerHand;
        [SerializeField] private CardGroup playerDiscard;
        [SerializeField] private CardGroup playerPlayed;
        [SerializeField] private CardGroup playerAttire;

        [SerializeField] private CardGroup marketRow;
        [SerializeField] private CardGroup marketDeck;
        [SerializeField] private CardGroup marketTrash;

        [SerializeField] private CardGroup monsterSlot;
        [SerializeField] private CardGroup altarSlot;
        [SerializeField] private CardGroup monsterDeck;
        [SerializeField] private CardGroup monsterDefeated;

        [SerializeField] private CardGroup aiDeck;
        [SerializeField] private CardGroup aiHand;
        [SerializeField] private CardGroup aiDiscard;
        [SerializeField] private CardGroup aiPlayed;
        [SerializeField] private CardGroup aiAttire;

        [Header("Zone tuning (area vs card size)")]
        [SerializeField] private SplayZoneTuning handTuning = new SplayZoneTuning();

        [SerializeField] private StackZoneTuning stackTuning = new StackZoneTuning();

        [SerializeField] private GridZoneTuning playedTuning = new GridZoneTuning { areaScale = new Vector2(6.2f, 2.6f), cardScale = 0.20f, cardsPerRow = 6 };
        [SerializeField] private GridZoneTuning attireTuning = new GridZoneTuning { areaScale = new Vector2(2.6f, 2.2f), cardScale = 0.18f, cardsPerRow = 3 };
        [SerializeField] private GridZoneTuning marketRowTuning = new GridZoneTuning { areaScale = new Vector2(6.2f, 2.4f), cardScale = 0.20f, cardsPerRow = 6 };

        [SerializeField] private SlotZoneTuning monsterSlotTuning = new SlotZoneTuning { areaScale = new Vector2(2.8f, 2.4f), cardScale = 0.24f };
        [SerializeField] private SlotZoneTuning altarSlotTuning = new SlotZoneTuning { areaScale = new Vector2(2.8f, 2.4f), cardScale = 0.24f };

        [SerializeField] private SplayZoneTuning aiHandTuning = new SplayZoneTuning { areaScale = new Vector2(6.2f, 1.6f), cardScale = 0.14f, arcMargin = 0.12f, arcCenterYOffset = 2.5f };
        [SerializeField] private GridZoneTuning aiPlayedTuning = new GridZoneTuning { areaScale = new Vector2(6.2f, 1.8f), cardScale = 0.16f, cardsPerRow = 6 };
        [SerializeField] private GridZoneTuning aiAttireTuning = new GridZoneTuning { areaScale = new Vector2(2.6f, 1.8f), cardScale = 0.16f, cardsPerRow = 3 };

        private CobCardDatabase _db;
        private CobGameEngine _engine;

        // Market state (matches web)
        private readonly List<CobCardRecord> _marketUnique = new List<CobCardRecord>();
        private readonly List<CobCardInstance> _marketDeckModel = new List<CobCardInstance>();
        private readonly CobCardInstance[] _marketRowModel = new CobCardInstance[6];

        private readonly List<CobCardInstance> _monsterDeckModel = new List<CobCardInstance>();
        private CobCardInstance _currentMonster;

        private void Start()
        {
            _db = Resources.Load<CobCardDatabase>("CoB/CobCardDatabase");
            if (_db == null)
            {
                Debug.LogError("[CoB] Board: missing `Assets/Resources/CoB/CobCardDatabase.asset`.");
                return;
            }

            _engine = new CobGameEngine(_db);

            templateCard = ResolveTemplateCard(templateCard);
            if (templateCard == null)
            {
                Debug.LogError("[CoB] Board: could not resolve template card (CcgCard.prefab).");
                return;
            }

            EnsureZones();
            LayoutZonesToCamera();

            StartNewMatch();
        }

        [ContextMenu("CoB/Apply Layout + Tuning")]
        private void ApplyLayoutAndTuningMenu()
        {
            LayoutZonesToCamera();
        }

        [ContextMenu("CoB/Debug/Print Zone Map")]
        private void DebugPrintZoneMapMenu()
        {
            Debug.Log(
                "[CoB] Zone map:\n" +
                DumpGroup(playerDeck, "playerDeck") +
                DumpGroup(playerHand, "playerHand") +
                DumpGroup(playerDiscard, "playerDiscard") +
                DumpGroup(playerPlayed, "playerPlayed") +
                DumpGroup(playerAttire, "playerAttire") +
                DumpGroup(marketDeck, "marketDeck") +
                DumpGroup(marketRow, "marketRow") +
                DumpGroup(marketTrash, "marketTrash") +
                DumpGroup(monsterDeck, "monsterDeck") +
                DumpGroup(monsterSlot, "monsterSlot") +
                DumpGroup(altarSlot, "altarSlot") +
                DumpGroup(monsterDefeated, "monsterDefeated") +
                DumpGroup(aiDeck, "aiDeck") +
                DumpGroup(aiHand, "aiHand") +
                DumpGroup(aiDiscard, "aiDiscard") +
                DumpGroup(aiPlayed, "aiPlayed") +
                DumpGroup(aiAttire, "aiAttire")
            );
        }

        private static string DumpGroup(CardGroup g, string label)
        {
            if (g == null) return $" - {label}: (null)\n";
            var c = g.MountedCards != null ? g.MountedCards.Count : -1;
            var p = g.transform.position;
            return $" - {label}: `{g.name}` cards={c} pos=({p.x:0.00},{p.y:0.00},{p.z:0.00})\n";
        }

        [ContextMenu("CoB/Apply CoB Clean Preset")]
        private void ApplyCobCleanPresetMenu()
        {
            // Top: AI. Under: market+monsters. Mid: played + attire left. Bottom: hand + deck/discard.
            yAi = 0.95f;
            yMonsters = 0.75f;
            yMarket = 0.75f;
            yPlayed = 0.40f;
            yHand = 0.12f;

            xHandCenter = 0.52f;
            xPlayedCenter = 0.52f;
            xMarketCenter = 0.34f;
            xMonstersCenter = 0.78f;
            xAttire = 0.12f;

            xLeft = 0.10f;
            xRight = 0.90f;

            safeMargin = 0.7f;
            altarMonsterGapX = 2.9f;

            // Default sizes closer to web proportions.
            handTuning.cardScale = 0.19f;
            handTuning.areaScale = new Vector2(6.6f, 2.0f);
            marketRowTuning.cardScale = 0.18f;
            marketRowTuning.areaScale = new Vector2(6.4f, 2.2f);
            monsterSlotTuning.cardScale = 0.22f;
            altarSlotTuning.cardScale = 0.22f;
            playedTuning.cardScale = 0.18f;
            stackTuning.cardScale = 0.16f;

            LayoutZonesToCamera();
        }

        private void StartNewMatch()
        {
            ClearAllSpawnedCards();

            _engine.StartNewGame(skipInitialDraw: true);

            // Player: build starter deck exactly like web (7 Prayer + 3 Strike)
            var prayer = FindByName(_db.starters, "Prayer") ?? new CobCardRecord { name = "Prayer", effect1 = "{Blessing 1}", cost = 0, color = "white", card_id = -10001 };
            var strike = FindByName(_db.starters, "Strike") ?? new CobCardRecord { name = "Strike", effect1 = "{Damage 1}", cost = 0, color = "red", card_id = -10002 };

            FillDeck(_engine.State.player.deck, prayer, 7);
            FillDeck(_engine.State.player.deck, strike, 3);
            _engine.Shuffle(_engine.State.player.deck);

            // AI: same starter deck
            FillDeck(_engine.State.ai.deck, prayer, 7);
            FillDeck(_engine.State.ai.deck, strike, 3);
            _engine.Shuffle(_engine.State.ai.deck);

            // Initial draws like web: player 3, ai 5
            _engine.DrawCards(_engine.State.player, 3);
            _engine.DrawCards(_engine.State.ai, 5);

            BuildMarketLikeWeb();
            BuildMonstersLikeWeb();

            SyncAll();
        }

        private void BuildMarketLikeWeb()
        {
            _marketUnique.Clear();
            _marketDeckModel.Clear();
            Array.Clear(_marketRowModel, 0, _marketRowModel.Length);

            // Eligible base: disciple + attire with cost > 0
            AddMarketEligible(_db.disciple, requireCostPositive: true);
            AddMarketEligible(_db.attire, requireCostPositive: true);

            if (enableConsumables) AddMarketEligible(_db.consumables, requireCostPositive: false);

            // Events: pick N random, but they should not appear in initial 6 slots
            var pickedEvents = new List<CobCardRecord>();
            if (eventCount > 0 && _db.events != null && _db.events.Count > 0)
            {
                pickedEvents.AddRange(_db.events);
                _engine.Shuffle(pickedEvents);
                if (pickedEvents.Count > eventCount) pickedEvents.RemoveRange(eventCount, pickedEvents.Count - eventCount);
                AddMarketEligible(pickedEvents, requireCostPositive: false);
            }

            // Expand by copies
            for (var i = 0; i < _marketUnique.Count; i++)
            {
                var def = _marketUnique[i];
                var n = Mathf.Max(1, def.copies);
                for (var c = 0; c < n; c++)
                {
                    _marketDeckModel.Add(_engine.NewInstance(def));
                }
            }

            _engine.Shuffle(_marketDeckModel);

            // Fill 6 slots: never place an event in initial row
            for (var i = 0; i < 6; i++)
            {
                _marketRowModel[i] = DrawNonEventFromMarketDeckOrNull(maxAttempts: 100);
            }

            var rowCount = 0;
            for (var i = 0; i < 6; i++) if (_marketRowModel[i] != null) rowCount++;
            Debug.Log($"[CoB] Market built. unique={_marketUnique.Count} deck={_marketDeckModel.Count} row={rowCount}/6 eventsPicked={(enableConsumables ? "yes" : "no")} eventCount={eventCount}");
        }

        private CobCardInstance DrawNonEventFromMarketDeckOrNull(int maxAttempts)
        {
            var attempts = 0;
            while (_marketDeckModel.Count > 0 && attempts++ < maxAttempts)
            {
                var idx = _marketDeckModel.Count - 1;
                var c = _marketDeckModel[idx];
                _marketDeckModel.RemoveAt(idx);

                if (!IsEvent(c.Def)) return c;

                // Put event back to bottom (front) like web unshift.
                _marketDeckModel.Insert(0, c);
            }
            return null;
        }

        private static bool IsEvent(CobCardRecord def)
        {
            var t = (def?.type ?? def?.card_type ?? string.Empty).Trim();
            return t.Equals("Event", StringComparison.OrdinalIgnoreCase) ||
                   t.Equals("events", StringComparison.OrdinalIgnoreCase);
        }

        private void AddMarketEligible(IReadOnlyList<CobCardRecord> defs, bool requireCostPositive)
        {
            if (defs == null) return;

            // Dedup by name (global)
            for (var i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (def == null) continue;

                var name = (def.name ?? string.Empty).Trim();
                if (name.Length == 0 || name.Equals("None", StringComparison.OrdinalIgnoreCase)) continue;

                if (requireCostPositive && def.cost <= 0) continue;

                var hasEffects =
                    !string.IsNullOrWhiteSpace(def.effect1) || !string.IsNullOrWhiteSpace(def.effect2) ||
                    !string.IsNullOrWhiteSpace(def.effect1text) || !string.IsNullOrWhiteSpace(def.effect2text);
                if (!hasEffects) continue;

                var already = false;
                for (var j = 0; j < _marketUnique.Count; j++)
                {
                    if (string.Equals(_marketUnique[j].name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        already = true;
                        break;
                    }
                }
                if (already) continue;

                _marketUnique.Add(def);
            }
        }

        private void BuildMonstersLikeWeb()
        {
            _monsterDeckModel.Clear();
            _currentMonster = null;

            if (_db.monsters != null)
            {
                for (var i = 0; i < _db.monsters.Count; i++)
                {
                    var def = _db.monsters[i];
                    if (def == null) continue;
                    var n = Mathf.Max(1, def.copies);
                    for (var c = 0; c < n; c++)
                    {
                        _monsterDeckModel.Add(_engine.NewInstance(def));
                    }
                }
            }

            _engine.Shuffle(_monsterDeckModel);
            RevealNextMonster();
        }

        private void RevealNextMonster()
        {
            if (_monsterDeckModel.Count == 0)
            {
                _currentMonster = null;
                return;
            }

            var idx = _monsterDeckModel.Count - 1;
            _currentMonster = _monsterDeckModel[idx];
            _monsterDeckModel.RemoveAt(idx);
        }

        private void EnsureZones()
        {
            // Create minimal zones if not assigned (editor-friendly, but works in play too if already present).
            if (playerDeck == null) playerDeck = CreateOrFindGroup("CoB_PlayerDeck", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardStack.prefab");
            if (playerHand == null) playerHand = CreateOrFindGroup("CoB_PlayerHand", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardHand.prefab");
            if (playerDiscard == null) playerDiscard = CreateOrFindGroup("CoB_PlayerDiscard", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardStackDiscard.prefab");
            if (playerPlayed == null) playerPlayed = CreateOrFindGroup("CoB_PlayerPlayed", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardGrid.prefab");
            if (playerAttire == null) playerAttire = CreateOrFindGroup("CoB_PlayerAttire", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardGrid.prefab");

            if (marketRow == null) marketRow = CreateOrFindGroup("CoB_MarketRow", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardGrid.prefab");
            if (marketDeck == null) marketDeck = CreateOrFindGroup("CoB_MarketDeck", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardStack.prefab");
            if (marketTrash == null) marketTrash = CreateOrFindGroup("CoB_MarketTrash", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardStackDiscard.prefab");

            if (monsterSlot == null) monsterSlot = CreateOrFindGroup("CoB_CurrentMonster", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardSlot (Single).prefab");
            if (altarSlot == null) altarSlot = CreateOrFindGroup("CoB_Altar", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardSlot (Single).prefab");
            if (monsterDeck == null) monsterDeck = CreateOrFindGroup("CoB_MonsterDeck", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardStack.prefab");
            if (monsterDefeated == null) monsterDefeated = CreateOrFindGroup("CoB_MonsterDefeated", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardStackDiscard.prefab");

            if (aiDeck == null) aiDeck = CreateOrFindGroup("CoB_AI_Deck", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardStack.prefab");
            if (aiHand == null) aiHand = CreateOrFindGroup("CoB_AI_Hand", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardHand.prefab");
            if (aiDiscard == null) aiDiscard = CreateOrFindGroup("CoB_AI_Discard", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardStackDiscard.prefab");
            if (aiPlayed == null) aiPlayed = CreateOrFindGroup("CoB_AI_Played", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardGrid.prefab");
            if (aiAttire == null) aiAttire = CreateOrFindGroup("CoB_AI_Attire", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardGrid.prefab");
        }

        private void LayoutZonesToCamera()
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return;

            var halfH = cam.orthographicSize;
            var halfW = halfH * cam.aspect;

            var cx = cam.transform.position.x;
            var cy = cam.transform.position.y;

            var minX = cx - halfW + safeMargin;
            var maxX = cx + halfW - safeMargin;
            var minY = cy - halfH + safeMargin;
            var maxY = cy + halfH - safeMargin;

            var left = Mathf.Lerp(minX, maxX, xLeft);
            var right = Mathf.Lerp(minX, maxX, xRight);
            var xHandW = Mathf.Lerp(minX, maxX, xHandCenter);
            var xPlayedW = Mathf.Lerp(minX, maxX, xPlayedCenter);
            var xMarketW = Mathf.Lerp(minX, maxX, xMarketCenter);
            var xMonW = Mathf.Lerp(minX, maxX, xMonstersCenter);
            var xAttireW = Mathf.Lerp(minX, maxX, xAttire);

            var yHandW = Mathf.Lerp(minY, maxY, yHand);
            var yPlayedW = Mathf.Lerp(minY, maxY, yPlayed);
            var yMarketW = Mathf.Lerp(minY, maxY, yMarket);
            var yMonW = Mathf.Lerp(minY, maxY, yMonsters);
            var yAiW = Mathf.Lerp(minY, maxY, yAi);

            // Player zones
            if (playerDeck != null) playerDeck.transform.position = new Vector3(left, yHandW, zPlane);
            if (playerDiscard != null) playerDiscard.transform.position = new Vector3(right, yHandW, zPlane);
            if (playerHand != null) playerHand.transform.position = new Vector3(xHandW, yHandW, zPlane);
            if (playerPlayed != null) playerPlayed.transform.position = new Vector3(xPlayedW, yPlayedW, zPlane);
            if (playerAttire != null) playerAttire.transform.position = new Vector3(xAttireW, yPlayedW, zPlane);

            // Market + monsters (CoB Clean: market left, monster right + altar left of monster)
            if (marketRow != null) marketRow.transform.position = new Vector3(xMarketW, yMarketW, zPlane);
            if (marketDeck != null) marketDeck.transform.position = new Vector3(left, yMarketW, zPlane);
            if (marketTrash != null) marketTrash.transform.position = new Vector3(right, yMarketW, zPlane);

            if (monsterSlot != null) monsterSlot.transform.position = new Vector3(xMonW, yMonW, zPlane);
            if (altarSlot != null) altarSlot.transform.position = new Vector3(xMonW - altarMonsterGapX, yMonW, zPlane);
            if (monsterDeck != null) monsterDeck.transform.position = new Vector3(left, yMonW, zPlane);
            if (monsterDefeated != null) monsterDefeated.transform.position = new Vector3(right, yMonW, zPlane);

            // AI zones (top strip)
            if (aiDeck != null) aiDeck.transform.position = new Vector3(left, yAiW, zPlane);
            if (aiDiscard != null) aiDiscard.transform.position = new Vector3(right, yAiW, zPlane);
            if (aiHand != null) aiHand.transform.position = new Vector3(xHandW, yAiW, zPlane);
            if (aiPlayed != null) aiPlayed.transform.position = new Vector3(xPlayedW, Mathf.Lerp(yPlayedW, yAiW, 0.65f), zPlane);
            if (aiAttire != null) aiAttire.transform.position = new Vector3(xAttireW, yAiW, zPlane);

            // Ensure groups have sane scales/layouts for TTS full-card sprites.
            ApplyGroupTuning();
        }

        private void ApplyGroupTuning()
        {
            // Important: group scale should define the "area"/spacing, but NOT the card scale.
            // We set UseMyScale=false in layouts, then apply a fixed card scale per zone after ApplyStrategy.

            // Hand: wide area, gentle splay
            TuneSplay(playerHand, handTuning);

            // Stacks
            TuneStack(playerDeck, stackTuning);
            TuneStack(playerDiscard, stackTuning);
            TuneStack(marketDeck, stackTuning);
            TuneStack(marketTrash, stackTuning);
            TuneStack(monsterDeck, stackTuning);
            TuneStack(monsterDefeated, stackTuning);

            // Grids
            TuneGrid(playerPlayed, playedTuning);
            TuneGrid(playerAttire, attireTuning);
            TuneGrid(marketRow, marketRowTuning);

            // Monster slot
            TuneSlot(monsterSlot, monsterSlotTuning);
            TuneSlot(altarSlot, altarSlotTuning);

            // AI
            TuneSplay(aiHand, aiHandTuning);
            TuneGrid(aiPlayed, aiPlayedTuning);
            TuneGrid(aiAttire, aiAttireTuning);
            TuneStack(aiDeck, stackTuning);
            TuneStack(aiDiscard, stackTuning);
        }

        private static void TuneSplay(CardGroup group, SplayZoneTuning t)
        {
            if (group == null) return;
            if (t != null) group.transform.localScale = new Vector3(t.areaScale.x, t.areaScale.y, 1f);

            var layout = group.GetComponent<SplayLayout>();
            if (layout != null)
            {
                layout.UseMyScale = false;
                layout.ArcMargin = Mathf.Clamp01(t != null ? t.arcMargin : 0.10f);
                // Recompute ArcCenterOffset in world space (Start() in SplayLayout only runs once).
                layout.ArcCenterOffset = group.transform.position + group.transform.up * (t != null ? t.arcCenterYOffset : -4.0f);
            }
            group.ApplyStrategy();
        }

        private static void TuneGrid(CardGroup group, GridZoneTuning t)
        {
            if (group == null) return;
            if (t != null) group.transform.localScale = new Vector3(t.areaScale.x, t.areaScale.y, 1f);
            var layout = group.GetComponent<CardGridLayout>();
            if (layout != null)
            {
                layout.UseMyScale = false;
                layout.CardsPerRow = Mathf.Max(1, t != null ? t.cardsPerRow : 6);
                layout.Straighten = true;
            }
            group.ApplyStrategy();
        }

        private static void TuneStack(CardGroup group, StackZoneTuning t)
        {
            if (group == null) return;
            if (t != null) group.transform.localScale = new Vector3(t.areaScale.x, t.areaScale.y, 1f);
            var layout = group.GetComponent<StackLayout>();
            if (layout != null)
            {
                layout.UseMyScale = false;
                layout.Straighten = true;
            }
            group.ApplyStrategy();
        }

        private static void TuneSlot(CardGroup group, SlotZoneTuning t)
        {
            if (group == null) return;
            if (t != null) group.transform.localScale = new Vector3(t.areaScale.x, t.areaScale.y, 1f);
            var layout = group.GetComponent<SlotLayout>();
            if (layout != null)
            {
                layout.UseMyScale = false;
            }
            group.ApplyStrategy();
        }

        private void SyncAll()
        {
            // Player
            SyncGroupToInstances(playerDeck, _engine.State.player.deck, faceUp: false);
            SyncGroupToInstances(playerHand, _engine.State.player.hand, faceUp: true);
            SyncGroupToInstances(playerDiscard, _engine.State.player.discard, faceUp: false);
            SyncGroupToInstances(playerPlayed, _engine.State.player.played, faceUp: true);
            SyncGroupToInstances(playerAttire, _engine.State.player.attire, faceUp: true);

            // Market
            var rowList = new List<CobCardInstance>(6);
            for (var i = 0; i < 6; i++) if (_marketRowModel[i] != null) rowList.Add(_marketRowModel[i]);
            SyncGroupToInstances(marketRow, rowList, faceUp: true);
            SyncGroupToInstances(marketDeck, _marketDeckModel, faceUp: false);
            SyncGroupToInstances(marketTrash, new List<CobCardInstance>(0), faceUp: false);

            // Monsters
            var cur = new List<CobCardInstance>(1);
            if (_currentMonster != null) cur.Add(_currentMonster);
            SyncGroupToInstances(monsterSlot, cur, faceUp: true);
            SyncGroupToInstances(altarSlot, new List<CobCardInstance>(0), faceUp: true);
            SyncGroupToInstances(monsterDeck, _monsterDeckModel, faceUp: false);

            // AI
            SyncGroupToInstances(aiDeck, _engine.State.ai.deck, faceUp: false);
            SyncGroupToInstances(aiHand, _engine.State.ai.hand, faceUp: false);
            SyncGroupToInstances(aiDiscard, _engine.State.ai.discard, faceUp: false);
            SyncGroupToInstances(aiPlayed, _engine.State.ai.played, faceUp: true);
            SyncGroupToInstances(aiAttire, _engine.State.ai.attire, faceUp: true);
        }

        private void SyncGroupToInstances(CardGroup group, List<CobCardInstance> instances, bool faceUp)
        {
            if (group == null) return;
            ClearGroup(group);

            var settings = group.GetComponent<global::CardHouse.CardGroupSettings>();
            if (settings != null)
            {
                settings.ForcedFacing = faceUp ? global::CardHouse.CardFacing.FaceUp : global::CardHouse.CardFacing.FaceDown;
            }

            for (var i = 0; i < instances.Count; i++)
            {
                var inst = instances[i];
                if (inst == null) continue;

                var card = Instantiate(templateCard, group.transform);
                card.name = $"CoB_{inst.instanceId}_{inst.Def?.name ?? "Card"}";

                var view = card.GetComponent<CobCardHouseCardView>();
                if (view == null) view = card.gameObject.AddComponent<CobCardHouseCardView>();
                view.Bind(inst);

                var click = card.GetComponent<ClickDetector>();
                if (click == null) click = card.gameObject.AddComponent<ClickDetector>();
                click.SetIsActive(true);
                click.OnButtonClicked.RemoveAllListeners();

                // Wire interactions
                if (group == marketRow)
                {
                    var slotIndex = FindMarketSlotIndex(inst);
                    click.OnButtonClicked.AddListener(() => TryBuyFromMarket(slotIndex));
                }
                else if (group == monsterSlot)
                {
                    click.OnButtonClicked.AddListener(() => TryAttackMonster());
                }
                else if (group == playerHand)
                {
                    var capturedInstanceId = inst.instanceId;
                    click.OnButtonClicked.AddListener(() => TryPlayFromHand(capturedInstanceId));
                }

                card.SetFacing(faceUp);
                group.Mount(card, instaFlip: true);
            }

            group.ApplyStrategy();
            ApplyFixedCardScale(group);
        }

        private void ApplyFixedCardScale(CardGroup group)
        {
            if (group == null) return;
            var s = GetCardScaleForGroup(group);
            if (s <= 0f) return;

            for (var i = 0; i < group.MountedCards.Count; i++)
            {
                var c = group.MountedCards[i];
                if (c == null) continue;
                // Force local scale so sprite-sheet "full card" tiles render at sane size.
                c.Scaling.StartSeeking(s, new InstantFloatSeeker(), useLocalSpace: true);
            }
        }

        private float GetCardScaleForGroup(CardGroup g)
        {
            if (g == playerHand) return handTuning != null ? handTuning.cardScale : 0.22f;
            if (g == marketRow) return marketRowTuning != null ? marketRowTuning.cardScale : 0.22f;
            if (g == playerPlayed) return playedTuning != null ? playedTuning.cardScale : 0.22f;
            if (g == playerAttire) return attireTuning != null ? attireTuning.cardScale : 0.20f;
            if (g == monsterSlot) return monsterSlotTuning != null ? monsterSlotTuning.cardScale : 0.24f;
            if (g == altarSlot) return altarSlotTuning != null ? altarSlotTuning.cardScale : 0.24f;
            if (g == aiHand) return aiHandTuning != null ? aiHandTuning.cardScale : 0.14f;
            if (g == aiPlayed) return aiPlayedTuning != null ? aiPlayedTuning.cardScale : 0.16f;
            if (g == aiAttire) return aiAttireTuning != null ? aiAttireTuning.cardScale : 0.16f;

            // stacks
            if (g == playerDeck || g == playerDiscard || g == marketDeck || g == marketTrash || g == monsterDeck || g == monsterDefeated ||
                g == aiDeck || g == aiDiscard)
                return stackTuning != null ? stackTuning.cardScale : 0.18f;
            return 0.22f;
        }

        private int FindMarketSlotIndex(CobCardInstance inst)
        {
            for (var i = 0; i < _marketRowModel.Length; i++)
            {
                if (_marketRowModel[i] != null && _marketRowModel[i].instanceId == inst.instanceId) return i;
            }
            return -1;
        }

        private void TryPlayFromHand(int instanceId)
        {
            if (_engine.State.currentPlayer != CobPlayerId.Player) return;
            var hand = _engine.State.player.hand;
            var idx = -1;
            for (var i = 0; i < hand.Count; i++)
            {
                if (hand[i] != null && hand[i].instanceId == instanceId) { idx = i; break; }
            }
            if (idx < 0) return;
            _engine.PlayCardFromHand(CobPlayerId.Player, idx);
            SyncAll();
        }

        private void TryBuyFromMarket(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 6) return;
            if (_engine.State.currentPlayer != CobPlayerId.Player) return;
            var card = _marketRowModel[slotIndex];
            if (card == null) return;

            // Web: events auto-resolve when revealed; for now, treat event as "trash it and refill".
            if (IsEvent(card.Def))
            {
                _marketRowModel[slotIndex] = DrawNonEventFromMarketDeckOrNull(maxAttempts: 100);
                SyncAll();
                return;
            }

            var cost = Mathf.Max(0, card.Def.cost);
            if (_engine.State.player.AvailableBlessing < cost) return;

            _engine.State.player.spentBlessing += cost;
            _engine.State.player.discard.Add(card);

            _marketRowModel[slotIndex] = DrawNonEventFromMarketDeckOrNull(maxAttempts: 100);
            SyncAll();
        }

        private void TryAttackMonster()
        {
            if (_engine.State.currentPlayer != CobPlayerId.Player) return;
            if (_currentMonster == null) return;

            var power = Mathf.Max(1, _currentMonster.Def.cost);
            if (_engine.State.player.damageThisTurn < power) return;

            _engine.State.player.damageThisTurn -= power;

            // Reward effect: use effect1 as reward (close enough to web reward_effect/effect1 fallback)
            if (!string.IsNullOrWhiteSpace(_currentMonster.Def.effect1))
            {
                _engine.ApplyEffectText(_currentMonster.Def.effect1, _currentMonster, _engine.State.player, _engine.State.ai);
            }

            // Move monster to defeated, reveal next
            if (monsterDefeated != null)
            {
                // keep list implicit by mounting visuals; model is ok to ignore for now
            }
            RevealNextMonster();
            SyncAll();
        }

        private void ClearAllSpawnedCards()
        {
            ClearGroup(playerDeck);
            ClearGroup(playerHand);
            ClearGroup(playerDiscard);
            ClearGroup(playerPlayed);
            ClearGroup(playerAttire);
            ClearGroup(marketRow);
            ClearGroup(marketDeck);
            ClearGroup(marketTrash);
            ClearGroup(monsterSlot);
            ClearGroup(altarSlot);
            ClearGroup(monsterDeck);
            ClearGroup(monsterDefeated);

            ClearGroup(aiDeck);
            ClearGroup(aiHand);
            ClearGroup(aiDiscard);
            ClearGroup(aiPlayed);
            ClearGroup(aiAttire);
        }

        private static void ClearGroup(CardGroup g)
        {
            if (g == null) return;
            for (var i = g.MountedCards.Count - 1; i >= 0; i--)
            {
                var c = g.MountedCards[i];
                g.UnMount(i);
                if (c != null) Destroy(c.gameObject);
            }
        }

        private static void FillDeck(List<CobCardInstance> dst, CobCardRecord def, int count)
        {
            if (dst == null || def == null) return;
            for (var i = 0; i < count; i++)
            {
                // Instances will be re-created by engine.NewInstance in a real pipeline; for now we just set def and fix ids later.
                dst.Add(new CobCardInstance(UnityEngine.Random.Range(1, int.MaxValue), def));
            }
        }

        private static CobCardRecord FindByName(List<CobCardRecord> list, string name)
        {
            if (list == null) return null;
            for (var i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c?.name != null && string.Equals(c.name, name, StringComparison.OrdinalIgnoreCase)) return c;
            }
            return null;
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
            return FindFirstObjectOfTypeCompat<Card>();
        }

        private static T FindFirstObjectOfTypeCompat<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<T>();
#else
            return UnityEngine.Object.FindObjectOfType<T>();
#endif
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
            if (prefab == null)
            {
                Debug.LogError($"[CoB] Missing CardHouse group prefab at `{prefabPath}`");
                return null;
            }
            var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            return go.GetComponent<CardGroup>();
#else
            return null;
#endif
        }
    }
}

