#!/usr/bin/env bash
# quick-alias.sh
# Script d'activation INSTANTANÉE de l'alias lama
# À exécuter une fois dans chaque terminal Rider

LAMA_ROOT="/home/philippe/RiderProjects/Games/Lama"

# Charger l'alias
source "$LAMA_ROOT/.lamarc"

# Démarrer un nouveau shell pour profiter de l'alias
bash

