using System;
using System.Collections.Generic;
using Cob.Data;
using Cob.Runtime.Effects;
using UnityEngine;

namespace Cob.Runtime
{
    public sealed class CobGameEngine
    {
        private readonly CobCardDatabase _db;
        private readonly CobEffectParser _parser = new CobEffectParser();
        private readonly System.Random _rng;
        private int _nextInstanceId = 1;

        public CobGameState State { get; }

        public CobGameEngine(CobCardDatabase db, int seed = 0, int hpPlayer = 50, int hpAi = 50)
        {
            _db = db ? db : throw new ArgumentNullException(nameof(db));
            _rng = seed == 0 ? new System.Random() : new System.Random(seed);
            State = new CobGameState(hpPlayer, hpAi);
        }

        public CobCardInstance NewInstance(CobCardRecord def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            return new CobCardInstance(_nextInstanceId++, def);
        }

        public void StartNewGame(bool skipInitialDraw = false)
        {
            ClearZones(State.player);
            ClearZones(State.ai);
            ResetTurnCounters(State.player);
            ResetTurnCounters(State.ai);

            CreateStartingDecks();

            if (!skipInitialDraw)
            {
                DrawCards(State.player, 3);
                DrawCards(State.ai, 5);
            }
        }

        private void ClearZones(CobPlayerState p)
        {
            p.deck.Clear();
            p.hand.Clear();
            p.discard.Clear();
            p.trash.Clear();
            p.played.Clear();
            p.attire.Clear();
        }

        private void ResetTurnCounters(CobPlayerState p)
        {
            p.poison = 0;
            p.bleed = 0;
            p.blessingThisTurn = 0;
            p.spentBlessing = 0;
            p.manaThisTurn = 0;
            p.spentMana = 0;
            p.damageThisTurn = 0;
            p.healThisTurn = 0;
            p.cardsDrawnThisTurn = 0;
            p.ignoreAttireDefenseThisTurn = false;
            p.nextAcquireToHandThisTurn = false;
            p.nextAcquireToTopdeckThisTurn = false;
        }

        private void CreateStartingDecks()
        {
            var prayer = FindByName(_db.starters, "Prayer") ?? new CobCardRecord { name = "Prayer", effect1 = "{Blessing 1}", cost = 0, color = "white", card_id = -10001 };
            var strike = FindByName(_db.starters, "Strike") ?? new CobCardRecord { name = "Strike", effect1 = "{Damage 1}", cost = 0, color = "red", card_id = -10002 };

            AddCopiesToDeck(State.player, prayer, 7);
            AddCopiesToDeck(State.player, strike, 3);
            Shuffle(State.player.deck);

            AddCopiesToDeck(State.ai, prayer, 7);
            AddCopiesToDeck(State.ai, strike, 3);
            Shuffle(State.ai.deck);
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

        private void AddCopiesToDeck(CobPlayerState p, CobCardRecord def, int count)
        {
            for (var i = 0; i < count; i++)
            {
                p.deck.Add(new CobCardInstance(_nextInstanceId++, def));
            }
        }

        public void DrawCards(CobPlayerState p, int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (p.deck.Count == 0)
                {
                    if (p.discard.Count == 0) return;
                    p.deck.AddRange(p.discard);
                    p.discard.Clear();
                    Shuffle(p.deck);
                }

                var card = PopTop(p.deck);
                p.hand.Add(card);
                p.cardsDrawnThisTurn += 1;
            }
        }

        private CobCardInstance PopTop(List<CobCardInstance> deck)
        {
            var idx = deck.Count - 1;
            var c = deck[idx];
            deck.RemoveAt(idx);
            return c;
        }

        public void Shuffle<T>(List<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public bool PlayCardFromHand(CobPlayerId who, int handIndex)
        {
            var p = State.Get(who);
            var opp = State.OpponentOf(who);
            if (State.currentPlayer != who) return false;
            if (handIndex < 0 || handIndex >= p.hand.Count) return false;

            var card = p.hand[handIndex];
            p.hand.RemoveAt(handIndex);

            card.playedThisTurn = true;
            card.activated = true;
            card.usedThisTurn = true;

            if (IsAttire(card))
            {
                p.attire.Add(card);
                card.usedThisTurn = false;
            }
            else
            {
                p.played.Add(card);
            }

            ApplyAutoOnPlay(card, p, opp);
            return true;
        }

        private static bool IsAttire(CobCardInstance card)
        {
            var t = (card.Def?.type ?? card.Def?.card_type ?? string.Empty).Trim();
            return t.Equals("Attire", StringComparison.OrdinalIgnoreCase) || t.Equals("attire", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyAutoOnPlay(CobCardInstance card, CobPlayerState p, CobPlayerState opp)
        {
            var effects = GatherEffectStrings(card.Def);
            for (var i = 0; i < effects.Count; i++)
            {
                var txt = effects[i];
                if (string.IsNullOrWhiteSpace(txt)) continue;

                if (IsAttire(card)) continue;

                var analysis = _parser.Analyze(txt);
                if (analysis.hasOr || analysis.hasTo || analysis.hasTrigger || analysis.hasChain || analysis.hasTrashThis)
                {
                    continue;
                }

                ApplyEffectText(txt, card, p, opp);
            }
        }

        private static List<string> GatherEffectStrings(CobCardRecord def)
        {
            var list = new List<string>(6);

            static string N(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return null;
                var t = s.Trim();
                if (t.Equals("none", StringComparison.OrdinalIgnoreCase)) return null;
                return t;
            }

            void Add(string s)
            {
                var v = N(s);
                if (v != null) list.Add(v);
            }

            if (def == null) return list;

            Add(def.effect1);
            Add(def.effect1text);
            Add(def.effect2);
            Add(def.effect2text);
            return list;
        }

        public void EndTurn(CobPlayerId who)
        {
            var p = State.Get(who);
            var opp = State.OpponentOf(who);

            if (who == CobPlayerId.Player && p.damageThisTurn > 0)
            {
                opp.hp -= p.damageThisTurn;
                p.damageThisTurn = 0;
            }

            p.blessingThisTurn = 0;
            p.spentBlessing = 0;
            p.manaThisTurn = 0;
            p.spentMana = 0;
            p.damageThisTurn = 0;
            p.healThisTurn = 0;
            p.cardsDrawnThisTurn = 0;
            p.ignoreAttireDefenseThisTurn = false;
            p.nextAcquireToHandThisTurn = false;
            p.nextAcquireToTopdeckThisTurn = false;

            while (p.hand.Count > 0)
            {
                p.discard.Add(PopTop(p.hand));
            }

            for (var i = 0; i < p.played.Count; i++)
            {
                var c = p.played[i];
                if (c.trashThis) p.trash.Add(c);
                else p.discard.Add(c);
            }
            p.played.Clear();

            // Web version draws 5 cards at end-turn.
            DrawCards(p, 5);

            State.turn += who == CobPlayerId.Ai ? 1 : 0;
            State.currentPlayer = who == CobPlayerId.Player ? CobPlayerId.Ai : CobPlayerId.Player;
        }

        public void ApplyEffectText(string effectText, CobCardInstance source, CobPlayerState p, CobPlayerState opp)
        {
            var normalized = CobEffectParser.Normalize(effectText);
            if (string.IsNullOrWhiteSpace(normalized)) return;
            if (normalized.IndexOf('X', StringComparison.Ordinal) >= 0)
            {
                Debug.LogWarning($"[CoB] Effect requires X choice: \"{normalized}\" (skipped)");
                return;
            }

            var analysis = _parser.Analyze(normalized);
            if (analysis.hasTrigger || analysis.hasChain)
            {
                Debug.LogWarning($"[CoB] Conditional effect not executed automatically yet: \"{analysis.normalized}\"");
                return;
            }

            if (analysis.hasTrashThis)
            {
                if (source != null) source.trashThis = true;
                var rest = analysis.normalized;
                rest = rest.Replace("{Trash_this}", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                if (rest.StartsWith("TO ", StringComparison.OrdinalIgnoreCase)) rest = rest[3..].Trim();
                if (rest.StartsWith("TO", StringComparison.OrdinalIgnoreCase) && rest.Length > 2 && char.IsWhiteSpace(rest[2])) rest = rest[3..].Trim();
                if (!string.IsNullOrWhiteSpace(rest))
                {
                    ApplyEffectText(rest, source, p, opp);
                }
                return;
            }

            if (analysis.hasOr && analysis.orOptions is { Count: > 0 })
            {
                var chosen = analysis.orOptions[0];
                ApplyEffectText(chosen, source, p, opp);
                return;
            }

            if (analysis.hasTo)
            {
                if (!TryApplyCosts(analysis.costPart, source, p, opp)) return;
                ApplyEffectText(analysis.effectPart, source, p, opp);
                return;
            }

            var tokens = analysis.tokens;
            if (tokens.Count == 0)
            {
                ApplyFreeText(normalized, p, opp);
                return;
            }

            for (var i = 0; i < tokens.Count; i++)
            {
                ApplyToken(tokens[i], source, p, opp);
            }
        }

        private void ApplyToken(string token, CobCardInstance source, CobPlayerState p, CobPlayerState opp)
        {
            var t = token.Trim();
            if (t.Length == 0) return;

            if (TryParseIntToken(t, "Damage", out var v))
            {
                p.damageThisTurn += v;
                return;
            }
            if (TryParseIntToken(t, "Blessing", out v))
            {
                p.blessingThisTurn += v;
                return;
            }
            if (TryParseIntToken(t, "Mana", out v))
            {
                p.manaThisTurn += v;
                return;
            }
            if (TryParseIntToken(t, "Heal", out v))
            {
                p.hp += v;
                p.healThisTurn += v;
                return;
            }
            if (TryParseIntToken(t, "Draw", out v))
            {
                DrawCards(p, v);
                return;
            }
            if (TryParseIntToken(t, "Poison", out v))
            {
                opp.poison += v;
                return;
            }
            if (TryParseIntToken(t, "Bleed", out v))
            {
                opp.bleed += v;
                return;
            }
            if (TryParseIntToken(t, "Stun", out v))
            {
                DiscardRandom(opp, v);
                return;
            }
            if (TryParseIntToken(t, "Discard", out v))
            {
                DiscardRandom(p, v);
                return;
            }
            if (TryParseIntToken(t, "Burn", out v))
            {
                var ok = TryPayBlessing(p, v);
                if (!ok) return;
                return;
            }
            if (TryParseIntToken(t, "Sacrifice", out v))
            {
                p.hp -= v;
                return;
            }
            if (TryParseIntToken(t, "Trash", out v))
            {
                TrashRandomFromHand(p, v == 0 ? 1 : v);
                return;
            }
            if (t.Equals("{Shuffle}", StringComparison.OrdinalIgnoreCase))
            {
                Shuffle(p.deck);
                return;
            }
            if (t.Equals("{Trash_this}", StringComparison.OrdinalIgnoreCase))
            {
                if (source != null) source.trashThis = true;
                return;
            }

            Debug.LogWarning($"[CoB] Unhandled token: {t}");
        }

        private bool TryPayBlessing(CobPlayerState p, int amount)
        {
            if (amount <= 0) return true;
            if (p.AvailableBlessing < amount) return false;
            p.spentBlessing += amount;
            return true;
        }

        private bool TryApplyCosts(string costPart, CobCardInstance source, CobPlayerState p, CobPlayerState opp)
        {
            var s = CobEffectParser.Normalize(costPart);
            if (string.IsNullOrWhiteSpace(s)) return true;

            var a = _parser.Analyze(s);
            for (var i = 0; i < a.tokens.Count; i++)
            {
                var tok = a.tokens[i].Trim();
                if (TryParseIntToken(tok, "Burn", out var burn))
                {
                    if (!TryPayBlessing(p, burn)) return false;
                    continue;
                }
                if (TryParseIntToken(tok, "Sacrifice", out var sac))
                {
                    p.hp -= sac;
                    continue;
                }
                if (TryParseIntToken(tok, "Discard", out var dis))
                {
                    DiscardRandom(p, dis);
                    continue;
                }
                if (TryParseIntToken(tok, "Trash", out var tr))
                {
                    TrashRandomFromHand(p, tr == 0 ? 1 : tr);
                    continue;
                }
                if (tok.Equals("{Trash_this}", StringComparison.OrdinalIgnoreCase))
                {
                    if (source != null) source.trashThis = true;
                    continue;
                }

                Debug.LogWarning($"[CoB] Unhandled cost token: {tok}");
            }

            return true;
        }

        private void DiscardRandom(CobPlayerState p, int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (p.hand.Count == 0) return;
                var idx = _rng.Next(p.hand.Count);
                var c = p.hand[idx];
                p.hand.RemoveAt(idx);
                p.discard.Add(c);
            }
        }

        private void TrashRandomFromHand(CobPlayerState p, int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (p.hand.Count == 0) return;
                var idx = _rng.Next(p.hand.Count);
                var c = p.hand[idx];
                p.hand.RemoveAt(idx);
                p.trash.Add(c);
            }
        }

        private static bool TryParseIntToken(string token, string name, out int value)
        {
            value = 0;
            if (!token.StartsWith("{", StringComparison.Ordinal) || !token.EndsWith("}", StringComparison.Ordinal)) return false;
            var inner = token[1..^1].Trim();
            if (!inner.StartsWith(name, StringComparison.OrdinalIgnoreCase)) return false;

            var rest = inner[name.Length..].Trim();
            if (rest.Length == 0) return false;
            return int.TryParse(rest, out value);
        }

        private void ApplyFreeText(string effectText, CobPlayerState p, CobPlayerState opp)
        {
            if (RegexLike(effectText, "ignore rival'?s attire defen[cs]e"))
            {
                p.ignoreAttireDefenseThisTurn = true;
                return;
            }

            if (RegexLike(effectText, "Next\\s+card\\s+you\\s+acquire\\s+this\\s+turn\\s+goes\\s+into\\s+your\\s+hand"))
            {
                p.nextAcquireToHandThisTurn = true;
                return;
            }

            if (RegexLike(effectText, "Next\\s+card\\s+you\\s+acquire(?:\\s+this\\s+turn)?\\s+goes\\s+on\\s+top\\s+of\\s+your\\s+deck"))
            {
                p.nextAcquireToTopdeckThisTurn = true;
                return;
            }

            Debug.LogWarning($"[CoB] Unhandled free-text effect: \"{effectText}\"");
        }

        private static bool RegexLike(string input, string pattern)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(input ?? string.Empty, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
    }
}

