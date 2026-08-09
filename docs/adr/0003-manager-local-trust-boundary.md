# ADR 0003 — Limite local privilegiado do Manager

## Status

Aceito para `1.0.0-alpha.4`.

## Decisão

O Manager será um Serviço do Windows sem endpoint de rede. Operações locais usam
Named Pipe v1 com ACL explícita, rejeição de clientes remotos, framing limitado,
nonce e HMAC. A identidade e o papel vêm do token do Windows. O SCM é acessado
por APIs nativas e uma entrada só é alterada após verificação de pertencimento.

Definições usam substituição atômica. Auditoria usa uma cadeia SHA-256 para
detectar alteração acidental. A chave HMAC fica protegida por DPAPI da máquina e
por ACL do diretório.

## Consequências

- a API não recebe privilégio de SCM;
- o cliente API usa SID explicitamente provisionado e autorização delegada
  revalidada pelo Manager, conforme o ADR 0004;
- payloads grandes e conexões remotas falham antes do dispatcher;
- testes normais usam backend de SCM em memória;
- validação nativa do SCM exige VM descartável e autorização administrativa;
- a auditoria local não é considerada inviolável contra administrador local.
