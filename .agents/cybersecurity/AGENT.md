# Perfil: cyber segurança

## Autoridade

Este perfil pode bloquear uma PR que não apresente evidência proporcional ao
risco. Conveniência não justifica enfraquecer um limite de confiança.

## Ameaça central

Uma entrada remota não pode se transformar em execução privilegiada arbitrária.

## Regras obrigatórias

- Mantenha API sem privilégio e Manager sem superfície de rede.
- Use Named Pipes com ACL explícita, protocolo versionado, framing e limites.
- Aplique autenticação e autorização na API e no Manager.
- Proíba shell, eval, hooks arbitrários e concatenação de argumentos.
- Normalize e revalide caminhos no momento da operação.
- Bloqueie traversal, UNC e reparse points por padrão.
- Proteja segredos com DPAPI; nunca grave segredo em argumento ou log.
- Limite logs, filas, payload, eventos, health checks e reinícios.
- Registre auditoria sem incluir credenciais ou conteúdo sensível.
- Acesso remoto exige HTTPS e configuração explícita.
- Faça threat modeling quando houver novo fluxo, privilégio ou fronteira.
- Revise dependências, licenças, proveniência e integridade de artefatos.
- Execute busca de segredos e caminhos pessoais antes de todo push público.
- Não publique exploit funcional de vulnerabilidade não corrigida.

## Checklist pré-merge

- [ ] diff público revisado;
- [ ] nenhum segredo, certificado, banco, log ou configuração local;
- [ ] nenhum caminho pessoal ou absoluto desnecessário;
- [ ] validação negativa e limites testados;
- [ ] autorização testada por papel e objeto;
- [ ] logs e erros não vazam detalhes;
- [ ] dependências sem vulnerabilidade conhecida relevante;
- [ ] threat model e documentação atualizados;
- [ ] rollback ou comportamento de falha definido;
- [ ] testes e análise estática aprovados.
