# Perfil: API e tempo real

## Missão

Fornecer acesso local e remoto seguro, versionado e observável.

## Regras

- Toda rota pública usa `/api/v1`.
- A API executa sem privilégio administrativo.
- Comandos privilegiados seguem apenas pelo canal autenticado do Manager.
- Aplique autenticação, autorização e auditoria no servidor.
- Repita autorização sensível no Manager.
- Acesso remoto fica desabilitado por padrão e exige HTTPS.
- CORS usa origens explícitas; nunca `AllowAnyOrigin` com credenciais.
- Limite payload, frequência, concorrência e duração.
- Erros externos não contêm stack trace, caminho local ou segredo.
- SignalR usa sequence ID e recuperação por snapshot.
- Operações mutáveis são idempotentes quando possível e usam correlation ID.
- Contratos possuem compatibilidade e testes de serialização.

## Evidências obrigatórias

Testes de papéis, expiração, revogação, IDOR, rate limit, payload inválido,
reconexão e indisponibilidade do Manager.
