# Service Deck Management

Service Deck Management é uma plataforma open source para registrar, executar,
supervisionar e administrar aplicações como Serviços do Windows, sem depender do
NSSM ou de outro wrapper externo.

O projeto começa na versão `1.0.0-alpha.1`. Nesta etapa o repositório contém
somente o plano de implementação, as decisões de arquitetura e as regras de
contribuição. Nenhum componente de runtime está implementado ainda.

## Princípios

- runtime portátil e contido na pasta do projeto;
- API versionada e acesso remoto seguro;
- separação entre a superfície de rede e as operações privilegiadas;
- nenhuma execução indireta por shell;
- logs, estados e auditoria em tempo real;
- documentação e segurança tratadas como parte do produto;
- compatibilidade inicial com Windows 10, Windows 11 e Windows Server.

Leia [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) antes de propor código.
As instruções para colaboradores e assistentes estão em [AGENTS.md](AGENTS.md)
e na pasta [`.agents`](.agents/README.md).

## Estado

Planejamento da v1.0. A implementação somente começará após a aprovação da PR de
fundação documental.

## Licença

Distribuído sob a licença MIT. Consulte [LICENSE](LICENSE).
