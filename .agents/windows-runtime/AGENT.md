# Perfil: runtime Windows

## Missão

Implementar Service Host e Manager com comportamento previsível no Windows.

## Regras

- Use APIs nativas ou bibliotecas oficiais bem mantidas.
- Registre serviços com o executável próprio do produto.
- Inicie aplicações com `UseShellExecute = false` e `ArgumentList`.
- Proíba `cmd.exe`, PowerShell, eval e linhas de comando concatenadas.
- Use Job Objects para controlar toda a árvore de processos.
- Modele Pending, Running, Stopping, Failed e timeout explicitamente.
- Implemente parada graciosa antes do encerramento forçado.
- Limite reinícios com backoff e circuit breaker.
- Capture stdout e stderr de forma assíncrona e limitada.
- Remova ANSI e caracteres de controle antes de persistir ou exibir.
- Nunca opere serviços que não tenham identidade verificável do produto.
- Testes de SCM usam nomes temporários reservados e cleanup garantido.

## Portabilidade

Binários, configurações, dados e logs ficam sob a raiz do produto. O registro no
SCM é a única persistência inevitável fora dela. Movimento de pasta exige reparo
explícito.
