# Fundação técnica 1.0.0-alpha.2

## Camadas

```text
Domain              sem dependência interna
Contracts           sem dependência interna
Application         Domain + Contracts
Infrastructure      Application + Domain + Contracts
```

Os testes verificam essa direção por reflexão. Executáveis serão adicionados em
PRs específicas para não introduzir privilégios ou superfícies incompletas.

## Raiz portátil

`ProductRootLocator` procura `.servicedeck-root` a partir do diretório do
executável e de seus pais, com profundidade limitada. O marcador precisa conter
`product=ServiceDeckManagement`.

`ProductPaths` deriva `app`, `config`, `data`, `logs` e `runtime` sem consultar
perfil de usuário, Registro ou unidade fixa.

`PortablePathResolver` canonicaliza caminhos, confirma que permanecem sob a
raiz e rejeita segmentos ambíguos, nomes de dispositivo e reparse points já
existentes no trajeto. A existência final do executável será verificada
novamente pelo Manager no momento privilegiado para reduzir risco de TOCTOU.
