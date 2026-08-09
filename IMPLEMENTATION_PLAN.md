# Plano de implementação — Service Deck Management v1.0

## 1. Estado e autoridade deste documento

Este documento é a fonte canônica de escopo da versão 1.0. O estado atual é
`fundação técnica`: contratos v1, validação, raiz portátil e testes de
arquitetura estão implementados. Manager, Service Host, API, launcher e
dashboard permanecem planejados.

Versão atual do produto: `1.0.0-alpha.2`.

Alterações de arquitetura, confiança, armazenamento, API pública ou escopo da
v1.0 exigem atualização deste documento ou um Architecture Decision Record
(ADR) aprovado na mesma PR.

## 2. Visão do produto

Service Deck Management é uma plataforma open source para transformar
aplicações em Serviços do Windows, supervisionar seus processos e administrá-las
local ou remotamente por launcher, dashboard e API.

O produto substitui wrappers externos por um runtime próprio. Não haverá
dependência, integração, migração automática ou compatibilidade com NSSM na
v1.0.

## 3. Princípios não negociáveis

1. Todo binário, configuração, banco, log e estado do produto fica na pasta do
   projeto ou da distribuição.
2. Nenhum caminho de unidade, usuário, `Program Files` ou `ProgramData` é fixado
   no código.
3. O único estado inevitavelmente externo é o registro de Serviços do Windows
   mantido pelo Service Control Manager (SCM).
4. A API de rede não executa operações privilegiadas diretamente.
5. Nenhuma aplicação gerenciada é iniciada por shell.
6. Acesso remoto é desabilitado por padrão.
7. Toda operação mutável é autenticada, autorizada e auditada.
8. Segurança, testes e documentação fazem parte da definição de pronto.
9. Arquivos textuais usam UTF-8 e português correto, sem mojibake.
10. O código público não contém segredos, dados locais ou caminhos pessoais.

## 4. Escopo da v1.0

### 4.1 Incluído

- Windows 10, Windows 11 e Windows Server suportados pelo .NET escolhido;
- instalação portátil dentro de uma pasta estável;
- criação, edição, início, parada, reinício e remoção de serviços;
- uma instância isolada do Service Host por aplicação gerenciada;
- captura de stdout e stderr;
- rotação por tamanho e retenção de logs;
- encerramento gracioso seguido de encerramento forçado com timeout;
- controle da árvore de processos com Windows Job Objects;
- política de reinício com backoff e limite de tentativas;
- health checks de processo, HTTP e TCP;
- Manager local privilegiado;
- comunicação local por Named Pipes;
- API REST `/api/v1` e eventos em tempo real por SignalR;
- dashboard responsivo;
- launcher Windows para instalação, reparo e operação;
- autenticação, papéis, auditoria e acesso HTTPS remoto;
- empacotamento self-contained para Windows x64;
- atualização e reparo preservando configuração e dados;
- documentação operacional, de arquitetura e segurança.

### 4.2 Fora da v1.0

- Linux ou macOS;
- cluster, alta disponibilidade ou gerenciamento central de múltiplos hosts;
- execução em contêiner;
- marketplace de extensões;
- atualização automática sem confirmação;
- scripts arbitrários de hooks;
- execução por PowerShell, `cmd.exe`, WSL ou shell configurável;
- importação de configurações de wrappers externos;
- suporte x86 ou ARM64, salvo decisão posterior documentada.

## 5. Layout portátil

O diretório raiz é resolvido a partir da localização real do executável de
entrada. Todos os caminhos de configuração são relativos a essa raiz.

```text
ServiceDeckManagement/
├── app/
│   ├── ServiceDeckManagement.Manager.exe
│   ├── ServiceDeckManagement.Host.exe
│   ├── ServiceDeckManagement.Api.exe
│   └── ServiceDeckManagement.Launcher.exe
├── config/
│   ├── application.json
│   ├── security.json
│   └── services/
├── data/
│   ├── servicedeckmanagement.db
│   └── protection-keys/
├── logs/
│   ├── api/
│   ├── manager/
│   └── services/
├── runtime/
│   ├── state/
│   └── staging/
├── dashboard/
└── ServiceDeckManagement.exe
```

No repositório de desenvolvimento, `runtime/`, `artifacts/`, bancos, logs e
configurações locais são ignorados pelo Git.

O SCM armazena o caminho absoluto do executável registrado. Portanto, a pasta
não pode ser movida enquanto os serviços estiverem instalados. O launcher terá
uma operação explícita de reparo que revalida a raiz e registra novamente os
caminhos após uma mudança autorizada.

## 6. Arquitetura e limites de confiança

```text
Dashboard / Launcher
        |
    HTTPS + API v1 + SignalR
        |
ServiceDeckManagement.Api (sem privilégio administrativo)
        |
Named Pipe local autenticado e versionado
        |
ServiceDeckManagement.Manager (serviço local privilegiado)
        |
Windows Service Control Manager
        |
ServiceDeckManagement.Host --service-id <id>
        |
Aplicação gerenciada + Job Object
```

### 6.1 Service Host

Cada aplicação é registrada como um Serviço do Windows separado, usando o mesmo
binário:

```text
ServiceDeckManagement.Host.exe --service-id minha-api
```

Responsabilidades:

- carregar uma definição validada e imutável durante a execução;
- comunicar corretamente estados Pending, Running, Paused e Stopped ao SCM;
- iniciar o executável diretamente com `ProcessStartInfo.ArgumentList`;
- aplicar diretório, argumentos e ambiente sem shell;
- vincular o processo a um Job Object com política de encerramento da árvore;
- capturar stdout e stderr de forma assíncrona e limitada;
- remover sequências ANSI antes da exibição e persistência;
- impedir crescimento ilimitado de buffers e arquivos;
- executar health checks;
- aplicar reinício com backoff, janela e circuit breaker;
- tentar parada graciosa e aplicar timeout antes de finalizar a árvore;
- publicar estado local ao Manager sem abrir porta de rede.

### 6.2 Manager

Serviço local que concentra operações privilegiadas. Não possui endpoint HTTP e
não aceita conexões remotas.

Responsabilidades:

- criar, atualizar e remover registros no SCM por APIs nativas;
- validar e persistir definições de serviço;
- iniciar, parar e reiniciar serviços;
- controlar ACL de configurações, dados e pipes;
- verificar se um serviço pertence ao produto antes de operá-lo;
- coordenar instalação, reparo, upgrade e desinstalação;
- fornecer inventário, estado e eventos à API;
- registrar auditoria local resistente a adulteração acidental.

### 6.3 API

A API executa sem privilégio administrativo e atua como gateway autenticado para
o Manager.

Responsabilidades:

- expor contratos REST versionados em `/api/v1`;
- autenticar usuários e sessões;
- aplicar papéis Viewer, Operator e Administrator;
- validar DTOs antes de enviar comandos ao Manager;
- aplicar rate limiting, CORS e limites de payload;
- publicar eventos SignalR;
- servir o dashboard compilado em produção;
- produzir auditoria de identidade, origem, operação e resultado;
- não acessar diretamente SCM, Registro ou processos gerenciados.

### 6.4 Launcher

Cliente Windows para configuração inicial e administração. Operações normais
usam a API. Elevação só pode ocorrer com consentimento explícito durante
instalação, reparo, upgrade ou desinstalação.

Responsabilidades:

- localizar e validar a raiz portátil;
- instalar ou reparar Manager e API;
- configurar acesso local ou remoto;
- operar serviços pela API;
- exibir estados e logs em tempo real;
- abrir dashboard e documentação;
- exportar diagnóstico sanitizado;
- nunca solicitar elevação ao apenas abrir ou consultar o sistema.

### 6.5 Dashboard

Aplicação web responsiva e acessível, servida pela API em produção. Nenhuma ação
administrativa pode existir apenas no frontend: toda autorização é repetida na
API e no Manager.

## 7. Estrutura prevista da solução

```text
src/
├── ServiceDeckManagement.Domain/
├── ServiceDeckManagement.Application/
├── ServiceDeckManagement.Contracts/
├── ServiceDeckManagement.Infrastructure/
├── ServiceDeckManagement.Manager/
├── ServiceDeckManagement.Host/
├── ServiceDeckManagement.Api/
├── ServiceDeckManagement.Web/
├── ServiceDeckManagement.Launcher/
└── ServiceDeckManagement.Installer/

tests/
├── ServiceDeckManagement.UnitTests/
├── ServiceDeckManagement.IntegrationTests/
├── ServiceDeckManagement.ManagerTests/
├── ServiceDeckManagement.HostTests/
├── ServiceDeckManagement.ApiTests/
├── ServiceDeckManagement.LauncherTests/
├── ServiceDeckManagement.SecurityTests/
└── ServiceDeckManagement.EndToEndTests/
```

Dependências apontam para dentro: Domain não conhece infraestrutura; Application
define casos de uso e portas; Contracts contém modelos versionados; componentes
Windows ficam em Infrastructure, Manager e Host.

## 8. Modelo de configuração

Cada arquivo em `config/services/<id>.json` usa `schemaVersion: 1`.

```json
{
  "schemaVersion": 1,
  "id": "minha-api",
  "displayName": "Minha API",
  "executable": "apps/MinhaApi/MinhaApi.exe",
  "workingDirectory": "apps/MinhaApi",
  "arguments": ["--environment", "Production"],
  "environment": {
    "ASPNETCORE_ENVIRONMENT": "Production"
  },
  "startMode": "automatic",
  "restartPolicy": {
    "enabled": true,
    "maximumAttempts": 5,
    "delaySeconds": 10,
    "maximumDelaySeconds": 120,
    "resetAfterMinutes": 15
  },
  "stopPolicy": {
    "gracefulTimeoutSeconds": 20,
    "terminateTree": true
  },
  "logging": {
    "enabled": true,
    "maximumFileSizeMb": 25,
    "retainedFiles": 10
  },
  "healthCheck": {
    "type": "http",
    "target": "http://127.0.0.1:8080/health",
    "intervalSeconds": 15,
    "timeoutSeconds": 3
  }
}
```

Regras mínimas:

- `id` imutável, normalizado e seguro para nome de arquivo e serviço;
- executável e diretório resolvidos sob raízes permitidas;
- caminhos UNC e reparse points bloqueados por padrão;
- argumentos armazenados em array, nunca em uma linha de shell;
- nomes de variáveis validados e valores secretos referenciados, não gravados;
- gravação atômica com arquivo temporário, flush e substituição;
- schema desconhecido é rejeitado, nunca interpretado parcialmente.

## 9. Ciclo de vida e estados

Estados de domínio:

- NotInstalled;
- Stopped;
- StartPending;
- Running;
- Degraded;
- RestartPending;
- StopPending;
- Failed;
- Disabled.

Transições inválidas retornam erro de conflito. Start, stop e restart são
idempotentes quando possível. Toda transição possui timeout, cancellation token,
correlation ID e registro de auditoria.

O reinício automático usa backoff limitado. Ao exceder tentativas na janela, o
serviço entra em Failed e exige ação explícita, evitando loops infinitos.

## 10. Logs e observabilidade

- stdout e stderr são lidos sem bloquear o processo;
- sequências ANSI e caracteres de controle perigosos são removidos;
- segredos conhecidos são redigidos antes de exibição e persistência;
- linhas possuem timestamp, stream, service ID e sequence ID;
- arquivos rotacionam por tamanho e respeitam retenção e limite total;
- a API oferece consulta paginada e tail em tempo real;
- clientes lentos não podem causar memória ilimitada;
- métricas incluem uptime, PID, reinícios, última saída e health check;
- logs do produto são separados dos logs das aplicações.

## 11. API v1

Endpoints previstos:

```text
GET    /api/v1/system/health
GET    /api/v1/system/version
POST   /api/v1/session
DELETE /api/v1/session

GET    /api/v1/services
GET    /api/v1/services/{id}
POST   /api/v1/services
PUT    /api/v1/services/{id}
DELETE /api/v1/services/{id}

POST   /api/v1/services/{id}/start
POST   /api/v1/services/{id}/stop
POST   /api/v1/services/{id}/restart

GET    /api/v1/services/{id}/logs
GET    /api/v1/audit
GET    /api/v1/events
```

Papéis:

- Viewer: inventário, estado e logs permitidos;
- Operator: Viewer mais start, stop e restart;
- Administrator: criação, edição, remoção, usuários, rede e atualização.

SignalR publica snapshots e deltas com sequence ID. Ao detectar lacuna, o
cliente refaz o snapshot. Eventos não substituem a validação do estado atual.

## 12. Segurança

### 12.1 Ameaças prioritárias

- execução remota de comando;
- escalada de privilégio da API para o Manager;
- serviço ou arquivo falso se passando pelo produto;
- alteração de configuração entre validação e execução;
- path traversal, junctions, symlinks e reparse points;
- roubo de sessão ou token;
- vazamento de segredo por log, argumento ou exportação;
- negação de serviço por logs, eventos, health checks ou loops de reinício;
- supply-chain em dependências, build e release;
- exposição acidental de arquivos locais no repositório público.

### 12.2 Controles obrigatórios

- Manager sem porta de rede;
- Named Pipe com protocolo versionado, framing limitado e ACL explícita;
- autenticação mútua local baseada em identidade Windows e nonce de sessão;
- API com HTTPS obrigatório quando não estiver em loopback;
- cookies seguros ou tokens de curta duração protegidos;
- senhas derivadas com algoritmo resistente e parâmetros versionados;
- DPAPI para segredos locais e chaves fora do Git;
- autorização aplicada na API e novamente no Manager;
- validação canônica do caminho no momento da operação;
- nenhum shell, eval, template executável ou argumento concatenado;
- limites de payload, concorrência, buffer, log e frequência;
- auditoria com correlation ID, identidade, origem, antes/depois e resultado;
- mensagens externas sem stack trace ou detalhes sensíveis;
- dependências fixadas e verificadas;
- artefatos de release com SHA-256 e assinatura quando disponível.

### 12.3 Gates para cada PR

- revisão do diff público;
- busca de segredos, chaves, tokens e caminhos pessoais;
- validação UTF-8 e busca de mojibake;
- testes relevantes;
- análise de pacotes vulneráveis;
- análise estática quando houver código;
- threat model atualizado quando o limite de confiança mudar;
- documentação e exemplos sem dados reais;
- confirmação de que runtime e artefatos continuam ignorados.

## 13. Banco e auditoria

SQLite fica em `data/` e usa migrations versionadas. O banco armazena usuários,
papéis, sessões revogáveis, auditoria e metadados; definições operacionais de
serviço permanecem em arquivos versionados por schema.

Auditoria registra somente metadados necessários. Segredos, conteúdo completo de
ambiente e credenciais nunca são persistidos em eventos.

## 14. Interface

### 14.1 Launcher

- tema consistente com controles totalmente estilizados;
- estados em tempo real sem botão de atualização;
- feedback centralizado e acionável;
- nenhuma sequência ANSI visível;
- seleção, foco e desabilitado acessíveis;
- confirmação para ações destrutivas;
- fluxo de instalação separado do uso normal;
- diagnóstico exportável com sanitização.

### 14.2 Dashboard

- responsivo para desktop, tablet e celular;
- design sóbrio e profissional, sem aparência genérica automatizada;
- navegação por teclado, foco visível e contraste WCAG AA;
- estados vazios, carregamento, erro e reconexão explícitos;
- ações determinadas pelo papel, sem confiar apenas em ocultação visual;
- logs virtualizados, pesquisáveis e com pausa de tail;
- timestamps com timezone explícito.

## 15. Instalação, reparo e atualização

O pacote é extraído em uma pasta escolhida pelo usuário. A instalação registra
Manager, API e serviços gerenciados usando os binários dessa pasta.

Reparo:

1. valida integridade e versão do layout;
2. compara os caminhos registrados no SCM com a raiz atual;
3. exige confirmação e elevação explícita;
4. registra novamente componentes sem apagar dados;
5. valida health checks após a operação.

Atualização usa staging dentro da raiz, checksum, backup de configuração,
substituição atômica e rollback. A desinstalação remove registros do Windows, mas
não remove aplicações gerenciadas nem dados sem confirmação separada.

## 16. Estratégia de implementação por PR

Branches e PRs não usam `agent` ou `codex`.

### PR 1 — Fundação da v1

Branch: `docs/v1-implementation-plan`

- este plano, regras, licença, segurança e automação de verificação pública;
- versão `1.0.0-alpha.1`;
- nenhuma implementação de runtime.

### PR 2 — Estrutura da solução e contratos

Branch: `feature/v1-foundation`

- solution, projetos, dependências e contratos versionados;
- resolução da raiz portátil;
- schema e validação de configuração;
- testes de arquitetura e caminhos.

Versão: `1.0.0-alpha.2`.

### PR 3 — Service Host

Branch: `feature/service-host`

- integração com Windows Service;
- ProcessStartInfo sem shell;
- Job Objects, logs, parada e reinício;
- testes unitários e integração Windows isolada.

Versão: `1.0.0-alpha.3`.

### PR 4 — Manager e canal local

Branch: `feature/service-manager`

- SCM nativo, persistência atômica, ACL e Named Pipes;
- inventário estrito e auditoria;
- instalação, edição, remoção e reparo.

Versão: `1.0.0-alpha.4`.

### PR 5 — API v1 e tempo real

Branch: `feature/api-v1`

- autenticação, autorização, REST, SignalR e rate limiting;
- integração exclusiva com Manager;
- testes de contrato e segurança.

Versão: `1.0.0-beta.1`.

### PR 6 — Launcher v1

Branch: `feature/launcher-v1`

- setup, reparo, operação, logs e estados pela API;
- consentimento explícito para elevação;
- acessibilidade e testes de apresentação.

Versão: `1.0.0-beta.2`.

### PR 7 — Dashboard v1

Branch: `feature/dashboard-v1`

- interface responsiva, administração, logs e auditoria;
- build servido pela API;
- testes unitários, integração e E2E.

Versão: `1.0.0-beta.3`.

### PR 8 — Hardening e distribuição

Branch: `feature/v1-hardening`

- instalador portátil, upgrade, rollback e desinstalação;
- threat model final, CodeQL, Dependabot e verificações de supply-chain;
- testes em máquina Windows limpa.

Versão: `1.0.0-rc.1`.

### PR 9 — Release

Branch: `release/v1.0.0`

- correções finais, documentação, checksums e notas de release;
- tag `v1.0.0` após aprovação.

## 17. Estratégia de testes

### Unitários

- validação e normalização de IDs e caminhos;
- state machine;
- backoff e circuit breaker;
- sanitização, rotação e retenção;
- autorização e políticas;
- serialização e compatibilidade de contratos.

### Integração Windows

- criar serviços temporários com prefixo reservado;
- garantir cleanup mesmo em falha;
- nunca operar serviços reais da máquina;
- validar SCM, Job Objects, Named Pipes e ACL;
- validar processos que travam, falham, ignoram parada ou geram muito log.

### API e segurança

- autenticação, expiração, revogação e papéis;
- IDOR, traversal, payload grande, rate limit e CORS;
- rejeição de shell, UNC, reparse points e contratos desconhecidos;
- desconexão e recuperação do Manager;
- eventos SignalR perdidos e ressincronização.

### E2E

- instalação em VM Windows limpa;
- criação, operação, logs, reboot, reparo, atualização e desinstalação;
- acesso local e remoto HTTPS;
- launcher e dashboard com leitor de tela e teclado;
- preservação de configuração e dados.

## 18. CI/CD e repositório público

- build e testes em Windows;
- validação de formatação e arquitetura;
- CodeQL;
- Dependabot;
- análise de dependências vulneráveis;
- secret scanning e push protection quando disponíveis;
- geração de SBOM;
- artefatos somente em releases, nunca em commits;
- checksums SHA-256 publicados junto dos binários;
- releases prévias marcadas como prerelease.

O repositório privado anterior não será importado com seu histórico. Reuso de
código ocorrerá apenas por revisão explícita de proveniência, licença, segredos e
aderência à nova arquitetura. Código específico de NSSM não será migrado.

## 19. Versionamento

SemVer:

- `1.0.0-alpha.x`: infraestrutura incompleta;
- `1.0.0-beta.x`: funcionalidades completas em validação;
- `1.0.0-rc.x`: candidato de release;
- `1.0.0`: primeira versão estável;
- `1.0.x`: correções compatíveis;
- `1.x.0`: funcionalidades compatíveis;
- `2.0.0`: contratos ou configuração incompatíveis.

Também são versionados:

- API: `/api/v1`;
- configuração: `schemaVersion: 1`;
- protocolo local: `protocolVersion: 1`;
- banco: migrations identificadas e reversíveis quando possível.

## 20. Riscos e mitigação

| Risco | Mitigação |
|---|---|
| execução remota privilegiada | separação API–Manager, RBAC duplo e sem shell |
| pasta movida | reparo explícito e validação de raiz |
| processos órfãos | Job Objects e testes de falha |
| loop de reinício | backoff, limite e estado Failed |
| disco cheio | rotação, retenção e limite global |
| vazamento em open source | repo novo, preflight, ignore e secret scanning |
| protocolo local explorável | ACL, framing limitado, versão e autenticação |
| atualização interrompida | staging, substituição atômica e rollback |
| regressão de encoding | UTF-8 obrigatório e verificação de mojibake |

## 21. Definição de pronto da v1.0

- nenhuma dependência ou referência a NSSM;
- instalação e reparo em pasta escolhida pelo usuário;
- serviços sobrevivem a logout e reboot;
- nenhuma árvore de processos fica órfã;
- políticas de parada e reinício são limitadas e testadas;
- logs respeitam sanitização, rotação e retenção;
- API remota exige HTTPS, autenticação e autorização;
- Manager não possui superfície de rede;
- dashboard e launcher atualizam sem refresh manual;
- operações mutáveis possuem auditoria;
- upgrade preserva dados e possui rollback;
- desinstalação não apaga aplicações do usuário por padrão;
- documentação operacional e de segurança está completa;
- testes, análise estática, dependências e secret scanning aprovados;
- release possui SBOM, SHA-256 e notas verificáveis;
- nenhum segredo, runtime ou caminho pessoal existe no repositório.

## 22. Aprovação para iniciar implementação

A aprovação desta documentação autoriza somente a abertura da PR 2. Cada etapa
posterior continua sujeita à revisão do escopo, dos testes e do risco da PR
anterior.
