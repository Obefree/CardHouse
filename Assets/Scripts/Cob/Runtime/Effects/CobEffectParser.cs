using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Cob.Runtime.Effects
{
    public sealed class CobEffectParser
    {
        private static readonly Regex TokenRegex = new(@"\{[^}]+\}", RegexOptions.Compiled);
        private static readonly Regex TriggerRegex = new(@"\{\s*Trigger\s*\}\s*(.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ChainRegex = new(@"\{\s*([RWBG])_chain\s*\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TrashThisRegex = new(@"\{\s*Trash_this\s*\}\s*(.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SacrificeToRegex = new(@"\{\s*Sacrifice\s+(\d+)\s*\}\s*TO\s*(.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex CompactTokenFixRegex = new(@"\{(Trash|Blessing|Damage|Draw|Heal|Poison|Bleed|Stun|Burn|Mana|Ready)(\d+)\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Insert spaces around uppercase TO when glued (TOGain, TOPut, destroyedTO{...})
        private static readonly Regex GlueToPrevRegex = new(@"([A-Za-z])TO(?=[A-Z\{])", RegexOptions.Compiled);
        private static readonly Regex GlueToNextRegex = new(@"(^|[^A-Za-z])TO(?=[A-Z\{])", RegexOptions.Compiled);

        // Real OR is uppercase OR as a word (lowercase "or less/more" is not).
        private static readonly Regex RealOrRegex = new(@"\bOR\b", RegexOptions.Compiled);
        private static readonly Regex OrLessMoreRegex = new(@"(?:or\s+less|or\s+more)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex UpperToRegex = new(@"\bTO\b", RegexOptions.Compiled);

        private readonly Dictionary<string, CobEffectParseResult> _cache = new(StringComparer.Ordinal);

        public CobEffectParseResult Analyze(string effectText)
        {
            var key = effectText ?? string.Empty;
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var res = new CobEffectParseResult
            {
                original = effectText ?? string.Empty,
                normalized = Normalize(effectText),
                orOptions = new List<string>(),
                tokens = new List<string>()
            };

            var text = res.normalized;

            var triggerMatch = TriggerRegex.Match(text);
            if (triggerMatch.Success)
            {
                res.hasTrigger = true;
                var triggerPart = triggerMatch.Groups[1].Value.Trim();

                // Trigger condition can contain uppercase TO (avoid matching lowercase "to" in prose).
                if (triggerPart.Contains("TO", StringComparison.Ordinal) && UpperToRegex.IsMatch(triggerPart))
                {
                    var m = Regex.Match(triggerPart, @"(.+?)\s*TO\s*(.+)");
                    if (m.Success)
                    {
                        res.triggerCondition = m.Groups[1].Value.Trim();
                        res.triggerEffect = m.Groups[2].Value.Trim();
                    }
                    else
                    {
                        res.triggerEffect = triggerPart;
                    }
                }
                else
                {
                    res.triggerEffect = triggerPart;
                }
            }

            // Free-text IF ... TO ... (treated as trigger-like conditional)
            if (!res.hasTrigger && text.Contains("TO", StringComparison.Ordinal))
            {
                var ifMatch = Regex.Match(text, @"IF\s+(.+?)\s*TO\s*(.+)", RegexOptions.IgnoreCase);
                if (ifMatch.Success)
                {
                    res.hasTrigger = true;
                    res.triggerCondition = ifMatch.Groups[1].Value.Trim();
                    res.triggerEffect = ifMatch.Groups[2].Value.Trim();
                }
            }

            var chainMatch = ChainRegex.Match(text);
            if (chainMatch.Success)
            {
                res.hasChain = true;
                res.chainColor = char.ToLowerInvariant(chainMatch.Groups[1].Value[0]); // r/w/b/g
            }

            var trashThisMatch = TrashThisRegex.Match(text);
            if (trashThisMatch.Success)
            {
                res.hasTrashThis = true;
            }

            // Sacrifice TO shortcut (common)
            var sacrificeMatch = SacrificeToRegex.Match(text);
            if (sacrificeMatch.Success)
            {
                res.hasTo = true;
                res.costPart = $"{{Sacrifice {sacrificeMatch.Groups[1].Value}}}";
                res.effectPart = sacrificeMatch.Groups[2].Value.Trim();
            }

            // Generic TO split (skip trigger/trash_this special-cases)
            if (!res.hasTo && !res.hasTrigger && !res.hasTrashThis && text.Contains("TO", StringComparison.Ordinal) && UpperToRegex.IsMatch(text))
            {
                var toMatch = Regex.Match(text, @"(.*)\bTO\b\s*([\s\S]*)$");
                if (toMatch.Success)
                {
                    res.hasTo = true;
                    var beforeTo = toMatch.Groups[1].Value.Trim();
                    var tokensBefore = TokenRegex.Matches(beforeTo);
                    res.costPart = tokensBefore.Count > 0 ? tokensBefore[^1].Value.Trim() : beforeTo;
                    res.effectPart = toMatch.Groups[2].Value.Trim();

                    // Fix "pre-effect TO cost+effect" pattern: move leading cost-like tokens into costPart.
                    if (!LooksLikeCostToken(res.costPart))
                    {
                        var rest = res.effectPart ?? string.Empty;
                        var leadingCosts = new List<string>();
                        while (true)
                        {
                            var m = Regex.Match(rest, @"^\s*(\{[^}]+\})\s*([\s\S]*)$");
                            if (!m.Success) break;
                            var tok = m.Groups[1].Value.Trim();
                            if (LooksLikeCostToken(tok))
                            {
                                leadingCosts.Add(tok);
                                rest = m.Groups[2].Value ?? string.Empty;
                                continue;
                            }
                            break;
                        }

                        if (leadingCosts.Count > 0)
                        {
                            res.costPart = string.Concat(leadingCosts);
                            res.effectPart = rest.Trim();
                        }
                    }
                }
            }

            var hasRealOr = RealOrRegex.IsMatch(text) && !OrLessMoreRegex.IsMatch(text);
            if (!res.hasTrigger && hasRealOr)
            {
                var parts = SplitTopLevelOr(text);
                if (parts.Count > 1)
                {
                    res.hasOr = true;
                    res.orOptions = parts;
                    if (res.hasTo)
                    {
                        var lastWithTo = string.Empty;
                        for (var i = 0; i < parts.Count; i++)
                        {
                            if (UpperToRegex.IsMatch(parts[i])) lastWithTo = parts[i];
                        }

                        if (!string.IsNullOrWhiteSpace(lastWithTo))
                        {
                            var m = Regex.Match(lastWithTo, @"(.*)\bTO\b\s*([\s\S]*)$");
                            if (m.Success)
                            {
                                var tokensBefore = TokenRegex.Matches(m.Groups[1].Value);
                                res.costPart = tokensBefore.Count > 0 ? tokensBefore[^1].Value.Trim() : res.costPart;
                                res.effectPart = m.Groups[2].Value.Trim();
                            }
                        }
                    }
                }
            }

            foreach (Match m in TokenRegex.Matches(text))
            {
                res.tokens.Add(m.Value);
            }

            _cache[key] = res;
            return res;
        }

        public static string Normalize(string effectText)
        {
            var s = (effectText ?? string.Empty).Replace("\r\n", "\n").Replace("\n", " ").Trim();
            if (s.Length == 0) return s;

            s = CompactTokenFixRegex.Replace(s, "{$1 $2}");
            s = GlueToPrevRegex.Replace(s, "$1 TO ");
            s = GlueToNextRegex.Replace(s, "$1TO ");
            return s.Trim();
        }

        private static bool LooksLikeCostToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            return Regex.IsMatch(
                token,
                @"\{(?:Discard\s+\d+|Burn\s+\d+|Sacrifice\s+\d+|Mana\s+\d+|Damage\s+\d+|Threshold_20|Trash_this)\}",
                RegexOptions.IgnoreCase
            );
        }

        private static List<string> SplitTopLevelOr(string effectText)
        {
            var s = (effectText ?? string.Empty).Trim();
            if (s.Length == 0) return new List<string>();

            var lastToMatch = Regex.Match(s, @"(.*)\bTO\b\s*([\s\S]*)$");
            if (!lastToMatch.Success)
            {
                var raw = Regex.Split(s, @"\bOR\b");
                var list = new List<string>(raw.Length);
                for (var i = 0; i < raw.Length; i++)
                {
                    var p = raw[i].Trim();
                    if (p.Length > 0) list.Add(p);
                }
                return list;
            }

            var beforeTo = lastToMatch.Groups[1].Value.Trim();
            var afterTo = lastToMatch.Groups[2].Value.Trim();

            var tokensBefore = TokenRegex.Matches(beforeTo);
            if (tokensBefore.Count == 0)
            {
                var raw = Regex.Split(s, @"\bOR\b");
                var list = new List<string>(raw.Length);
                for (var i = 0; i < raw.Length; i++)
                {
                    var p = raw[i].Trim();
                    if (p.Length > 0) list.Add(p);
                }
                return list;
            }

            var costToken = tokensBefore[^1].Value;
            var costStart = beforeTo.LastIndexOf(costToken, StringComparison.Ordinal);
            if (costStart < 0)
            {
                var raw = Regex.Split(s, @"\bOR\b");
                var list = new List<string>(raw.Length);
                for (var i = 0; i < raw.Length; i++)
                {
                    var p = raw[i].Trim();
                    if (p.Length > 0) list.Add(p);
                }
                return list;
            }

            var toBlock = (beforeTo[costStart..] + " TO " + afterTo).Trim();
            var beforeBlock = beforeTo[..costStart].Trim();
            var beforeParts = beforeBlock.Length > 0 ? SplitTopLevelOr(beforeBlock) : new List<string>();
            beforeParts.Add(toBlock);
            return beforeParts;
        }
    }
}

