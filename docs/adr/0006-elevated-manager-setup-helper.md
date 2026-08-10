# ADR 0006 — Helper elevado para configuração do Manager

## Status

Aceito para `1.0.0-beta.4`.

## Contexto

O Launcher opera como usuário comum, mas o primeiro registro e o reparo do
Manager no SCM exigem privilégio administrativo. Elevar todo o Launcher
aumentaria a superfície privilegiada e faria o uso cotidiano depender de UAC.

## Decisão

Um executável separado, `ServiceDeckManagement.Setup.exe`, executa elevado e
aceita somente operações de instalação explicitamente implementadas. Na beta.4,
a única operação pública é `install-manager` com o SID do usuário local que
iniciou o fluxo.

O Launcher inicia esse executável diretamente com o verbo `runas`, após
confirmação do usuário. O helper:

- localiza a raiz pelo marcador do produto;
- valida o SID e rejeita identidades privilegiadas genéricas;
- exige os binários esperados dentro de `app/`;
- grava `manager-security.json` de forma atômica;
- protege `app/`, configurações, dados e chave de transporte com ACL explícita;
- cria ou repara somente o serviço `ServiceDeckManagement.Manager`;
- recusa um registro existente sem o marcador de propriedade esperado;
- inicia o Manager e retorna somente códigos de resultado estáveis.

O helper não instala a API, não aceita caminho de binário pela linha de comando,
não executa shell e não recebe operações arbitrárias.

## Consequências

- abrir e consultar o Launcher nunca solicita elevação;
- a primeira configuração e o reparo solicitam UAC uma vez por ação explícita;
- o processo privilegiado possui superfície menor que a interface WPF;
- a pasta `app/` deixa de ser gravável pelo usuário autorizado e permanece
  somente leitura e execução para ele;
- uma pasta movida exige reparo explícito;
- cancelamento do UAC não altera a instalação;
- desinstalação, atualização e rollback completos continuam na etapa de
  distribuição e hardening.
