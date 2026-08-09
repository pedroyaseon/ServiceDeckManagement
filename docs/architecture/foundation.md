# Fundação técnica 1.0.0-alpha.3

## Camadas

```text
Domain              sem dependência interna
Contracts           sem dependência interna
Application         Domain + Contracts
Infrastructure      Application + Domain + Contracts
Host                Infrastructure + Application + Domain + Contracts
```

Os testes verificam a direção das camadas de fundação por reflexão. O Host é o
primeiro executável e depende das portas e validações internas sem inverter as
dependências existentes.

## Raiz portátil

`ProductRootLocator` procura `.servicedeck-root` a partir do diretório do
executável e de seus pais, com profundidade limitada. O marcador precisa conter
`product=ServiceDeckManagement`.

`ProductPaths` deriva `app`, `config`, `data`, `logs` e `runtime` sem consultar
perfil de usuário, Registro ou unidade fixa.

`PortablePathResolver` canonicaliza caminhos, confirma que permanecem sob a
raiz e rejeita segmentos ambíguos, nomes de dispositivo e reparse points já
existentes no trajeto. A existência final do executável é verificada novamente
pelo Host imediatamente antes de cada inicialização e será repetida pelo Manager
no limite privilegiado.
