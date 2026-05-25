using System.IO;
using System.Text.Json;
using SerialMaster.UI.Models;

namespace SerialMaster.UI.Services;

public class FavoritesService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SerialMaster", "favorites.json");

    /// <summary>Fired after a successful Save so live Sessions can refresh their quick-send strip.</summary>
    public event EventHandler? Changed;

    public List<FavoriteItem> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<FavoriteItem>>(json) ?? new();
            }
        }
        catch { }
        return new();
    }

    public void Save(List<FavoriteItem> items)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch { }
    }
}
