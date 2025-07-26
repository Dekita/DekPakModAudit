// 
// DekitaRPG@gmail.com
// DekPakModAudit - A tool to compare assets in game Paks with modded assets
// This tool scans the main game Paks and a specified mod directory, comparing assets
// and generating a JSON report of overridden and unique assets per VFS key.
// 
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
// CUE4Parse namespaces
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Versions;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.IO.Objects;

class Program
{

    class MultiTextWriter : TextWriter
    {
        private readonly TextWriter _console;
        private readonly TextWriter _log;

        public MultiTextWriter(TextWriter console, TextWriter log)
        {
            _console = console;
            _log = log;
        }

        public override Encoding Encoding => _console.Encoding;

        public override void Write(char value)
        {
            _console.Write(value);
            _log.Write(value);
        }

        public override void WriteLine(string value)
        {
            _console.WriteLine(value);
            _log.WriteLine(value);
        }

        public override void Flush()
        {
            _console.Flush();
            _log.Flush();
        }
    }


    class VfsAssetsInfo
    {
        public bool OverridesDefaultAssets { get; set; } = false;
        public List<string> OverriddenAssets { get; set; } = new List<string>();
        public List<string> UniqueAssets { get; set; } = new List<string>();
        public string ChunkID { get; set; } = "-1"; // Default value for chunk ID
    }

    public class ToolConfig
    {
        public string MainPaksFolder { get; set; }
        public string MainModsFolder { get; set; }
        public string MappingsPath { get; set; }
    }

    static string StripFirstDirectory(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? string.Join("/", parts.Skip(1)) : path;
    }    

    static async Task Main(string[] args)
    {
        await Task.Run(() =>
        {


            string exeDir = AppContext.BaseDirectory;
            string configPath = Path.Combine(exeDir, "input\\DekPakModAuditConfig.json");
            string outputLogPath = Path.Combine(exeDir, "output\\DekPakModAudit.log");
            string jsonOutputFile = Path.Combine(exeDir, "output\\DekPakModAudit.json");

            string? folderPath = Path.GetDirectoryName(jsonOutputFile);
            if (!string.IsNullOrEmpty(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }


            // Set up log file writer
            var logWriter = new StreamWriter(outputLogPath, append: false)
            {
                AutoFlush = true
            };

            // Redirect Console output to both console and file
            Console.SetOut(new MultiTextWriter(Console.Out, logWriter));
            Console.SetError(new MultiTextWriter(Console.Error, logWriter)); // optional

            if (!File.Exists(configPath))
            {
                Console.WriteLine("Missing config.json!");
                return;
            }

            ToolConfig config = JsonConvert.DeserializeObject<ToolConfig>(File.ReadAllText(configPath));
            string mainPaksFolder = Path.GetFullPath(Path.Combine(exeDir, config.MainPaksFolder));
            string mainModsFolder = Path.Combine(mainPaksFolder, config.MainModsFolder);
            string mappingsPath = Path.Combine(exeDir, config.MappingsPath);

            // Look for --modsfolder=VALUE argument
            foreach (var arg in args)
            {
                if (arg.StartsWith("--modsfolder="))
                {
                    var value = arg.Substring("--modsfolder=".Length);
                    if (!string.IsNullOrEmpty(value))
                    {
                        mainModsFolder = Path.Combine(mainPaksFolder, value);
                    }
                }
            }

            if (!Directory.Exists(mainPaksFolder))
            {
                Console.WriteLine($"ERROR: Game directory not found at {mainPaksFolder}");
                return;
            }
            if (!Directory.Exists(mainModsFolder))
            {
                Console.WriteLine($"ERROR: Mod directory not found at {mainModsFolder}");
                return;
            }
            if (!File.Exists(mappingsPath))
            {
                Console.WriteLine($"ERROR: Mappings file not found at {mappingsPath}");
                return;
            }

            Console.WriteLine($"Main Paks Folder: {mainPaksFolder}");
            Console.WriteLine($"Main Mods Folder: {mainModsFolder}");
            Console.WriteLine($"Mappings File: {mappingsPath}");
            Console.WriteLine("Starting asset comparison... Please wait.\n");

            var versionContainer = new VersionContainer(EGame.GAME_UE4_26);

            // Step 1: Scan main Paks (top-level only)
            var mainPaksProvider = new DefaultFileProvider(mainPaksFolder, SearchOption.TopDirectoryOnly, isCaseInsensitive: true, versionContainer);
            mainPaksProvider.MappingsContainer = new FileUsmapTypeMappingsProvider(mappingsPath);
            mainPaksProvider.Initialize();
            mainPaksProvider.Mount();

            var mainPaksAssets = new HashSet<string>(
                mainPaksProvider.Files
                    .Where(kv => kv.Value is FIoStoreEntry)
                    .Select(kv => StripFirstDirectory(((FIoStoreEntry)kv.Value).Path)),
                StringComparer.OrdinalIgnoreCase);

            Console.WriteLine($"Main Paks assets count (top-level): {mainPaksAssets.Count}");

            // Step 2: Scan input folder recursively
            var inputProvider = new DefaultFileProvider(mainModsFolder, SearchOption.AllDirectories, isCaseInsensitive: true, versionContainer);
            inputProvider.MappingsContainer = new FileUsmapTypeMappingsProvider(mappingsPath);
            inputProvider.Initialize();
            inputProvider.Mount();

            // Step 3: Build dictionary keyed by VfsKey, each containing:
            // - OverridesDefaultAssets (bool)
            // - OverriddenAssets (list)
            // - UniqueAssets (list)

            var outputDict = new Dictionary<string, VfsAssetsInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in inputProvider.Files.OrderBy(f => f.Key))
            {
                if (kv.Value is FIoStoreEntry entry)
                {
                    // Properties of FIoStoreEntry:
                    // Property: IsEncrypted Type: Boolean
                    // Property: CompressionMethod Type: CompressionMethod
                    // Property: ChunkId Type: FIoChunkId
                    // Property: IoStoreReader Type: IoStoreReader
                    // Property: Vfs Type: IVfsReader
                    // Property: Offset Type: Int64
                    // Property: Path Type: String
                    // Property: Size Type: Int64
                    // Property: Directory Type: String
                    // Property: PathWithoutExtension Type: String
                    // Property: Name Type: String
                    // Property: NameWithoutExtension Type: String
                    // Property: Extension Type: String
                    // Property: IsUePackage Type: Boolean
                    // Property: IsUePackagePayload Type: Boolean

                    // Get string key for Vfs
                    string vfsKey = entry.Vfs?.ToString() ?? "UnknownVfs";

                    if (!outputDict.TryGetValue(vfsKey, out var vfsInfo))
                    {
                        vfsInfo = new VfsAssetsInfo();
                        var chunkIdStr = entry.ChunkId.ToString();
                        var chunkIdClean = chunkIdStr.Split(" | ")[0];
                        vfsInfo.ChunkID = chunkIdClean;
                        outputDict[vfsKey] = vfsInfo;
                    }

                    // Strip the first folder from the path
                    string normalizedPath = StripFirstDirectory(entry.Path);
                    bool existsInMain = mainPaksAssets.Contains(normalizedPath);

                    if (existsInMain)
                    {
                        vfsInfo.OverridesDefaultAssets = true;
                        vfsInfo.OverriddenAssets.Add(normalizedPath);
                    }
                    else
                    {
                        vfsInfo.UniqueAssets.Add(normalizedPath);
                    }
                }
            }

            // Dictionary to map chunk IDs to the list of vfsKeys that have that chunk ID
            var chunkIdToKeys = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            // Log override info to console
            Console.WriteLine("\n== Mods that override base game assets ==\n");

            if (outputDict.Count == 0)
            {
                Console.WriteLine("No Vfs keys found.");
                return;
            }

            if (outputDict.Values.All(v => !v.OverridesDefaultAssets))
            {
                Console.WriteLine("No mods override base game assets.");
                return;
            }
            // Iterate through the output dictionary and print mods that override default assets
            foreach (var kvp in outputDict)
            {
                var vfsKey = kvp.Key;
                var vfsInfo = kvp.Value;

                var chunkId = vfsInfo.ChunkID;

                if (string.IsNullOrWhiteSpace(chunkId) || chunkId == "-1")
                    continue; // ignore invalid/no chunk id

                if (!chunkIdToKeys.TryGetValue(chunkId, out var keys))
                {
                    keys = new List<string>();
                    chunkIdToKeys[chunkId] = keys;
                }

                keys.Add(vfsKey);

                if (vfsInfo.OverridesDefaultAssets && vfsInfo.OverriddenAssets.Any())
                {
                    Console.WriteLine($"Mod: {vfsKey}");
                    foreach (var asset in vfsInfo.OverriddenAssets)
                    {
                        Console.WriteLine($"  - {asset}");
                    }
                    Console.WriteLine(); // Add an empty line for separation
                }
            }

            foreach (var kvp in outputDict)
            {
                var vfsKey = kvp.Key;
                var vfsInfo = kvp.Value;

                var chunkId = vfsInfo.ChunkID;

                if (string.IsNullOrWhiteSpace(chunkId) || chunkId == "-1")
                    continue; // ignore invalid/no chunk id

                if (!chunkIdToKeys.TryGetValue(chunkId, out var keys))
                {
                    keys = new List<string>();
                    chunkIdToKeys[chunkId] = keys;
                }
            }

            // Now print duplicates, i.e., chunk IDs that have more than one associated key
            var duplicateChunkIds = chunkIdToKeys.Where(kvp => kvp.Value.Count > 1);

            Console.WriteLine("\n== Mods that share chunk IDs ==\n");

            if (duplicateChunkIds.Any())
            {
                foreach (var dup in duplicateChunkIds)
                {
                    Console.WriteLine($"Chunk ID: {dup.Key} used by mods:");
                    foreach (var key in dup.Value)
                    {
                        Console.WriteLine($"  - {key}");
                    }
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("No duplicate chunk IDs found.");
            }


            // Serialize and write output JSON
            var json = JsonConvert.SerializeObject(outputDict, Formatting.Indented);
            File.WriteAllText(jsonOutputFile, json);

            Console.WriteLine("\n== Output Files ==\n");
            Console.WriteLine($"  - Logs: {outputLogPath}");
            Console.WriteLine($"  - JSON: {jsonOutputFile}");

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        });
    }
    
}
