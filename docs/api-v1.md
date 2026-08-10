# API v1

Versão do componente: `1.0.0-beta.3`.

A API é um gateway opcional para clientes remotos, especialmente o Dashboard.
O Launcher local não depende dela e se comunica diretamente com o Manager pelo
canal local autenticado.

## Limite de rede

A API escuta somente em `127.0.0.1` e usa a porta `5180` por padrão. O arquivo
local `config/api.json` pode alterar a porta, mas esta versão recusa acesso
remoto. Isso evita publicar uma interface administrativa sem HTTPS configurado.

## Inicialização e sessões

Quando o banco ainda não possui usuários, a API imprime no console um código
aleatório de uso único, válido por 15 minutos. O endpoint de bootstrap aceita
esse código apenas por loopback e cria o primeiro administrador.

As senhas são armazenadas com `PasswordHasher` do ASP.NET Core. Tokens de sessão
são opacos, aleatórios, válidos por oito horas e persistidos somente como hash
SHA-256. O logout revoga a sessão no SQLite.

## Autorização

- `viewer`: inventário, detalhes e logs;
- `operator`: permissões de `viewer`, mais iniciar, parar e reiniciar;
- `administrator`: todas as permissões, incluindo criar, editar, reparar,
  remover e consultar auditoria.

A API valida a função e envia a identidade autenticada ao Manager. O Manager
repete a autorização antes de tocar no SCM do Windows.

## Rotas

Todas as rotas usam o prefixo `/api/v1`.

| Método | Rota | Acesso |
| --- | --- | --- |
| `GET` | `/system/health` | anônimo |
| `GET` | `/system/version` | anônimo |
| `GET` | `/bootstrap/status` | anônimo |
| `POST` | `/bootstrap` | anônimo, loopback, uso único |
| `POST` | `/sessions` | anônimo |
| `DELETE` | `/sessions/current` | autenticado |
| `GET` | `/services` | viewer |
| `GET` | `/services/{id}` | viewer |
| `GET` | `/services/{id}/logs` | viewer |
| `POST` | `/services` | administrator |
| `PUT` | `/services/{id}` | administrator |
| `DELETE` | `/services/{id}` | administrator |
| `POST` | `/services/{id}/start` | operator |
| `POST` | `/services/{id}/stop` | operator |
| `POST` | `/services/{id}/restart` | operator |
| `POST` | `/services/{id}/repair` | administrator |
| `GET` | `/audit` | administrator |
| SignalR | `/events` | viewer |

O evento SignalR `services.snapshot` transporta `sequence`, `generatedAt` e a
lista completa. Ao detectar lacuna na sequência, o cliente deve recuperar o
estado por `GET /services/snapshot`.

## Controles de segurança

- corpo HTTP limitado a 1 MiB e frame local limitado a 64 KiB;
- JSON estrito, sem propriedades desconhecidas, duplicadas ou comentários;
- consultas SQLite parametrizadas;
- rate limit global e limite reforçado para login e bootstrap;
- erros externos genéricos, sem stack trace ou caminhos locais;
- auditoria de autenticação e mutações;
- nenhuma chamada de shell e nenhum acesso direto da API ao SCM.
