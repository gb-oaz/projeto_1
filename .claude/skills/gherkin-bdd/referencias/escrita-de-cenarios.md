# Boas práticas de escrita de cenários (para propor ao PO)

Quem escreve no `.md` é o `deboaz-po`, no formato da skill `requisito`. Os outros agentes usam isto para **propor** um cenário na resposta, já no estilo do projeto.

## Princípios

1. **Declarativo, não imperativo**: descreva a intenção, não os cliques. `Quando informa o código e entra`, não `Quando clica no campo, digita… e clica no botão azul`.
2. **Dados concretos**: pessoa com nome (das personas de `00_visao-produto.md`), valores reais, texto exibido entre aspas. `Dado a staff Maria Souza, ativa, da escola "EBFGA"`.
3. **Um comportamento por cenário**: um `Quando`; vários `Então/E` só se forem do mesmo resultado.
4. **Observável**: o `Então` diz o que a pessoa vê ou o que a API devolve, nunca estado interno (tabela, classe, variável).
5. **Linguagem ubíqua**: termos do `99_glossario.md`, iguais em cenário, vista, contrato e código.
6. **Sem tecnologia em cenário de tela**: endereço, status e código de erro só em `cenarios-api`.
7. **Curto**: 1 a 3 cenários por regra; linha ≤ 100 caracteres (quebre em mais um `E`).
8. **Tabela vira `Esquema do Cenário`**: inclua as linhas de fronteira e as de recusa.
9. **JSON expandido**, um campo por linha, em doc string `"""`.

## Antipadrões

| Ruim | Melhor |
|---|---|
| `Então o sistema funciona corretamente` | `Então vê "Cadastro salvo." e o João aparece na lista` |
| `Dado um usuário` | `Dado o educador João Lima, ativo, da escola "EBFGA"` |
| `Quando clica em #btn-salvar` | `Quando salva o cadastro` |
| Dois `Quando` no mesmo cenário | dois cenários |
| `Então grava na coleção usuarios` | `Então a resposta é 201 com o id do João` (API) |
| Só o caminho feliz | caminho feliz + recusa + limite |

## Derivar os cenários que faltam

- **Negativos**: dado inválido, obrigatório ausente, duplicado, sem permissão, recurso inexistente, sessão vencida, estado que não permite a ação.
- **Alternativos**: outro ator, outra plataforma (web, PWA, mobile), outro caminho válido para o mesmo resultado.
- **Limites** (`RNF` com número): no limite, logo abaixo, logo acima.
- **Estados de tela**: vazio, carregando, erro, sucesso, sem conexão.

## Formato da proposta (na resposta, para o `deboaz-po`)

````
Proposta para 1_Requisitos/<doc>.md — <RF-xx ou "novo ID">
Motivo: <lacuna/ambiguidade encontrada>

```gherkin
Cenário: educador sem permissão tenta salvar
  Dado o educador João Lima sem a permissão "Editar usuários"
  Quando abre o cadastro da Maria
  Então vê o cadastro só para leitura, sem "Salvar"
```
````
