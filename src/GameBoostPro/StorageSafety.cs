using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GameBoostPro;

public sealed record DiskFile(string Path, long Size, DateTime Modified);
public sealed record DiskScan(IReadOnlyList<DiskFile> Files, IReadOnlyList<DiskFile> Folders, long Bytes, long Count, long Errors, bool Cancelled);
public sealed record CleanupResult(int Deleted, int Skipped, int Failed);

public static class StorageSafety
{
    static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    public static bool Within(string path, string root)
    {
        var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var child = Path.GetFullPath(path);
        return child.Equals(parent, Comparison) || child.StartsWith(Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar, Comparison);
    }
    public static bool NoLinks(string path)
    {
        FileSystemInfo? item = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        while (item is not null)
        {
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0) return false;
            item = item is FileInfo file ? file.Directory : ((DirectoryInfo)item).Parent;
        }
        return true;
    }
    static void Top(List<DiskFile> list, DiskFile item)
    {
        list.Add(item); list.Sort((a, b) => b.Size.CompareTo(a.Size));
        if (list.Count > 20) list.RemoveAt(20);
    }
    sealed class Frame(string path, IEnumerator<string> entries)
    {
        public string Path = path;
        public IEnumerator<string> Entries = entries;
        public long Bytes;
    }
    public static DiskScan Scan(string root, CancellationToken token)
    {
        root = Path.GetFullPath(root);
        if (!NoLinks(root)) throw new IOException("A raiz é um link ou ponto de montagem redirecionado.");
        var files = new List<DiskFile>(); var folders = new List<DiskFile>();
        var stack = new Stack<Frame>(); long count = 0, errors = 0, bytes = 0;
        bool cancelled = false;
        try
        {
            stack.Push(new(root, Directory.EnumerateFileSystemEntries(root).GetEnumerator()));
            while (stack.Count > 0)
            {
                if (token.IsCancellationRequested) { cancelled = true; break; }
                var frame = stack.Peek(); string? path = null;
                try { if (frame.Entries.MoveNext()) path = frame.Entries.Current; }
                catch (Exception ex) when (AccessError(ex)) { errors++; Program.Log("Análise: " + frame.Path + ": " + ex.Message); }
                if (path is null)
                {
                    stack.Pop().Entries.Dispose();
                    if (stack.Count > 0) { stack.Peek().Bytes += frame.Bytes; Top(folders, new(frame.Path, frame.Bytes, default)); }
                    continue;
                }
                try
                {
                    if (!Within(path, root) || !NoLinks(path)) { errors++; continue; }
                    var attr = File.GetAttributes(path);
                    if ((attr & FileAttributes.Directory) != 0)
                    {
                        if (stack.Count >= 128) { errors++; continue; }
                        stack.Push(new(path, Directory.EnumerateFileSystemEntries(path).GetEnumerator()));
                    }
                    else
                    {
                        var info = new FileInfo(path); var size = info.Length;
                        count++; bytes += size; frame.Bytes += size;
                        Top(files, new(path, size, info.LastWriteTimeUtc));
                    }
                }
                catch (Exception ex) when (AccessError(ex)) { errors++; Program.Log("Análise: " + path + ": " + ex.Message); }
            }
        }
        finally { foreach (var frame in stack) frame.Entries.Dispose(); }
        return new(files, folders, bytes, count, errors, cancelled);
    }
    public static bool AccessError(Exception ex) => ex is IOException or UnauthorizedAccessException or System.Security.SecurityException;
    public static bool Temporary(DiskFile file, string root, DateTime cutoff)
    {
        // Deliberately only direct .tmp files: no game caches, logs or subdirectories.
        if (!Within(file.Path, root) || !string.Equals(Path.GetDirectoryName(Path.GetFullPath(file.Path)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), Comparison)) return false;
        if (!Path.GetExtension(file.Path).Equals(".tmp", StringComparison.OrdinalIgnoreCase) || !NoLinks(file.Path)) return false;
        var info = new FileInfo(file.Path);
        return info.Exists && info.Length == file.Size && info.LastWriteTimeUtc == file.Modified && info.LastWriteTimeUtc < cutoff &&
            (info.Attributes & (FileAttributes.Directory | FileAttributes.ReadOnly | FileAttributes.System)) == 0;
    }
    public static CleanupResult Clean(IEnumerable<DiskFile> files, string root, string volume, DateTime cutoff, string? confirmation,
        Action<DiskFile>? remove = null)
    {
        int deleted = 0, skipped = 0, failed = 0;
        var list = files.DistinctBy(f => f.Path).ToArray();
        if (confirmation != "LIMPAR") return new(0, list.Length, 0);
        foreach (var file in list)
        {
            try
            {
                if (!Within(root, volume) || !Temporary(file, root, cutoff)) { skipped++; continue; }
                (remove ?? DeleteLocked)(file);
                deleted++; Program.Log("Temporário excluído: " + file.Path);
            }
            catch (Exception ex) when (AccessError(ex) || ex is Win32Exception)
            { failed++; Program.Log("Temporário preservado/falha: " + file.Path + ": " + ex.Message); }
        }
        return new(deleted, skipped, failed);
    }
    static void DeleteLocked(DiskFile snapshot)
    {
        // Never retry with broader sharing: files in use remain untouched.
        using var file = new FileStream(snapshot.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete);
        if (file.Length != snapshot.Size || File.GetLastWriteTimeUtc(snapshot.Path) != snapshot.Modified || !NoLinks(snapshot.Path)) throw new IOException("Arquivo alterado após a prévia.");
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Exclusão por handle exige Windows.");
        var final = new System.Text.StringBuilder(32768);
        var length = GetFinalPathNameByHandle(file.SafeFileHandle, final, (uint)final.Capacity, 0);
        var expected = Path.GetFullPath(snapshot.Path);
        var actual = final.ToString(); if (actual.StartsWith(@"\\?\UNC\")) actual = @"\\" + actual[8..]; else if (actual.StartsWith(@"\\?\")) actual = actual[4..];
        if (length == 0 || length >= final.Capacity || !actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("Identidade do arquivo não confirmada.");
        var disposition = new Disposition { Delete = true };
        if (!SetFileInformationByHandle(file.SafeFileHandle, 4, ref disposition, (uint)Marshal.SizeOf<Disposition>())) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    [StructLayout(LayoutKind.Sequential)] struct Disposition { [MarshalAs(UnmanagedType.U1)] public bool Delete; }
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetFileInformationByHandle(SafeFileHandle handle, int infoClass, ref Disposition info, uint size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, System.Text.StringBuilder name, uint size, uint flags);
}
