# Introdução e Arquitetura

O **ShopFloor Digital Twin** replica virtualmente uma oficina de restauro de carros clássicos, com foco na monitorização e documentação do processo.

## Objetivos

- Representar no espaço virtual a oficina e os veículos.
- Sincronizar dados de sensores IoT com o ambiente virtual.
- Demonstrar em tempo real o progresso do restauro.

## Arquitetura do Sistema

**Componentes principais:**

- **Unity 3D**: Motor de visualização e simulação do ambiente da oficina e dos carros.
- **Camunda BPM**: Orquestração e gestão dos processos de restauro.
- **APIs e IoT**: Comunicação com sensores reais ou simulados.
- **Base de Dados (Charter of Turin Monitor)**: Registo e histórico do restauro.

## Fluxo de Dados

1. Sensores e operadores enviam dados.
2. API processa e envia para a aplicação Unity.
3. Unity atualiza a simulação em tempo real.
4. Camunda gere a lógica do workflow e etapas do restauro.

## Foco da Documentação

Esta documentação não cobre a API interna de Unity (por motivos técnicos), mas descreve:

- Como correr e usar a aplicação.
- Como os dados são processados e visualizados.
- Como adaptar ou expandir o sistema.

---
