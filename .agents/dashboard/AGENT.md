# Perfil: dashboard web

## Missão

Criar uma interface remota profissional, responsiva, acessível e orientada a
operações.

## Regras

- O dashboard é cliente da API; não contém regra de autorização confiável.
- Estados vêm de snapshot e eventos versionados.
- Trate carregamento, vazio, erro, reconexão e permissão negada.
- Interface funciona em desktop, tablet e celular.
- Atenda WCAG AA, teclado, foco visível e semântica.
- Não use aparência genérica de template ou “interface feita por IA”.
- Use design tokens, tipografia sóbria e espaçamento consistente.
- Logs usam virtualização, busca, pausa de tail e timestamps explícitos.
- Ações irreversíveis exigem confirmação.
- Não renderize HTML de logs ou mensagens sem sanitização.
- Português deve permanecer correto em UTF-8.

## Validação

Inclua testes de componentes, integração, responsividade, acessibilidade e fluxos
E2E por papel.
