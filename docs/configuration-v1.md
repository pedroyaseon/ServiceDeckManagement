# Configuração de serviço v1

## Estado

O contrato, o schema e a validação sintática estão implementados. Persistência,
DPAPI e consumo pelo Service Host ainda estão planejados.

## Arquivos públicos e locais

O repositório publica somente:

- `config/schemas/service-definition.v1.schema.json`;
- `config/examples/service-definition.example.json`;
- `config/README.md`.

Configurações reais em `config/services/`, `config/application.json` e
`config/security.json` são ignoradas pelo Git.

## Regras principais

- `schemaVersion` deve ser `1`;
- `id` usa letras ASCII minúsculas, números e hífen, com até 63 caracteres;
- IDs reservados do produto são rejeitados;
- executável deve ser `.exe` e usar caminho relativo;
- caminho absoluto, UNC, traversal, segmento reservado ou ambíguo e reparse
  point existente são rejeitados;
- argumentos são um array e não uma linha de shell;
- nomes de variáveis seguem o formato do Windows;
- valores sensíveis usam `secretReferences`;
- health checks HTTP e TCP aceitam apenas loopback na v1;
- políticas possuem limites explícitos de tempo, tamanho e tentativa;
- propriedades JSON obrigatórias ausentes, desconhecidas ou duplicadas causam
  erro.

## Exemplo

Consulte `config/examples/service-definition.example.json`. O exemplo não
contém segredo nem caminho pessoal e não deve ser usado como configuração de
produção sem revisão.
