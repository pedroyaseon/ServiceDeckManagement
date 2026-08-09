# Service Manager alpha.4

## Função

`ServiceDeckManagement.Manager.exe` é um Serviço do Windows local e privilegiado.
Ele mantém as definições portáteis em `config/services`, registra cada definição
no SCM, executa start/stop/restart e fornece inventário à futura API. Não abre
porta TCP ou HTTP.

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

- ACL sem herança, permitindo somente LocalSystem e Administradores;
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
registro exigem `Administrator`. Na alpha.4, a ACL admite somente tokens
administrativos ou LocalSystem. A PR da API adicionará seu SID de serviço de
forma explícita e manterá autorização em duas camadas.

## O que ainda não está concluído

- cliente API e tempo real;
- provisionamento do SID da API na ACL;
- resolução de `secretReferences` pelo Host;
- instalador, upgrade, rollback e desinstalador do launcher;
- gate administrativo do SCM em VM descartável.
