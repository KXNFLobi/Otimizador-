using GameBoostPro;

static class StorageTests
{
    public static void Run(string workspace, Action<bool, string> check)
    {
        var root = Path.Combine(workspace, "storage"); Directory.CreateDirectory(root);
        var nested = Directory.CreateDirectory(Path.Combine(root, "personal"));
        var outside = Directory.CreateDirectory(Path.Combine(workspace, "outside"));
        File.WriteAllText(Path.Combine(root, "small.txt"), "abc");
        File.WriteAllText(Path.Combine(nested.FullName, "save.dat"), "12345");
        File.WriteAllText(Path.Combine(outside.FullName, "private.txt"), "do not follow");
        Directory.CreateSymbolicLink(Path.Combine(root, "junction"), outside.FullName);
        File.CreateSymbolicLink(Path.Combine(root, "linked.txt"), Path.Combine(outside.FullName, "private.txt"));
        check(StorageSafety.Within(root, Path.GetPathRoot(root)!), "seleção aceita caminhos na raiz real do volume");
        check(!StorageSafety.Within(root + "-other", root), "seleção rejeita prefixo semelhante de outra pasta");
        check(StorageMenu.SelectIndex("2", 2) == 1 && StorageMenu.SelectIndex("3", 2) == -1 && StorageMenu.SelectIndex(null, 2) == -1, "seleção de unidade e cancelamento são limitados à lista");
        var scan = StorageSafety.Scan(root, CancellationToken.None);
        check(scan.Count == 2 && scan.Bytes == 8 && scan.Errors == 2, "análise conta arquivos reais e ignora links/pontos redirecionados");
        check(scan.Files[0].Size == 5 && scan.Folders.Any(f => f.Path == nested.FullName && f.Size == 5), "análise ordena maiores arquivos e soma pastas");
        check(File.Exists(Path.Combine(nested.FullName, "save.dat")), "análise preserva arquivos pessoais");
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        check(StorageSafety.Scan(root, cancel.Token).Cancelled, "análise pode ser cancelada sem alterações");
        var temp = Directory.CreateDirectory(Path.Combine(workspace, "known-temp")).FullName;
        DiskFile Create(string path, int age = 10)
        {
            File.WriteAllText(path, "tmp"); File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-age));
            var f = new FileInfo(path); return new(path, f.Length, f.LastWriteTimeUtc);
        }
        var old = Create(Path.Combine(temp, "old.tmp"));
        var recent = Create(Path.Combine(temp, "recent.tmp"), 1);
        var personal = Create(Path.Combine(temp, "photo.jpg"));
        var sub = Directory.CreateDirectory(Path.Combine(temp, "sub"));
        var subFile = Create(Path.Combine(sub.FullName, "save.tmp"));
        var external = Create(Path.Combine(outside.FullName, "outside.tmp"));
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var planned = new[] { old, recent, personal, subFile, external };
        int called = 0;
        var result = StorageSafety.Clean(planned, temp, workspace, cutoff, "", _ => called++);
        check(called == 0 && result.Skipped == 5 && File.Exists(old.Path), "limpeza cancelada não toca nenhum arquivo");
        result = StorageSafety.Clean(planned, temp, outside.FullName, cutoff, "LIMPAR", _ => called++);
        check(called == 0 && result.Skipped == 5, "limpeza não sai da unidade/raiz selecionada");
        result = StorageSafety.Clean(planned, temp, workspace, cutoff, "LIMPAR", _ => called++);
        check(called == 1 && result.Deleted == 1 && result.Skipped == 4, "somente candidato antigo direto é encaminhado à exclusão");
        var linked = Path.Combine(temp, "alias.tmp"); File.CreateSymbolicLink(linked, old.Path);
        check(!StorageSafety.Temporary(new(linked, old.Size, old.Modified), temp, cutoff), "temporário com link é preservado");
        File.AppendAllText(old.Path, "changed");
        result = StorageSafety.Clean([old], temp, workspace, cutoff, "LIMPAR", _ => called++);
        check(result.Skipped == 1 && called == 1, "arquivo alterado após prévia é revalidado e preservado");
        var denied = Create(Path.Combine(temp, "denied.tmp"));
        result = StorageSafety.Clean([denied], temp, workspace, cutoff, "LIMPAR", _ => throw new UnauthorizedAccessException("simulada"));
        check(result.Failed == 1 && File.Exists(denied.Path), "falha de permissão simulada é registrada sem excluir arquivo");
        result = StorageSafety.Clean([denied], temp, workspace, cutoff, "LIMPAR", _ => throw new IOException("arquivo em uso simulado"));
        check(result.Failed == 1 && File.Exists(denied.Path), "arquivo em uso simulado é preservado sem tentativa forçada");
        var disposable = Create(Path.Combine(temp, "disposable.tmp"));
        result = StorageSafety.Clean([disposable], temp, workspace, cutoff, "LIMPAR", f => File.Delete(f.Path));
        check(result.Deleted == 1 && !File.Exists(disposable.Path) && File.Exists(personal.Path) && File.Exists(subFile.Path), "pipeline exclui candidato autorizado e mantém dados pessoais/subpastas");
        check(InstalledApps.Optional("Google Chrome", "Google LLC", false, false, false), "aplicativo conhecido permite revisão manual");
        foreach (var name in new[] { "Windows Defender", "AMD Driver", "EasyAntiCheat", "Microsoft Visual C++", "Unknown" })
            check(!InstalledApps.Optional(name, "Microsoft", false, false, false), name + " não é candidato de desinstalação");
        check(!InstalledApps.Optional("Google Chrome", "Google LLC", true, false, false) && !InstalledApps.Optional("Google Chrome", "Google LLC", false, true, false), "componentes e atualizações preservados mesmo com nome conhecido");
        var app = new InstalledApp("Google Chrome", "Google LLC", "1", root, null, true);
        check(InstalledApps.LocationGroup(app, root) == "Na unidade escolhida" && InstalledApps.LocationGroup(app with { Location = "" }, root) == "Localização desconhecida", "localização confirmada é separada de desconhecida");
        int opened = 0;
        check(!InstalledApps.RequestRemoval(app, "", () => opened++) && opened == 0, "cancelamento não abre desinstalação");
        check(!InstalledApps.RequestRemoval(app with { Optional = false }, "DESINSTALAR", () => opened++) && opened == 0, "app protegido não permite solicitação");
        bool failed = false; try { InstalledApps.RequestRemoval(app, "DESINSTALAR", () => throw new IOException("Windows indisponível")); } catch (IOException) { failed = true; }
        check(failed && opened == 0, "falha ao abrir desinstalação não é tratada como sucesso");
        check(InstalledApps.RequestRemoval(app, "DESINSTALAR", () => opened++) && opened == 1, "confirmação apenas abre ferramenta oficial uma vez");
    }
}
