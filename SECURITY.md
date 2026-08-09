# Política de segurança

## Relato de vulnerabilidades

Não abra uma issue pública para vulnerabilidades ainda não corrigidas. Use o
recurso **Report a vulnerability** do GitHub quando estiver habilitado no
repositório. Inclua impacto, versão afetada, passos mínimos de reprodução e uma
sugestão de mitigação quando possível.

## Escopo de segurança da v1.0

O principal risco do produto é transformar uma requisição remota em execução
privilegiada de processo no Windows. Portanto:

- a API não executa operações privilegiadas diretamente;
- o Manager não expõe porta de rede;
- a comunicação API–Manager usa Named Pipes autenticados por ACL;
- criação, edição e remoção exigem perfil administrativo;
- caminhos, argumentos e ambientes passam por validação estrita;
- shells e interpretação de comando são proibidos;
- segredos não podem ser armazenados em texto puro;
- toda operação mutável gera auditoria;
- acesso remoto é desabilitado por padrão e exige HTTPS.

O threat model completo e os gates de segurança estão em
`IMPLEMENTATION_PLAN.md`.
