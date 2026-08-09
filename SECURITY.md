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

## Controles implementados

- resolução de caminhos limitada à raiz portátil;
- bloqueio de caminhos absolutos, UNC, traversal, nomes de dispositivo,
  segmentos ambíguos e reparse points existentes;
- schema JSON estrito, sem propriedades desconhecidas;
- IDs, argumentos, variáveis, limites operacionais e health checks validados;
- valores com nomes sensíveis direcionados a referências de segredo;
- dependências restauradas por fonte única, versão central e lock file;
- `.gitignore` testado automaticamente;
- verificação pública de segredos, caminhos pessoais e encoding.
- inicialização direta de arquivos `.exe`, com `UseShellExecute = false` e
  argumentos separados;
- revalidação do executável e do diretório imediatamente antes da execução;
- Windows Job Object com `KILL_ON_JOB_CLOSE` para conter a árvore de processos;
- stdout e stderr limitados, sanitizados e persistidos em UTF-8;
- rotação e retenção de logs com limites de arquivo e total;
- reinício com backoff, limite de tentativas e circuit breaker;
- health checks limitados a processo e alvos HTTP/TCP em loopback;
- falha fechada para referências de segredo enquanto o Manager não existe.

O Service Host não é um sandbox para executar código hostil. Somente aplicações
confiáveis, escolhidas por um administrador, podem ser gerenciadas. Autorização
em duas camadas, DPAPI, Named Pipes e operações no SCM pertencem às próximas
etapas e não devem ser descritas como implementadas.
