# Service Host 1.0.0-alpha.3

## Estado

O Service Host está implementado e testado no Windows. Ele ainda não instala a
si próprio como Serviço do Windows; registro, atualização e remoção no SCM serão
responsabilidade exclusiva do Manager.

O executável aceita somente:

```text
ServiceDeckManagement.Host.exe --service-id <id>
```

O identificador localiza `config/services/<id>.json` sob a raiz portátil. O nome
interno da definição deve ser idêntico ao nome do arquivo.

## Inicialização

1. localiza `.servicedeck-root` a partir do diretório do Host;
2. lê no máximo 1 MiB de JSON UTF-8 válido;
3. desserializa o schema v1 de forma estrita;
4. valida identidade, caminhos, argumentos, ambiente e limites;
5. rejeita referências de segredo até que o Manager possa resolvê-las com
   proteção local;
6. revalida executável e diretório imediatamente antes de iniciar;
7. executa o `.exe` diretamente com `UseShellExecute = false` e `ArgumentList`;
8. associa o processo a um Windows Job Object;
9. inicia captura assíncrona de stdout e stderr.

Não existe passagem por `cmd.exe`, PowerShell, associação de arquivos ou linha
de comando concatenada.

## Processos e parada

O Job Object usa `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`. Processos filhos normais
permanecem na mesma árvore e são encerrados quando o Host fecha o job.

Na parada, o Host tenta primeiro fechar a janela principal da aplicação e
aguarda `gracefulTimeoutSeconds`. Aplicações sem janela ou protocolo cooperativo
podem não responder a essa tentativa; após o timeout, toda a árvore é encerrada
pelo Job Object. `terminateTree` deve ser `true` na versão 1.

Job Objects fornecem controle de ciclo de vida, não isolamento de segurança. O
produto não deve executar aplicações não confiáveis como se fossem sandbox.

Referências oficiais:

- [Windows Service com BackgroundService](https://learn.microsoft.com/dotnet/core/extensions/windows-service)
- [Job Objects](https://learn.microsoft.com/windows/win32/procthread/job-objects)
- [AssignProcessToJobObject](https://learn.microsoft.com/windows/win32/api/jobapi2/nf-jobapi2-assignprocesstojobobject)

## Logs

- stdout, stderr e eventos do supervisor usam JSON Lines;
- cada entrada contém timestamp UTC, sequência, origem e mensagem;
- sequências CSI, OSC e outros controles de terminal são removidos;
- o caractere Unicode de substituição não é persistido;
- linhas e mensagens são truncadas em limites definidos;
- os arquivos rotacionam por tamanho e respeitam retenção e limite total;
- os arquivos ficam em `logs/services/<id>/`.

Aplicações gerenciadas devem emitir UTF-8. O Host decodifica os streams como
UTF-8 e remove caracteres inválidos; ele não tenta adivinhar páginas de código,
pois isso poderia gravar texto aparentemente válido, mas incorreto.

## Reinício e health checks

Saídas inesperadas seguem `restartPolicy`. O atraso dobra até
`maximumDelaySeconds`; após `maximumAttempts`, o Host encerra com falha. Uma
execução estável por `resetAfterMinutes` reinicia o contador.

Health checks suportados:

- `process`: confirma que o processo principal continua ativo;
- `http`: GET em URL HTTP ou HTTPS de loopback, sem proxy ou redirecionamento;
- `tcp`: conexão em host de loopback e porta validada.

Uma falha de health check é registrada como estado degradado observável nos
logs. O contrato atual não encerra automaticamente um processo apenas por falha
de health check.

## Limites desta etapa

- nenhum registro é criado ou alterado no SCM;
- não existem API, Named Pipe, autenticação, launcher ou dashboard;
- pause e continue não são expostos porque não há semântica cooperativa comum
  para aplicações arbitrárias;
- a validação completa em máquina limpa ocorrerá no hardening da v1.
