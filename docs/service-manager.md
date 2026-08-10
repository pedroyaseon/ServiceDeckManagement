# Service Manager v1

## Função

`ServiceDeckManagement.Manager.exe` é um Serviço do Windows local e privilegiado.
Ele mantém as definições portáteis em `config/services`, registra cada definição
no SCM, executa start/stop/restart e fornece inventário ao Launcher local e à API
opcional. Não abre porta TCP ou HTTP.

Na beta.4, `ServiceDeckManagement.Setup.exe` registra ou repara o Manager após
confirmação explícita do UAC. O helper valida o layout portátil, preserva o SID
opcional da API, autoriza o usuário do Launcher e inicia o serviço. O Launcher
não acessa o SCM diretamente.

## Registro no SCM

Cada aplicação usa:

```text
ServiceDeckManagement.Managed.<id>
"<raiz>\app\ServiceDeckManagement.Host.exe" --service-id <id>
```

A descrição `ServiceDeckManagement:v1:<id>` funciona como marcador adicional.
Antes de start, stop, restart, update ou remove, o Manager consulta a configuração
e recusa a operação se a identidade não corresponder. `repair` pode restaurar os
campos mutáveis somente quando nome e marcador continuam válidos.

O adaptador usa diretamente `OpenSCManager`, `CreateService`,
`ChangeServiceConfig`, `QueryServiceConfig`, `QueryServiceStatusEx`,
`StartService`, `ControlService` e `DeleteService`. A lista oficial dessas APIs
está na [documentação de funções de serviço do Windows](https://learn.microsoft.com/windows/win32/services/service-functions).

## Persistência e auditoria

- JSON estrito, UTF-8 e até 1 MiB por definição;
- gravação em arquivo temporário no mesmo diretório, `WriteThrough`, flush e
  substituição atômica;
- rejeição de IDs inválidos, arquivos especiais e reparse points;
- auditoria JSONL append-only em `data/manager/audit-v1.jsonl`;
- cada evento contém o hash do anterior e seu próprio SHA-256;
- uma cadeia alterada faz o Manager falhar de modo fechado ao anexar novo evento.

A cadeia detecta dano e alteração acidental. Um administrador local continua
capaz de substituir código e dados, portanto ela não é apresentada como defesa
contra administrador malicioso.

## Named Pipe v1

Nome: `ServiceDeckManagement.Manager.v1`.

Controles:

- ACL sem herança, permitindo LocalSystem, Administradores e os SIDs explícitos
  dos clientes Launcher e API;
- `PIPE_REJECT_REMOTE_CLIENTS` para rejeitar conexões originadas pela rede;
- no máximo oito instâncias e sessão de 15 segundos;
- frame `uint32 little-endian + payload`, limitado a 64 KiB;
- JSON estrito e uma requisição por conexão;
- desafio aleatório de 256 bits;
- prova HMAC-SHA-256 do servidor e do cliente;
- chave protegida por DPAPI da máquina, dentro de diretório com ACL restrita;
- SID e papel obtidos por impersonação do token do Windows;
- autorização repetida no dispatcher do Manager.

O Windows oferece ACL explícita por `NamedPipeServerStreamAcl`, conforme a
[documentação oficial do .NET](https://learn.microsoft.com/dotnet/api/system.io.pipes.namedpipeserverstreamacl),
e define o bloqueio remoto em
[`CreateNamedPipe`](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-createnamedpipea).
A identidade é obtida apenas após autenticação do cliente do pipe, seguindo a
[orientação de impersonação](https://learn.microsoft.com/windows/win32/ipc/impersonating-a-named-pipe-client).

## Operações do protocolo

- `ping`
- `inventory.list`
- `service.details`
- `service.create`
- `service.update`
- `service.remove`
- `service.start`
- `service.stop`
- `service.restart`
- `service.repair`

Leitura exige `Viewer`; operações de execução exigem `Operator`; alterações de
registro exigem `Administrator`. O Launcher autenticado pelo SID explícito é um
cliente administrativo local. Campos de identidade e função enviados por ele no
payload são ignorados. A API usa outro SID e só pode delegar uma identidade
autenticada, que o Manager autoriza novamente.

`config/manager-security.json` exige `launcherClientSid` e `apiClientSid`; cada
campo pode ser `null` enquanto o componente correspondente não estiver
provisionado. SIDs genéricos privilegiados e o mesmo SID nos dois campos são
recusados. A ACL da chave de transporte concede leitura somente aos SIDs
configurados.

## O que ainda não está concluído

- resolução de `secretReferences` pelo Host;
- instalador, upgrade, rollback e desinstalador do launcher;
- gate administrativo do SCM em VM descartável.
