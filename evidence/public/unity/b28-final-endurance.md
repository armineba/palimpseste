# Endurance du lecteur Unity final — 19 septembre 2026

**Build mesuré :** ZIP Windows x64 IL2CPP de 44 000 288 octets,
SHA-256 `343a585873220c1511c53c30d37412208fdbc5ad29845f6060c817e4833791c2`.
Une copie vérifiée par hash a été extraite dans le dossier de preuve privé.
Le lecteur extrait a été lancé en fenêtre 1280 × 720 avec
`-palimpseste-b28`. Machine : AMD Ryzen 7 5800X, NVIDIA RTX 3070,
32 GiB de RAM, Windows 11 Famille.

La fixture **manuelle locale** « Braise en sillage » a été ouverte hors ligne.
Pendant l'essai, 64 clics de lancement ont été envoyés : un initial,
13 espacés de 50 s et deux bursts de 20 puis 30 clics espacés de 1 s.
Deux remises à zéro ont été déclenchées. Le jeu admet les lancers selon son
cooldown : après le second reset, le [HUD final](offline-final-endurance.png)
montrait **19 lancers, 69 touches, 100,0 dégâts et 44 statuts**. Ces nombres
ne sont pas le total de toute la session, puisque les resets remettent les
compteurs à zéro. Cette fixture ne provient pas de Luna.

| Mesure du processus Windows | Premier point | Dernier point | Maximum observé |
|---|---:|---:|---:|
| Ensemble résident (RSS), octets | 617 381 888 | 630 480 896 (+13 099 008) | 630 669 312 |
| Octets privés, octets | 868 331 520 | 895 758 336 (+27 426 816) | 897 040 384 |
| Temps CPU cumulé, secondes | 392,219 | 3 562,188 (+3 169,969) | — |

L'échantillonnage a duré **662,734 s au mur** du 19:29:36,898 au
19:40:39,632 UTC, avec **133 points** espacés d'environ 5 s. Le dernier point
est à 662,722 s ; tous rapportent `Responding=True`. Le processus n'est pas
sorti pendant la mesure et sa fenêtre a ensuite été fermée normalement.
Ces compteurs sont ceux de Windows ; ils ne séparent pas les coûts du moteur,
du rendu et de la logique des sorts.

Le [sous-ensemble public du log](player-b28-final-excerpt.log) contient
79 fenêtres de dix secondes du compteur interne de la boucle Unity, entre
**1 245,8 et 1 633,6 frames calculées par seconde**, et au plus deux
instances de sort actives simultanément. Le compteur a commencé à l'entrée
dans le labo avant l'échantillonnage Windows et a continué jusqu'à la
fermeture. Il ne mesure ni les images présentées à l'écran ni une latence
par frame. Aucune capture Unity Profiler n'a été produite : le lecteur livré
est compilé avec `BuildOptions.None`, alors que [Unity demande un Development
Build pour profiler un Player](https://docs.unity3d.com/cn/6000.0/Manual/profiling-target-device.html).

Les preuves brutes sont conservées sous
`C:\ProgramData\Palimpseste\evidence-private\b28-endurance-20260919\final-343a5858`,
hors Git avec ACL privées. Le CSV `samples.csv` a le SHA-256
`14f416f7aafd3321f97debd78c3237c682d5d34c76375b109378cb2ccdb5848e` ;
le log intégral `player.log`,
`725901e7cc95e3b8e7aaaf95b12c9aad86b17630d36eb29f902a7a5757447781`.
La capture publique revue a le SHA-256
`6bf03d8e0c7d609339d4348822c7760fbda8395b24402b3e16fdcb9e7ba11e1f`.

Cette endurance documente une croissance observée de 13,1 Mo RSS et
27,4 Mo d'octets privés sur onze minutes, sans échantillon non répondant.
Elle ne démontre pas la stabilité à plus long terme, la saturation de
nombreuses instances, le seuil CPU p95 du cahier ou la qualité des FPS
visibles. **B28 reste partiel** jusqu'aux captures Profiler, à une saturation
plus forte et à la recette humaine.
