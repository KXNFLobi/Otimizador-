using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace GameBoostPro;

public static class Program
{
    public static readonly string[] Options = ["Desativar recursos", "Desinstalar apps", "Atualizar drivers", "Limpar temporários", "Recursos de energia", "Configurações visuais", "Limpar Prefetch", "Apps de inicialização", "Monitorar hardware e temperatura", "Apps de segundo plano", "Configurar Game Bar", "Relatórios de erro", "Configurar Cortana", "Prioridade da CPU / GPU", "Configurar telemetria", "Prioridade foreground", "Diagnosticar conexão e ping", "MODO JOGO — FPS E ESTABILIDADE", "ANALISAR E LIMPAR SSD", "ANALISAR E LIMPAR HD"];
    public static int Main(string[] args)
    {
        if (args.Contains("--help")) { Console.WriteLine("GAME BOOST PRO — menu Windows 10/11. Execute sem argumentos no Windows."); return 0; }
        if (!OperatingSystem.IsWindows()) { Console.Error.WriteLine("As operações exigem Windows 10/11. Nenhum ajuste foi aplicado."); return 1; }
        using var instance = GameMode.AcquireLock();
        if (instance is null) return 1;
        try { GameMode.Recover(); } catch (Exception ex) { Console.WriteLine($"Restauração pendente: {ex.Message}"); }
        while (true)
        {
            Console.WriteLine("\n==================================================\n                 GAME BOOST PRO\n==================================================");
            for (int i = 0; i < Options.Length; i++) Console.WriteLine($"[{i + 1,2}] > {Options[i]}");
            Console.WriteLine("[ 0] > SAIR"); Console.Write("\nEscolha uma opção: ");
            var input = Console.ReadLine(); if (input is null || input == "0") return 0;
            if (!int.TryParse(input, out int n) || n < 1 || n > Options.Length) { Console.WriteLine("Opção inválida."); continue; }
            try { Execute(n); }
            catch (Exception ex) { Console.WriteLine($"Falha: {ex.Message}"); Log($"Falha na opção {n}: {ex.Message}"); }
        }
    }
    static void Open(string target) { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); Console.WriteLine("Ferramenta do Windows aberta. Revise e aplique as mudanças nela."); Log($"Aberto: {target}"); }
    static void Execute(int n)
    {
        switch (n)
        {
            case 1: Open("optionalfeatures.exe"); break;
            case 2: if (OperatingSystem.IsWindows()) StorageMenu.Apps(null); break;
            case 3: Console.WriteLine("Use Windows Update ou o fabricante. Nenhum driver será baixado por este aplicativo."); Open("ms-settings:windowsupdate"); break;
            case 4: StorageMenu.Temporaries(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))!); break;
            case 5: Console.WriteLine("Planos de desempenho podem aumentar consumo e temperatura."); Open("powercfg.cpl"); break;
            case 6: Open("SystemPropertiesPerformance.exe"); break;
            case 7: Console.WriteLine("Indisponível: excluir Prefetch indiscriminadamente pode piorar a inicialização. Use a limpeza de temporários."); break;
            case 8: Open("ms-settings:startupapps"); break;
            case 9: Monitor(); break;
            case 10: Open("ms-settings:privacy-backgroundapps"); break;
            case 11: Open("ms-settings:gaming-gamebar"); break;
            case 12: Console.WriteLine("Revise relatórios em Segurança e Manutenção. Não desativamos serviços automaticamente."); Open("control.exe"); break;
            case 13: Console.WriteLine("A Cortana foi descontinuada; se instalada, gerencie sua remoção na lista de aplicativos."); Open("ms-settings:appsfeatures"); break;
            case 14: Console.WriteLine("GPU: selecione o aplicativo nas configurações gráficas. Prioridade de CPU não é alterada automaticamente."); Open("ms-settings:display-advancedgraphics"); break;
            case 15: Open("ms-settings:privacy-feedback"); break;
            case 16: Console.WriteLine("Em Avançado > Agendamento do processador, revise a preferência por Programas. Sem ajustes automáticos no registro."); Open("SystemPropertiesPerformance.exe"); break;
            case 17: PingHost(); break;
            case 18: GameMode.Run(); break;
            case 19: StorageMenu.Run("SSD"); break;
            case 20: StorageMenu.Run("HD"); break;
        }
    }
    public static bool Eligible(string path, string root, DateTime cutoff)
    {
        var full = Path.GetFullPath(path); var basePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return full.StartsWith(basePath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            && File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) == 0 && File.GetLastWriteTimeUtc(full) < cutoff;
    }
    static void PingHost()
    {
        Console.Write("Host do servidor do jogo (sem https://): "); string? host = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(host) || Uri.CheckHostName(host) == UriHostNameType.Unknown) { Console.WriteLine("Host inválido."); return; }
        using var ping = new Ping(); var times = new List<long>();
        for (int i = 0; i < 4; i++) { var reply = ping.Send(host, 1500); Console.WriteLine(reply.Status == IPStatus.Success ? $"{reply.RoundtripTime} ms" : reply.Status.ToString()); if (reply.Status == IPStatus.Success) times.Add(reply.RoundtripTime); }
        Console.WriteLine($"Perda nesta amostra: {(4 - times.Count) * 25}%." + (times.Count > 0 ? $" Média: {times.Average():F1} ms." : " ICMP pode estar bloqueado."));
        Console.WriteLine("Prefira cabo e pause downloads. Este diagnóstico não altera DNS nem promete reduzir ping.");
    }
    [StructLayout(LayoutKind.Sequential)]
    struct Memory { public uint Length, Load; public ulong Total, Available, PageTotal, PageAvailable, VirtualTotal, VirtualAvailable, Extended; }
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GlobalMemoryStatusEx(ref Memory memory);
    static void Monitor()
    {
        var memory = new Memory { Length = (uint)Marshal.SizeOf<Memory>() };
        if (GlobalMemoryStatusEx(ref memory)) Console.WriteLine($"RAM: {memory.Load}% em uso; disponível {memory.Available / 1073741824.0:F2} / {memory.Total / 1073741824.0:F2} GiB");
        else Console.WriteLine("RAM indisponível.");
        Console.WriteLine($"Processadores lógicos: {Environment.ProcessorCount}; tempo ligado: {TimeSpan.FromMilliseconds(Environment.TickCount64):g}");
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady)) Console.WriteLine($"{drive.Name}: {drive.AvailableFreeSpace / 1073741824.0:F1} GiB livres");
        Console.WriteLine("Temperatura, uso de GPU e FPS indisponíveis nesta versão: exigem sensores/provedores compatíveis.");
    }
    internal static void Log(string message)
    {
        try { string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameBoostPro"); Directory.CreateDirectory(dir); File.AppendAllText(Path.Combine(dir, "history.log"), $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Console.Error.WriteLine("Não foi possível gravar o histórico local."); }
    }
}
