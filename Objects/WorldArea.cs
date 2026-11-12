using System;
using System.Globalization;
using System.Text;

namespace UselessPlayerInfo.Objects;

[Serializable]
public class WorldArea
{
    public string Name { get; set; } = "";
    public string Notes { get; set; } = "";
    public uint TerritoryId { get; set; }
    public uint MapId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }

    public string ToCode()
    {
        var x = X.ToString("0.0", CultureInfo.InvariantCulture);
        var y = Y.ToString("0.0", CultureInfo.InvariantCulture);
        var raw = $"{TerritoryId}:{MapId}:{x}:{y}:{Name}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public static bool TryParseCode(string code, out WorldArea? spot)
    {
        spot = null;

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(code.Trim()));
            var parts = raw.Split(':', 5);

            if (parts.Length != 5
                || !uint.TryParse(parts[0], out var territoryId)
                || !uint.TryParse(parts[1], out var mapId)
                || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                return false;
            }

            spot = new WorldArea
            {
                Name = parts[4],
                TerritoryId = territoryId,
                MapId = mapId,
                X = x,
                Y = y,
            };
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
