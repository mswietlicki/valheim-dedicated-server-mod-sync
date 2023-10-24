using HtmlAgilityPack;
using Microsoft.Win32;

Console.WriteLine("Hi, welcome to Valheim Dedicated Server Mod Sync by @mswietlicki");
Console.WriteLine();
Console.Write("Looking for Valheim...");

var steam = GetSteamPath();
var valheim = Path.Combine(steam, "steamapps", "common", "Valheim");
if (steam == null || !Path.Exists(Path.Combine(valheim, "valheim.exe")))
{
    Console.WriteLine(" not found.");
    Console.WriteLine("Please install Steam and Valheim first.");
    Console.ReadLine();
    return -1;
}
else
{
    Console.WriteLine($" found at {valheim}");
}

string userDataPath = "valheim_server.txt";
var serverHostName = ReadSavedServerUrl(userDataPath);

if (string.IsNullOrWhiteSpace(serverHostName))
{
    Console.Write("Please enter url of your Valheim server: ");
    serverHostName = Console.ReadLine();
    SaveSavedServerUrl(userDataPath, serverHostName);
}

Console.WriteLine($"Reading mods and configs from {serverHostName}");
var serverUrl = $"http://{serverHostName}:9002/";
var files = await ListFiles(serverUrl);

foreach (var file in files.Where(f => !f.Contains("plugins/")))
    DownloadFile(serverUrl, file, Path.Combine(valheim, "BepInEx/config/", file));

foreach (var file in files.Where(f => f.Contains("plugins/")))
    DownloadFile(serverUrl, file, Path.Combine(valheim, "BepInEx/", file));

Console.WriteLine();
Console.WriteLine("All done! Press any key to exit.");
Console.ReadLine();

return 0;

static async Task<IReadOnlyCollection<string>> ListFiles(string baseUrl, string? directory = null)
{
    var url = new Uri(new Uri(baseUrl), directory ?? string.Empty).AbsoluteUri;
    var client = new HttpClient();

    try
    {
        var response = await client.GetStringAsync(url);
        var doc = new HtmlDocument();
        doc.LoadHtml(response);

        var files = new List<string>();
        foreach (var link in doc.DocumentNode.SelectNodes("//a[@href]"))
        {
            string hrefValue = link.GetAttributeValue("href", string.Empty);
            if (!string.IsNullOrEmpty(hrefValue) && !hrefValue.StartsWith('/'))
            {
                var relativePath = directory + hrefValue;
                string fullUrl = new Uri(new Uri(url), relativePath).AbsoluteUri;

                if (hrefValue.EndsWith('/'))
                    files.AddRange(await ListFiles(baseUrl, relativePath));
                else
                    files.Add(relativePath);
            }
        }
        return files;
    }
    catch (System.Exception e)
    {
        Console.Error.WriteLine($"Failed to list files at {url}: {e.Message}");
        return Array.Empty<string>();
    }
}

static void DownloadFile(string serverUrl, string remotePath, string localPath)
{
    if (string.IsNullOrEmpty(localPath))
        throw new ArgumentNullException(nameof(localPath), "Local path cannot be null or empty.");

    var url = new Uri(new Uri(serverUrl), remotePath).AbsoluteUri;
    var client = new HttpClient();
    var fileData = client.GetByteArrayAsync(url).Result;
    //create directories if not exists
    Directory.CreateDirectory(Path.GetDirectoryName(localPath));
    File.WriteAllBytes(localPath, fileData);
    Console.WriteLine($"Downloaded {remotePath} to {localPath}");
}

static string? GetSteamPath() =>
    Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath") as string;

static string? ReadSavedServerUrl(string userDataPath) => File.Exists(userDataPath) ? File.ReadAllText(userDataPath) : null;
static void SaveSavedServerUrl(string userDataPath, string? serverUrl) => File.WriteAllText(userDataPath, serverUrl);