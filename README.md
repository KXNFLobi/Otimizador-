# GAME BOOST PRO

Primeira etapa: aplicativo de **terminal**, inspirado na imagem de referência, com 18 opções em português. Requer Windows 10/11 para executar operações e SDK .NET 8 para desenvolver. Não requer execução permanente como administrador.

## Executar e testar

```sh
dotnet run --project src/GameBoostPro
dotnet run --project tests/GameBoostPro.Tests
dotnet publish src/GameBoostPro -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```

Abra `artifacts/win-x64/GameBoostPro.exe` no Windows. Não é um instalador. Em Linux, é possível compilar e testar a lógica; as operações Windows são bloqueadas. O teste de links simbólicos pode exigir Modo de Desenvolvedor no Windows.

## O que está implementado

- Menu no estilo do terminal da referência, com 18 opções e saída por 0.
- Limpeza real de arquivos diretamente na pasta temporária do usuário, com mais de sete dias, prévia e confirmação `LIMPAR`. Ignora links e não percorre subpastas; arquivos em uso podem falhar. A exclusão é permanente.
- Consulta real de RAM disponível, processadores lógicos, tempo ligado e espaço em disco pelas APIs do sistema.
- Quatro medições ICMP para um host informado, média e perda da amostra. ICMP bloqueado não comprova falha da conexão do jogo.
- Abertura das ferramentas oficiais para recursos opcionais, desinstalação, Windows Update, energia, efeitos visuais, inicialização, segundo plano, Game Bar, gráficos e diagnóstico/privacidade. Disponibilidade depende da versão e das políticas do Windows. Abrir uma tela não significa aplicar otimização.
- Histórico local em `%LOCALAPPDATA%\GameBoostPro\history.log`, sem transmissão externa.

## Limitações desta etapa

Ainda não há WPF/MVVM, SQLite, perfis, monitoramento contínuo, gerenciador geral de processos, temperatura, uso de GPU ou medição de FPS. A restauração cobre somente o plano de energia alterado pelo Modo Jogo. O programa informa essas limitações; não apresenta métricas simuladas. Prefetch não é apagado. CPU, GPU, serviços, telemetria e registro não são alterados automaticamente. Relatórios de erro são encaminhados ao Painel de Controle para revisão manual. Cortana pode não existir nas versões atuais do Windows. Não há promessa de ganho de FPS ou ping.

A limpeza e o Modo Jogo aplicam ações diretamente após confirmação; as demais configurações ficam a cargo do usuário nas ferramentas do Windows. Revise os efeitos antes de mudar recursos, aplicativos ou planos de energia. Antivírus, firewall, anticheats e drivers não são desativados pelo programa.

A execução real das ferramentas e APIs precisa de validação em Windows 10/11; compilar em Linux não valida esse comportamento.

## [18] MODO JOGO — FPS E ESTABILIDADE

Uma única opção reúne a preparação para jogar:

1. Informe o caminho completo de um `.exe` existente (sem argumentos). O programa mostra RAM disponível, espaço no disco do jogo, plano ativo (GUID) e driver de vídeo via CIM.
2. Opcionalmente autorize a consulta local ao log Application, eventos 1000/1001 dos últimos sete dias. A consulta examina até 2000 eventos e mostra até dez correspondências por nome/caminho. Nomes iguais podem ser ambíguos; a ausência de eventos não comprova estabilidade.
3. Selecione navegadores opcionais por número ou pressione Enter para preservar todos. Somente Chrome, Edge e Firefox com janela visível, na sessão atual, nome original correspondente e assinatura válida do editor conhecido são oferecidos. Links/junções no caminho e identidades inacessíveis são preservados. Outros aplicativos e processos essenciais não são oferecidos. Consumo exibido: memória de trabalho por processo, não CPU nem memória total de todos os subprocessos do navegador.
4. Autorize, se desejar, o plano padrão Alto desempenho **já existente**. O programa não cria planos e não altera configurações de GPU, prioridades ou serviços. Computadores Modern Standby podem não oferecer esse plano.
5. Revise a prévia e digite exatamente `APLICAR`. Qualquer outra resposta cancela. Aplicativos selecionados recebem somente uma solicitação normal de fechamento; podem permanecer abertos pedindo para salvar dados. Não há encerramento forçado nem restauração das sessões dos aplicativos.
6. O jogo é aberto pelo Windows. Alguns jogos exigem inicialização pelo launcher; falhas são registradas. Pressione Enter no otimizador para encerrar o modo e restaurar o plano anterior; isso não fecha o jogo. Fechar o jogo não encerra automaticamente o modo, pois launchers podem trocar de processo.

Antes de alterar energia, o plano anterior é gravado em `%LOCALAPPDATA%\GameBoostPro\pending-power.json`. Se o programa fechar inesperadamente, reabra e digite `RESTAURAR` quando solicitado. Uma nova sessão não começa enquanto houver restauração pendente. Falhas de restauração mantêm esse arquivo para nova tentativa. Uma única instância pode executar por vez. Não apague o arquivo de recuperação enquanto houver alteração pendente.

Pressão de memória e pouco espaço geram recomendações baseadas nas leituras atuais, sem afirmar causa definitiva de crashes. Use a verificação de arquivos do launcher e drivers oficiais. O programa não apaga eventos nem modifica arquivos do jogo. Não há medição de FPS, nem promessa de aumentar FPS ou eliminar crashes.

### Validação

Os testes automatizados em Linux exercitam confirmação/cancelamento sem efeitos, política conservadora de identidade, gravação anterior à mudança de energia, recuperação por nova instância, falha de abertura do jogo, restauração e erros de persistência. Não substituem validação de APIs Windows.

Antes de distribuir como validado em Windows 10/11, conferir em uma conta comum:

- Caminho com espaços; cancelamento na prévia deve preservar aplicativos e energia.
- RAM/disco/driver reais; PowerShell ou eventos indisponíveis devem mostrar a limitação.
- Navegadores reconhecidos selecionados: fechamento normal, incluindo janela com trabalho não salvo; processos desconhecidos e serviços nunca oferecidos.
- Plano Alto desempenho disponível e indisponível; confirmar GUID antes, durante e após a sessão.
- Interrupção do otimizador durante a sessão e recuperação na próxima abertura; falha de acesso ao plano deve manter o registro pendente.
- Jogo que abre por launcher; falha de abertura e diagnóstico com e sem eventos correspondentes.

Nenhuma dessas verificações de Windows foi executada no ambiente Linux.
