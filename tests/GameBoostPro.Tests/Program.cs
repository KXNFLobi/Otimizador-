using GameBoostPro;

var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
Directory.CreateDirectory(root);
int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine($"PASS: {name}"); passed++; }
try
{
    var old = Path.Combine(root, "old.tmp"); File.WriteAllText(old, "old"); File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-10));
    var fresh = Path.Combine(root, "fresh.tmp"); File.WriteAllText(fresh, "fresh");
    var cutoff = DateTime.UtcNow.AddDays(-7);
    Check(GameBoostPro.Program.Eligible(old, root, cutoff), "arquivo antigo elegível");
    Check(!GameBoostPro.Program.Eligible(fresh, root, cutoff), "arquivo recente preservado");
    Check(!GameBoostPro.Program.Eligible(old, root + "-other", cutoff), "arquivo fora da raiz preservado");
    var link = Path.Combine(root, "link.tmp"); File.CreateSymbolicLink(link, old);
    Check(!GameBoostPro.Program.Eligible(link, root, cutoff), "link simbólico preservado");
    Check(GameBoostPro.Program.Options.Length == 17, "17 opções disponíveis");
    if (!OperatingSystem.IsWindows()) Check(GameBoostPro.Program.Main([]) == 1, "operações bloqueadas fora do Windows");
    Console.WriteLine($"{passed} testes aprovados.");
}
finally { Directory.Delete(root, true); }
