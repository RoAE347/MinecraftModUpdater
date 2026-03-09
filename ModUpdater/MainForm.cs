using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModUpdater
{
    public partial class MainForm : Form
    {
        private static readonly HttpClient client = new HttpClient();
        private List<string> riskyMods = new List<string>();

        public MainForm()
        {
            InitializeComponent();
            client.DefaultRequestHeaders.Add("User-Agent", "ModUpdater/4.0");
            comboBoxLoader.SelectedIndex = 0;
        }

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                textBoxModsPath.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private async void buttonStart_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxModsPath.Text) || !Directory.Exists(textBoxModsPath.Text))
            {
                MessageBox.Show("Please select a valid mods folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (string.IsNullOrWhiteSpace(textBoxVersion.Text))
            {
                MessageBox.Show("Please enter a Minecraft version.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            buttonStart.Enabled = false;
            listBoxProgress.Items.Clear();
            progressBar.Value = 0;
            riskyMods.Clear();

            await Task.Run(() => RunUpdateProcess());

            buttonStart.Enabled = true;
        }

        private async Task RunUpdateProcess()
        {
            string sourcePath = textBoxModsPath.Text;
            string targetVersion = textBoxVersion.Text;
            string? loader = comboBoxLoader.SelectedItem?.ToString();

            if (loader == null)
            {
                MessageBox.Show("Please select a mod loader.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string newFolder = Path.Combine(desktop, $"Mods_{targetVersion}_{loader}");
            Directory.CreateDirectory(newFolder);

            UpdateProgress("Saving to: " + newFolder);

            string[] files = Directory.GetFiles(sourcePath, "*.jar");
            List<string> failedMods = new List<string>();
            
            progressBar.Maximum = files.Length;

            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                UpdateProgress($"{fileName} -> ");

                string? projectId = null;
                string modNameForSearch = CleanModName(fileName);
                string? internalId = GetModIdFromJar(file);

                if (!string.IsNullOrEmpty(internalId))
                    modNameForSearch = internalId;

                try
                {
                    string hash = CalculateSha1(file);
                    string hashUrl = $"https://api.modrinth.com/v2/version_file/{hash}?algorithm=sha1";
                    var response = await client.GetAsync(hashUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
                        projectId = json?["project_id"]?.ToString();
                    }
                }
                catch { }

                if (projectId == null)
                {
                    string searchUrl = $"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(modNameForSearch)}&limit=5";
                    try
                    {
                        var sMsg = await client.GetStringAsync(searchUrl);
                        var sNode = JsonNode.Parse(sMsg);
                        var hits = sNode?["hits"]?.AsArray();

                        if (hits != null && hits.Count > 0)
                        {
                            JsonNode? bestHit = hits.FirstOrDefault(h => h?["slug"]?.ToString().Equals(modNameForSearch, StringComparison.OrdinalIgnoreCase) == true);
                            if (bestHit == null)
                            {
                                bestHit = hits.FirstOrDefault(h =>
                                    h?["categories"]?.AsArray().Any(c => c?.ToString().Equals(loader, StringComparison.OrdinalIgnoreCase) == true) == true
                                );
                            }
                            if (bestHit == null)
                            {
                                bestHit = hits[0];
                            }
                            if (bestHit != null)
                            {
                                projectId = bestHit["project_id"]?.ToString();
                            }
                        }
                    }
                    catch { }
                }

                bool success = false;
                if (projectId != null)
                {
                    success = await DownloadModrinth(projectId, targetVersion, loader, newFolder, modNameForSearch);
                }
                if (!success)
                {
                    success = await DownloadGitHub(modNameForSearch, targetVersion, newFolder);
                }

                if (!success)
                {
                    failedMods.Add(modNameForSearch);
                    UpdateProgress("[Not found]");
                }
                
                IncrementProgress();
            }
            
            ShowSummary(failedMods, targetVersion);
        }

        private void UpdateProgress(string message)
        {
            if (listBoxProgress.InvokeRequired)
            {
                listBoxProgress.Invoke(new Action<string>(UpdateProgress), message);
            }
            else
            {
                listBoxProgress.Items.Add(message);
                listBoxProgress.TopIndex = listBoxProgress.Items.Count - 1;
            }
        }
        
        private void IncrementProgress()
        {
            if (progressBar.InvokeRequired)
            {
                progressBar.Invoke(new Action(IncrementProgress));
            }
            else
            {
                progressBar.Value++;
            }
        }

        private void ShowSummary(List<string> failedMods, string targetVersion)
        {
            if (riskyMods.Count > 0)
            {
                string message = $"Game version: {targetVersion}. Versions mismatched for these mods:\n\n";
                message += string.Join("\n", riskyMods.Select(r => "• " + r));
                message += "\n\nCheck these manually!";
                MessageBox.Show(message, "Warning! Critical Alert", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (failedMods.Count > 0)
            {
                string message = $"Could not find {failedMods.Count} mods on Modrinth or GitHub.\n\nOpen search on CurseForge in browser?";
                if (MessageBox.Show(message, "Failed Mods", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    foreach (var mod in failedMods)
                    {
                        string url = $"https://www.curseforge.com/minecraft/search?class=mc-mods&search={mod}";
                        OpenUrl(url);
                    }
                }
            }
        }

        async Task<bool> DownloadModrinth(string projectId, string version, string loader, string outputDir, string modName)
        {
            try
            {
                string vUrl = $"https://api.modrinth.com/v2/project/{projectId}/version?loaders=[\"{loader}\"]";
                var vJson = await client.GetStringAsync(vUrl);
                var versions = JsonNode.Parse(vJson)?.AsArray();

                if (versions == null || versions.Count == 0) return false;

                var bestMatch = versions.FirstOrDefault(v =>
                    v?["game_versions"]?.AsArray().Any(gv => gv?.ToString() == version) == true
                );

                bool isRisky = false;
                string downloadedVersion = "";

                if (bestMatch == null)
                {
                    bestMatch = versions[0];
                    isRisky = true;
                    if (bestMatch != null)
                    {
                        downloadedVersion = bestMatch["game_versions"]?[0]?.ToString() ?? "";
                        UpdateProgress($" [Modrinth: MISSING {version} -> {downloadedVersion}]");
                    }
                }
                else
                {
                    UpdateProgress(" [Modrinth: OK]");
                }

                if (bestMatch?["files"]?[0]?["url"]?.ToString() is string dlUrl && bestMatch["files"]?[0]?["filename"]?.ToString() is string fName)
                {
                    byte[] data = await client.GetByteArrayAsync(dlUrl);
                    await File.WriteAllBytesAsync(Path.Combine(outputDir, fName), data);

                    if (isRisky) riskyMods.Add($"{modName} (Modrinth: {downloadedVersion})");

                    return true;
                }

                return false;
            }
            catch { return false; }
        }

        async Task<bool> DownloadGitHub(string modName, string version, string outputDir)
        {
            try
            {
                string query = $"{modName} minecraft mod";
                string searchUrl = $"https://api.github.com/search/repositories?q={Uri.EscapeDataString(query)}&sort=stars&order=desc";

                var searchJson = JsonNode.Parse(await client.GetStringAsync(searchUrl));
                var items = searchJson?["items"]?.AsArray();

                if (items == null || items.Count == 0) return false;

                string? repoOwner = items[0]?["owner"]?["login"]?.ToString();
                string? repoName = items[0]?["name"]?.ToString();

                if (repoOwner == null || repoName == null) return false;

                string releasesUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";
                var releaseJson = JsonNode.Parse(await client.GetStringAsync(releasesUrl));

                var assets = releaseJson?["assets"]?.AsArray();
                if (assets == null) return false;

                foreach (var asset in assets)
                {
                    string? fName = asset?["name"]?.ToString();

                    if (fName != null && fName.EndsWith(".jar") && !fName.Contains("-sources") && !fName.Contains("-api"))
                    {
                        string? dlUrl = asset?["browser_download_url"]?.ToString();
                        
                        if (dlUrl != null)
                        {
                            UpdateProgress(" [GitHub: Found]");

                            byte[] data = await client.GetByteArrayAsync(dlUrl);
                            await File.WriteAllBytesAsync(Path.Combine(outputDir, fName), data);
                            
                            riskyMods.Add($"{modName} (Downloaded from GitHub - Check version manually!)");
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        static string CalculateSha1(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        static string? GetModIdFromJar(string path)
        {
            try
            {
                using (ZipArchive zip = ZipFile.OpenRead(path))
                {
                    var fEntry = zip.GetEntry("fabric.mod.json");
                    if (fEntry != null)
                    {
                        using (var r = new StreamReader(fEntry.Open()))
                            return JsonNode.Parse(r.ReadToEnd())?["id"]?.ToString();
                    }
                    var forgeEntry = zip.GetEntry("META-INF/mods.toml");
                    if (forgeEntry != null)
                    {
                        using (var r = new StreamReader(forgeEntry.Open()))
                        {
                            var m = Regex.Match(r.ReadToEnd(), "modId\\s*=\\s*\"([^\"]+)\"");
                            if (m.Success) return m.Groups[1].Value;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        static string CleanModName(string filename)
        {
            string name = Path.GetFileNameWithoutExtension(filename);
            name = name.Replace("_", " ").Replace("-", " ");
            var match = Regex.Match(name, "^([a-zA-Z\\s]+)");
            if (match.Success) return match.Groups[1].Value.Trim();
            return name;
        }

        static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); } catch { }
        }
    }
}
