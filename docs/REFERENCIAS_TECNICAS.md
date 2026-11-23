# Referências técnicas avaliadas

As referências foram consultadas para identificar padrões úteis. A solução mantém as decisões confirmadas para este projeto: API como único acesso ao MySQL, painel MVC, MAUI para Android, associação global da pessoa aprovada e fila offline de ações permitidas.

## ProClubes

- Inspirou ViewModels por tela, estado de sincronização visível e cache vinculado à pessoa autenticada.
- O ProClubes limita o modo offline principalmente à consulta; este projeto mantém a fila de reservas e manifestações prevista no roteiro.
- Os módulos de votação, carteirinha QR e encargos financeiros ficam fora do escopo inicial.

## VanderClub

- Reforçou a importância de contratos da API documentados, validação de respostas e testes HTTP com armazenamento isolado.
- O repositório separa uma especificação extensa de uma declaração de implementação; funcionalidades adicionais não foram tratadas como prontas só por aparecerem no documento.
- Sua interface móvel usa code-behind e não oferece fila offline de gravação na entrega descrita, então a estrutura MVVM e a fila local deste projeto foram mantidas.

## Projeto-Clube

- Reforçou testar autorização por clube, colisões de agenda e processamento idempotente da fila.
- O padrão de painel Blazor aparece em várias referências, mas MVC continua adequado e corresponde à arquitetura originalmente aprovada. Migrar o painel agora não reduziria o risco das melhorias solicitadas.
- A trava de agenda implementada coordena requisições simultâneas dentro de uma instância da API; para múltiplas instâncias, será necessária coordenação persistente.

Os exemplos são referências de projeto, não dependências de runtime. Os contratos e os fluxos deste código continuam definidos pelos requisitos do Projeto Clubes.
