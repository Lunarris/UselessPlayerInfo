using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace UselessPlayerInfo.Functions;

internal static class WeatherHelper
{
    private const long PeriodSeconds = 1400;

    public static unsafe byte GetCurrentWeatherId()
    {
        return WeatherManager.Instance()->GetCurrentWeather();
    }

    public static Weather? GetCurrentWeather()
    {
        if (Housing.GetOriginalHouseTerritoryTypeId() is { } interiorId)
        {
            return GetWeatherAt(Housing.ToWardTerritory(interiorId), DateTimeOffset.UtcNow.ToUnixTimeSeconds() / PeriodSeconds);
        }

        return Plugin.DataManager.GetExcelSheet<Weather>().TryGetRow(GetCurrentWeatherId(), out var row) ? row : null;
    }

    public static uint CurrentWeatherTerritory()
        => Housing.GetOriginalHouseTerritoryTypeId() is { } interiorId
            ? Housing.ToWardTerritory(interiorId)
            : Plugin.ClientState.TerritoryType;

    private static Weather? GetWeatherAt(uint territoryId, long period)
    {
        if (!Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory)
            || territory.WeatherRate.RowId == 0
            || territory.WeatherRate.ValueNullable is not { } rate)
        {
            return null;
        }

        return PickWeather(rate, CalculateTarget(period * PeriodSeconds));
    }

    private static uint cachedTerritory;
    private static long cachedPeriod = -1;
    private static int cachedCount;
    private static List<(Weather Weather, DateTimeOffset Start)> cachedForecast = new();

    public static IReadOnlyList<(Weather Weather, DateTimeOffset Start)> GetForecast(uint territoryId, int count)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var period = now / PeriodSeconds;

        if (period == cachedPeriod && territoryId == cachedTerritory && count == cachedCount)
        {
            return cachedForecast;
        }

        cachedPeriod = period;
        cachedTerritory = territoryId;
        cachedCount = count;
        cachedForecast = new List<(Weather, DateTimeOffset)>();

        if (!Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory)
            || territory.WeatherRate.RowId == 0
            || territory.WeatherRate.ValueNullable is not { } rate)
        {
            return cachedForecast;
        }

        for (var i = 1; i <= count; i++)
        {
            var start = (period + i) * PeriodSeconds;
            if (PickWeather(rate, CalculateTarget(start)) is { } weather)
            {
                cachedForecast.Add((weather, DateTimeOffset.FromUnixTimeSeconds(start)));
            }
        }

        return cachedForecast;
    }

    private static Weather? PickWeather(WeatherRate rate, int target)
    {
        var sum = 0;
        for (var i = 0; i < rate.Rate.Count; i++)
        {
            sum += rate.Rate[i];
            if (target < sum)
            {
                return rate.Weather[i].ValueNullable;
            }
        }

        return null;
    }

    private static int CalculateTarget(long unixSeconds)
    {
        var bell = unixSeconds / 175;
        var increment = (uint)(bell + 8 - bell % 8) % 24;
        var totalDays = (uint)(unixSeconds / 4200);
        var calcBase = totalDays * 100 + increment;
        var step1 = (calcBase << 11) ^ calcBase;
        var step2 = (step1 >> 8) ^ step1;
        return (int)(step2 % 100);
    }
}
