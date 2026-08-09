# ADR 0004 — autenticação e limite local da API

## Status

Aceito para `1.0.0-beta.1`.

## Decisão

A primeira API pública usa ASP.NET Core, SQLite local e sessões opacas. Senhas
são derivadas pelo `PasswordHasher` do ASP.NET Core; somente o hash SHA-256 do
token de sessão é persistido. O primeiro administrador é criado por um código
aleatório, temporário, de uso único e aceito apenas em loopback.

A API escuta exclusivamente em `127.0.0.1` nesta versão. Habilitar acesso remoto
será uma decisão posterior, condicionada a HTTPS, política de origem explícita e
testes de implantação. A API não acessa o SCM.

O Manager autoriza um SID dedicado da API no Named Pipe. Requisições originadas
desse SID carregam o identificador e a função da sessão. O Manager não confia
somente na autorização HTTP: ele valida novamente a função para cada operação.
Clientes administrativos diretos continuam derivados do token do Windows e
ignoram qualquer identidade declarada no payload.

## Consequências

- o Launcher pode operar sem elevação e sem conhecer o protocolo privilegiado;
- comprometer uma sessão `viewer` não concede ações de `operator` ou
  `administrator` no Manager;
- o instalador futuro precisa criar a identidade dedicada e gravar seu SID na
  configuração local;
- configuração ausente mantém o pipe restrito a LocalSystem e Administradores;
- exposição remota não pode ser ativada silenciosamente por variável ou JSON;
- lacunas nos eventos SignalR são recuperadas por um snapshot REST sequenciado.
