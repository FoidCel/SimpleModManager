using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

#nullable enable
namespace SimpleModManager
{

  public class SyncResponse
  {
    public bool success { get; set; }
    public string? error { get; set; }
    public List<SyncMod>? mods { get; set; }
  }

  public class SyncMod
  {
    public int id { get; set; }
    public string name { get; set; } = "";
    public string download_url { get; set; } = "";
  }

  public class AppConfig
  {
    public string SelectedPlatform { get; set; } = "";
    public string DbdPaksPath { get; set; } = "";
    public string LicenseKey { get; set; } = "";
    public double WindowOpacity { get; set; } = 0.84;
    public List<string> EnabledMods { get; set; } = new();
  }

  public partial class MainWindow : Window
  {

    private ObservableCollection<PakMods> _modsList = new();
    private AppConfig _config = new();
    private DispatcherTimer _gameCheckTimer = new();
    private static readonly HttpClient _httpClient = new();

    private readonly string _configFilePath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "Isolumia", "ModManager", "modmanager.json");

    private string _tempSelectedPlatform = "";

    public MainWindow()
    {
      InitializeComponent();
      ModsItemsControl.ItemsSource = _modsList;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      AutoPathFinder.GetOrCreateLocalModsDirectory();

      LoadConfiguration();

      if (!string.IsNullOrEmpty(_config.LicenseKey))
      {
        LicenseKeyTextBox.Text = _config.LicenseKey;
      }

      double sliderVal = (_config.WindowOpacity - 0.60) / 0.40 * 100.0;
      if (sliderVal < 0) sliderVal = 0;
      if (sliderVal > 100) sliderVal = 100;

      OpacitySlider.Value = sliderVal;
      ApplyWindowOpacity(_config.WindowOpacity);

      if (string.IsNullOrEmpty(_config.SelectedPlatform) || string.IsNullOrEmpty(_config.DbdPaksPath))
      {
        ShowSetupOverlay();
      }
      else
      {
        ActivePlatformLabel.Text = _config.SelectedPlatform.ToUpper();
        RefreshAvailableMods();
      }

      StartGameRunningCheck();
    }

    private void StartGameRunningCheck()
    {
      _gameCheckTimer.Interval = TimeSpan.FromSeconds(5);

      _gameCheckTimer.Tick += (s, e) =>
      {
        bool isRunning = AutoPathFinder.IsGameRunning();
        GameRunningBanner.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;
        CloseGameBtn.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;
      };

      bool initialCheck = AutoPathFinder.IsGameRunning();
      GameRunningBanner.Visibility = initialCheck ? Visibility.Visible : Visibility.Collapsed;
      CloseGameBtn.Visibility = initialCheck ? Visibility.Visible : Visibility.Collapsed;

      _gameCheckTimer.Start();
    }

    private void LoadConfiguration()
    {
      if (!File.Exists(_configFilePath)) return;

      try
      {
        string json = File.ReadAllText(_configFilePath);
        var loaded = JsonSerializer.Deserialize<AppConfig>(json);

        if (loaded != null)
        {
          _config = loaded;
        }
      }
      catch { }
    }

    private void SaveConfiguration()
    {
      try
      {
        Directory.CreateDirectory(Path.GetDirectoryName(_configFilePath)!);
        string json = JsonSerializer.Serialize(_config);
        File.WriteAllText(_configFilePath, json);
      }
      catch { }
    }

    private void ApplyWindowOpacity(double opacityValue)
    {
      if (opacityValue < 0.1) opacityValue = 0.1;
      if (opacityValue > 1.0) opacityValue = 1.0;

      byte alpha = (byte)(opacityValue * 255);
      var baseColor = Color.FromArgb(alpha, 15, 23, 23);

      Resources["WindowBg"] = new SolidColorBrush(baseColor);
    }

    private void ShowSetupOverlay()
    {
      OverlayPanel.Visibility = Visibility.Visible;
      ConfirmPlatformBtn.IsEnabled = false;
      _tempSelectedPlatform = "";
      DbdPathTextBox.Text = "";
      DetectionStatusLabel.Text = "Please select a platform...";
      DetectionStatusLabel.Foreground = Brushes.Gray;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      if (e.LeftButton == MouseButtonState.Pressed)
      {
        DragMove();
      }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
      WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
      Application.Current.Shutdown();
    }

    private void NavBtn_Checked(object sender, RoutedEventArgs e)
    {
      if (ModsPanel == null || CleanerPanel == null || SettingsPanel == null) return;

      ModsPanel.Visibility = Visibility.Collapsed;
      CleanerPanel.Visibility = Visibility.Collapsed;
      SettingsPanel.Visibility = Visibility.Collapsed;
      ContactPanel.Visibility = Visibility.Collapsed;
      AboutPanel.Visibility = Visibility.Collapsed;
      FindModsPanel.Visibility = Visibility.Collapsed;
      SyncModsPanel.Visibility = Visibility.Collapsed;

      if (sender == ModsNavBtn)
        ModsPanel.Visibility = Visibility.Visible;
      else if (sender == FindModsNavBtn)
        FindModsPanel.Visibility = Visibility.Visible;
      else if (sender == SyncModsNavBtn)
        SyncModsPanel.Visibility = Visibility.Visible;
      else if (sender == CleanerNavBtn)
        CleanerPanel.Visibility = Visibility.Visible;
      else if (sender == ContactNavBtn)
        ContactPanel.Visibility = Visibility.Visible;
      else if (sender == AboutNavBtn)
        AboutPanel.Visibility = Visibility.Visible;
      else if (sender == SettingsNavBtn)
        SettingsPanel.Visibility = Visibility.Visible;
    }

    private void PlatformSelect_Click(object sender, RoutedEventArgs e)
    {
      if (sender == SteamSelectBtn)
        _tempSelectedPlatform = "Steam";
      else if (sender == EpicSelectBtn)
        _tempSelectedPlatform = "Epic";
      else if (sender == MicrosoftSelectBtn)
        _tempSelectedPlatform = "Microsoft Store";

      string autoPath = AutoPathFinder.DetectDbdPath(_tempSelectedPlatform);
      DbdPathTextBox.Text = autoPath;

      if (!string.IsNullOrEmpty(autoPath))
      {
        DetectionStatusLabel.Text = $"Path detected for {_tempSelectedPlatform}.";
        DetectionStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(30, 215, 96));
        ConfirmPlatformBtn.IsEnabled = true;
      }
      else
      {
        DetectionStatusLabel.Text = $"Could not detect {_tempSelectedPlatform} installation. Browse manually.";
        DetectionStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(230, 57, 70));
        ConfirmPlatformBtn.IsEnabled = false;
      }
    }

    private void BrowseDbdPath_Click(object sender, RoutedEventArgs e)
    {
      if (string.IsNullOrEmpty(_tempSelectedPlatform))
      {
        MessageBox.Show("Please choose a platform first.", "Platform Required", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      var dialog = new Microsoft.Win32.OpenFolderDialog
      {
        Title = "Select Dead by Daylight Pak Folder"
      };

      if (dialog.ShowDialog() == true)
      {
        DbdPathTextBox.Text = dialog.FolderName;
        DetectionStatusLabel.Text = "Custom path set. Ready to continue.";
        DetectionStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(30, 215, 96));
        ConfirmPlatformBtn.IsEnabled = true;
      }
    }

    private void ConfirmPlatform_Click(object sender, RoutedEventArgs e)
    {
      if (string.IsNullOrEmpty(_tempSelectedPlatform) || string.IsNullOrEmpty(DbdPathTextBox.Text)) return;

      _config.SelectedPlatform = _tempSelectedPlatform;
      _config.DbdPaksPath = DbdPathTextBox.Text;
      SaveConfiguration();

      ActivePlatformLabel.Text = _config.SelectedPlatform.ToUpper();
      OverlayPanel.Visibility = Visibility.Collapsed;

      RefreshAvailableMods();
    }

    private void ChangePlatform_Click(object sender, RoutedEventArgs e)
    {
      ShowSetupOverlay();
      _tempSelectedPlatform = _config.SelectedPlatform;
      DbdPathTextBox.Text = _config.DbdPaksPath;

      if (!string.IsNullOrEmpty(_config.DbdPaksPath))
      {
        DetectionStatusLabel.Text = "Current configured path loaded.";
        ConfirmPlatformBtn.IsEnabled = true;
      }
    }

    private void RefreshMods_Click(object sender, RoutedEventArgs e)
    {
      RefreshAvailableMods();
    }

    private void RefreshAvailableMods()
    {
      var detected = AutoPathFinder.ScanForAvailableMods();
      _modsList.Clear();

      foreach (var mod in detected)
      {
        if (_config.EnabledMods.Contains(mod.PakName))
        {
          mod.IsActive = true;
        }

        _modsList.Add(mod);
      }

      UpdateConflictStates();
    }

    private void UpdateConflictStates()
    {
      var activeMods = _modsList.Where(m => m.IsActive).ToList();
      var chunkUsage = new Dictionary<int, List<PakMods>>();

      foreach (var mod in activeMods)
      {
        var chunkIds = AutoPathFinder.GetModChunkIds(mod);

        foreach (var id in chunkIds)
        {
          if (!chunkUsage.ContainsKey(id))
            chunkUsage[id] = new List<PakMods>();

          chunkUsage[id].Add(mod);
        }
      }

      foreach (var mod in _modsList)
        mod.HasConflict = false;

      foreach (var kvp in chunkUsage.Where(k => k.Value.Count > 1))
      {
        foreach (var mod in kvp.Value)
          mod.HasConflict = true;
      }
    }

    private void ToggleModStatus_Click(object sender, RoutedEventArgs e)
    {
      if (sender is not Button btn || btn.DataContext is not PakMods mod) return;

      bool targetState = !mod.IsActive;

      if (targetState)
      {
        bool success = AutoPathFinder.DeployModFiles(mod, _config.DbdPaksPath, _config.SelectedPlatform);

        if (success)
        {
          mod.IsActive = true;

          if (!_config.EnabledMods.Contains(mod.PakName))
          {
            _config.EnabledMods.Add(mod.PakName);
          }

          SaveConfiguration();
          UpdateConflictStates();
        }
        else
        {
          MessageBox.Show(
            "Failed to apply mod. Make sure the game is not running and your paths are correct.",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        }
      }
      else
      {
        AutoPathFinder.RevokeModFiles(mod, _config.DbdPaksPath, _config.SelectedPlatform);

        mod.IsActive = false;
        _config.EnabledMods.Remove(mod.PakName);
        SaveConfiguration();
        UpdateConflictStates();
      }
    }

    private void TurnOffAll_Click(object sender, RoutedEventArgs e)
    {
      var activeMods = _modsList.Where(m => m.IsActive).ToList();

      foreach (var mod in activeMods)
      {
        AutoPathFinder.RevokeModFiles(mod, _config.DbdPaksPath, _config.SelectedPlatform);
        mod.IsActive = false;
      }

      _config.EnabledMods.Clear();
      SaveConfiguration();
      UpdateConflictStates();
    }

    private void PurgeMods_Click(object sender, RoutedEventArgs e)
    {
      var result = MessageBox.Show(
        "This will remove all active mods from your game's pak folder and revert Dead by Daylight to vanilla.\n\nAre you sure you want to continue?",
        "Confirm",
        MessageBoxButton.YesNo,
        MessageBoxImage.Warning);

      if (result != MessageBoxResult.Yes) return;

      int count = AutoPathFinder.PurgeAllMods(_config.DbdPaksPath);

      foreach (var mod in _modsList)
      {
        mod.IsActive = false;
      }

      _config.EnabledMods.Clear();
      SaveConfiguration();

      MessageBox.Show(
        $"Done. {count} mod file(s) removed.",
        "Complete",
        MessageBoxButton.OK,
        MessageBoxImage.Information);
    }

    private void OpenModsFolder_Click(object sender, RoutedEventArgs e)
    {
      string modsDir = AutoPathFinder.GetOrCreateLocalModsDirectory();

      try
      {
        System.Diagnostics.Process.Start("explorer.exe", modsDir);
      }
      catch (Exception ex)
      {
        MessageBox.Show($"Failed to open folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
      if (OpacityValueText == null) return;

      int percentage = (int)e.NewValue;
      OpacityValueText.Text = $"{percentage}%";

      double actualOpacity = 0.60 + (e.NewValue / 100.0) * 0.40;
      ApplyWindowOpacity(actualOpacity);

      _config.WindowOpacity = actualOpacity;
      SaveConfiguration();
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
      if (e.Data.GetDataPresent(DataFormats.FileDrop))
      {
        e.Effects = DragDropEffects.Copy;
        DragOverlay.Visibility = Visibility.Visible;
      }
      else
      {
        e.Effects = DragDropEffects.None;
      }

      e.Handled = true;
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
      DragOverlay.Visibility = Visibility.Collapsed;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
      DragOverlay.Visibility = Visibility.Collapsed;

      if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

      string[] dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
      string modsDir = AutoPathFinder.GetOrCreateLocalModsDirectory();
      bool anyAdded = false;

      foreach (string path in dropped)
      {
        try
        {
          if (Directory.Exists(path))
          {
            string destDir = Path.Combine(modsDir, Path.GetFileName(path));
            CopyDirectory(path, destDir);
            anyAdded = true;
          }
          else if (File.Exists(path) && (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                                         path.EndsWith(".rar", StringComparison.OrdinalIgnoreCase) ||
                                         path.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)))
          {
            string destFile = Path.Combine(modsDir, Path.GetFileName(path));
            File.Copy(path, destFile, true);
            anyAdded = true;
          }
        }
        catch { }
      }

      if (anyAdded)
      {
        RefreshAvailableMods();
      }
    }

    private static void CopyDirectory(string source, string dest)
    {
      Directory.CreateDirectory(dest);

      foreach (var file in Directory.GetFiles(source))
        File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);

      foreach (var dir in Directory.GetDirectories(source))
        CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    private void OpenDiscord_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
          FileName = "https://discord.gg/isolumia",
          UseShellExecute = true
        });
      }
      catch { }
    }

    private void OpenModHub_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
          FileName = "https://isolumia.com/panel/modhub",
          UseShellExecute = true
        });
      }
      catch { }
    }

    private async void SyncMods_Click(object sender, RoutedEventArgs e)
    {
      string key = LicenseKeyTextBox.Text.Trim();

      if (string.IsNullOrEmpty(key))
      {
        SyncStatusLabel.Text = "Please enter a license key.";
        SyncStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(230, 57, 70));
        return;
      }

      _config.LicenseKey = key;
      SaveConfiguration();

      SyncStatusLabel.Text = "Syncing with Isolumia Mod Hub...";
      SyncStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(30, 215, 96));
      SyncModsBtn.IsEnabled = false;

      try
      {
        string url = $"https://isolumia.com/api/mods/manager/sync?key={Uri.EscapeDataString(key)}";
        using var response = await _httpClient.GetAsync(url);
        string json = await response.Content.ReadAsStringAsync();

        var result = JsonSerializer.Deserialize<SyncResponse>(json, new JsonSerializerOptions
        {
          PropertyNameCaseInsensitive = true
        });

        if (result == null)
        {
          SyncStatusLabel.Text = "Error: Invalid response from server.";
          SyncStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(230, 57, 70));
          SyncModsBtn.IsEnabled = true;
          return;
        }

        if (!result.success)
        {
          SyncStatusLabel.Text = result.error ?? "Invalid license key.";
          SyncStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(230, 57, 70));
          SyncModsBtn.IsEnabled = true;
          return;
        }

        if (result.mods == null || result.mods.Count == 0)
        {
          SyncStatusLabel.Text = "No mods selected on the Mod Hub.";
          SyncStatusLabel.Foreground = Brushes.Gray;
          SyncModsBtn.IsEnabled = true;
          return;
        }

        string modsDir = AutoPathFinder.GetOrCreateLocalModsDirectory();
        int downloadCount = 0;

        foreach (var mod in result.mods)
        {
          if (string.IsNullOrEmpty(mod.download_url)) continue;

          SyncStatusLabel.Text = $"Downloading {mod.name}...";

          try
          {
            using var fileResponse = await _httpClient.GetAsync(mod.download_url, HttpCompletionOption.ResponseHeadersRead);
            fileResponse.EnsureSuccessStatusCode();

            string fileName = $"{mod.name}.zip";
            if (fileResponse.Content.Headers.ContentDisposition != null)
            {
                if (!string.IsNullOrEmpty(fileResponse.Content.Headers.ContentDisposition.FileNameStar))
                {
                    fileName = fileResponse.Content.Headers.ContentDisposition.FileNameStar;
                }
                else if (!string.IsNullOrEmpty(fileResponse.Content.Headers.ContentDisposition.FileName))
                {
                    fileName = fileResponse.Content.Headers.ContentDisposition.FileName.Trim('"');
                }
            }

            string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            foreach (char c in invalidChars)
            {
              fileName = fileName.Replace(c.ToString(), "");
            }

            string targetPath = Path.Combine(modsDir, fileName);
            using var fileStream = await fileResponse.Content.ReadAsStreamAsync();
            using var targetStream = File.Create(targetPath);
            await fileStream.CopyToAsync(targetStream);

            downloadCount++;
          }
          catch { }
        }

        SyncStatusLabel.Text = $"Successfully synced {downloadCount} mod(s)!";
        SyncStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(30, 215, 96));

        RefreshAvailableMods();
      }
      catch
      {
        SyncStatusLabel.Text = "Error connecting to Isolumia servers.";
        SyncStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(230, 57, 70));
      }
      finally
      {
        SyncModsBtn.IsEnabled = true;
      }
    }

    private void FixPakbypass_Click(object sender, RoutedEventArgs e)
    {
      string logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeadByDaylight", "Saved", "Logs");

      if (Directory.Exists(logsDir))
      {
        try
        {
          foreach (string file in Directory.GetFiles(logsDir))
          {
            try
            {
              File.Delete(file);
            }
            catch { }
          }

          foreach (string dir in Directory.GetDirectories(logsDir))
          {
            try
            {
              Directory.Delete(dir, true);
            }
            catch { }
          }
        }
        catch { }
      }

      MessageBox.Show("fixed pakbypass if you still have issues open a ticket", "Fix Pakbypass", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CloseGame_Click(object sender, RoutedEventArgs e)
    {
      var result = MessageBox.Show("u sure", "Close Game", MessageBoxButton.YesNo, MessageBoxImage.Question);
      if (result == MessageBoxResult.Yes)
      {
        try
        {
          foreach (var process in System.Diagnostics.Process.GetProcessesByName("DeadByDaylight-Win64-Shipping"))
          {
            try
            {
              process.Kill();
            }
            catch { }
          }
        }
        catch { }
      }
    }

  }
}
