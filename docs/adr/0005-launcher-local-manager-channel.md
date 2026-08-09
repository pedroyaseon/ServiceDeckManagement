# ADR 0005 — Launcher local independente da API

## Status

Aceito para `1.0.0-beta.3`.

## Contexto

O Launcher é a interface de administração da máquina local. Exigir a API,
bootstrap e login antes de exibir ou operar serviços tornou um componente de
acesso remoto um requisito indevido para o uso local.

## Decisão

O Launcher se comunica diretamente com o Manager pelo mesmo Named Pipe v1
autenticado e limitado usado pela API. Ele não acessa o SCM, o Registro ou
processos diretamente. A identidade do Launcher vem do token do Windows e seu
SID deve ser provisionado explicitamente em `manager-security.json`.

A ACL do pipe e da chave de transporte concede ao SID do Launcher somente o
acesso necessário ao protocolo local. O Manager reconhece esse SID como cliente
administrativo direto; campos de identidade declarados no payload são ignorados.
O SID dedicado da API continua separado e usa autorização delegada por sessão.

A API deixa de ser dependência do Launcher. Ela é um gateway de rede opcional,
habilitado depois quando o usuário quiser conectar o dashboard. A exposição
remota continua proibida até existir HTTPS e configuração explícita.

## Consequências

- o Launcher abre sem login, URL ou porta de API;
- o Manager continua sendo o único componente que opera o SCM;
- instalação ou reparo deve registrar explicitamente o SID do usuário local
  autorizado;
- configuração ausente continua falhando de modo fechado;
- qualquer usuário local não provisionado permanece sem acesso ao pipe e à
  chave de transporte;
- o inventário e os logs são sincronizados periodicamente pelo canal local;
- dashboard, HTTPS e ativação da API remota permanecem em etapa separada.
