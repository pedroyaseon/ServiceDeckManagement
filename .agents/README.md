# Perfis de trabalho

Esta pasta contém contexto persistente para colaboradores e assistentes. Ela não
substitui o código, os testes ou `IMPLEMENTATION_PLAN.md`.

## Ordem de leitura

1. `../AGENTS.md`;
2. `../IMPLEMENTATION_PLAN.md`;
3. `project-scope/AGENT.md`;
4. o perfil especializado da tarefa;
5. `cybersecurity/AGENT.md` para qualquer mudança que altere execução, rede,
   identidade, configuração, logs, persistência, instalação ou privilégio;
6. `documentation/AGENT.md` antes de concluir a PR.

## Protocolo comum

- Inspecione arquivos e estado real antes de afirmar fatos.
- Não invente resultados. Use `planejado`, `implementado`, `testado` e
  `bloqueado` com precisão.
- Escreva de forma técnica e direta.
- Preserve UTF-8 e acentuação correta em português.
- Rejeite sequências típicas de UTF-8 decodificado incorretamente e o caractere
  de substituição Unicode.
- Não persista códigos ANSI ou caracteres de controle invisíveis.
- Não adicione caminho de usuário, segredo, token, certificado ou runtime.
- Use caminhos relativos à raiz portátil.
- Não reintroduza NSSM ou execução por shell.
- Atualize documentação e testes junto da implementação.
- Branches e PRs não usam `agent` ou `codex` no nome.

Um perfil pode interromper uma entrega quando um gate de segurança, teste,
documentação ou evidência não for atendido.
