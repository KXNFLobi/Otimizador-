using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace GameBoostPro;

public static class StorageMenu
{
    public static int SelectIndex(string? text, int count) => int.TryParse(text, out var index) && index >= 1 && index <= count ? index - 1 : -1;
    static string GiB(long bytes) => $"{bytes / 1073741824.0:F2} GiB";
    static string Safe(string text) => new(text.Where(c => !char.IsControl(c)).ToArray());
    public static void Run(string requested)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Requer Windows.");
        var types = MediaTypes(); var drives = DriveInfo.GetDrives();
        Console.WriteLine($"ANALISAR E LIMPAR {requested} — selecione pela lista; letras não determinam o tipo.");
        for (int i = 0; i < drives.Length; i++)
        {
            var d = drives[i];
            try
            {
                if (!d.IsReady) { Console.WriteLine($"[{i + 1}] {d.Name} — sem mídia ou desconectada"); continue; }
                Console.WriteLine($"[{i + 1}] {d.Name} {Safe(d.VolumeLabel)} — {types.GetValueOrDefault(d.Name.TrimEnd('\\'), "Tipo desconhecido")} — {d.DriveType}; total {GiB(d.TotalSize)}, usado {GiB(d.TotalSize - d.TotalFreeSpace)}, livre {GiB(d.AvailableFreeSpace)}");
            }
            catch (Exception ex) when (StorageSafety.AccessError(ex)) { Console.WriteLine($"[{i + 1}] {d.Name} — sem acesso: {ex.Message}"); }
        }
        Console.Write("Número da unidade (Enter cancela): ");
        int selected = SelectIndex(Console.ReadLine(), drives.Length); if (selected < 0) return;
        var drive = drives[selected];
        if (!drive.IsReady || drive.DriveType is DriveType.Network or DriveType.CDRom) { Console.WriteLine("Unidade indisponível ou sem suporte para limpeza local."); return; }
        if (!StorageSafety.NoLinks(drive.RootDirectory.FullName)) throw new IOException("Raiz redirecionada não é suportada.");
        var actual = types.GetValueOrDefault(drive.Name.TrimEnd('\\'), "Tipo desconhecido");
        if (actual != requested)
        {
            Console.Write($"Tipo detectado: {actual}. Continuar manualmente com {drive.Name}? Digite SIM: ");
            if (Console.ReadLine() != "SIM") return;
        }
        while (true)
        {
            Console.WriteLine($"\nUnidade {drive.Name}: [1] Analisar espaço [2] Temporários [3] Aplicativos [4] Lixeira [5] Limpeza oficial do Windows [6] Otimizar Unidades [0] Voltar");
            var choice = Console.ReadLine(); if (choice is null || choice == "0") return;
            try
            {
                if (!drive.IsReady) { Console.WriteLine("Unidade desconectada."); return; }
                var before = drive.AvailableFreeSpace;
                switch (choice)
                {
                    case "1": Analyze(drive.Name); break;
                    case "2": Temporaries(drive.Name); break;
                    case "3": Apps(drive.Name); break;
                    case "4": Recycle(drive.Name); break;
                    case "5": OfficialCleanup(drive.Name); break;
                    case "6": Open("dfrgui.exe"); Console.WriteLine("Ferramenta oficial aberta; revise a unidade. Nenhuma otimização executada pelo programa."); break;
                    default: Console.WriteLine("Opção inválida."); continue;
                }
                var after = new DriveInfo(drive.Name).AvailableFreeSpace;
                Report($"{drive.Name} livre antes: {GiB(before)}; depois: {GiB(after)}. Outros processos podem alterar o espaço; a diferença não comprova efeito exclusivo da limpeza.");
            }
            catch (Exception ex) { Report("Operação interrompida: " + ex.Message); }
        }
    }
    static Dictionary<string, string> MediaTypes()
    {
        try
        {
            // Storage provider association: partition -> disk -> physical disk unique ID.
            // Do not guess from drive letters or model names. Arrays/virtual disks may be unknown.
            var json = GameMode.Shell("$physical=@(Get-PhysicalDisk -ErrorAction Stop); $r=@(Get-Partition -ErrorAction Stop | Where-Object DriveLetter | ForEach-Object { $d=$_ | Get-Disk -ErrorAction Stop; $m=@($physical | Where-Object { $_.UniqueId -and $_.UniqueId -eq $d.UniqueId }); $t='Tipo desconhecido'; if($m.Count -eq 1){if([int]$m[0].MediaType -eq 4){$t='SSD'}elseif([int]$m[0].MediaType -eq 3){$t='HD'}}; [pscustomobject]@{Drive=([string]$_.DriveLetter+':');Type=$t} }); ConvertTo-Json -InputObject $r -Compress");
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("Drive").GetString()!, x => x.GetProperty("Type").GetString()!, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) { Report("Detecção SSD/HD indisponível; seleção manual permitida: " + ex.Message); return new(StringComparer.OrdinalIgnoreCase); }
    }
    static void Analyze(string volume)
    {
        Console.WriteLine("Análise informativa: nada será apagado. Pastas podem se sobrepor; tamanhos não representam espaço físico exato (compressão/hardlinks). Pressione Q ou Ctrl+C para cancelar.");
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += handler;
        try
        {
            var task = Task.Run(() => StorageSafety.Scan(volume, cancellation.Token));
            while (!task.Wait(150))
                if (!Console.IsInputRedirected && Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q) cancellation.Cancel();
            var result = task.GetAwaiter().GetResult();
            Console.WriteLine("Maiores arquivos (somente informação):"); foreach (var f in result.Files) Console.WriteLine($"{GiB(f.Size)} — {Safe(f.Path)}");
            Console.WriteLine("Maiores pastas (conteúdo acessível; podem se sobrepor):"); foreach (var f in result.Folders) Console.WriteLine($"{GiB(f.Size)} — {Safe(f.Path)}");
            Report($"Análise {(result.Cancelled ? "cancelada/parcial" : "concluída")}: {result.Count} arquivos, {GiB(result.Bytes)} lidos em metadados, {result.Errors} falhas/links/limites ignorados. Nenhum arquivo alterado.");
        }
        finally { Console.CancelKeyPress -= handler; }
    }
    static void Temporaries(string volume)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");
        Console.WriteLine("Categorias inicialmente desmarcadas. Temporários do Windows/componentes: use [5], limpeza oficial; não serão apagados diretamente.");
        if (!StorageSafety.Within(root, volume)) { Console.WriteLine("A pasta temporária conhecida do usuário está em outra unidade. Nenhum arquivo candidato nesta categoria."); return; }
        if (!Directory.Exists(root) || !StorageSafety.NoLinks(root)) { Console.WriteLine("Pasta temporária indisponível ou redirecionada; preservada."); return; }
        var cutoff = DateTime.UtcNow.AddDays(-7); var files = new List<DiskFile>(); int errors = 0;
        foreach (var path in Directory.EnumerateFiles(root).Take(10001))
        {
            try
            {
                var info = new FileInfo(path); var snapshot = new DiskFile(path, info.Length, info.LastWriteTimeUtc);
                if (StorageSafety.Temporary(snapshot, root, cutoff)) files.Add(snapshot);
            }
            catch (Exception ex) when (StorageSafety.AccessError(ex)) { errors++; }
        }
        Console.WriteLine($"[1] Temporários do usuário: .tmp diretamente em {root}, com mais de sete dias. Subpastas e outros formatos preservados. Até 10001 entradas examinadas por execução; erros: {errors}.");
        foreach (var f in files) Console.WriteLine($"{f.Size} bytes — {Safe(f.Path)}");
        Console.WriteLine($"{files.Count} candidatos; tamanho estimado {GiB(files.Sum(f => f.Size))}. Ser antigo/.tmp só autoriza candidatura neste local conhecido, não indica que qualquer arquivo seja inútil.");
        if (files.Count == 0) return;
        Console.Write("Digite 1 para selecionar esta categoria; Enter preserva tudo: "); if (Console.ReadLine() != "1") return;
        Console.Write("Exclusão permanente, sem restauração. Arquivos em uso serão preservados. Digite LIMPAR para confirmar: ");
        var result = StorageSafety.Clean(files, root, volume, cutoff, Console.ReadLine());
        Report($"Temporários: excluídos {result.Deleted}; ignorados {result.Skipped}; falhas/arquivos em uso {result.Failed}.");
    }
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    static void Apps(string volume)
    {
        var apps = InstalledApps.Read();
        Console.WriteLine("Aplicativos tradicionais do registro (MSIX/Store podem não aparecer). Tamanho é informado pelo instalador, não medido. Itens desconhecidos/protegidos são apenas informativos.");
        Console.Write("Pesquisar nome/editor, ou Enter para listar: "); var query = Console.ReadLine() ?? "";
        var visible = apps.Where(a => a.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || a.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Where(a => InstalledApps.LocationGroup(a, volume) != "Outra unidade").ToList();
        for (int i = 0; i < visible.Count; i++)
        {
            var a = visible[i]; Console.WriteLine($"[{i + 1}] {Safe(a.Name)} | {Safe(a.Publisher)} | {Safe(a.Version)} | {Safe(a.Location)} | {(a.SizeKiB.HasValue ? (a.SizeKiB.Value / 1024.0).ToString("F1") + " MiB (estimativa)" : "Tamanho desconhecido")} | {InstalledApps.LocationGroup(a, volume)} | {(a.Optional ? "Revisão de desinstalação permitida" : "Preservado; identidade/tipo não confirmados")}");
        }
        Console.Write("Escolha um aplicativo por vez (Enter volta): "); int selected = SelectIndex(Console.ReadLine(), visible.Count); if (selected < 0) return;
        var app = visible[selected]; if (!app.Optional) { Console.WriteLine("Preservado: não oferecido para desinstalação por este programa."); return; }
        Console.WriteLine($"Selecionado: {Safe(app.Name)}. Desinstalar pode remover dados/configurações. A localização informada não garante onde todos os dados estão.");
        Console.Write("Digite DESINSTALAR para abrir Aplicativos do Windows; procure o nome selecionado e confirme lá: ");
        if (InstalledApps.RequestRemoval(app, Console.ReadLine(), () => Open("ms-settings:appsfeatures")))
        {
            Report("Tela oficial aberta para revisão de " + app.Name + ". Desinstalação NÃO confirmada. Verifique o resultado no Windows e atualize a lista.");
            Console.WriteLine("Conclua ou cancele no Windows e pressione Enter para medir novamente."); Console.ReadLine();
        }
        else Console.WriteLine("Cancelado. Nenhuma desinstalação solicitada.");
    }
    static void OfficialCleanup(string volume)
    {
        Console.WriteLine("Temporários do Windows e componentes: ferramenta oficial. Revise as categorias; não selecione Downloads/arquivos pessoais. Administrador pode ser solicitado pelo Windows ao limpar arquivos de sistema.");
        Console.Write("Digite ABRIR para iniciar a limpeza oficial nesta unidade: "); if (Console.ReadLine() != "ABRIR") return;
        var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cleanmgr.exe")) { UseShellExecute = true };
        info.ArgumentList.Add("/d"); info.ArgumentList.Add(volume.TrimEnd('\\'));
        using var p = Process.Start(info);
        Report("Limpeza oficial aberta; nenhuma exclusão foi confirmada pelo otimizador.");
        Console.WriteLine("Conclua ou cancele no Windows e pressione Enter para medir novamente."); Console.ReadLine();
    }
    static void Recycle(string volume)
    {
        Console.Write($"Esvaziar somente a Lixeira de {volume}? Exclusão permanente, sem restauração. Digite ESVAZIAR: ");
        if (Console.ReadLine() != "ESVAZIAR") return;
        // Keep Windows' own confirmation and progress dialogs enabled.
        var hr = SHEmptyRecycleBin(IntPtr.Zero, volume, 0);
        if (hr != 0) { Report($"Lixeira não confirmada (cancelamento/falha, HRESULT 0x{hr:X8})."); return; }
        Report("Windows confirmou esvaziamento da Lixeira da unidade " + volume);
    }
    static void Open(string target) { using var p = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
    static void Report(string text) { Console.WriteLine(text); Program.Log(text); }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHEmptyRecycleBin(IntPtr window, string root, uint flags);
}
