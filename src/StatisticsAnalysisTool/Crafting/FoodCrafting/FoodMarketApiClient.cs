using Serilog;
using StatisticsAnalysisTool.Common;
using StatisticsAnalysisTool.Common.UserSettings;
using StatisticsAnalysisTool.Enumerations;
using StatisticsAnalysisTool.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StatisticsAnalysisTool.Crafting.FoodCrafting;

// [FORK] Food crafting feature. Fork-only file.
// Own client because ApiController merges multi-item responses by quality only (breaks batched requests).
public sealed class FoodMarketApiClient
{
    private const int BatchSize = 50;
    private const int MaximumAttempts = 3;
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Current prices of all items in one city.</summary>
    public async Task<Dictionary<string, FoodMarketPrice>> GetPricesAsync(
        IReadOnlyCollection<string> itemUniqueNames,
        MarketLocation city,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, FoodMarketPrice>(StringComparer.OrdinalIgnoreCase);
        var location = Uri.EscapeDataString(GetApiLocationName(city));

        foreach (var batch in Batch(itemUniqueNames))
        {
            var url = $"{GetBaseUrl()}/stats/prices/{string.Join(",", batch.Select(Uri.EscapeDataString))}.json?locations={location}";
            var responses = await GetAsync<List<MarketResponse>>(url, cancellationToken).ConfigureAwait(false) ?? [];

            foreach (var group in responses.Where(x => !string.IsNullOrWhiteSpace(x?.ItemTypeId)).GroupBy(x => x.ItemTypeId, StringComparer.OrdinalIgnoreCase))
            {
                var bestSell = group
                    .Where(x => x.SellPriceMin > 0)
                    .OrderBy(x => x.SellPriceMin)
                    .ThenByDescending(x => x.SellPriceMinDate)
                    .FirstOrDefault();
                var bestBuy = group
                    .Where(x => x.BuyPriceMax > 0)
                    .OrderByDescending(x => x.BuyPriceMax)
                    .ThenByDescending(x => x.BuyPriceMaxDate)
                    .FirstOrDefault();

                result[group.Key] = new FoodMarketPrice
                {
                    SellPriceMin = bestSell?.SellPriceMin ?? 0,
                    SellPriceMinDateUtc = AsUtc(bestSell?.SellPriceMinDate ?? DateTime.MinValue),
                    BuyPriceMax = bestBuy?.BuyPriceMax ?? 0,
                    BuyPriceMaxDateUtc = AsUtc(bestBuy?.BuyPriceMaxDate ?? DateTime.MinValue)
                };
            }
        }

        return result;
    }

    /// <summary>Average items sold per day in one city over the last <paramref name="days"/> days.</summary>
    public async Task<Dictionary<string, decimal>> GetDailyVolumesAsync(
        IReadOnlyCollection<string> itemUniqueNames,
        MarketLocation city,
        int days,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var location = Uri.EscapeDataString(GetApiLocationName(city));
        days = Math.Clamp(days, 1, 28);
        var fromUtc = DateTime.UtcNow.Date.AddDays(-days);
        var fromText = fromUtc.ToString("yyyy-M-d", CultureInfo.InvariantCulture);

        foreach (var batch in Batch(itemUniqueNames))
        {
            var url = $"{GetBaseUrl()}/stats/history/{string.Join(",", batch.Select(Uri.EscapeDataString))}.json?locations={location}&date={fromText}&time-scale=24";
            var responses = await GetAsync<List<MarketHistoriesResponse>>(url, cancellationToken).ConfigureAwait(false) ?? [];

            foreach (var group in responses.Where(x => !string.IsNullOrWhiteSpace(x?.ItemId)).GroupBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase))
            {
                var sold = group
                    .SelectMany(x => x.Data ?? new List<MarketHistoryResponse>())
                    .Where(x => x != null && AsUtc(x.Timestamp) >= fromUtc)
                    .Sum(x => (decimal) Math.Max(0, x.ItemCount));

                result[group.Key] = Math.Round(sold / days, 2, MidpointRounding.AwayFromZero);
            }
        }

        return result;
    }

    public static string GetApiLocationName(MarketLocation city)
    {
        return city switch
        {
            MarketLocation.MartlockMarket => "Martlock",
            MarketLocation.LymhurstMarket => "Lymhurst",
            MarketLocation.BridgewatchMarket => "Bridgewatch",
            MarketLocation.FortSterlingMarket => "Fort Sterling",
            MarketLocation.ThetfordMarket => "Thetford",
            MarketLocation.CaerleonMarket => "Caerleon",
            _ => "Brecilien"
        };
    }

    private static IEnumerable<List<string>> Batch(IReadOnlyCollection<string> names)
    {
        var distinct = (names ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < distinct.Count; i += BatchSize)
        {
            yield return distinct.Skip(i).Take(BatchSize).ToList();
        }
    }

    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string GetBaseUrl()
    {
        var settings = SettingsController.CurrentSettings;
        var url = AppDataPaths.ActiveUserDataServerLocation switch
        {
            ServerLocation.America => settings.AlbionDataProjectBaseUrlWest,
            ServerLocation.Asia => settings.AlbionDataProjectBaseUrlEast,
            _ => settings.AlbionDataProjectBaseUrlEurope
        };

        return (url ?? string.Empty).TrimEnd('/');
    }

    private static async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken) where T : class
    {
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

            try
            {
                using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaximumAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 5), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning("Food crafting market request failed. Status={StatusCode}, Attempt={Attempt}, Url={Url}", response.StatusCode, attempt, url);
                    if (attempt < MaximumAttempts && (int) response.StatusCode >= 500)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token).ConfigureAwait(false);
                return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < MaximumAttempts)
            {
                Log.Warning("Food crafting market request timed out. Attempt={Attempt}, Url={Url}", attempt, url);
            }
            catch (HttpRequestException e) when (attempt < MaximumAttempts)
            {
                Log.Warning(e, "Food crafting market request failed. Attempt={Attempt}, Url={Url}", attempt, url);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Log.Error(e, "Food crafting market request failed. Url={Url}", url);
                return null;
            }
        }

        return null;
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };

        return new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }
}

public sealed class FoodMarketPrice
{
    public ulong SellPriceMin { get; init; }
    public DateTime SellPriceMinDateUtc { get; init; }
    public ulong BuyPriceMax { get; init; }
    public DateTime BuyPriceMaxDateUtc { get; init; }
}
