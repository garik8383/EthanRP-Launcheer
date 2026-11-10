```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
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
    private const string ManifestUrl =
        "https://raw.githubusercontent.com/garik8383/EthanRP-Launcheer/main/manifest.json";

    private const string ServerAddress = "217.106.106.44:7777";

    private readonly HttpClient http = new()
    {
        Timeout = TimeSpan.FromMinutes(20)
    };

    private readonly Label status = new()
    {
        Dock = DockStyle.Top,
        Height = 55,
        ForeColor = Color.White,
        TextAlign = ContentAlignment.MiddleCenter
    };

    private readonly ProgressBar progress = new()
    {
        Dock = DockStyle.Top,
        Height = 25,
        Minimum = 0,
        Maximum = 100
    };

    private readonly Button play = new()
    {
        Text = "ПРОВЕРИТЬ ИГРУ",
        Dock = DockStyle.Top,
        Height = 55,
        BackColor = Color.Goldenrod,
        ForeColor = Color.Black,
        FlatStyle = FlatStyle.Flat
    };

    private readonly string root = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
        "EthanRP",
        "Game"
    );

    private bool busy = false;

    public LauncherForm()
    {
        Text = "EthanRP Launcher 2.0";

        Size = new Size(650, 330);
        MinimumSize = new Size(550, 280);

        BackColor = Color.FromArgb(20, 20, 20);
        StartPosition = FormStartPosition.CenterScreen;

        var heading = new Label
        {
            Text = "ETHAN RP",
            Dock = DockStyle.Top,
            Height = 90,
            ForeColor = Color.Gold,
            Font = new Font(
                "Segoe UI",
                28,
                FontStyle.Bold
            ),
            TextAlign = ContentAlignment.MiddleCenter
        };

        Controls.Add(play);
        Controls.Add(progress);
        Controls.Add(status);
        Controls.Add(heading);

        play.Click += async (_, _) => await InstallOrPlay();

        Shown += async (_, _) => await InstallOrPlay();
    }

    private async Task InstallOrPlay()
    {
        if (busy)
            return;

        busy = true;
        play.Enabled = false;

        try
        {
            Directory.CreateDirectory(root);

            status.Text = "Проверяем файлы EthanRP...";

            using var response = await http.GetAsync(ManifestUrl);

            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();

            var manifest = JsonSerializer.Deserialize<Manifest>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );

            if (manifest == null)
                throw new Exception("Пустой манифест.");

            if (manifest.Files == null)
                throw new Exception("Нет списка файлов.");

            var files = manifest.Files;

            int completed = 0;

            foreach (var file in files)
            {
                string relative = file.Path.Replace(
                    '/',
                    Path.DirectorySeparatorChar
                );

                string full = Path.GetFullPath(
                    Path.Combine(root, relative)
                );

                string rootFull = Path.GetFullPath(root);

                if (!full.StartsWith(
                    rootFull + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception(
                        "Недопустимый путь файла."
                    );
                }

                bool needsDownload = !File.Exists(full);

                if (!needsDownload)
                {
                    string currentHash = await HashFile(full);

                    needsDownload = !string.Equals(
                        currentHash,
                        file.Sha256,
                        StringComparison.OrdinalIgnoreCase
                    );
                }

                if (needsDownload)
                {
                    if (!Uri.TryCreate(
                        file.Url,
                        UriKind.Absolute,
                        out var uri))
                    {
                        throw new Exception(
                            "Неверный адрес загрузки."
                        );
                    }

                    if (uri.Scheme != Uri.UriSchemeHttps)
                    {
                        throw new Exception(
                            "Разрешены только HTTPS-ссылки."
                        );
                    }

                    string host = uri.Host.ToLowerInvariant();

                    if (host != "github.com" &&
                        host != "objects.githubusercontent.com" &&
                        host != "release-assets.githubusercontent.com" &&
                        host != "raw.githubusercontent.com")
                    {
                        throw new Exception(
                            "Недопустимый сервер загрузки."
                        );
                    }

                    Directory.CreateDirectory(
                        Path.GetDirectoryName(full)!
                    );

                    string temp = full + ".download";

                    status.Text = "Скачиваем: " + file.Path;

                    try
                    {
                        using var download = await http.GetAsync(
                            uri,
                            HttpCompletionOption.ResponseHeadersRead
                        );

                        download.EnsureSuccessStatusCode();

                        await using (var source =
                            await download.Content.ReadAsStreamAsync())
                        {
                            await using var dest = File.Create(temp);

                            await source.CopyToAsync(dest);
                        }

                        string downloadedHash = await HashFile(temp);

                        if (!string.Equals(
                            downloadedHash,
                            file.Sha256,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            throw new Exception(
                                "Ошибка SHA-256: " + file.Path
                            );
                        }

                        File.Move(temp, full, true);
                    }
                    finally
                    {
                        if (File.Exists(temp))
                            File.Delete(temp);
                    }
                }

                completed++;

                progress.Value = files.Count == 0
                    ? 100
                    : completed * 100 / files.Count;
            }

            progress.Value = 100;

            string samp = Path.Combine(root, "samp.exe");
            string gta = Path.Combine(root, "gta_sa.exe");

            if (!File.Exists(samp) || !File.Exists(gta))
            {
                status.Text =
                    "GTA San Andreas или SA-MP не найдены. " +
                    "Установите совместимую игру.";

                play.Text = "ПРОВЕРИТЬ СНОВА";
                return;
            }

            status.Text = "Готово! Запускаем EthanRP...";

            play.Text = "ИГРАТЬ";

            Process.Start(new ProcessStartInfo
            {
                FileName = samp,
                Arguments = ServerAddress,
                WorkingDirectory = root,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            status.Text = "Ошибка: " + ex.Message;
            play.Text = "ПОВТОРИТЬ";
        }
        finally
        {
            busy = false;
            play.Enabled = true;
        }
    }

    private static async Task<string> HashFile(string path)
    {
        await using var stream = File.OpenRead(path);

        byte[] hash = await SHA256.HashDataAsync(stream);

        return Convert.ToHexString(hash);
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
```
