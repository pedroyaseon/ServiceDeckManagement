# Desenvolvimento da fundação

## SDK

O SDK é fixado por `global.json`. Para manter ferramentas dentro do projeto, o
SDK pode ser instalado em `.dotnet/`; essa pasta é ignorada pelo Git.

O wrapper `scripts/dotnet.ps1`:

- prefere `.dotnet/dotnet.exe`;
- usa `.dotnet-home/` para estado da CLI;
- usa `.packages/` para pacotes NuGet;
- desabilita telemetria e experiência inicial;
- não gera certificado ASP.NET durante comandos da fundação.

## Comandos

```powershell
& '.\scripts\dotnet.ps1' restore ServiceDeckManagement.sln
& '.\scripts\dotnet.ps1' build ServiceDeckManagement.sln --no-restore
& '.\scripts\dotnet.ps1' test ServiceDeckManagement.sln --no-build
```

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

## Limites desta etapa

Os projetos atuais não instalam serviços, não iniciam processos e não abrem
porta de rede. Testes que alterem o SCM não pertencem a esta PR.
