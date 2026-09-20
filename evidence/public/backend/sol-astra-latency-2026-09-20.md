# D10 — diagnostic de latence et changement des modèles

## Mesures effectivement lues

Lecture seule de PostgreSQL le 20 septembre 2026. Les durées proviennent de
`jobs.created_at/updated_at` et de `provider_attempts.started_at/finished_at` ;
aucun appel modèle n'a été exécuté pour ce relevé historique. Le diagnostic
actif Sol → Astra exécuté ensuite est décrit séparément plus bas.

| Job réel | Titre | Réception → prêt | Attente avant A | A | B et réparations |
|---|---|---:|---:|---:|---:|
| `cc4d17da-c003-4f13-95f2-c9ab255141ff` | Envol du spectre aux longs voiles | 285 s | 1 s | 143 s | 113 s rejeté, puis 28 s réparation |
| `9ac45ba4-a730-45ce-b4f0-8c71a8580449` | Éclosion des cinq pétales | 214 s | 2 s | 132 s | 80 s |
| `7851777c-ddb1-49eb-84f2-641463b15364` | Le Nœud filant | 214 s | 1 s | 128 s | 84 s |
| `e3537609-1196-47d6-8750-fe79124f1a0a` | Enclume filante | 1 869 s | 1 s | 114 s | échec processus 2 s ; reprise ultérieure 78 s |

Le dernier dessin a donc pris **4 min 45 s côté serveur**. Le diagnostic ne
retrouve pas 50 minutes pour cette génération. L'Enclume a en revanche passé
environ 28 minutes entre son échec B et sa reprise : cette attente n'était
pas du calcul modèle. Une ancienne réparation de « Crosse de rappel » a pris
921 s et retourné 25 380 tokens de raisonnement.

Les dernières étapes A utilisaient Astra/max, environ 28 880 tokens d'entrée ;
B utilisait Luna/max, environ 17 216 tokens d'entrée. La file ne contribuait
que 1–2 secondes aux derniers jobs observés. Ces mesures expliquent des
minutes de génération mais ne constituent pas une garantie de durée future.

## Correction implémentée

- Politique explicite D10 : A `gpt-5.6-sol` / `high`, B `gpt-6-astra` / `high`.
- Chaque étape conserve un processus `codex exec` isolé, sans outils, ni
  nouvelle authentification, ni clé API, ni recharge automatique.
- B recevait déjà uniquement les recettes mentionnées dans A. La projection
  réduit désormais aussi les définitions de porteurs, d'effets et de formes
  à celles de la description figée. Les limites globales et la composition
  VFX restent présentes ; le compilateur conserve le catalogue complet.
- A reçoit les familles d'effets, sans toutes les bornes numériques utiles
  seulement au plan B. Toutes les recettes restent disponibles pour A.
- Le schéma de sortie est minifié sans retirer de contrainte. Avant l'ajout
  VFX D10, les mêmes schémas passaient de 19 862 à 7 854 caractères pour A et
  de 33 845 à 15 231 pour B. Ce sont des tailles de fichiers, pas des mesures
  de tokens ni une accélération fournisseur démontrée.
- Les tentatives privées enregistrent les octets prompt/schéma et la durée
  mesurée. Le doctor ajoute début, fin et durée de chaque étape.
- GET job expose les horodatages serveur, `elapsed_ms`, et le début réel
  d'une tentative en cours. Il n'invente pas de pourcentage ni de temps restant.
- Les règles de reprise durable demeurent : une tentative incertaine n'est
  pas relancée automatiquement ; A reste figé lors d'une réparation de B.

## Activation technique vérifiable

`ops/configure-sol-astra.ps1` prépare les nouveaux réglages et invalide les
anciennes preuves Luna/max. Un doctor local doit observer les outils
désactivés ; un doctor actif doit ensuite avoir réellement exécuté Sol/high
et Astra/high, validé le plan et compilé les données. Le script vérifie les
hashes du CLI, des sorties et du rapport avant d'activer les portes techniques.
Il ne prétend jamais établir une acceptation visuelle humaine.

## Couple Sol → Astra réellement exécuté

Le [rapport actif du 20 septembre](sol-astra-active-2026-09-20.json) atteste
deux vrais processus `codex exec` sous `PalRuntimeSvc`, avec modèles et efforts
rapportés identiques à ceux demandés :

| Étape | Modèle / effort | Durée mesurée | Issue |
|---|---|---:|---|
| A, interprétation | `gpt-5.6-sol` / `high` | 31,177 s | Succès, code 0 |
| B, composition | `gpt-6-astra` / `high` | 29,226 s | Succès, code 0 |

La somme des deux appels est **60,403 s**, sans réparation. Le diagnostic
complet a duré environ **63 s** lors de l'intégration. Le plan est validé et
compilé ; sa géométrie provient de l'interprétation contrôlée
`sp.geometry.resolver/2.0.semantic`. Le paquet du diagnostic a pour SHA-256
`68BC6277F1D56947C7FCC2F1B17F2B5C9D06B5B5C396C0D89555D00DBBD8AE68`.
Le rapport public a pour SHA-256
`C2F8885CCD7D40E70CDA5F36B6594EF509380ECC19ADCBC88514128C4FB04401`.

Le diagnostic reprend les images du dessin ayant donné le spectre historique
en **285 s**. Ce nouvel essai est plus court sur cette entrée, mais il ne
mesure pas le parcours complet d'un nouveau dessin envoyé par le Player et
ne constitue pas une garantie de performance : il porte sur un seul dessin,
avec de nouveaux modèles, efforts et prompts, et sans la réparation B du job
historique. Les contributions de chaque changement ne sont pas isolées.

L'[API et le worker D10 sont déployés localement](sol-astra-deployment-2026-09-20.json) ;
la santé HTTP répond 200, l'API PID `8932` et le worker PID `19656` tournent sous
`PalRuntimeSvc`, les 17 fichiers de spécification correspondent et la porte technique du
nouveau couple est approuvée. Aucun verdict artistique humain n'est déduit
de cette activation. Les générations joueur ne lancent aucun test ni build.
