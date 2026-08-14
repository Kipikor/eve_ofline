using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace EveOffline
{
    public static class MarketService
    {
        public const int JitaStationId = 60003760;
        public const int TheForgeRegionId = 10000002;
        public const string SourceName = "EVE ESI + Fuzzwork — Jita 4-4";
        const long CacheSeconds = 300;
        const int TimeoutSeconds = 15;
        const int MaxPreciseEsiTypes = 13;
        const int MaxEsiPagesPerType = 20;
        static bool refreshInProgress;

        [Serializable]
        sealed class OrdersEnvelope { public EsiOrder[] orders = Array.Empty<EsiOrder>(); }

        [Serializable]
        sealed class EsiOrder
        {
            public long location_id = 0;
            public bool is_buy_order = false;
            public double price = 0;
            public long volume_remain = 0;
        }

        public static bool IsFresh(GameSave save)
        {
            return save?.PriceCache != null && save.PriceCache.UpdatedUnix > 0 &&
                   DateTimeOffset.UtcNow.ToUnixTimeSeconds() - save.PriceCache.UpdatedUnix < CacheSeconds;
        }

        public static IEnumerator Refresh(GameSave save, Action<bool, string> completed)
        {
            if (refreshInProgress) { completed?.Invoke(false, "Обновление цен Jita уже выполняется."); yield break; }
            if (save == null) { completed?.Invoke(false, "Нет сохранения."); yield break; }
            if (save.PriceCache == null) save.PriceCache = new MarketPriceCache();
            if (save.PriceCache.Entries == null) save.PriceCache.Entries = new List<MarketPriceEntry>();
            if (IsFresh(save)) { completed?.Invoke(true, "Цены Jita уже свежие."); yield break; }
            refreshInProgress = true;

            var ids = CollectTypeIds().Where(typeId => typeId > 0).Distinct().ToArray();
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var snapshotUpdated = 0;
            var esiUpdated = 0;

            // Fuzzwork exposes the complete Jita station aggregate in one request. This
            // prevents a large catalog from starving behind a small per-refresh ESI cap.
            if (ids.Length > 0)
            {
                var types = string.Join(",", ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));
                var url = $"https://market.fuzzwork.co.uk/aggregates/?region={TheForgeRegionId}&station={JitaStationId}&types={types}";
                using var request = UnityWebRequest.Get(url);
                request.timeout = TimeoutSeconds;
                request.SetRequestHeader("Accept", "application/json");
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success &&
                    TryParseFuzzwork(request.downloadHandler.text, out var quotes))
                {
                    foreach (var quote in quotes)
                    {
                        if (UpsertPrice(save, quote.TypeId, quote.BuyPrice, quote.SellPrice, now, "Fuzzwork Jita 4-4"))
                            snapshotUpdated++;
                    }
                }
            }

            // ESI remains the authoritative source. Refresh the highest-value gameplay
            // quotes precisely and overwrite the aggregate snapshot when station orders exist.
            foreach (var typeId in CollectPriorityTypeIds().Where(id => id > 0).Distinct().Take(MaxPreciseEsiTypes))
            {
                var stationOrders = new List<EsiOrder>();
                var pages = 1;
                var receivedAnyPage = false;
                for (var page = 1; page <= pages && page <= MaxEsiPagesPerType; page++)
                {
                    var url = $"https://esi.evetech.net/latest/markets/{TheForgeRegionId}/orders/?datasource=tranquility&order_type=all&type_id={typeId}&page={page}";
                    using var request = UnityWebRequest.Get(url);
                    request.timeout = TimeoutSeconds;
                    request.SetRequestHeader("Accept", "application/json");
                    request.SetRequestHeader("X-Compatibility-Date", "2026-08-01");
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success) break;
                    if (!TryParseOrders(request.downloadHandler.text, out var orders)) break;
                    receivedAnyPage = true;
                    stationOrders.AddRange(orders.Where(order => order.location_id == JitaStationId && order.price > 0));

                    if (page == 1 && int.TryParse(request.GetResponseHeader("X-Pages"), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out var pageCount))
                        pages = Math.Max(1, Math.Min(pageCount, MaxEsiPagesPerType));
                }

                if (!receivedAnyPage) continue;
                var buy = stationOrders.Where(order => order.is_buy_order)
                    .OrderByDescending(order => order.price).FirstOrDefault()?.price ?? 0;
                var sell = stationOrders.Where(order => !order.is_buy_order)
                    .OrderBy(order => order.price).FirstOrDefault()?.price ?? 0;
                if (UpsertPrice(save, typeId, buy, sell, now,
                        buy > 0 && sell > 0 ? "EVE ESI Jita 4-4" : "EVE ESI + Fuzzwork Jita 4-4"))
                    esiUpdated++;
            }

            var updated = snapshotUpdated + esiUpdated;
            if (updated <= 0)
            {
                // Preserve both the last known prices and their timestamp. A failed refresh
                // must not turn zero/missing quotes into free items or mark stale data fresh.
                refreshInProgress = false;
                completed?.Invoke(false, "Сеть цен недоступна — используются последние известные и встроенные цены.");
                yield break;
            }

            save.PriceCache.StationId = JitaStationId;
            save.PriceCache.Source = snapshotUpdated > 0 && esiUpdated > 0
                ? SourceName
                : snapshotUpdated > 0 ? "Fuzzwork — Jita 4-4" : "EVE ESI — Jita 4-4";
            save.PriceCache.SourceScope = $"Jita 4-4: snapshot {snapshotUpdated}/{ids.Length}, ESI {esiUpdated}";
            save.PriceCache.UpdatedUnix = now;
            refreshInProgress = false;
            completed?.Invoke(true, $"Jita: snapshot {snapshotUpdated}/{ids.Length}, ESI уточнил {esiUpdated} типов.");
        }

        static bool TryParseOrders(string json, out EsiOrder[] orders)
        {
            orders = Array.Empty<EsiOrder>();
            if (string.IsNullOrWhiteSpace(json) || json[0] != '[') return false;
            try
            {
                var envelope = JsonUtility.FromJson<OrdersEnvelope>("{\"orders\":" + json + "}");
                orders = envelope?.orders ?? Array.Empty<EsiOrder>();
                return true;
            }
            catch { return false; }
        }

        readonly struct FuzzworkQuote
        {
            public readonly int TypeId;
            public readonly double BuyPrice;
            public readonly double SellPrice;

            public FuzzworkQuote(int typeId, double buyPrice, double sellPrice)
            {
                TypeId = typeId;
                BuyPrice = buyPrice;
                SellPrice = sellPrice;
            }
        }

        static bool TryParseFuzzwork(string json, out List<FuzzworkQuote> quotes)
        {
            quotes = new List<FuzzworkQuote>();
            if (string.IsNullOrWhiteSpace(json) || json[0] != '{') return false;
            try
            {
                const string itemPattern = "\"(?<id>\\d+)\"\\s*:\\s*\\{\\s*\"buy\"\\s*:\\s*\\{(?<buy>[^{}]*)\\}\\s*,\\s*\"sell\"\\s*:\\s*\\{(?<sell>[^{}]*)\\}\\s*\\}";
                foreach (Match match in Regex.Matches(json, itemPattern, RegexOptions.CultureInvariant | RegexOptions.Singleline))
                {
                    if (!int.TryParse(match.Groups["id"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var typeId))
                        continue;
                    TryReadJsonNumber(match.Groups["buy"].Value, "max", out var buy);
                    TryReadJsonNumber(match.Groups["sell"].Value, "min", out var sell);
                    if (IsUsablePrice(buy) || IsUsablePrice(sell)) quotes.Add(new FuzzworkQuote(typeId, buy, sell));
                }
                return quotes.Count > 0;
            }
            catch { return false; }
        }

        static bool TryReadJsonNumber(string jsonObjectBody, string property, out double value)
        {
            value = 0;
            var pattern = "\"" + Regex.Escape(property) + "\"\\s*:\\s*\"?(?<value>[-+0-9.eE]+)\"?";
            var match = Regex.Match(jsonObjectBody ?? string.Empty, pattern, RegexOptions.CultureInvariant);
            return match.Success && double.TryParse(match.Groups["value"].Value, NumberStyles.Float,
                       CultureInfo.InvariantCulture, out value) && IsUsablePrice(value);
        }

        static bool UpsertPrice(GameSave save, int typeId, double buyPrice, double sellPrice, long now, string sourceScope)
        {
            var hasBuy = IsUsablePrice(buyPrice);
            var hasSell = IsUsablePrice(sellPrice);
            if (!hasBuy && !hasSell) return false;

            var entry = save.PriceCache.Find(typeId);
            if (entry == null)
            {
                entry = new MarketPriceEntry { TypeId = typeId };
                save.PriceCache.Entries.Add(entry);
            }
            if (hasBuy) entry.BuyPrice = buyPrice;
            if (hasSell) entry.SellPrice = sellPrice;
            entry.UpdatedUnix = now;
            entry.SourceScope = sourceScope;
            return true;
        }

        static bool IsUsablePrice(double value)
        {
            return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        public static double GetBuyPrice(GameSave save, int typeId, double fallback)
        {
            var value = save?.PriceCache?.Find(typeId)?.BuyPrice ?? 0;
            return IsUsablePrice(value) ? value : fallback;
        }

        public static double GetSellPrice(GameSave save, int typeId, double fallback)
        {
            var value = save?.PriceCache?.Find(typeId)?.SellPrice ?? 0;
            return IsUsablePrice(value) ? value : fallback;
        }

        public static double OreBuyPerUnit(GameSave save, OreDefinition ore) => GetBuyPrice(save, ore.TypeId, ore.FallbackBuyPrice);
        public static double HullSellPrice(GameSave save, ShipDefinition hull) => hull.FallbackBuyPrice <= 0 ? 0 : GetSellPrice(save, hull.TypeId, hull.FallbackBuyPrice);
        public static double ModuleSellPrice(GameSave save, MiningModuleDefinition module) => GetSellPrice(save, module.TypeId, module.FallbackPrice);
        public static double DroneSellPrice(GameSave save, DroneDefinition drone) => GetSellPrice(save, drone.TypeId, drone.FallbackPrice);
        public static double CrystalSellPrice(GameSave save, MiningCrystalDefinition crystal) => GetSellPrice(save, crystal.TypeId, crystal.FallbackPrice);
        public static double BurstChargeSellPrice(GameSave save, BurstChargeDefinition charge) => GetSellPrice(save, charge.TypeId, charge.FallbackPrice);

        /// <summary>
        /// Simplified offline rule: every Large Skill Injector grants the same
        /// amount and never depends on the pilot's current or total SP.
        /// Parameters are retained for compatibility with existing callers.
        /// </summary>
        public static double InjectorSp(GameSave save, CharacterSave pilot) => Catalog.LargeSkillInjectorSkillPoints;

        public static double InjectorPrice(GameSave save) => GetSellPrice(save, Catalog.LargeSkillInjectorTypeId, Catalog.LargeSkillInjectorFallbackPrice);

        /// <summary>
        /// Simplified offline rule: every Small Skill Injector grants a fixed
        /// 80,000 unallocated SP, regardless of the pilot's total SP.
        /// </summary>
        public static double SmallInjectorSp(GameSave save, CharacterSave pilot) => Catalog.SmallSkillInjectorSkillPoints;

        public static double SmallInjectorPrice(GameSave save) => GetSellPrice(save, Catalog.SmallSkillInjectorTypeId, Catalog.SmallSkillInjectorFallbackPrice);

        static IEnumerable<int> CollectPriorityTypeIds()
        {
            yield return Catalog.GetOre("clear-icicle").TypeId;
            yield return Catalog.GetShip("perseverance").TypeId;
            yield return Catalog.GetModule("ice-mining-laser-i").TypeId;
            yield return Catalog.GetModule("ice-mining-laser-ii").TypeId;
            yield return Catalog.GetModule("ore-ice-mining-laser").TypeId;
            yield return Catalog.LargeSkillInjectorTypeId;
            yield return Catalog.SmallSkillInjectorTypeId;
            foreach (var ore in Catalog.Ores.Take(4)) yield return ore.TypeId;
            foreach (var module in Catalog.Modules.Take(2)) yield return module.TypeId;
        }

        static IEnumerable<int> CollectTypeIds()
        {
            foreach (var ore in Catalog.Ores) if (ore.TypeId > 0) yield return ore.TypeId;
            foreach (var ship in Catalog.Ships) if (ship.TypeId > 0) yield return ship.TypeId;
            foreach (var module in Catalog.Modules) if (module.TypeId > 0) yield return module.TypeId;
            foreach (var drone in Catalog.Drones) if (drone.TypeId > 0) yield return drone.TypeId;
            foreach (var crystal in Catalog.Crystals) if (crystal.TypeId > 0) yield return crystal.TypeId;
            foreach (var tank in Catalog.TankPresets)
                foreach (var typeId in tank.ComponentTypeIds ?? Array.Empty<int>())
                    if (typeId > 0) yield return typeId;
            foreach (var skill in Catalog.Skills) if (skill.TypeId > 0) yield return skill.TypeId;
            yield return Catalog.LargeSkillInjectorTypeId;
            yield return Catalog.SmallSkillInjectorTypeId;
        }
    }
}
