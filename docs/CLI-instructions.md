# Instructions pour l'usage de la console CLI

## Création d'une nouvelle partie
```
lama create new game > new_game_id.txt
```

## Rejoindre une partie existante
```
lama join as philippe game < join_game_id.txt
lama join as sophie game < join_game_id.txt
```

## Lister les parties en cours avec les joueurs connectés
```
lama list games
```

## Lister les parties en cours avec les joueurs connectés et leurs scores
```
lama list games --with-scores
```

## Lister les parties en cours avec les joueurs connectés et leurs scores, triées par score décroissant
```
lama list games --with-scores --sort-by-score
```

## Terminer une partie
```
lama end game < end_game_id.txt
```

## Terminer une partie et afficher le classement final
```
lama end game < end_game_id.txt --with-scores
```

## Terminer toutes les parties en cours
```
lama end all games
``` 

## Redémarrer le système de jeu
```
lama restart system
```
## Jouer un coup
Placer le mot "MAISON" à partir de la case H8 horizontalement.
```
lama play H8 MAISON H
```
## Jouer un autre coup
Placer le mot "NOISETTE" à partir de la case H13 verticalement.
```
lama play H13 NOISETTE V
```
## Passer son tour
```
lama play nothing
```
## Réafficher son rack

```
lama show rack
```

## Réafficher le plateau de jeu

```
lama show plateau
```



