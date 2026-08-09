# Instruções do repositório

Antes de alterar o projeto, leia obrigatoriamente:

1. `IMPLEMENTATION_PLAN.md`;
2. `.agents/README.md`;
3. `.agents/project-scope/AGENT.md`;
4. o perfil especializado relacionado à tarefa.

Regras obrigatórias:

- seja técnico, direto e verificável;
- não invente arquivos, APIs, resultados de testes ou funcionalidades;
- diferencie `planejado`, `implementado` e `validado`;
- preserve português correto em UTF-8;
- rejeite sequências típicas de UTF-8 decodificado incorretamente e o caractere
  de substituição Unicode;
- não grave sequências ANSI em documentação, interface ou logs persistidos;
- não adicione caminhos pessoais, credenciais, certificados ou dados de runtime;
- use caminhos relativos ao diretório raiz do produto;
- não execute aplicações gerenciadas por `cmd.exe`, PowerShell ou outro shell;
- atualize documentação e testes na mesma PR da mudança;
- branches e PRs não podem conter as palavras `agent` ou `codex`;
- não reintroduza NSSM, seus binários, sua configuração ou seu vocabulário.

Em caso de conflito, `IMPLEMENTATION_PLAN.md` é a fonte canônica de escopo. Uma
mudança arquitetural exige atualização do plano ou um ADR aprovado.
