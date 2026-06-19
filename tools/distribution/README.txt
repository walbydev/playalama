
  ██╗      █████╗ ███╗   ███╗ █████╗
  ██║     ██╔══██╗████╗ ████║██╔══██╗
  ██║     ███████║██╔████╔██║███████║
  ██║     ██╔══██║██║╚██╔╝██║██╔══██║
  ███████╗██║  ██║██║ ╚═╝ ██║██║  ██║
  ╚══════╝╚═╝  ╚═╝╚═╝     ╚═╝╚═╝  ╚═╝

  Jeu de mots en console — inspire du Scrabble

  Portail du jeu  : https://game.playalama.online
  Version         : consulter le nom du ZIP

==============================================================
  TABLE DES MATIERES
==============================================================

  1. Description du jeu
  2. Regles du jeu
  3. Lancer l'application
  4. Guide pour l'hote (createur de partie)
  5. Guide pour un joueur (rejoint une partie)
  6. Options globales
  7. Notes et support

==============================================================
  1. DESCRIPTION DU JEU
==============================================================

LAMA est un jeu de mots inspire du Scrabble, developpe en C# / .NET 10.

  - Posez des mots sur une grille 15x15.
  - Chaque lettre vaut un certain nombre de points.
  - Profitez des cases bonus (multiplicateurs de lettre et de mot).
  - Le premier joueur a vider son rack apres que le sac est epuise gagne.

La console offre deux modes de jeu :

  > Mode interactif  : menus, prompts, affichage du plateau en temps reel.
                       Lance simplement `lama` (ou `lama.exe` sur Windows).

  > Mode commande    : une commande = une action, ideal pour scripts et tests.
                       Ex : `lama game create Alice`

Cet executable est autonome : aucun runtime .NET n'est requis sur la machine.
Les assets de langue (dictionnaire, valeurs des lettres) sont integres.

==============================================================
  2. REGLES DU JEU
==============================================================

PLATEAU
  - Grille carree 15x15 par defaut (configurable de 15 a 26)
  - Coordonnees : lettre = colonne (A-O), chiffre = ligne (1-15)
    Ex : H8 est la case centrale (depart obligatoire du premier mot)
  - Cases bonus : multiplicateurs de lettre (x2, x3) et de mot (x2, x3)

TUILES
  - Chaque lettre a une valeur definie par la langue (style Scrabble classique)
  - 2 jokers par defaut : peuvent remplacer n'importe quelle lettre (0 point)
    Convention : tapez la lettre en MINUSCULE pour indiquer que c'est un joker
    Ex : `lama play move H8 LaMA H`  — ici le `a` minuscule est le joker
  - Rack de 7 lettres par joueur

TOUR DE JEU
  A chaque tour, le joueur actif doit choisir une action :

    Poser un mot    lama play move <case> <mot> <direction>
    Passer          lama play pass
    Echanger        lama play swap <lettres>   ou   lama play swap --all
    Contester       lama play challenge

PLACEMENT D'UN MOT
  - Le premier mot DOIT passer par H8
  - Direction : H (horizontal) ou V (vertical)
  - Chaque mot pose doit etre adjacent ou croiser des lettres existantes
  - Tous les mots formes (principal + croisements) doivent etre dans le dico
  - Longueur minimale : 2 lettres

CROISEMENTS
  Quand votre mot croise un mot existant :
    1. Specifiez le mot COMPLET, lettre de croisement incluse
    2. La lettre doit correspondre a celle deja posee
    3. Le systeme valide les croisements automatiquement

  Exemple : LAMA est pose horizontalement en H8.
  Vous pouvez poser MAISON verticalement en J8 :
    lama play move J8 MAISON V
  Le M de MAISON (en J8) croise le M de LAMA — valide.

SCORE
  - Score de base = somme des valeurs des lettres posees
  - Multiplicateurs de lettre s'appliquent avant multiplicateurs de mot
  - Tous les mots formes dans le meme tour sont comptes
  - Bonus 50 pts si vous posez les 7 lettres du rack en un seul coup (Bingo)

FIN DE PARTIE
  La partie se termine quand :
    - Le sac est vide ET un joueur epuise son rack
    - Le nombre max de tours est atteint (--max-turns N)
    - Le score max est atteint (--max-score N)
    - Un joueur met fin manuellement : lama game end

==============================================================
  3. LANCER L'APPLICATION
==============================================================

Linux / macOS :
  chmod +x ./lama          # (premiere fois uniquement)
  ./lama                   # mode interactif

Windows :
  lama.exe                 # mode interactif

macOS — sécurité Gatekeeper :
  Si macOS bloque l'executable au premier lancement :
    Aller dans Preferences Systeme > Confidentialite et securite
    Cliquer "Ouvrir quand meme" en regard de lama
  Ou via terminal :
    xattr -d com.apple.quarantine ./lama

==============================================================
  4. GUIDE POUR L'HOTE (CREATEUR DE PARTIE)
==============================================================

L'hote cree la partie et y ajoute les joueurs.

--- Creer une partie ---

  lama game create Alice

  Resultat : affiche l'ID de la partie et le rack initial d'Alice.

--- Ajouter des joueurs ---

  lama game join Bob
  lama game join Sophie

  Chaque joueur rejoint dans l'ordre ; l'ID de partie est stocke en session.

--- Parametres avances (optionnels) ---

  lama game create Alice --board-size 21   # plateau 21x21
  lama game create Alice --rack-size 8     # rack de 8 lettres
  lama game create Alice --max-turns 50    # limite de tours

--- Afficher l'etat de la partie ---

  lama show board          # affiche le plateau
  lama show rack           # affiche le rack du joueur courant
  lama show scores         # tableau des scores

--- Jouer un mot ---

  lama play move H8 LAMA H       # LAMA horizontal a partir de H8
  lama play move J8 MAISON V     # MAISON vertical a partir de J8

--- Passer son tour ---

  lama play pass

--- Echanger des lettres ---

  lama play swap AEI             # echange A, E et I du rack
  lama play swap --all           # echange toutes les lettres

--- Contester le dernier mot adverse ---

  lama play challenge

--- Terminer la partie ---

  lama game end

--- Afficher l'aide ---

  lama --help
  lama game --help
  lama play --help

==============================================================
  5. GUIDE POUR UN JOUEUR (REJOINT UNE PARTIE)
==============================================================

Un joueur rejoint une partie existante identifiee par son ID.

--- Rejoindre ---

  lama game join Bob --game-id <ID_DE_LA_PARTIE>

  Si vous jouez sur la meme machine que l'hote (sessions isolees),
  definissez LAMA_SESSION_DIR avant de lancer :

    Linux/macOS :
      export LAMA_SESSION_DIR=~/.config/lama/session-bob
      lama game join Bob --game-id <ID>

    Windows (PowerShell) :
      $env:LAMA_SESSION_DIR = "$env:APPDATA\lama\session-bob"
      lama game join Bob --game-id <ID>

--- Jouer ---

  lama play move J8 MAISON V
  lama play pass
  lama play swap AEI
  lama play challenge

--- Consulter ---

  lama show board
  lama show rack
  lama show scores

--- Suggestion de coup ---

  lama play suggest             # propose des mots jouables
  lama play suggest --top 5     # top 5 suggestions

==============================================================
  6. OPTIONS GLOBALES
==============================================================

Ces options s'ajoutent a n'importe quelle commande :

  --output json     Sortie en JSON (ideal pour scripts)
  --output csv      Sortie en CSV
  --no-color        Desactive les couleurs ANSI
  --lang fr         Force la langue (fr par defaut)

Exemple :
  lama show board --output json --no-color

Variables d'environnement :

  LAMA_SESSION_DIR    Dossier de session (fichiers de partie)
                      Defaut : ~/.config/lama
  LAMA_RUNTIME_MODE   online | local (defaut : local)
  LAMA_SERVER_URL     URL du serveur online

==============================================================
  7. NOTES ET SUPPORT
==============================================================

  - L'executable est autonome, aucune dependance externe requise.
  - Les donnees de partie sont stockees dans ~/.config/lama/games/
    (ou dans le dossier LAMA_SESSION_DIR si defini).
  - Les sauvegardes sont automatiques apres chaque action.

  Pour les nouveautes, le mode multijoueur en ligne et les annonces :
    https://game.playalama.online

  Bonne partie !

==============================================================

