using System.Drawing;
using System.Reflection;
using AdeptiScanner_ZZZ;
using Tesseract;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: PortraitTest <ScannerFiles directory> <example image>");
    return 2;
}

Assembly scannerAssembly = typeof(Disc).Assembly;
Type databaseType = scannerAssembly.GetType("AdeptiScanner_ZZZ.Database", true)!;
databaseType.GetField("appDir")!.SetValue(null, args[0]);
databaseType.GetMethod("GenerateFilters")!.Invoke(null, null);
string portraitPath = Path.Join(args[0], "AgentPortraits", "Lycaon.webp");
Console.WriteLine($"Catalog directory: {Directory.GetFiles(Path.Join(args[0], "AgentPortraits")).Length} files; Lycaon exists: {File.Exists(portraitPath)}");

using Bitmap sourceImage = new(args[1]);
Type imageProcessingType = scannerAssembly.GetType("AdeptiScanner_ZZZ.ImageProcessing", true)!;
MethodInfo getEquippedCharacter = imageProcessingType.GetMethod("getEquippedCharacter")!;
string location = (string)getEquippedCharacter.Invoke(null, new object?[] { sourceImage, null })!;
object portraitCache = imageProcessingType.GetField("agentPortraits", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
object portraitList = portraitCache.GetType().GetProperty("Value")!.GetValue(portraitCache)!;
int portraitCount = (int)portraitList.GetType().GetProperty("Count")!.GetValue(portraitList)!;
object?[] pixelArgs = { sourceImage, 0 };
byte[] rgbPixels = (byte[])imageProcessingType.GetMethod("GetRgbPixels", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, pixelArgs)!;
int stride = (int)pixelArgs[1]!;
object? anchor = imageProcessingType.GetMethod("FindAgentAnchor", BindingFlags.NonPublic | BindingFlags.Static)!
    .Invoke(null, new object?[] { rgbPixels, stride, new Rectangle(0, 0, sourceImage.Width, sourceImage.Height) });
Console.WriteLine($"Loaded portraits: {portraitCount}; anchor: {anchor ?? "not found"}");

using TesseractEngine engine = new(Path.Join(args[0], "tessdata"), "genshin");
MethodInfo getDiscImage = imageProcessingType.GetMethod("getDiscImg")!;
object?[] discImageArgs =
{
    sourceImage,
    new Rectangle(0, 0, sourceImage.Width, sourceImage.Height),
    null,
    false,
    true
};
using Bitmap filteredImage = (Bitmap)getDiscImage.Invoke(null, discImageArgs)!;
int[] rows = (int[])discImageArgs[2]!;

MethodInfo getDisc = imageProcessingType.GetMethod("getDisc")!;
Disc disc = (Disc)getDisc.Invoke(null, new object?[]
{
    filteredImage,
    rows,
    false,
    engine,
    sourceImage,
    null
})!;

Console.WriteLine(disc);
Console.WriteLine($"Location = \"{disc.location}\"; direct match = \"{location}\"");

bool HasSubstat(string key, int upgrades) => disc.subs.Any(stat => stat.Key == key && stat.Upgrades == upgrades);
string exampleName = Path.GetFileNameWithoutExtension(args[1]);
string expectedLocation = exampleName switch
{
    "ExampleEquippedBy-1" => "Lycaon",
    "ExampleEquippedBy-2" => "Lucy",
    "ExampleEquippedBy-3" => "Burnice",
    _ => throw new ArgumentException("Unknown example capture: " + exampleName)
};

bool validStats = exampleName switch
{
    "ExampleEquippedBy-1" => disc.slot is { Key: "SwingJazz", Slot: 5 }
        && disc.level is { Level: 15, Tier: Rarity.S }
        && disc.main is { Key: "ice_dmg_" }
        && disc.subs.Count == 4
        && HasSubstat("anomProf", 2)
        && HasSubstat("hp", 1)
        && HasSubstat("atk", 2)
        && HasSubstat("atk_", 3),
    "ExampleEquippedBy-2" => disc.slot is { Key: "SwingJazz", Slot: 2 }
        && disc.level is { Level: 15, Tier: Rarity.S }
        && disc.main is { Key: "atk" }
        && disc.subs.Count == 4
        && HasSubstat("pen", 3)
        && HasSubstat("crit_", 2)
        && HasSubstat("crit_dmg_", 3)
        && HasSubstat("def_", 1),
    "ExampleEquippedBy-3" => disc.slot is { Key: "PhaethonsMelody", Slot: 6 }
        && disc.level is { Level: 15, Tier: Rarity.S }
        && disc.main is { Key: "anomMas_" }
        && disc.subs.Count == 4
        && HasSubstat("crit_dmg_", 2)
        && HasSubstat("def", 2)
        && HasSubstat("atk_", 3)
        && HasSubstat("atk", 1),
    _ => false
};

bool passed = validStats && location == expectedLocation && disc.location == expectedLocation;
Console.WriteLine(passed ? "PASS" : "FAIL");
return passed ? 0 : 1;