using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

#nullable enable
namespace SimpleModManager
{
  public static class AutoPathFinder
  {

    private static readonly string SteamDefaultPath =
      @"C:\Program Files (x86)\Steam\steamapps\common\Dead by Daylight\DeadByDaylight\Content\Paks";

    private static readonly string EpicDefaultPath =
      @"C:\Program Files\Epic Games\DeadByDaylight\DeadByDaylight\Content\Paks";

    private static readonly string MicrosoftStoreDefaultPath =
      @"C:\XboxGames\Dead By Daylight\Content\DeadByDaylight\Content\Paks";

    public static string DetectDbdPath(string platform)
    {
      if (platform.Equals("Steam", StringComparison.OrdinalIgnoreCase))
      {
        return Directory.Exists(SteamDefaultPath) ? SteamDefaultPath : "";
      }

      if (platform.Equals("Epic", StringComparison.OrdinalIgnoreCase))
      {
        return Directory.Exists(EpicDefaultPath) ? EpicDefaultPath : "";
      }

      if (platform.Equals("Microsoft Store", StringComparison.OrdinalIgnoreCase))
      {
        return Directory.Exists(MicrosoftStoreDefaultPath) ? MicrosoftStoreDefaultPath : "";
      }

      return "";
    }

    public static string GetOrCreateLocalModsDirectory()
    {
      string modsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".isolumia", "Mods");

      if (!Directory.Exists(modsDir))
      {
        Directory.CreateDirectory(modsDir);
      }

      return modsDir;
    }

    public static List<PakMods> ScanForAvailableMods()
    {
      var discovered = new List<PakMods>();
      string modsDir = GetOrCreateLocalModsDirectory();

      foreach (var subDir in Directory.GetDirectories(modsDir))
      {
        string folderName = Path.GetFileName(subDir);

        if (Directory.EnumerateFiles(subDir, "*.pak", SearchOption.AllDirectories).Any())
        {
          discovered.Add(new PakMods
          {
            PakName = folderName,
            LocationPath = subDir,
            IsActive = false
          });
        }
      }

      foreach (var zipFile in Directory.GetFiles(modsDir, "*.zip"))
      {
        string zipName = Path.GetFileNameWithoutExtension(zipFile);

        try
        {
          using var archive = ZipFile.OpenRead(zipFile);

          bool hasPak = archive.Entries.Any(e =>
            e.FullName.EndsWith(".pak", StringComparison.OrdinalIgnoreCase));

          if (hasPak)
          {
            discovered.Add(new PakMods
            {
              PakName = zipName,
              LocationPath = zipFile,
              IsActive = false
            });
          }
        }
        catch { }
      }

      return discovered;
    }

    public static string RetrievePlatformSuffix(string platform)
    {
      if (platform.Equals("Steam", StringComparison.OrdinalIgnoreCase))
        return "-Windows";

      if (platform.Equals("Microsoft Store", StringComparison.OrdinalIgnoreCase))
        return "-WinGDK";

      return "-EGS";
    }

    public static List<int> GetModChunkIds(PakMods mod)
    {
      var chunkIds = new List<int>();

      if (Directory.Exists(mod.LocationPath))
      {
        foreach (var pakPath in Directory.EnumerateFiles(mod.LocationPath, "*.pak", SearchOption.AllDirectories))
        {
          var match = Regex.Match(
            Path.GetFileNameWithoutExtension(pakPath),
            @"pakchunk(\d+)",
            RegexOptions.IgnoreCase);

          if (match.Success && int.TryParse(match.Groups[1].Value, out int id))
            chunkIds.Add(id);
        }
      }
      else if (File.Exists(mod.LocationPath) &&
               mod.LocationPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
      {
        try
        {
          using var archive = ZipFile.OpenRead(mod.LocationPath);

          foreach (var entry in archive.Entries.Where(e =>
            e.FullName.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)))
          {
            var match = Regex.Match(
              Path.GetFileNameWithoutExtension(entry.FullName),
              @"pakchunk(\d+)",
              RegexOptions.IgnoreCase);

            if (match.Success && int.TryParse(match.Groups[1].Value, out int id))
              chunkIds.Add(id);
          }
        }
        catch { }
      }

      return chunkIds;
    }

    public static bool IsGameRunning()
    {
      return Process.GetProcessesByName("DeadByDaylight-Win64-Shipping").Length > 0;
    }

    public static bool DeployModFiles(PakMods mod, string targetPaksFolder, string platform)
    {
      if (!Directory.Exists(targetPaksFolder)) return false;

      string suffix = RetrievePlatformSuffix(platform);
      string[] extensions = { ".pak", ".sig", ".ucas", ".utoc" };

      string sigTemplate = Path.Combine(targetPaksFolder, $"pakchunk0{suffix}.sig");

      if (!File.Exists(sigTemplate))
      {
        sigTemplate = Directory.GetFiles(targetPaksFolder, $"*{suffix}.sig").FirstOrDefault() ?? "";
      }

      try
      {
        if (Directory.Exists(mod.LocationPath))
        {
          var pakFiles = Directory.EnumerateFiles(mod.LocationPath, "*.pak", SearchOption.AllDirectories);

          foreach (var pakPath in pakFiles)
          {
            string modSubFolder = Path.GetDirectoryName(pakPath) ?? mod.LocationPath;
            string originalBase = Path.GetFileNameWithoutExtension(pakPath);

            var match = Regex.Match(originalBase, @"(pakchunk\d+)", RegexOptions.IgnoreCase);
            string chunkBase = match.Success ? match.Groups[1].Value : originalBase;
            string targetBase = $"{chunkBase}{suffix}";

            foreach (string ext in extensions)
            {
              string src = Path.Combine(modSubFolder, originalBase + ext);
              string dest = Path.Combine(targetPaksFolder, targetBase + ext);

              if (File.Exists(src))
              {
                File.Copy(src, dest, true);
              }
              else if (ext == ".sig" && !File.Exists(dest))
              {
                if (File.Exists(sigTemplate))
                  File.Copy(sigTemplate, dest, true);
                else
                  File.WriteAllBytes(dest, Array.Empty<byte>());
              }
            }
          }

          return true;
        }

        if (File.Exists(mod.LocationPath) &&
            mod.LocationPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
          using var archive = ZipFile.OpenRead(mod.LocationPath);

          var pakEntries = archive.Entries
            .Where(e => e.FullName.EndsWith(".pak", StringComparison.OrdinalIgnoreCase));

          foreach (var entry in pakEntries)
          {
            string entryFileName = Path.GetFileName(entry.FullName);
            string originalBase = Path.GetFileNameWithoutExtension(entryFileName);

            var match = Regex.Match(originalBase, @"(pakchunk\d+)", RegexOptions.IgnoreCase);
            string chunkBase = match.Success ? match.Groups[1].Value : originalBase;
            string targetBase = $"{chunkBase}{suffix}";

            foreach (string ext in extensions)
            {
              string lookupName = originalBase + ext;
              var fileEntry = archive.Entries.FirstOrDefault(e =>
                Path.GetFileName(e.FullName).Equals(lookupName, StringComparison.OrdinalIgnoreCase));

              string dest = Path.Combine(targetPaksFolder, targetBase + ext);

              if (fileEntry != null)
              {
                fileEntry.ExtractToFile(dest, true);
              }
              else if (ext == ".sig" && !File.Exists(dest))
              {
                if (File.Exists(sigTemplate))
                  File.Copy(sigTemplate, dest, true);
                else
                  File.WriteAllBytes(dest, Array.Empty<byte>());
              }
            }
          }

          return true;
        }
      }
      catch (Exception)
      {
        return false;
      }

      return false;
    }

    public static void RevokeModFiles(PakMods mod, string targetPaksFolder, string platform)
    {
      if (!Directory.Exists(targetPaksFolder)) return;

      string suffix = RetrievePlatformSuffix(platform);
      string[] extensions = { ".pak", ".sig", ".ucas", ".utoc" };

      try
      {
        if (Directory.Exists(mod.LocationPath))
        {
          var pakFiles = Directory.EnumerateFiles(mod.LocationPath, "*.pak", SearchOption.AllDirectories);

          foreach (var pakPath in pakFiles)
          {
            string originalBase = Path.GetFileNameWithoutExtension(pakPath);

            var match = Regex.Match(originalBase, @"(pakchunk\d+)", RegexOptions.IgnoreCase);
            string chunkBase = match.Success ? match.Groups[1].Value : originalBase;
            string targetBase = $"{chunkBase}{suffix}";

            foreach (string ext in extensions)
            {
              string targetFile = Path.Combine(targetPaksFolder, targetBase + ext);

              if (File.Exists(targetFile))
              {
                File.Delete(targetFile);
              }
            }
          }

          return;
        }

        if (File.Exists(mod.LocationPath) &&
            mod.LocationPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
          using var archive = ZipFile.OpenRead(mod.LocationPath);

          var pakEntries = archive.Entries
            .Where(e => e.FullName.EndsWith(".pak", StringComparison.OrdinalIgnoreCase));

          foreach (var entry in pakEntries)
          {
            string entryFileName = Path.GetFileName(entry.FullName);
            string originalBase = Path.GetFileNameWithoutExtension(entryFileName);

            var match = Regex.Match(originalBase, @"(pakchunk\d+)", RegexOptions.IgnoreCase);
            string chunkBase = match.Success ? match.Groups[1].Value : originalBase;
            string targetBase = $"{chunkBase}{suffix}";

            foreach (string ext in extensions)
            {
              string targetFile = Path.Combine(targetPaksFolder, targetBase + ext);

              if (File.Exists(targetFile))
              {
                File.Delete(targetFile);
              }
            }
          }
        }
      }
      catch { }
    }

    public static int PurgeAllMods(string targetPaksFolder)
    {
      if (!Directory.Exists(targetPaksFolder)) return 0;

      int deletedCount = 0;

      var filePaths = Directory.EnumerateFiles(targetPaksFolder, "*.*", SearchOption.TopDirectoryOnly)
        .Where(f =>
        {
          string ext = Path.GetExtension(f).ToLower();
          return ext == ".pak" || ext == ".sig" || ext == ".utoc" || ext == ".ucas";
        });

      foreach (var filePath in filePaths)
      {
        string filename = Path.GetFileNameWithoutExtension(filePath);
        var match = Regex.Match(filename, @"pakchunk(\d+)", RegexOptions.IgnoreCase);

        if (match.Success && int.TryParse(match.Groups[1].Value, out int chunkId))
        {
          if (chunkId > 6000)
          {
            try
            {
              File.Delete(filePath);
              deletedCount++;
            }
            catch { }
          }
        }
      }

      return deletedCount;
    }

  }
}
