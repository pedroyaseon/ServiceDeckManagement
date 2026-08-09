# Service Deck Management

Service Deck Management é uma plataforma open source para registrar, executar,
supervisionar e administrar aplicações como Serviços do Windows, sem depender do
NSSM ou de outro wrapper externo.

O projeto está na versão `1.0.0-alpha.3`. A fundação e o Service Host estão
implementados. O Host executa uma aplicação diretamente, contém sua árvore em
um Windows Job Object, captura logs limitados e aplica health checks e política
de reinício. Manager, API, launcher e dashboard permanecem planejados.

## Princípios

- runtime portátil e contido na pasta do projeto;
- API versionada e acesso remoto seguro;
- separação entre a superfície de rede e as operações privilegiadas;
- nenhuma execução indireta por shell;
- logs, estados e auditoria em tempo real;
- documentação e segurança tratadas como parte do produto;
- compatibilidade inicial com Windows 10, Windows 11 e Windows Server.

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

Consulte [docs/development.md](docs/development.md),
[docs/configuration-v1.md](docs/configuration-v1.md) e
[docs/service-host.md](docs/service-host.md).

## Estado

Service Host em validação. Esta etapa ainda não instala registros no Windows:
essa operação privilegiada pertence ao Manager da próxima PR.

## Licença

Distribuído sob a licença MIT. Consulte [LICENSE](LICENSE).
