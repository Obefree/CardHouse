using System.Collections.Generic;
using CardHouse;
using Cob.Data;
using Cob.Runtime;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cob.Presentation.CardHouse
{
    [DisallowMultipleComponent]
    public sealed class CobCardHouseDemoController : MonoBehaviour
    {
        [Header("Scene wiring (optional if auto-find works)")]
        [SerializeField] private int playerIndex = 1;
        [SerializeField] private CardGroup deck;
        [SerializeField] private CardGroup hand;
        [SerializeField] private CardGroup discard;
        [SerializeField] private CardGroup board;
        [SerializeField] private CardGroup market;
        [SerializeField] private bool verboseLogs = true;
        [SerializeField] private bool forceRelayoutGroups = true;

        [Header("Card spawning")]
        [Tooltip("If empty, first card found in scene will be used as template.")]
        [SerializeField] private Card templateCard;

        private CobGameEngine _engine;
        private CobCardDatabase _db;
        private CobCardHouseHud _hud;

        private readonly Dictionary<int, Card> _byInstanceId = new Dictionary<int, Card>();
        private readonly List<Card> _allSpawned = new List<Card>();

        // Minimal "market" outside of engine for now.
        private readonly List<CobCardInstance> _marketDeck = new List<CobCardInstance>();
        private readonly List<CobCardInstance> _marketRow = new List<CobCardInstance>();
        private int _nextExternalInstanceId = 1_000_000;

        private void Start()
        {
            StartCoroutine(Boot());
        }

        private System.Collections.IEnumerator Boot()
        {
            if (verboseLogs) Debug.Log("[CoB] Boot starting");
            TryAutoFindGroups();
            templateCard = ResolveTemplateCard(templateCard);

            DisableCardHouseSamplePopulation();
            EnsureCobBoard();

            _db = Resources.Load<CobCardDatabase>("CoB/CobCardDatabase");
            if (_db == null)
            {
                Debug.LogError("[CoB] Could not load `Assets/Resources/CoB/CobCardDatabase.asset`. Re-import the JSON into a ScriptableObject.");
                yield break;
            }
            if (verboseLogs)
            {
                Debug.Log($"[CoB] DB loaded. disciple={_db.disciple.Count}, starters={_db.starters.Count}, attire={_db.attire.Count}, monsters={_db.monsters.Count}, consumables={_db.consumables.Count}, events={_db.events.Count}, total={_db.All.Count}");
            }

            _engine = new CobGameEngine(_db);
            EnsureHud();

            // Give CardHouse scene one frame to initialize its System/Groups.
            yield return null;
            TryAutoFindGroups();
            if (forceRelayoutGroups) RelayoutGroupsToCamera();
            RestartGame();
        }

        private void EnsureCobBoard()
        {
            if (GroupRegistry.Instance == null) return;

            // Create our own clean groups (so we don't depend on the demo scene layout/settings).
            // We keep them as normal scene objects, not saved assets.
#if UNITY_EDITOR
            deck = deck != null ? deck : CreateOrFindGroup("CoB_Deck", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardStack.prefab");
            hand = hand != null ? hand : CreateOrFindGroup("CoB_Hand", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardHand.prefab");
            discard = discard != null ? discard : CreateOrFindGroup("CoB_Discard", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardStackDiscard.prefab");
            board = board != null ? board : CreateOrFindGroup("CoB_Board", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardGrid.prefab");
            market = market != null ? market : CreateOrFindGroup("CoB_Market", "Assets/CardHouse/CardHouseCore/CardGroupPrefabs/CardGrid.prefab");

            WireRegistry(playerIndex, GroupName.Deck, deck);
            WireRegistry(playerIndex, GroupName.Hand, hand);
            WireRegistry(playerIndex, GroupName.Discard, discard);
            WireRegistry(playerIndex, GroupName.Board, board);
            WireRegistry(playerIndex, GroupName.A, market);

            if (verboseLogs) Debug.Log("[CoB] Ensured clean CoB board groups + registry wiring.");
#endif
        }

#if UNITY_EDITOR
        private static CardGroup CreateOrFindGroup(string name, string prefabPath)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
            {
                var cg = existing.GetComponent<CardGroup>();
                if (cg != null) return cg;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[CoB] Missing CardHouse group prefab at `{prefabPath}`");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            return go.GetComponent<CardGroup>();
        }

        private static void WireRegistry(int ownerIndex, GroupName name, CardGroup group)
        {
            if (GroupRegistry.Instance == null || group == null) return;

            // Replace existing mapping if present.
            for (var i = GroupRegistry.Instance.Groups.Count - 1; i >= 0; i--)
            {
                var g = GroupRegistry.Instance.Groups[i];
                if (g.PlayerIndex == ownerIndex && g.Name == name)
                {
                    GroupRegistry.Instance.Groups.RemoveAt(i);
                }
            }

            GroupRegistry.Instance.Groups.Add(new GroupRegistry.NamedGroup
            {
                PlayerIndex = ownerIndex,
                Name = name,
                Group = group
            });
        }
#endif

        private static void DisableCardHouseSamplePopulation()
        {
            // The sample scenes (like Ccg.unity) use CardHouse.GroupSetup / DeckSetup to spawn demo cards.
            // We disable them so only CoB drives what cards exist.
            var groupSetups = FindObjectsOfTypeCompat<global::CardHouse.GroupSetup>(includeInactive: true);
            for (var i = 0; i < groupSetups.Length; i++)
            {
                var gs = groupSetups[i];
                if (gs == null) continue;
                gs.RunOnStart = false;
                gs.StopAllCoroutines();
                gs.enabled = false;
            }

            var deckSetups = FindObjectsOfTypeCompat<global::CardHouse.DeckSetup>(includeInactive: true);
            for (var i = 0; i < deckSetups.Length; i++)
            {
                var ds = deckSetups[i];
                if (ds == null) continue;
                ds.RunOnStart = false;
                ds.StopAllCoroutines();
                ds.enabled = false;
            }
        }

        private void EnsureHud()
        {
            if (_hud != null) return;
            var go = new GameObject("CoB_CardHouse_HUD");
            go.transform.SetParent(transform, false);
            _hud = go.AddComponent<CobCardHouseHud>();
            _hud.Bind(_engine);
            _hud.WireButtons(
                onEndTurn: () =>
                {
                    _engine.EndTurn(_engine.State.currentPlayer);
                    RunAiTurnIfNeeded();
                    SyncAllZones();
                },
                onRestart: () => RestartGame()
            );
        }

        private void RestartGame()
        {
            if (verboseLogs) Debug.Log("[CoB] RestartGame");
            ClearExistingCards();
            _engine.StartNewGame(skipInitialDraw: false);
            BuildMarket();
            SpawnAndMountFromEngine();
            _hud.Refresh();
            StartCoroutine(PurgeNonCobCardsDelayed());
        }

        private void TryAutoFindGroups()
        {
            if (deck != null && hand != null && discard != null && board != null) return;

            if (GroupRegistry.Instance == null)
            {
                Debug.LogWarning("[CoB] GroupRegistry not found in scene (CardHouse not initialized?).");
                return;
            }

            // Prefer the configured playerIndex, but fall back to "any" if not found.
            if (deck == null) deck = GroupRegistry.Instance.Get(GroupName.Deck, playerIndex) ?? GroupRegistry.Instance.Get(GroupName.Deck, null);
            if (hand == null) hand = GroupRegistry.Instance.Get(GroupName.Hand, playerIndex) ?? GroupRegistry.Instance.Get(GroupName.Hand, null);
            if (discard == null) discard = GroupRegistry.Instance.Get(GroupName.Discard, playerIndex) ?? GroupRegistry.Instance.Get(GroupName.Discard, null);
            if (board == null) board = GroupRegistry.Instance.Get(GroupName.Board, playerIndex) ?? GroupRegistry.Instance.Get(GroupName.Board, null);
            if (market == null) market = GroupRegistry.Instance.Get(GroupName.A, playerIndex) ?? GroupRegistry.Instance.Get(GroupName.A, null);

            if (market == null)
            {
                EnsureMarketGroupByCloningHand();
            }

            if (verboseLogs)
            {
                Debug.Log($"[CoB] Groups: deck={(deck ? deck.name : "null")} hand={(hand ? hand.name : "null")} discard={(discard ? discard.name : "null")} board={(board ? board.name : "null")} market={(market ? market.name : "null")}, playerIndex={playerIndex}");
            }

            if (deck == null || hand == null || discard == null || board == null)
            {
                Debug.LogWarning("[CoB] Could not auto-find groups (Deck/Hand/Discard/Board). Open `Assets/CardHouse/SampleGames/CCG/Ccg.unity` or assign groups in inspector.");
            }
        }

        private void EnsureMarketGroupByCloningHand()
        {
            if (market != null) return;
            if (hand == null) return;

            var cloneGo = Instantiate(hand.gameObject, hand.transform.parent);
            cloneGo.name = "Market (CoB)";
            cloneGo.transform.position = ComputeMarketPosition();
            cloneGo.transform.rotation = hand.transform.rotation;

            market = cloneGo.GetComponent<CardGroup>();
            if (market != null)
            {
                market.MountedCards.Clear();
            }

            if (verboseLogs)
            {
                Debug.Log($"[CoB] Market group created by cloning `{hand.name}` → `{cloneGo.name}`");
            }
        }

        private Vector3 ComputeMarketPosition()
        {
            // The CCG sample scene places some groups slightly outside the camera view (Y≈3.7 while orthoSize=3).
            // Place market near the table/board and clamp into view.
            var basePos = board != null ? board.transform.position : hand.transform.position;
            var pos = basePos + new Vector3(0f, -1.9f, 0f);

            var cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                var halfH = cam.orthographicSize;
                var minY = cam.transform.position.y - halfH + 0.8f;
                var maxY = cam.transform.position.y + halfH - 0.8f;
                pos.y = Mathf.Clamp(pos.y, minY, maxY);
            }

            return pos;
        }

        private void ClearExistingCards()
        {
            // Remove ALL cards from relevant groups, including any sample/demo cards.
            if (GroupRegistry.Instance != null)
            {
                for (var i = 0; i < GroupRegistry.Instance.Groups.Count; i++)
                {
                    var named = GroupRegistry.Instance.Groups[i];
                    var g = named.Group;
                    if (g == null) continue;
                    var groupName = named.Name;
                    if (groupName != GroupName.Deck &&
                        groupName != GroupName.Hand &&
                        groupName != GroupName.Discard &&
                        groupName != GroupName.Board &&
                        groupName != GroupName.A)
                    {
                        continue;
                    }

                    for (var ci = g.MountedCards.Count - 1; ci >= 0; ci--)
                    {
                        var c = g.MountedCards[ci];
                        g.UnMount(ci);
                        if (c != null) Destroy(c.gameObject);
                    }
                }
            }

            for (var i = _allSpawned.Count - 1; i >= 0; i--)
            {
                var c = _allSpawned[i];
                if (c != null) Destroy(c.gameObject);
            }

            _allSpawned.Clear();
            _byInstanceId.Clear();

            _marketDeck.Clear();
            _marketRow.Clear();
        }

        private System.Collections.IEnumerator PurgeNonCobCardsDelayed()
        {
            // Sometimes sample scripts instantiate cards slightly later; purge again after a couple frames.
            yield return null;
            yield return null;

            var cards = FindObjectsOfTypeCompat<global::CardHouse.Card>(includeInactive: true);
            var removed = 0;
            for (var i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                if (c == null) continue;
                if (c.GetComponent<CobCardHouseCardView>() != null) continue; // ours
                Destroy(c.gameObject);
                removed++;
            }

            if (verboseLogs && removed > 0) Debug.Log($"[CoB] Purged non-CoB cards: {removed}");
        }

        private void RelayoutGroupsToCamera()
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return;

            var cy = cam.transform.position.y;
            var cx = cam.transform.position.x;
            var halfH = cam.orthographicSize;
            var halfW = halfH * cam.aspect;

            var marginX = 0.8f;
            var marginY = 0.8f;

            var top = cy + halfH - marginY;
            var bottom = cy - halfH + marginY;
            var left = cx - halfW + marginX;
            var right = cx + halfW - marginX;

            // Layout:
            // - Hand bottom center
            // - Board center
            // - Deck/Discard bottom left/right
            // - Market top center (if present/created)
            if (hand != null) hand.transform.position = new Vector3(cx, bottom, hand.transform.position.z);
            if (board != null) board.transform.position = new Vector3(cx, Mathf.Lerp(bottom, top, 0.55f), board.transform.position.z);
            if (deck != null) deck.transform.position = new Vector3(left, bottom, deck.transform.position.z);
            if (discard != null) discard.transform.position = new Vector3(right, bottom, discard.transform.position.z);
            if (market != null) market.transform.position = new Vector3(cx, top, market.transform.position.z);

            if (verboseLogs)
            {
                Debug.Log($"[CoB] Relayout groups to camera. bottom={bottom:F2} top={top:F2} left={left:F2} right={right:F2}");
            }
        }

        private void SpawnAndMountFromEngine()
        {
            if (deck == null || hand == null || discard == null || board == null)
            {
                TryAutoFindGroups();
            }

            if (deck == null || hand == null || discard == null || board == null)
            {
                Debug.LogError("[CoB] Missing CardGroups (Deck/Hand/Discard/Board).");
                return;
            }

            if (templateCard == null)
            {
                Debug.LogError("[CoB] Could not resolve a template `Card`. Assign `Template Card` in inspector, or ensure `CcgCard.prefab` exists in the project.");
                return;
            }

            var p = _engine.State.player;
            if (verboseLogs) Debug.Log($"[CoB] Spawning player zones: deck={p.deck.Count}, hand={p.hand.Count}, discard={p.discard.Count}, played={p.played.Count}, marketRow={_marketRow.Count}");
            SpawnZone(deck, p.deck, faceUp: false);
            SpawnZone(hand, p.hand, faceUp: true);
            SpawnZone(discard, p.discard, faceUp: false);
            SpawnZone(board, p.played, faceUp: true);

            if (market != null)
            {
                SpawnZone(market, _marketRow, faceUp: true, isMarket: true);
                market.ApplyStrategy();
            }

            deck.ApplyStrategy();
            hand.ApplyStrategy();
            discard.ApplyStrategy();
            board.ApplyStrategy();
        }

        private static T FindFirstObjectOfTypeCompat<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }

        private static T[] FindObjectsOfTypeCompat<T>(bool includeInactive) where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<T>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );
#else
            return Object.FindObjectsOfType<T>(includeInactive);
#endif
        }

        private static Card ResolveTemplateCard(Card current)
        {
            if (current != null) return current;

            var fromScene = FindFirstObjectOfTypeCompat<Card>();
            if (fromScene != null) return fromScene;

#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardHouse/SampleGames/CCG/Prefabs/CcgCard.prefab");
            if (prefab == null)
            {
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardHouse/SampleGames/CCG/Prefabs/CcgCardWithCost.prefab");
            }
            if (prefab != null)
            {
                var card = prefab.GetComponent<Card>();
                if (card != null) return card;
                card = prefab.GetComponentInChildren<Card>();
                if (card != null) return card;
            }
#endif

            return null;
        }

        private void BuildMarket()
        {
            _marketDeck.Clear();
            _marketRow.Clear();

            if (_db == null) return;

            // Mirrors web `createMarket()`:
            // - Base: disciple + attire with cost > 0
            // - Add consumables (if enabled; for now: always add)
            // - Events: in web they are optional + never appear in initial row; for now: exclude from row entirely
            // - Deduplicate by name
            // - Multiply by `copies` (fallback 1)
            AddMarketEligible(_db.disciple, requireCostPositive: true);
            AddMarketEligible(_db.attire, requireCostPositive: true);
            AddMarketEligible(_db.consumables, requireCostPositive: false);

            // Shuffle using engine RNG, so it is stable with seed (if added later).
            _engine.Shuffle(_marketDeck);

            // Web fills 6 slots. We'll do 6 as well.
            for (var i = 0; i < 6; i++)
            {
                RefillMarketSlot();
            }

            void AddMarketEligible(List<CobCardRecord> defs, bool requireCostPositive)
            {
                if (defs == null) return;

                var seenNames = new HashSet<string>();
                for (var i = 0; i < defs.Count; i++)
                {
                    var def = defs[i];
                    if (def == null) continue;

                    var name = (def.name ?? string.Empty).Trim();
                    if (name.Length == 0 || name.Equals("None", System.StringComparison.OrdinalIgnoreCase)) continue;

                    // Dedup by name within each list build pass; we'll do global dedup after collecting.
                    // (Keeps behavior close enough without a full stable union pass.)
                    if (!seenNames.Add(name)) continue;

                    var cost = def.cost;
                    if (requireCostPositive && cost <= 0) continue;

                    var hasEffects = !string.IsNullOrWhiteSpace(def.effect1) || !string.IsNullOrWhiteSpace(def.effect2) ||
                                     !string.IsNullOrWhiteSpace(def.effect1text) || !string.IsNullOrWhiteSpace(def.effect2text);
                    if (!hasEffects) continue;

                    var copies = ParseCopies(def.copies);
                    for (var c = 0; c < copies; c++)
                    {
                        _marketDeck.Add(new CobCardInstance(_nextExternalInstanceId++, def));
                    }
                }
            }
        }

        private static int ParseCopies(int raw)
        {
            return Mathf.Max(1, raw);
        }

        private void RefillMarketSlot()
        {
            if (_marketDeck.Count == 0) return;
            var idx = _marketDeck.Count - 1;
            var c = _marketDeck[idx];
            _marketDeck.RemoveAt(idx);
            _marketRow.Add(c);
        }

        private void SpawnZone(CardGroup targetGroup, List<CobCardInstance> instances, bool faceUp, bool isMarket = false)
        {
            for (var i = 0; i < instances.Count; i++)
            {
                var inst = instances[i];
                var card = Instantiate(templateCard, targetGroup.transform);
                card.name = $"CoB_{inst.instanceId}_{inst.Def?.name ?? "Card"}";

                var view = card.gameObject.GetComponent<CobCardHouseCardView>();
                if (view == null) view = card.gameObject.AddComponent<CobCardHouseCardView>();
                view.Bind(inst);

                var click = card.gameObject.GetComponent<ClickDetector>();
                if (click == null) click = card.gameObject.AddComponent<ClickDetector>();
                click.SetIsActive(true);
                click.OnButtonClicked.RemoveAllListeners();
                if (isMarket)
                {
                    click.OnButtonClicked.AddListener(() => OnMarketCardClicked(card));
                }
                else
                {
                    click.OnButtonClicked.AddListener(() => OnCardClicked(card));
                }

                card.SetFacing(faceUp);
                targetGroup.Mount(card, instaFlip: true);

                _allSpawned.Add(card);
                _byInstanceId[inst.instanceId] = card;
            }
        }

        private void OnCardClicked(Card clicked)
        {
            if (clicked == null || _engine == null) return;

            var view = clicked.GetComponent<CobCardHouseCardView>();
            if (view == null) return;

            if (_engine.State.currentPlayer != CobPlayerId.Player)
            {
                clicked.ToggleFocus();
                return;
            }

            var handIndex = IndexOfInstance(_engine.State.player.hand, view.InstanceId);
            if (handIndex < 0)
            {
                clicked.ToggleFocus();
                return;
            }

            var ok = _engine.PlayCardFromHand(CobPlayerId.Player, handIndex);
            if (!ok)
            {
                clicked.ToggleFocus();
                return;
            }

            SyncAllZones();
        }

        private void OnMarketCardClicked(Card clicked)
        {
            if (clicked == null || _engine == null) return;
            var view = clicked.GetComponent<CobCardHouseCardView>();
            if (view == null) return;

            // Acquire: move from market row -> player's discard (very first simple rule).
            var marketIndex = IndexOfInstance(_marketRow, view.InstanceId);
            if (marketIndex < 0)
            {
                clicked.ToggleFocus();
                return;
            }

            var acquired = _marketRow[marketIndex];
            _marketRow.RemoveAt(marketIndex);
            _engine.State.player.discard.Add(acquired);

            RefillMarketSlot();
            SyncAllZones();
        }

        private void RunAiTurnIfNeeded()
        {
            if (_engine.State.currentPlayer != CobPlayerId.Ai) return;

            var safety = 20;
            while (_engine.State.currentPlayer == CobPlayerId.Ai && safety-- > 0)
            {
                if (_engine.State.ai.hand.Count > 0)
                {
                    _engine.PlayCardFromHand(CobPlayerId.Ai, 0);
                }
                else
                {
                    _engine.EndTurn(CobPlayerId.Ai);
                }
            }
        }

        private void SyncAllZones()
        {
            var p = _engine.State.player;

            SyncZone(deck, p.deck, faceUp: false);
            SyncZone(hand, p.hand, faceUp: true);
            SyncZone(discard, p.discard, faceUp: false);
            SyncZone(board, p.played, faceUp: true);
            if (market != null) SyncZone(market, _marketRow, faceUp: true, isMarket: true);

            deck.ApplyStrategy();
            hand.ApplyStrategy();
            discard.ApplyStrategy();
            board.ApplyStrategy();
            if (market != null) market.ApplyStrategy();

            _hud.Refresh();
        }

        private void SyncZone(CardGroup target, List<CobCardInstance> zone, bool faceUp, bool isMarket = false)
        {
            if (target == null) return;

            for (var i = 0; i < zone.Count; i++)
            {
                var inst = zone[i];
                if (!_byInstanceId.TryGetValue(inst.instanceId, out var card) || card == null)
                {
                    card = Instantiate(templateCard, target.transform);
                    card.name = $"CoB_{inst.instanceId}_{inst.Def?.name ?? "Card"}";

                    var view = card.gameObject.GetComponent<CobCardHouseCardView>();
                    if (view == null) view = card.gameObject.AddComponent<CobCardHouseCardView>();
                    view.Bind(inst);

                    var click = card.gameObject.GetComponent<ClickDetector>();
                    if (click == null) click = card.gameObject.AddComponent<ClickDetector>();
                    click.SetIsActive(true);
                    click.OnButtonClicked.RemoveAllListeners();
                    if (isMarket)
                    {
                        click.OnButtonClicked.AddListener(() => OnMarketCardClicked(card));
                    }
                    else
                    {
                        click.OnButtonClicked.AddListener(() => OnCardClicked(card));
                    }

                    _allSpawned.Add(card);
                    _byInstanceId[inst.instanceId] = card;
                }

                card.SetFacing(faceUp);
                if (card.Group != target) target.Mount(card, instaFlip: true);
            }
        }

        private static int IndexOfInstance(List<CobCardInstance> list, int instanceId)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i]?.instanceId == instanceId) return i;
            }
            return -1;
        }
    }
}

