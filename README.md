# Service Deck Management

Service Deck Management é uma plataforma open source para registrar, executar,
supervisionar e administrar aplicações como Serviços do Windows, sem depender do
NSSM ou de outro wrapper externo.

O projeto está na versão `1.0.0-beta.4`. A fundação, o Service Host, o Manager,
a API local v1 e o Launcher estão implementados. O Launcher usa diretamente o
canal local autenticado do Manager e não depende da API. A API fica reservada
como gateway opcional para o futuro Dashboard remoto. Instalador e Dashboard
permanecem em etapas separadas.

## Princípios

- runtime portátil e contido na pasta do projeto;
- API versionada e acesso remoto seguro;
- separação entre a superfície de rede e as operações privilegiadas;
- nenhuma execução indireta por shell;
- logs, estados e auditoria em tempo real;
- documentação e segurança tratadas como parte do produto;
- compatibilidade inicial x64 com Windows 10, Windows 11 e Windows Server.

Leia [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) antes de propor código.

## Desenvolvimento

Instale o SDK definido em `global.json` localmente ou em `.dotnet/`. Em seguida:

```powershell
& '.\scripts\dotnet.ps1' restore ServiceDeckManagement.sln
& '.\scripts\dotnet.ps1' build ServiceDeckManagement.sln --no-restore
& '.\scripts\dotnet.ps1' test ServiceDeckManagement.sln --no-build
& '.\scripts\verify-public-repository.ps1'
& '.\scripts\verify-gitignore.ps1'
```

Para gerar uma distribuição portátil de desenvolvimento com Launcher, helper
de configuração, Manager e Host:

```powershell
& '.\scripts\publish-portable.ps1'
```

O pacote é criado em `artifacts/`, que permanece fora do Git.

Consulte [docs/development.md](docs/development.md),
[docs/configuration-v1.md](docs/configuration-v1.md),
[docs/service-host.md](docs/service-host.md) e
[docs/service-manager.md](docs/service-manager.md). O contrato HTTP e SignalR
está documentado em [docs/api-v1.md](docs/api-v1.md). O uso e os limites do
aplicativo desktop estão em [docs/launcher-v1.md](docs/launcher-v1.md).

## Estado

A beta.4 está em validação. Os testes normais usam um backend de SCM em memória
e não criam, param ou removem serviços reais. A validação administrativa em uma
VM descartável é um gate da futura release, não uma ação automática de CI.

## Licença

Distribuído sob a licença MIT. Consulte [LICENSE](LICENSE).
