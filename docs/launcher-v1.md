# Launcher v1

## Objetivo

O Launcher é o cliente desktop local do Service Deck Management. Ele apresenta
o inventário de serviços, estado da API e do Manager, comandos operacionais,
edição de definições e logs. Toda a comunicação ocorre pela API v1; o aplicativo
não acessa o SCM, o Named Pipe ou um shell.

Versão: `1.0.0-beta.2`.

## Execução em desenvolvimento

Com a API e o Manager iniciados na mesma raiz portátil:

```powershell
& '.\scripts\dotnet.ps1' run --project '.\src\ServiceDeckManagement.Launcher'
```

O manifesto usa `asInvoker`. Abrir o Launcher não solicita elevação. As
operações privilegiadas são autorizadas pela API e executadas exclusivamente
pelo Manager.

## Autenticação e papéis

Na primeira execução, a API exibe no console um código temporário. O Launcher
solicita esse código, o nome e a senha para criar o primeiro administrador.
Depois disso, apresenta somente o login normal.

- visualizador: consulta inventário, detalhes e logs;
- operador: inicia, para e reinicia serviços;
- administrador: também adiciona, edita, repara e remove.

O token de acesso permanece somente na memória do processo e é revogado ao
fechar a janela. Senhas e tokens não são salvos em arquivo ou em logs.

## Atualização em tempo real

Após o login, o Launcher conecta ao endpoint SignalR `/api/v1/events`. Cada
snapshot tem uma sequência monotônica. Se houver um salto de sequência, o
Launcher busca novamente o inventário completo antes de continuar. Quando o
canal em tempo real cai, o aplicativo ativa sincronização periódica pela API;
não existe botão de atualizar nem recarga da janela.

## Adição e edição

`Adicionar` oferece duas escolhas:

- `Adicionar nova`: cria uma definição com identificador ainda não existente;
- `Adicionar existente`: registra no produto um executável já presente na
  raiz portátil.

O editor aceita somente executáveis e diretórios dentro da pasta do produto.
Os caminhos enviados à API são relativos. Argumentos são transmitidos como uma
lista, sem concatenação ou interpretação por shell.

`Remover` exige confirmação explícita. A API solicita ao Manager que pare o
serviço, remova o registro identificado do SCM e, somente depois, exclua a
definição. Falhas mantêm o erro visível na faixa inferior da janela.

## Logs

O painel mostra stdout e stderr sanitizados pelo Host, sempre em UTF-8 e sem
sequências ANSI. `Limpar` afeta apenas a visualização atual. `Copiar` envia o
texto exibido para a área de transferência e `Exportar` cria um arquivo escolhido
pelo usuário. Nenhuma contagem de eventos é exibida no título.

## Configuração local

O arquivo opcional `config/launcher.json` segue
`config/schemas/launcher.v1.schema.json`. Na beta.2, a URL deve usar HTTP em um
endereço IP de loopback, porta não privilegiada e caminho raiz. A restrição
evita exposição remota antes da etapa de HTTPS. O arquivo real é local e está
protegido pelo `.gitignore`.

## Testes iniciais

Os testes automatizados cobrem configuração segura, autenticação, propagação
do bearer token, bloqueio de chamadas sem sessão e mensagens de falha estáveis.
A compilação WPF valida todos os recursos, bindings e handlers declarados em
XAML. O teste administrativo real do SCM deve ocorrer em uma VM Windows
descartável e permanece separado da suíte normal.
