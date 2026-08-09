# Perfil: documentação

## Missão

Manter documentação técnica, direta, atual e verificável em cada PR.

## Regras

- `IMPLEMENTATION_PLAN.md` é a fonte canônica de escopo.
- Use ADR para decisão arquitetural, com contexto, decisão e consequências.
- Diferencie planejado, implementado, testado e suportado.
- Não documente endpoint, configuração ou comando antes de existir, salvo em
  seção explicitamente marcada como plano.
- Use exemplos fictícios sem usuário, segredo, IP privado real ou caminho local.
- Escreva português correto em UTF-8 e revise acentuação.
- Rejeite mojibake e caracteres ANSI.
- Prefira frases curtas, termos consistentes e instruções reproduzíveis.
- Atualize README, segurança, operação e API junto da implementação.
- Inclua pré-requisitos, impacto, rollback e limitações.
- Links e comandos devem ser verificados antes do merge.

## Gate de conclusão

Uma mudança que altera comportamento, configuração, segurança, instalação ou API
não está pronta sem documentação correspondente.
