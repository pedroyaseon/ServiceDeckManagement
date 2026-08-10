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
- a comunicação de clientes locais com o Manager usa Named Pipes autenticados
  por ACL e HMAC;
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
- schema JSON estrito, sem propriedades desconhecidas ou duplicadas;
- IDs, argumentos, variáveis, limites operacionais e health checks validados;
- dependências restauradas por fonte única, versão central e lock file;
- `.gitignore` e conteúdo público verificados automaticamente;
- inicialização direta de arquivos `.exe`, sem shell e com argumentos separados;
- Windows Job Object para conter a árvore de processos;
- stdout e stderr limitados, sanitizados e persistidos em UTF-8;
- rotação e retenção de logs com limites de arquivo e total;
- reinício com backoff, limite de tentativas e circuit breaker;
- health checks limitados a processo e alvos HTTP/TCP em loopback;
- Manager sem listener TCP ou HTTP;
- Named Pipe com ACL explícita para LocalSystem e Administradores, além da opção
  nativa `PIPE_REJECT_REMOTE_CLIENTS`;
- frames de 64 KiB, timeout de sessão e uma requisição por conexão;
- autenticação mútua HMAC-SHA-256 com nonce de 256 bits;
- chave de transporte protegida por DPAPI da máquina e ACL local restrita;
- clientes diretos recebem papel pelo token do Windows; o Launcher só é aceito
  pelo SID explicitamente configurado e não controla sua função pelo payload;
- identidade delegada só é aceita do SID separado da API e é reautorizada pelo
  Manager;
- registros do SCM identificados por namespace, marcador e comando esperado;
- persistência de definições com substituição atômica e flush em disco;
- auditoria append-only com cadeia SHA-256 para detectar alteração acidental.

O Service Host não é um sandbox para executar código hostil. Somente aplicações
confiáveis, escolhidas por um administrador, podem ser gerenciadas. A cadeia
SHA-256 da auditoria detecta alteração acidental, mas não substitui um destino de
auditoria externo contra um administrador local malicioso.

## Limites da beta.4

- a API aceita somente loopback; acesso remoto aguarda HTTPS e configuração de
  origens explícita;
- os SIDs dedicados da API e do Launcher devem ser provisionados separadamente
  em `config/manager-security.json`;
- o pipeline normal não modifica o SCM real;
- instalação, upgrade e desinstalação completas permanecem na etapa do
  instalador; a beta.4 configura e repara somente o Manager pelo helper elevado;
- o Launcher executa como usuário comum, não chama SCM ou shell e usa somente o
  canal local autenticado do Manager; nenhuma API ou credencial é exigida;
- o helper elevado aceita apenas a operação fechada `install-manager`, recusa
  serviços sem marcador do produto e remove escrita e propriedade do usuário
  comum sobre os binários privilegiados;
- referências de segredo continuam recusadas pelo Host até a integração segura
  entre Manager e Host ser concluída.
