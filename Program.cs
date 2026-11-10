using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Forms;

namespace EthanRP.Launcher;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new LauncherForm());
    }
}

public sealed class LauncherForm : Form
{
    private const string ManifestUrl = "https://raw.githubusercontent.com/garik8383/EthanRP-Launcheer/main/manifest.json";
    private const string ServerAddress = "217.106.106.44:7777";
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(20) };
    private readonly Label status = new() { Dock = DockStyle.Top, Height = 48, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter };
    private readonly ProgressBar progress = new() { Dock = DockStyle.Top, Height = 25 };
    private readonly Button play = new() { Text = "ПРОВЕРИТЬ ИГРУ", Dock = DockStyle.Top, Height = 55, BackColor = Color.Goldenrod, FlatStyle = FlatStyle.Flat };
    private readonly string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EthanRP", "Game");
    private bool busy;

    public LauncherForm()
    {
        Text = "EthanRP Launcher 2.0";
        Size = new Size(650, 330);
        MinimumSize = new Size(550, 280);
        BackColor = Color.FromArgb(20, 20, 20);
        StartPosition = FormStartPosition.CenterScreen;
        var heading = new Label { Text = "ETHAN RP", Dock = DockStyle.Top, Height = 90, ForeColor = Color.Gold, Font = new Font("Segoe UI", 28, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
        Controls.Add(play);
        Controls.Add(progress);
        Controls.Add(status);
        Controls.Add(heading);
        play.Click += async (_, _) => await InstallOrPlay();
        Shown += async (_, _) => await InstallOrPlay();
    }

    private async Task InstallOrPlay()
    {
        if (busy) return;
        busy = true;
        play.Enabled = false;
        try
        {
            Directory.CreateDirectory(root);
            status.Text = "Проверяем файлы EthanRP...";
            using var response = await http.GetAsync(ManifestUrl);
            response.EnsureSuccessStatusCode();
            var manifest = await response.Content.ReadFromJsonAsync<Manifest>() ?? throw new Exception("Пустой манифест");
            if (manifest.Files is null) throw new Exception("Нет списка файлов");
            var files = manifest.Files;
            int completed = 0;
            foreach (var file in files)
            {
                var relative = file.Path.Replace('/', Path.DirectorySeparatorChar);
                var full = Path.GetFullPath(Path.Combine(root, relative));
                if (!full.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Недопустимый путь файла");
                if (!File.Exists(full) || !string.Equals(await HashFile(full), file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    if (!Uri.TryCreate(file.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.Host is not ("github.com" or "objects.githubusercontent.com" or "release-assets.githubusercontent.com" or "raw.githubusercontent.com"))
                        throw new Exception("Недопустимый адрес загрузки");
                    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                    var temp = full + ".download";
                    status.Text = $"Скачиваем {file.Path}";
                    try
                    {
                        using var download = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
                        download.EnsureSuccessStatusCode();
                        await using (var source = await download.Content.ReadAsStreamAsync())
                        await using (var dest = File.Create(temp)) await source.CopyToAsync(dest);
                        if (!string.Equals(await HashFile(temp), file.Sha256, StringComparison.OrdinalIgnoreCase))
                            throw new Exception($"Ошибка проверки SHA-256: {file.Path}");
                        File.Move(temp, full, true);
                    }
                    finally { if (File.Exists(temp)) File.Delete(temp); }
                }
                completed++;
                progress.Value = files.Count == 0 ? 100 : completed * 100 / files.Count;
            }
            var samp = Path.Combine(root, "samp.exe");
            var gta = Path.Combine(root, "gta_sa.exe");
            if (!File.Exists(samp) || !File.Exists(gta))
            {
                status.Text = "Нужна законно установленная GTA San Andreas и совместимый SA-MP. Файлы EthanRP проверены.";
                play.Text = "ПРОВЕРИТЬ СНОВА";
                return;
            }
            status.Text = "Готово! Подключаемся к серверу...";
            Process.Start(new ProcessStartInfo { FileName = samp, Arguments = ServerAddress, WorkingDirectory = root, UseShellExecute = true });
            play.Text = "ИГРАТЬ";
        }
        catch (Exception ex)
        {
            status.Text = "Ошибка: " + ex.Message;
            play.Text = "ПОВТОРИТЬ";
        }
        finally { busy = false; play.Enabled = true; }
    }

    private static async Task<string> HashFile(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }
}

public sealed class Manifest
{
    public List<GameFile>? Files { get; set; }
}
public sealed class GameFile
{
    public string Path { get; set; } = "";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
}
