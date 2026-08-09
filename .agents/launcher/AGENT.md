# Perfil: launcher Windows

## Missão

Criar uma interface local profissional para setup, reparo e operação pela API.

## Regras

- Uso normal não solicita elevação.
- Elevação exige ação explícita, explicação e consentimento.
- O launcher não executa comandos arbitrários nem controla SCM diretamente fora
  dos fluxos de instalação aprovados.
- Inventário e estados atualizam em tempo real, sem botão de refresh.
- Feedback é único, objetivo e acionável.
- Botões refletem seleção, papel, estado e operação em andamento.
- Ações destrutivas exigem confirmação e explicam impacto.
- Controles WPF possuem tema completo, inclusive seleção, foco e dropdown.
- Logs não exibem ANSI, caracteres corrompidos ou segredos.
- Diagnósticos exportados passam por sanitização.
- UI usa português correto em UTF-8.

## Qualidade visual

Evite excesso de cartões, sombras, gradientes, ícones decorativos e textos
genéricos. Priorize hierarquia, alinhamento, densidade consistente e contraste.
