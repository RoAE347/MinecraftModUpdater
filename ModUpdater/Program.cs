using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private static List<string> riskyMods = new List<string>();

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.Unicode;
        Console.InputEncoding = Encoding.Unicode;

        client.DefaultRequestHeaders.Add("User-Agent", "ModUpdater/4.0");

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==========================================");
        Console.WriteLine("      MINECRAFT MOD UPDATER v1.1");
        Console.WriteLine("==========================================");
        Console.ResetColor();

        string sourcePath = "";
        while (true)
        {
            Console.Write("\nEnter path to mods folder: ");
            string input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input)) continue;

            sourcePath = input.Trim().Trim('"');
            if (Directory.Exists(sourcePath)) break;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Folder not found: {sourcePath}");
            Console.ResetColor();
        }

        Console.Write("Enter Minecraft version (e.g. 1.21.1): ");
        string targetVersion = Console.ReadLine().Trim();

        Console.Write("Enter mod loader (fabric or forge): ");
        string loader = Console.ReadLine().Trim().ToLower();

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string newFolder = Path.Combine(desktop, $"Mods_{targetVersion}_{loader}");
        Directory.CreateDirectory(newFolder);

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\nSaving to: {newFolder}\n");
        Console.ResetColor();

        string[] files = Directory.GetFiles(sourcePath, "*.jar");
        List<string> failedMods = new List<string>();
        riskyMods.Clear();

        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);
            Console.Write($"{fileName} -> ");

            string projectId = null;
            string modNameForSearch = CleanModName(fileName);
            string internalId = GetModIdFromJar(file);

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
                    projectId = json["project_id"]?.ToString();
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
                    var hits = sNode["hits"].AsArray();

                    if (hits.Count > 0)
                    {
                        // 1. Exact slug match (best)
                        JsonNode bestHit = hits.FirstOrDefault(h => h["slug"]?.ToString().Equals(modNameForSearch, StringComparison.OrdinalIgnoreCase) == true);

                        // 2. First hit that explicitly lists support for the right loader in its categories
                        if (bestHit == null)
                        {
                            bestHit = hits.FirstOrDefault(h =>
                                h["categories"]?.AsArray().Any(c => c.ToString().Equals(loader, StringComparison.OrdinalIgnoreCase)) == true
                            );
                        }

                        // 3. Fallback to the most relevant (first) result
                        if (bestHit == null)
                        {
                            bestHit = hits[0];
                        }

                        projectId = bestHit["project_id"].ToString();
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
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[Not found]");
                Console.ResetColor();
                failedMods.Add(modNameForSearch);
            }
        }

        if (riskyMods.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            Console.WriteLine("                     WARNING! CRITICAL ALERT                      ");
            Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            Console.WriteLine($"Game version: {targetVersion}. Versions mismatched for these mods:");
            Console.WriteLine("------------------------------------------------------------------");

            foreach (var risk in riskyMods)
            {
                Console.WriteLine($" • {risk}");
            }

            Console.WriteLine("------------------------------------------------------------------");
            Console.WriteLine("Check these manually!");
            Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            Console.ResetColor();
        }

        if (failedMods.Count > 0)
        {
            Console.WriteLine("\n------------------------------------------------");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"Could not find {failedMods.Count} mods on Modrinth or GitHub.");
            Console.WriteLine("Open search on CurseForge in browser? (y/n)");
            Console.ResetColor();

            var key = Console.ReadKey();
            if (key.Key == ConsoleKey.Y)
            {
                Console.WriteLine("\nOpening browser...");
                foreach (var mod in failedMods)
                {
                    string url = $"https://www.curseforge.com/minecraft/search?class=mc-mods&search={mod}";
                    OpenUrl(url);
                    await Task.Delay(300);
                }
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n\nDone! Press Enter to exit.");
        Console.ReadLine();
    }

    static async Task<bool> DownloadModrinth(string projectId, string version, string loader, string outputDir, string modName)
    {
        try
        {
            string vUrl = $"https://api.modrinth.com/v2/project/{projectId}/version?loaders=[\"{loader}\"]";
            var vJson = await client.GetStringAsync(vUrl);
            var versions = JsonNode.Parse(vJson).AsArray();

            if (versions.Count == 0) return false;

            var bestMatch = versions.FirstOrDefault(v =>
                v["game_versions"].AsArray().Any(gv => gv.ToString() == version)
            );

            bool isRisky = false;
            string downloadedVersion = "";

            if (bestMatch == null)
            {
                bestMatch = versions[0];
                isRisky = true;
                downloadedVersion = bestMatch["game_versions"][0].ToString();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($" [Modrinth: MISSING {version} -> {downloadedVersion}]");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(" [Modrinth: OK]");
            }

            string dlUrl = bestMatch["files"][0]["url"].ToString();
            string fName = bestMatch["files"][0]["filename"].ToString();

            byte[] data = await client.GetByteArrayAsync(dlUrl);
            await File.WriteAllBytesAsync(Path.Combine(outputDir, fName), data);

            Console.ResetColor();
            Console.WriteLine();

            if (isRisky) riskyMods.Add($"{modName} (Modrinth: {downloadedVersion})");

            return true;
        }
        catch { return false; }
    }

    static async Task<bool> DownloadGitHub(string modName, string version, string outputDir)
    {
        try
        {
            string query = $"{modName} minecraft mod";
            string searchUrl = $"https://api.github.com/search/repositories?q={Uri.EscapeDataString(query)}&sort=stars&order=desc";

            var searchJson = JsonNode.Parse(await client.GetStringAsync(searchUrl));
            var items = searchJson["items"]?.AsArray();

            if (items == null || items.Count == 0) return false;

            string repoOwner = items[0]["owner"]["login"].ToString();
            string repoName = items[0]["name"].ToString();

            string releasesUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";
            var releaseJson = JsonNode.Parse(await client.GetStringAsync(releasesUrl));

            var assets = releaseJson["assets"]?.AsArray();
            if (assets == null) return false;

            foreach (var asset in assets)
            {
                string fName = asset["name"].ToString();

                if (fName.EndsWith(".jar") && !fName.Contains("-sources") && !fName.Contains("-api"))
                {
                    string dlUrl = asset["browser_download_url"].ToString();

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write(" [GitHub: Found]");

                    byte[] data = await client.GetByteArrayAsync(dlUrl);
                    await File.WriteAllBytesAsync(Path.Combine(outputDir, fName), data);

                    Console.ResetColor();
                    Console.WriteLine();

                    riskyMods.Add($"{modName} (Downloaded from GitHub - Check version manually!)");
                    return true;
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

    static string GetModIdFromJar(string path)
    {
        try
        {
            using (ZipArchive zip = ZipFile.OpenRead(path))
            {
                var fEntry = zip.GetEntry("fabric.mod.json");
                if (fEntry != null)
                {
                    using (var r = new StreamReader(fEntry.Open()))
                        return JsonNode.Parse(r.ReadToEnd())["id"]?.ToString();
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