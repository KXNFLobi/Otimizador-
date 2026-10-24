# GAME BOOST PRO

Primeira etapa: aplicativo de **terminal**, inspirado na imagem de referência, com 17 opções em português. Requer Windows 10/11 para executar operações e SDK .NET 8 para desenvolver. Não requer execução permanente como administrador.

## Executar e testar

```sh
dotnet run --project src/GameBoostPro
dotnet run --project tests/GameBoostPro.Tests
dotnet publish src/GameBoostPro -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```

Abra `artifacts/win-x64/GameBoostPro.exe` no Windows. Não é um instalador. Em Linux, é possível compilar e testar a lógica; as operações Windows são bloqueadas. O teste de links simbólicos pode exigir Modo de Desenvolvedor no Windows.

## O que está implementado

- Menu no estilo do terminal da referência, com 17 opções e saída por 0.
- Limpeza real de arquivos diretamente na pasta temporária do usuário, com mais de sete dias, prévia e confirmação `LIMPAR`. Ignora links e não percorre subpastas; arquivos em uso podem falhar. A exclusão é permanente.
- Consulta real de RAM disponível, processadores lógicos, tempo ligado e espaço em disco pelas APIs do sistema.
- Quatro medições ICMP para um host informado, média e perda da amostra. ICMP bloqueado não comprova falha da conexão do jogo.
- Abertura das ferramentas oficiais para recursos opcionais, desinstalação, Windows Update, energia, efeitos visuais, inicialização, segundo plano, Game Bar, gráficos e diagnóstico/privacidade. Disponibilidade depende da versão e das políticas do Windows. Abrir uma tela não significa aplicar otimização.
- Histórico local em `%LOCALAPPDATA%\GameBoostPro\history.log`, sem transmissão externa.

## Limitações desta etapa

Ainda não há WPF/MVVM, SQLite, perfis, restauração de ajustes, monitoramento contínuo, controle de processos, temperatura, GPU ou medição de FPS. O programa informa essas limitações; não apresenta métricas simuladas. Prefetch não é apagado. CPU, GPU, serviços, telemetria e registro não são alterados automaticamente. Relatórios de erro são encaminhados ao Painel de Controle para revisão manual. Cortana pode não existir nas versões atuais do Windows. Não há promessa de ganho de FPS ou ping.

Somente a limpeza aplica uma alteração diretamente; as demais configurações ficam a cargo do usuário nas ferramentas do Windows. Revise os efeitos antes de mudar recursos, aplicativos ou planos de energia. Antivírus, firewall, anticheats e drivers não são desativados pelo programa.

A execução real das ferramentas e APIs precisa de validação em Windows 10/11; compilar em Linux não valida esse comportamento.
