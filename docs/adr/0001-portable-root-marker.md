# ADR 0001 — marcador de raiz portátil

## Status

Aceito na fundação `1.0.0-alpha.2`.

## Contexto

Os binários de produção ficam em `app/`, enquanto configurações, dados e logs
ficam em diretórios irmãos. O produto não pode depender de uma unidade ou pasta
do sistema.

## Decisão

A raiz contém `.servicedeck-root` com versão e identidade do produto. Cada
processo procura esse arquivo a partir de `AppContext.BaseDirectory`, subindo no
máximo oito níveis. Um marcador ausente ou incompatível interrompe a operação.

## Consequências

- a instalação pode existir em qualquer unidade local;
- não há fallback silencioso para diretório do usuário;
- uma pasta movida precisa do fluxo de reparo para atualizar o SCM;
- o Manager revalidará caminho e reparse points no momento da operação;
- o marcador é público e não representa autenticação ou segredo.
