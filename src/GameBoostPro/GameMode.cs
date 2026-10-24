using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace GameBoostPro;

public sealed record PendingPower(Guid Previous, Guid Applied);

// Persist intent before touching Windows. Failed restoration retains the recovery file.
public sealed class PowerSession(string file, Func<Guid> current, Action<Guid> activate)
{
    public bool Pending => File.Exists(file);
    public void Apply(Guid target)
    {
        if (Pending) throw new InvalidOperationException("Restaure a sessão pendente primeiro.");
        var previous = current(); if (previous == target) return;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temporary = file + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(stream, new PendingPower(previous, target)); stream.Flush(true); }
            File.Move(temporary, file, false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        activate(target);
        if (current() != target) throw new IOException("Windows não confirmou o plano solicitado.");
    }
    public void Restore()
    {
        if (!Pending) return;
        var state = JsonSerializer.Deserialize<PendingPower>(File.ReadAllText(file)) ?? throw new IOException("Registro de restauração inválido.");
        if (state.Previous == Guid.Empty || state.Applied == Guid.Empty) throw new IOException("Plano inválido no registro.");
        activate(state.Previous);
        if (current() != state.Previous) throw new IOException("Restauração não confirmada.");
        File.Delete(file);
    }
}

public static class GameMode
{
    static readonly string Data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameBoostPro");
    static readonly Guid HighPerformance = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    static PowerSession Session() => new(Path.Combine(Data, "pending-power.json"), ActivePlan, SetPlan);
    public static bool Confirmed(string? input) => input == "APLICAR";
    public static bool ExecuteSession(string? confirmation, PowerSession session, bool high, Guid target, Action close, Action launch, Action wait)
    {
        if (!Confirmed(confirmation)) return false;
        try
        {
            if (high) session.Apply(target);
            close(); launch(); wait();
            return true;
        }
        finally { if (session.Pending) session.Restore(); }
    }
    internal static FileStream? AcquireLock()
    {
        try
        {
            Directory.CreateDirectory(Data);
            return new FileStream(Path.Combine(Data, "instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Console.WriteLine("Outra instância está ativa ou o armazenamento local está indisponível: " + ex.Message); return null; }
    }
    public static bool OptionalIdentity(string name, string signer, bool valid, bool visible, bool sameSession) => valid && visible && sameSession && (name.ToLowerInvariant() switch
    {
        "chrome" => signer.Contains("O=Google LLC,", StringComparison.OrdinalIgnoreCase),
        "msedge" => signer.Contains("O=Microsoft Corporation,", StringComparison.OrdinalIgnoreCase),
        "firefox" => signer.Contains("O=Mozilla Corporation,", StringComparison.OrdinalIgnoreCase),
        _ => false
    });
    public static void Recover()
    {
        var session = Session(); if (!session.Pending) return;
        Console.WriteLine("Há um plano de energia anterior pendente. Digite RESTAURAR para recuperá-lo.");
        if (Console.ReadLine() == "RESTAURAR") { session.Restore(); Report("Plano anterior restaurado."); }
    }
    public static void Run()
    {
        var session = Session();
        if (session.Pending) { Recover(); if (session.Pending) { Console.WriteLine("Restaure o plano pendente antes de iniciar outra sessão."); return; } }
        Console.Write("Caminho completo do executável do jogo (.exe), ou Enter para cancelar: ");
        var input = Console.ReadLine()?.Trim().Trim('"'); if (string.IsNullOrWhiteSpace(input)) return;
        var game = Path.GetFullPath(input);
        if (!File.Exists(game) || !game.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Selecione um .exe existente.");
        Console.WriteLine($"Jogo: {game}");
        ShowResources(game);
        try { Console.WriteLine("Driver de vídeo:\n" + Shell("Get-CimInstance Win32_VideoController -ErrorAction Stop | Select-Object Name,DriverVersion | Format-List | Out-String")); }
        catch (Exception ex) { Report("Driver indisponível: " + ex.Message); }
        Console.Write("Consultar falhas deste executável nos últimos 7 dias? Digite SIM: ");
        if (Console.ReadLine() == "SIM") Crashes(game);
        var candidates = Candidates(game);
        for (int i = 0; i < candidates.Count; i++) Console.WriteLine($"[{i + 1}] {candidates[i].Name} PID {candidates[i].Pid} — {candidates[i].Memory / 1048576.0:F1} MiB — {candidates[i].Path}");
        Console.WriteLine("Somente navegadores com janela, assinatura válida e na sessão atual são oferecidos. Desconhecidos são preservados.");
        Console.Write("Números separados por vírgula para fechar, ou Enter para preservar todos: ");
        var selection = Console.ReadLine(); var chosen = new List<App>();
        if (!string.IsNullOrWhiteSpace(selection))
            foreach (var token in selection.Split(','))
            {
                if (!int.TryParse(token.Trim(), out var n) || n < 1 || n > candidates.Count) throw new ArgumentException("Seleção inválida. Nada aplicado.");
                if (!chosen.Contains(candidates[n - 1])) chosen.Add(candidates[n - 1]);
            }
        bool high = false;
        try
        {
            Console.WriteLine($"Plano atual: {ActivePlan()}");
            if (Plans().Contains(HighPerformance))
            { Console.Write("Ativar Alto desempenho durante a sessão? Aumenta consumo/temperatura. Digite SIM: "); high = Console.ReadLine() == "SIM"; }
            else Console.WriteLine("Plano Alto desempenho existente: indisponível. Plano atual será preservado.");
        }
        catch (Exception ex) { Report("Planos indisponíveis; serão preservados: " + ex.Message); }
        Console.WriteLine($"PRÉVIA: abrir {game}; fechar normalmente {chosen.Count} aplicativos; alto desempenho: {high}.");
        foreach (var app in chosen) Console.WriteLine($"Fechar: {app.Name} PID {app.Pid}");
        Console.WriteLine("Aplicativos podem pedir para salvar dados ou recusar o fechamento. Suas sessões não serão restauradas.");
        Console.WriteLine("Sem garantia de mais FPS ou de eliminar crashes. FPS/temperatura não são medidos nesta versão.");
        Console.Write("Digite APLICAR para confirmar, qualquer outra entrada cancela: ");
        var confirmation = Console.ReadLine();
        try
        {
            var completed = ExecuteSession(confirmation, session, high, HighPerformance,
                () => { foreach (var app in chosen) Close(app); },
                () => {
                    if (high) Report("Plano de desempenho solicitado; plano anterior guardado quando houve mudança.");
                    using var process = Process.Start(new ProcessStartInfo(game) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(game)! });
                    Report("Solicitação de abertura enviada ao Windows: " + game);
                    ShowResources(game);
                },
                () => {
                    Console.WriteLine("Modo Jogo ativo. Pressione Enter para encerrar o modo e restaurar energia. O jogo continuará aberto.");
                    Console.WriteLine("Se o otimizador fechar inesperadamente, reabra-o para recuperar o plano anterior.");
                    Console.ReadLine();
                });
            Report(completed ? "Modo Jogo encerrado; ajustes de energia concluídos/restaurados." : "Cancelado. Nada aplicado.");
        }
        catch (Exception ex)
        {
            Report("Modo Jogo interrompido: " + ex.Message);
            if (session.Pending) Report("Restauração pendente: reabra o programa para tentar recuperar o plano anterior.");
        }
    }

    sealed record App(int Pid, string Name, string Path, DateTime Started, long Memory);
    static List<App> Candidates(string game)
    {
        var apps = new List<App>(); using var self = Process.GetCurrentProcess();
        foreach (var p in Process.GetProcesses())
        {
            using (p) try
            {
                if (p.SessionId != self.SessionId || p.MainWindowHandle == IntPtr.Zero || !new[] { "chrome", "msedge", "firefox" }.Contains(p.ProcessName.ToLowerInvariant())) continue;
                var path = p.MainModule?.FileName;
                if (path is null || string.Equals(path, game, StringComparison.OrdinalIgnoreCase) || !Trusted(p.ProcessName, path)) continue;
                apps.Add(new(p.Id, p.ProcessName, path, p.StartTime.ToUniversalTime(), p.WorkingSet64));
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException or IOException or UnauthorizedAccessException or JsonException) { Program.Log("Processo preservado por identidade não verificável: " + ex.Message); }
        }
        return apps.OrderByDescending(a => a.Memory).ToList();
    }
    static bool Trusted(string name, string path)
    {
        // Reject reparse points in the entire path; preserve unrecognized installations.
        for (var item = new FileInfo(path) as FileSystemInfo; item is not null; item = item is FileInfo f ? f.Directory : ((DirectoryInfo)item).Parent)
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0) return false;
        if (!string.Equals(Path.GetFileName(path), name + ".exe", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetFileName(FileVersionInfo.GetVersionInfo(path).OriginalFilename), name + ".exe", StringComparison.OrdinalIgnoreCase)) return false;
        var signature = Shell("$s=Get-AuthenticodeSignature -LiteralPath $env:GBP_SELECTED_PATH -ErrorAction Stop; [pscustomobject]@{Valid=($s.Status -eq 'Valid');Subject=$s.SignerCertificate.Subject}|ConvertTo-Json -Compress", path);
        using var doc = JsonDocument.Parse(signature); var node = doc.RootElement;
        return OptionalIdentity(name, node.GetProperty("Subject").GetString() ?? "", node.GetProperty("Valid").GetBoolean(), true, true);
    }
    static void Close(App app)
    {
        try
        {
            using var process = Process.GetProcessById(app.Pid); using var self = Process.GetCurrentProcess();
            if (process.StartTime.ToUniversalTime() != app.Started || process.SessionId != self.SessionId || process.MainWindowHandle == IntPtr.Zero ||
                !string.Equals(process.MainModule?.FileName, app.Path, StringComparison.OrdinalIgnoreCase) || !Trusted(process.ProcessName, app.Path))
            { Report($"Preservado PID {app.Pid}: identidade alterada ou não verificável."); return; }
            if (!process.CloseMainWindow()) { Report($"PID {app.Pid} não aceitou solicitação de fechamento."); return; }
            Report(process.WaitForExit(3000) ? $"Fechamento confirmado PID {app.Pid}." : $"PID {app.Pid}: fechamento solicitado; ainda aberto, possivelmente aguardando salvar dados.");
        }
        catch (Exception ex) { Report($"PID {app.Pid} preservado/falha no fechamento: {ex.Message}"); }
    }
    static void Crashes(string game)
    {
        try
        {
            // Match structured event fields, never inject the selected executable into script text.
            var result = Shell("$p=$env:GBP_SELECTED_PATH; $n=[IO.Path]::GetFileName($p); $events=Get-WinEvent -FilterHashtable @{LogName='Application';Id=1000,1001;StartTime=(Get-Date).AddDays(-7)} -MaxEvents 2000 -ErrorAction Stop; $matches=@($events | Where-Object { $x=[xml]$_.ToXml(); @($x.Event.EventData.Data | Where-Object { $_.InnerText -ieq $p -or $_.InnerText -ieq $n }).Count -gt 0 } | Select-Object -First 10 TimeCreated,Id,Message); if($matches.Count -eq 0){'Nenhum evento correspondente na amostra.'}else{$matches|Format-List|Out-String}", game);
            Console.WriteLine("Eventos Application 1000/1001, até 2000 eventos consultados e 10 correspondências (nome pode ser ambíguo):\n" + result);
            Program.Log("Consulta local de crashes concluída para " + game);
        }
        catch (Exception ex) { Report("Eventos indisponíveis ou sem registros acessíveis: " + ex.Message); }
        Console.WriteLine("Diagnóstico inconclusivo: um evento isolado não identifica a causa. Verifique arquivos pelo launcher e drivers no Windows Update/fabricante. Nenhum arquivo do jogo foi alterado.");
    }
    [StructLayout(LayoutKind.Sequential)] struct Memory { public uint Length, Load; public ulong Total, Available, PageTotal, PageAvailable, VirtualTotal, VirtualAvailable, Extended; }
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref Memory value);
    static void ShowResources(string game)
    {
        var memory = new Memory { Length = (uint)Marshal.SizeOf<Memory>() };
        if (GlobalMemoryStatusEx(ref memory))
        {
            Report($"RAM disponível: {memory.Available / 1073741824.0:F2} GiB; uso: {memory.Load}%.");
            if (memory.Load >= 90) Console.WriteLine("Pressão de memória elevada neste instante; pode contribuir para travamentos, mas não comprova a causa.");
        }
        else Console.WriteLine("RAM indisponível.");
        try { var d = new DriveInfo(Path.GetPathRoot(game)!); Report($"Disco do jogo: {d.AvailableFreeSpace / 1073741824.0:F2} GiB livres de {d.TotalSize / 1073741824.0:F2} GiB."); if (d.AvailableFreeSpace < 5L * 1073741824) Console.WriteLine("Pouco espaço livre; verifique os requisitos do jogo. Isso não confirma a causa de um crash."); }
        catch (Exception ex) { Report("Espaço indisponível: " + ex.Message); }
    }
    static string Shell(string script, string? selected = null)
    {
        var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop';[Console]::OutputEncoding=[Text.UTF8Encoding]::new();" + script)) }) info.ArgumentList.Add(arg);
        if (selected is not null) info.Environment["GBP_SELECTED_PATH"] = selected;
        using var p = Process.Start(info) ?? throw new IOException("PowerShell indisponível.");
        var output = p.StandardOutput.ReadToEndAsync(); var error = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(30000)) { p.Kill(); throw new TimeoutException("Consulta excedeu 30 segundos."); }
        Task.WaitAll(output, error);
        if (p.ExitCode != 0) throw new IOException(error.Result.Trim());
        return output.Result.Trim();
    }
    static void Report(string message) { Console.WriteLine(message); Program.Log(message); }
    [DllImport("powrprof.dll")] static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr guid);
    [DllImport("powrprof.dll")] static extern uint PowerSetActiveScheme(IntPtr root, ref Guid guid);
    [DllImport("powrprof.dll")] static extern uint PowerEnumerate(IntPtr root, IntPtr scheme, IntPtr subgroup, uint access, uint index, byte[] buffer, ref uint size);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr pointer);
    static Guid ActivePlan() { if (PowerGetActiveScheme(IntPtr.Zero, out var p) != 0) throw new IOException("Não foi possível ler o plano ativo."); try { return Marshal.PtrToStructure<Guid>(p); } finally { LocalFree(p); } }
    static void SetPlan(Guid guid) { if (PowerSetActiveScheme(IntPtr.Zero, ref guid) != 0) throw new IOException("Não foi possível ativar o plano; verifique permissões/políticas do Windows."); }
    static IEnumerable<Guid> Plans()
    {
        for (uint i = 0; ; i++) { uint size = 16; var buffer = new byte[16]; var code = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 16, i, buffer, ref size); if (code == 259) yield break; if (code != 0) throw new IOException("Não foi possível listar planos."); yield return new Guid(buffer); }
    }
}
