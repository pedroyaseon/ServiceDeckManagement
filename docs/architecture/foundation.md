# Fundação técnica 1.0.0-alpha.4

## Camadas

```text
Domain              sem dependência interna
Contracts           sem dependência interna
Application         Domain + Contracts
Infrastructure      Application + Domain + Contracts
Host                Infrastructure + Application + Domain + Contracts
Manager             Infrastructure + Application + Domain + Contracts
```

Os testes verificam por reflexão a direção das camadas de fundação. Host e
Manager são pontos de composição; regras e portas permanecem nas camadas
internas.

## Raiz portátil

`ProductRootLocator` procura `.servicedeck-root` a partir do diretório do
executável e de seus pais, com profundidade limitada. O marcador precisa conter
`product=ServiceDeckManagement`.

`ProductPaths` deriva `app`, `config`, `data`, `logs` e `runtime` sem consultar
perfil de usuário, Registro ou unidade fixa.

`PortablePathResolver` canonicaliza caminhos, confirma que permanecem sob a
raiz e rejeita segmentos ambíguos, nomes de dispositivo e reparse points já
existentes no trajeto. Host e Manager repetem validações no limite em que usam o
caminho.

## Limite privilegiado

O Manager é o único componente que chama as APIs nativas do SCM. Ele não possui
servidor de rede e recebe comandos apenas pelo Named Pipe local v1. O contrato,
o framing, a autenticação e a autorização são independentes da futura API.
