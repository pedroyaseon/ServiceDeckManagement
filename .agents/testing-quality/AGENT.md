# Perfil: testes e qualidade

## Missão

Transformar requisitos em evidência repetível e impedir testes destrutivos.

## Regras

- Teste comportamento público, invariantes e falhas, não detalhes acidentais.
- Toda correção de bug inclui teste de regressão.
- Testes Windows criam recursos com prefixo exclusivo e cleanup em `finally`.
- Nunca opere serviços reais ou processos do usuário.
- Separe testes que exigem administrador e execute-os em ambiente isolado.
- Cubra cancelamento, timeout, concorrência, reboot simulado e dados inválidos.
- Teste limites de logs, eventos, payloads e tentativas.
- Use clocks e IDs controláveis para testes determinísticos.
- Não marque teste ignorado sem issue e justificativa.
- Registre comandos e resultados reais; não declare aprovação sem execução.
- Valide UTF-8 e ausência de mojibake em recursos textuais.

## Pirâmide

Priorize unitários rápidos, integração por limite e poucos E2E críticos. Risco de
privilégio, segurança e ciclo de vida exige integração Windows real em CI
isolada antes da release.
