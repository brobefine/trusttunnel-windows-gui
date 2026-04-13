using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using TrustTunnelGui.Models;

namespace TrustTunnelGui.Services;

public class ProfileStore
{
    private static readonly string AppDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrustTunnelGui");

    private static readonly string ProfilesFile = Path.Combine(AppDir, "profiles.json");

    public ObservableCollection<ServerProfile> Profiles { get; } = new();
    public Guid? ActiveId { get; set; }

    public ProfileStore() => Load();

    public ServerProfile? Active => ActiveId is Guid id ? Profiles.FirstOrDefault(p => p.Id == id) : null;

    public void Add(ServerProfile p) { Profiles.Add(p); Save(); }
    public void Remove(ServerProfile p) { Profiles.Remove(p); if (ActiveId == p.Id) ActiveId = null; Save(); }
    public void SetActive(ServerProfile p) { ActiveId = p.Id; Save(); }

    public string ConfigPathFor(ServerProfile p) =>
        Path.Combine(AppDir, "configs", $"{p.Id}.toml");

    public void Save()
    {
        Directory.CreateDirectory(AppDir);
        var dto = new StoreDto
        {
            ActiveId = ActiveId,
            Profiles = Profiles.ToList()
        };
        File.WriteAllText(ProfilesFile,
            JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void Load()
    {
        if (!File.Exists(ProfilesFile)) return;
        try
        {
            var dto = JsonSerializer.Deserialize<StoreDto>(File.ReadAllText(ProfilesFile));
            if (dto?.Profiles is null) return;
            foreach (var p in dto.Profiles) Profiles.Add(p);
            ActiveId = dto.ActiveId;
        }
        catch { /* corrupt file — start fresh */ }
    }

    private class StoreDto
    {
        public Guid? ActiveId { get; set; }
        public List<ServerProfile> Profiles { get; set; } = new();
    }
}
