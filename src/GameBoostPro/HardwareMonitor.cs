using System.Runtime.InteropServices;
using System.Text.Json;

namespace GameBoostPro;

public readonly record struct CpuSample(ulong Idle, ulong Kernel, ulong User);

public static class HardwareMonitor
{
    public static double? CpuPercent(CpuSample before, CpuSample after)
    {
        if (after.Idle < before.Idle || after.Kernel < before.Kernel || after.User < before.User) return null;
        double total = (double)(after.Kernel - before.Kernel) + (after.User - before.User);
        double idle = after.Idle - before.Idle;
        return total <= 0 || idle > total ? null : (total - idle) / total * 100;
    }
    public static string Temperature(string name, double? value) =>
        value is null || !double.IsFinite(value.Value) ? $"{name}: indisponível" : $"{name}: {value.Value:F1} °C";

    public static void Run()
    {
        if (!OperatingSystem.IsWindows()) { Console.WriteLine("Monitor de hardware exige Windows."); return; }
        while (true)
        {
            Console.WriteLine($"\n========== HARDWARE E TEMPERATURA — {DateTime.Now:T} ==========\nConsultando dados reais. Aguarde; esta tela permanece aberta até você escolher voltar.");
            Section("CPU", () =>
            {
                if (!OperatingSystem.IsWindows()) return;
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                Console.WriteLine("CPU: " + (key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "nome indisponível"));
                if (!GetSystemTimes(out var idle1, out var kernel1, out var user1)) throw new IOException("Não foi possível iniciar a leitura de CPU.");
                Thread.Sleep(600);
                if (!GetSystemTimes(out var idle2, out var kernel2, out var user2)) throw new IOException("Não foi possível concluir a leitura de CPU.");
                var usage = CpuPercent(new(idle1.Value, kernel1.Value, user1.Value), new(idle2.Value, kernel2.Value, user2.Value));
                Console.WriteLine(usage.HasValue ? $"Uso total de CPU (amostra de ~0,6 s): {usage:F1}%" : "Uso de CPU: indisponível nesta amostra.");
            });
            Section("RAM", () =>
            {
                var memory = new Memory { Length = (uint)Marshal.SizeOf<Memory>() };
                if (!GlobalMemoryStatusEx(ref memory)) throw new IOException("API de memória indisponível.");
                Console.WriteLine($"RAM: {memory.Load}% em uso; {GiB(memory.Total - memory.Available)} usados / {GiB(memory.Total)} totais; {GiB(memory.Available)} disponíveis.");
            });
            Console.WriteLine($"Processadores lógicos: {Environment.ProcessorCount}; tempo ligado: {TimeSpan.FromMilliseconds(Environment.TickCount64):g}");
            Section("Unidades", () =>
            {
                foreach (var drive in DriveInfo.GetDrives())
                    Section(drive.Name, () => Console.WriteLine(drive.IsReady ? $"{drive.Name}: {GiB((ulong)drive.AvailableFreeSpace)} livres / {GiB((ulong)drive.TotalSize)}" : $"{drive.Name}: sem mídia/desconectada."));
            });
            Console.WriteLine("Consultando placa de vídeo e sensores compatíveis...");
            Section("GPU/driver", () => Console.WriteLine(GameMode.Shell("Get-CimInstance Win32_VideoController -OperationTimeoutSec 3 -ErrorAction Stop | Select-Object Name,DriverVersion | Format-List | Out-String")));
            Section("Temperaturas", ReadTemperatures);
            Console.WriteLine("Uso de GPU e FPS: indisponíveis neste monitor. Nenhuma métrica é simulada.");
            Console.Write("\nEnter ou R: atualizar leituras | 0: voltar ao menu: ");
            var input = Console.ReadLine(); if (input is null || input.Trim() == "0") return;
        }
    }
    static void ReadTemperatures()
    {
        // Read existing sensor providers only. Never install a kernel driver implicitly.
        var json = GameMode.Shell("$r=@(); foreach($ns in @('root/LibreHardwareMonitor','root/OpenHardwareMonitor')) { try { $r+=@(Get-CimInstance -Namespace $ns -ClassName Sensor -OperationTimeoutSec 3 -ErrorAction Stop | Where-Object SensorType -eq 'Temperature' | Select-Object @{n='Provider';e={$ns}},Name,Identifier,Value) } catch {} }; ConvertTo-Json -InputObject @($r) -Compress");
        using var document = JsonDocument.Parse(json); var count = 0;
        foreach (var sensor in document.RootElement.EnumerateArray())
        {
            var name = sensor.GetProperty("Name").GetString() ?? "Sensor";
            var value = sensor.GetProperty("Value");
            double? temperature = value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) ? number : null;
            Console.WriteLine(Temperature(name, temperature) + $" — {sensor.GetProperty("Provider").GetString()} {sensor.GetProperty("Identifier").GetString()}");
            if (temperature.HasValue && double.IsFinite(temperature.Value)) count++;
        }
        if (count == 0)
            Console.WriteLine("Temperatura CPU/GPU: indisponível. Não há sensores legíveis dos provedores LibreHardwareMonitor/OpenHardwareMonitor. Se desejar, execute LibreHardwareMonitor obtido do projeto oficial e atualize esta tela; o acesso depende do hardware e pode exigir administrador nesse programa. Este otimizador não instala drivers nem inventa temperaturas.");
        else Console.WriteLine("Sensores fornecidos pelo programa externo; atualização e disponibilidade dependem dele. Sensores ausentes não são estimados.");
    }
    static string GiB(ulong value) => $"{value / 1073741824.0:F2} GiB";
    static void Section(string name, Action read)
    {
        try { read(); }
        catch (Exception ex) { Console.WriteLine($"{name}: indisponível — {ex.Message}"); Program.Log($"Monitor {name}: {ex.Message}"); }
    }
    [StructLayout(LayoutKind.Sequential)] struct FileTime { public uint Low, High; public readonly ulong Value => ((ulong)High << 32) | Low; }
    [StructLayout(LayoutKind.Sequential)] struct Memory { public uint Length, Load; public ulong Total, Available, PageTotal, PageAvailable, VirtualTotal, VirtualAvailable, Extended; }
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GlobalMemoryStatusEx(ref Memory memory);
}
