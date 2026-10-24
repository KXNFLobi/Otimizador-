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
    Check(GameBoostPro.Program.Options.Length == 20, "20 opções disponíveis");
    if (!OperatingSystem.IsWindows()) Check(GameBoostPro.Program.Main([]) == 1, "operações bloqueadas fora do Windows");
    Check(!GameMode.Confirmed(null) && !GameMode.Confirmed("SIM") && !GameMode.Confirmed(""), "cancelamento e EOF não autorizam alterações");
    Check(GameMode.Confirmed("APLICAR"), "confirmação explícita reconhecida");
    Check(GameMode.OptionalIdentity("chrome", "CN=Google LLC, O=Google LLC, C=US", true, true, true), "navegador assinado elegível");
    Check(!GameMode.OptionalIdentity("chrome", "O=Outro, C=US", true, true, true), "editor desconhecido protegido");
    Check(!GameMode.OptionalIdentity("chrome", "O=Google LLC, C=US", false, true, true), "assinatura inválida protegida");
    Check(!GameMode.OptionalIdentity("chrome", "O=Google LLC, C=US", true, false, true), "processo sem janela protegido");
    Check(!GameMode.OptionalIdentity("chrome", "O=Google LLC, C=US", true, true, false), "outra sessão protegida");
    foreach (var critical in new[] { "MsMpEng", "System", "explorer", "steam", "EasyAntiCheat", "unknown" })
        Check(!GameMode.OptionalIdentity(critical, "O=Microsoft Corporation, C=US", true, true, true), critical + " protegido");
    var original = Guid.NewGuid(); var target = Guid.NewGuid(); var active = original;
    var stateFile = Path.Combine(root, "power.json"); int changes = 0;
    var power = new PowerSession(stateFile, () => active, plan => {
        Check(File.Exists(stateFile), "recuperação persistida antes de mudar energia"); active = plan; changes++;
    });
    power.Apply(target);
    Check(active == target && power.Pending, "aplicação guarda plano original");
    bool blocked = false; try { power.Apply(Guid.NewGuid()); } catch (InvalidOperationException) { blocked = true; }
    Check(blocked && changes == 1, "sessão pendente não é sobrescrita");
    // Simulate a new process after an unexpected shutdown.
    var recovered = new PowerSession(stateFile, () => active, plan => active = plan);
    recovered.Restore(); Check(active == original && !recovered.Pending, "nova instância recupera plano após interrupção");
    recovered.Restore(); Check(active == original, "restauração repetida não muda estado");
    recovered.Apply(target);
    var denied = new PowerSession(stateFile, () => active, _ => throw new IOException("acesso negado"));
    bool failed = false; try { denied.Restore(); } catch (IOException) { failed = true; }
    Check(failed && denied.Pending && active == target, "falha ao restaurar preserva recuperação");
    recovered.Restore();
    var applyDenied = new PowerSession(stateFile, () => active, _ => throw new IOException("plano indisponível"));
    failed = false; try { applyDenied.Apply(target); } catch (IOException) { failed = true; }
    Check(failed && active == original && applyDenied.Pending, "falha ao aplicar não perde plano anterior");
    recovered.Restore();
    int actions = 0;
    Check(!GameMode.ExecuteSession("", recovered, true, target, () => actions++, () => actions++, () => actions++) && actions == 0 && active == original && !recovered.Pending, "cancelamento não fecha apps, não abre jogo e não muda energia");
    failed = false;
    try { GameMode.ExecuteSession("APLICAR", recovered, true, target, () => actions++, () => throw new IOException("falha ao iniciar jogo"), () => actions++); }
    catch (IOException) { failed = true; }
    Check(failed && actions == 1 && active == original && !recovered.Pending, "falha ao abrir jogo restaura energia e não entra em espera");
    Check(GameMode.ExecuteSession("APLICAR", recovered, true, target, () => actions++, () => actions++, () => { Check(active == target, "plano aplicado durante sessão"); actions++; }), "sessão confirmada executa ações");
    Check(actions == 4 && active == original && !recovered.Pending, "fim da sessão restaura plano original");
    File.WriteAllText(stateFile, "inválido");
    failed = false; try { recovered.Restore(); } catch (System.Text.Json.JsonException) { failed = true; }
    Check(failed && recovered.Pending && active == original, "registro inválido não aplica plano e é preservado");
    StorageTests.Run(root, Check);
    Console.WriteLine($"{passed} testes aprovados.");
}
finally { Directory.Delete(root, true); }
