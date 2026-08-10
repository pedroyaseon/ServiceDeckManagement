# Desenvolvimento

## SDK

O SDK é fixado por `global.json`. Para manter ferramentas dentro do projeto, o
SDK pode ser instalado em `.dotnet/`; essa pasta é ignorada pelo Git.

O wrapper `scripts/dotnet.ps1` prefere `.dotnet/dotnet.exe`, mantém estado da CLI
em `.dotnet-home/`, pacotes em `.packages/` e desabilita telemetria.

## Comandos

```powershell
& '.\scripts\dotnet.ps1' restore ServiceDeckManagement.sln
& '.\scripts\dotnet.ps1' build ServiceDeckManagement.sln --no-restore
& '.\scripts\dotnet.ps1' test ServiceDeckManagement.sln --no-build
```

Uma distribuição portátil de desenvolvimento para Windows x64 é criada com:

```powershell
& '.\scripts\publish-portable.ps1'
```

O script publica Launcher, helper de configuração, Manager e Host de forma
autocontida em `artifacts/`, gera `SHA256SUMS` e não inclui a API opcional.

Antes do commit:

```powershell
& '.\scripts\verify-public-repository.ps1'
& '.\scripts\verify-gitignore.ps1'
git diff --check
```

## Dependências

- versões ficam em `Directory.Packages.props`;
- somente `https://api.nuget.org/v3/index.json` é aceito por `NuGet.Config`;
- pacotes são restaurados em `.packages/`;
- `packages.lock.json` é versionado;
- CI usa `dotnet restore --locked-mode`;
- auditoria NuGet verifica dependências diretas e transitivas.

## Testes do Service Host

`ServiceDeckManagement.HostTests` usa apenas processos auxiliares compilados no
repositório. Valida argumentos separados, logs, rotação, reinício, carregamento
da definição e encerramento da árvore pelo Job Object.

## Testes do Manager

`ServiceDeckManagement.ManagerTests` valida persistência atômica, DPAPI,
integridade da auditoria, framing, autenticação, autorização e regras de
pertencimento ao SCM. O adaptador do SCM é substituído por um backend em memória:
nenhum teste normal cria, para ou remove serviços reais.

O teste real das APIs nativas requer uma VM Windows descartável, execução
administrativa, nomes temporários reservados e cleanup em `finally`. Esse teste
é um gate manual de release e não deve ser executado em uma estação de trabalho
com serviços do usuário.

## Testes do helper e do Launcher

`ServiceDeckManagement.SetupTests` valida a gramática fechada do helper, a marca
de propriedade do serviço, a preservação do SID opcional da API e os fluxos de
criação e reparo sem acessar o SCM real. `ServiceDeckManagement.LauncherTests`
valida o acionamento do helper, pacote incompleto e cancelamento do UAC sem
expor detalhes nativos.
