# GAME BOOST PRO

Primeira etapa: aplicativo de **terminal**, inspirado na imagem de referência, com 20 opções em português. Requer Windows 10/11 para executar operações e SDK .NET 8 para desenvolver. Não requer execução permanente como administrador.

## Executar e testar

```sh
dotnet run --project src/GameBoostPro
dotnet run --project tests/GameBoostPro.Tests
dotnet publish src/GameBoostPro -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```

Abra `artifacts/win-x64/GameBoostPro.exe` no Windows. Não é um instalador. Em Linux, é possível compilar e testar a lógica; as operações Windows são bloqueadas. O teste de links simbólicos pode exigir Modo de Desenvolvedor no Windows.

## O que está implementado

- Menu no estilo do terminal da referência, com 20 opções e saída por 0.
- Limpeza real de arquivos .tmp na pasta temporária conhecida do usuário, com escolha de idade mínima de sete dias ou 24 horas, prévia e confirmação `LIMPAR`. Ignora links e só inclui subpastas com autorização explícita; arquivos em uso podem falhar. A exclusão é permanente.
- Consulta real de RAM disponível, processadores lógicos, tempo ligado e espaço em disco pelas APIs do sistema.
- Quatro medições ICMP para um host informado, média e perda da amostra. ICMP bloqueado não comprova falha da conexão do jogo.
- Abertura das ferramentas oficiais para recursos opcionais, desinstalação, Windows Update, energia, efeitos visuais, inicialização, segundo plano, Game Bar, gráficos e diagnóstico/privacidade. Disponibilidade depende da versão e das políticas do Windows. Abrir uma tela não significa aplicar otimização.
- Histórico local em `%LOCALAPPDATA%\GameBoostPro\history.log`, sem transmissão externa.

## Limitações desta etapa

Ainda não há WPF/MVVM, SQLite, perfis, monitoramento contínuo, gerenciador geral de processos, temperatura, uso de GPU ou medição de FPS. A restauração cobre somente o plano de energia alterado pelo Modo Jogo. O programa informa essas limitações; não apresenta métricas simuladas. Prefetch não é apagado. CPU, GPU, serviços, telemetria e registro não são alterados automaticamente. Relatórios de erro são encaminhados ao Painel de Controle para revisão manual. Cortana pode não existir nas versões atuais do Windows. Não há promessa de ganho de FPS ou ping.

As limpezas, a Lixeira e o Modo Jogo aplicam ações diretamente após confirmação; as demais configurações ficam a cargo do usuário nas ferramentas do Windows. Revise os efeitos antes de mudar recursos, aplicativos ou planos de energia. Antivírus, firewall, anticheats e drivers não são desativados pelo programa.

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

## [19] ANALISAR E LIMPAR SSD / [20] ANALISAR E LIMPAR HD

As duas entradas mantêm as opções anteriores e usam o mesmo fluxo de seleção de unidade. Não há servidor ou serviço novo para iniciar.

- **Unidades:** letra, nome, capacidade, espaço usado/livre e tipo de conexão/unidade. SSD/HD usa o provedor Storage do Windows (`Get-Partition` → `Get-Disk`, associação por `UniqueId` com `Get-PhysicalDisk`). Sem associação única, privilégios ou suporte do dispositivo, aparece **Tipo desconhecido**, com confirmação para seleção manual. Volumes virtuais, RAID e alguns USB podem não ter classificação. C: e D: não implicam tipo de mídia. Mídias ausentes são indicadas; rede e CD não oferecem limpeza local.
- **[1] Analisar espaço:** percorre somente a unidade escolhida, mostra até 20 maiores arquivos e 20 pastas, sem excluir nada. Q ou Ctrl+C cancela a análise; resultados cancelados são parciais. Links, junções/pontos de montagem internos, caminhos sem acesso e profundidade superior a 128 níveis são ignorados e contabilizados. Pastas aninhadas se sobrepõem; tamanhos são comprimentos lógicos, não uso físico exato (hardlinks, compressão e arquivos esparsos). Uma análise de disco inteiro pode demorar. Falhas de acesso são registradas no histórico local.
- **[2] Temporários do usuário:** somente `.tmp` em `%LOCALAPPDATA%\Temp` com idade mínima selecionável de sete dias (padrão) ou 24 horas, sem atributos de sistema/somente leitura e sem redirecionamento. Examina até 10000 entradas por execução e mostra os candidatos com caminho, tamanho e total estimado. Não usa uma variável TEMP arbitrária como autorização de exclusão. Se a pasta conhecida estiver em outra unidade, não é limpa nessa seleção. Subpastas podem ser incluídas explicitamente. Caminhos que indiquem shaders, saves, mods, anticheats, logs ou configurações são preservados; outros formatos, Downloads e documentos não são categorias de limpeza. A categoria começa desmarcada: digite `1` para selecionar e `LIMPAR` para confirmar. A exclusão é permanente. Antes de excluir, revalida caminho, idade, tamanho, data e ausência de links; abre com compartilhamento restrito e verifica o caminho final do handle. Usa `SetFileInformationByHandle` no arquivo aberto, sem exclusão recursiva. Arquivos alterados, em uso ou sem permissão são preservados. Esse critério é conservador e pode liberar pouco espaço; ele não declara inúteis arquivos arbitrários por idade/extensão.
- **[3] Aplicativos:** lê as entradas Uninstall de HKCU/HKLM nas visões 32/64 bits, sem `Win32_Product` e sem executar strings de desinstalação do registro. Pesquisa por nome/editor e exibe nome, editor, versão, localização e estimativa do instalador ou tamanho desconhecido. Itens confirmados na unidade são identificados separadamente dos de localização desconhecida; os confirmados em outra unidade também aparecem, identificados, para permitir encontrar o aplicativo. A opção principal [2] lista todas as unidades. Apps MSIX/Store podem não aparecer. A lista permite selecionar aplicativos para revisão manual no Windows sem a antiga limitação a cinco produtos. Entradas sinalizadas como componentes de sistema, atualizações ou remoção proibida e nomes que indiquem drivers, segurança, anticheats ou runtimes são protegidos. Nenhuma string de desinstalação desconhecida é executada pelo programa. Selecione um por vez e digite `DESINSTALAR`: abre a ferramenta Aplicativos do Windows, onde você procura e confirma a remoção. Não apaga pastas e não afirma que o aplicativo foi removido. Dados e configurações podem ser perdidos pelo desinstalador.
- **[4] Lixeira:** digite `ESVAZIAR`; o Windows apresenta também sua confirmação. `SHEmptyRecycleBin` recebe somente a raiz selecionada. Nunca apaga manualmente `$Recycle.Bin`. A exclusão é permanente e falhas/cancelamentos não são tratados como sucesso.
- **[5] Limpeza oficial:** `ABRIR` inicia `cleanmgr.exe /d <unidade>`, sem opções automáticas. Temporários do Windows/componentes são tratados ali; nenhuma pasta protegida é apagada diretamente. Só o Windows solicita elevação ao escolher limpeza de sistema. Revise as categorias e não selecione arquivos pessoais. O otimizador não roda permanentemente como administrador.
- **[6] Otimizar Unidades:** abre `dfrgui.exe` para revisão manual na ferramenta oficial. Não executa desfragmentação nem manutenção automática do SSD/HD.

Após cada operação, mostra espaço livre antes e depois na mesma unidade. A diferença pode incluir atividade de outros processos, e não mede ganho de FPS. Ao abrir uma ferramenta externa de limpeza/desinstalação, conclua ou cancele nela e pressione Enter no otimizador para medir novamente; o otimizador não confirma o resultado dessa ferramenta.

### Validação das opções 19/20

No Linux: testes com arquivos reais isolados verificam análise, soma/ordenação, preservação de dados pessoais, cancelamento, fronteira da raiz e links simbólicos. O pipeline de exclusão usa um executor de teste para validar confirmação, revalidação e preservação, com erros de acesso/arquivo em uso **simulados**. Testa também política conservadora de aplicativos, localização desconhecida e cancelamento/falha ao abrir a ferramenta de desinstalação. Isso não valida bloqueios, permissões ACL nem exclusão por handle no Windows.

**Pendentes em Windows 10/11:** classificação real de SSD/HD/USB/RAID; unidade desconectada; análise em NTFS com junções/mountpoints e diretórios sem acesso; cancelamento por Q/Ctrl+C; exclusão em pasta temporária com arquivo real em uso e sem permissão; confirmação independente da Lixeira de uma única unidade; leitura do registro, abertura de Aplicativos e cleanmgr; verificação de que só os candidatos aprovados são removidos e de que dados pessoais, outros volumes e shaders permanecem intactos. Nenhuma dessas operações Windows foi executada no ambiente Linux.

### Correção de exclusão e acesso aos aplicativos

A opção principal **[2] Desinstalar apps** agora exibe a mesma lista pesquisável das opções 19/20, sem restringir a revisão manual a cinco aplicativos. A confirmação abre Aplicativos do Windows; a remoção efetiva é feita pelo usuário nessa tela. Componentes protegidos não são oferecidos.

A opção principal **[4] Limpar temporários** agora usa a limpeza .tmp com prévia, escolha de 7 dias/24 horas e inclusão opcional de subpastas de Temp. Arquivos em uso continuam preservados. A abertura nativa foi corrigida para solicitar `DELETE` além de leitura antes de `SetFileInformationByHandle`; anteriormente a ausência desse acesso podia impedir a exclusão no Windows. Usa `OPEN_REPARSE_POINT` e verifica o caminho final. Essa correção foi compilada para Windows, mas ainda exige teste real de exclusão/ACL/arquivo em uso no Windows.

## [9] Monitorar hardware e temperatura

A tela de hardware agora permanece aberta: **Enter/R** consulta uma nova amostra; **0** volta ao menu. Não é atualização automática. Exibe nome do processador, percentual total de CPU por diferença de `GetSystemTimes` (amostra de aproximadamente 0,6 segundo), RAM em uso/disponível/total, discos, tempo ligado e nome/driver da GPU. Cada seção trata falhas separadamente, para que uma unidade ou consulta indisponível não esconda o restante. A tela informa quando está consultando provedores.

Temperaturas são lidas somente se um programa externo já disponibilizar sensores WMI em `root/LibreHardwareMonitor` ou `root/OpenHardwareMonitor`. Fonte oficial opcional: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases . Execute esse programa separadamente e atualize a opção 9; o acesso pode exigir administrador no programa de sensores e depende do hardware/versão. O otimizador não instala nem carrega drivers de sensores. Sensores ausentes/inacessíveis são exibidos como indisponíveis, não como zero. A atualidade das leituras depende do provedor externo. Uso de GPU e FPS continuam indisponíveis.

Os testes Linux verificam o cálculo de CPU e a apresentação de temperaturas ausentes/não finitas. Leitura nativa de CPU/RAM/discos/registro, consultas CIM e integração com sensores WMI ainda precisam ser verificadas no Windows. Antes de considerar esta opção validada, testar Enter/R e 0, sensores presentes/ausentes, falta de permissão, PowerShell indisponível e uma unidade inacessível, garantindo que as demais seções continuam visíveis.
