# Lama.MoveSuggestion.Benchmarks

Micro-benchmark local pour mesurer la latence de `MoveSuggestionEngine`.

## Scenarios

- `first-move`: plateau vide, rack debut de partie.
- `mid-game`: plateau avec ancres existantes.

## Execution

```bash
dotnet run --project /home/philippe/RiderProjects/Games/Lama/tools/benchmarks/Lama.MoveSuggestion.Benchmarks -- --iterations 60 --warmup 10 --top 8
```

## Parametres

- `--iterations <n>`: nombre de mesures (defaut: `60`)
- `--warmup <n>`: nombre de runs de chauffe (defaut: `10`)
- `--top <n>`: nombre max de suggestions calculees (defaut: `8`)

## Sortie

Le programme affiche `min`, `p50`, `p95`, `avg`, `max` en millisecondes pour chaque scenario.

