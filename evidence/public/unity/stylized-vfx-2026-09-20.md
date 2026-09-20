# D11 — preuve du rendu stylisé, 20 septembre 2026

## Périmètre

Cette évolution du client `1.2.1` ajoute des spirales ascendantes, des couronnes
interrompues, des sceaux animés et quatre familles de particules : lances
lumineuses, points en orbite, étoiles à quatre branches et nappes diffuses.
Les impacts se dissipent en 1,25 s. Le rythme d'un soin exploite les effets
`heal`/`regen` du nœud. Les anciennes spires opaques du vortex de zone ont été
retirées pour laisser lire les rubans translucides. Le profil URP utilise
le HDR et un bloom réglé pour conserver la couleur des effets.

Ce travail modifie le rendu du client. **Aucun appel fournisseur n'a été
effectué pour ces captures** ; elles n'ajoutent aucune étape à la génération
joueur. Les descriptions, plans, collisions et paramètres mécaniques des
sorts conservés ne sont pas réécrits.

## Exécutions et corrections

| Exécution | Résultat observé | Correction ou portée |
| --- | --- | --- |
| Première exécution ciblée, [XML](stylized-vfx/first-run.xml) | 2 réussites, 4 échecs | Unity refusait le mélange des modes de courbes de vitesse des particules ; modes uniformisés. |
| Deuxième exécution, [XML](stylized-vfx/second-run.xml) et [journal](stylized-vfx/second-run.log) | 5 réussites, 1 échec | Le spectre conservé et les quatre contrôles du renderer passent. La galerie révèle une puissance fractionnaire d'un sinus légèrement négatif à l'extrémité d'une spirale ; entrée bornée avant `Pow`. |
| Galerie après correction, `stylized-vfx-gallery.xml` | 1 réussite sur 1 | Reprise de la seule capture concernée. |
| Galerie finale après retrait des tubes opaques, [XML](stylized-vfx/gallery.xml) et [journal](stylized-vfx/gallery.log) | 1 réussite sur 1 | Neuf images renouvelées avec le code retenu pour la livraison. |

Il n'existe pas de passage unique « 6/6 » revendiqué ici. Les quatre contrôles
réussis dans la deuxième exécution portent sur les 21 formes et la libération
de leurs ressources, l'absence de copie du contour du dessin sur un projectile
sémantique, l'impact sans composant de gameplay et sa destruction, ainsi que
l'étendue visuelle de la barrière. La dernière exécution ne répète que la
galerie qui avait échoué.

## Galerie de fixtures

Les [métadonnées de la galerie](stylized-vfx/capture.json) identifient
explicitement des nœuds écrits pour la revue graphique, **pas des sorts
produits par un nouveau dessin ou par un modèle**. Trois images par effet
montrent le soin vert, l'impulsion violette et le projectile de feu avec
son impact, dans le décor réel du labo avec une caméra rapprochée.

- [Soin vert à 1,23 s](stylized-vfx/healing-field-02.png).
- [Impulsion violette à 0,25 s](stylized-vfx/arcane-pulse-01.png) et [impact à 0,65 s](stylized-vfx/arcane-pulse-02.png).
- [Projectile de feu à 0,38 s](stylized-vfx/fire-projectile-01.png) et [impact à 0,92 s](stylized-vfx/fire-projectile-02.png).

Périphérique observé : **NVIDIA GeForce RTX 3070**. Maxima mesurés par fixture
sur ces neuf images : **22 renderers, 69 particules vivantes, capacité totale
de 192 particules, une lumière, zéro collider ajouté**. Chaque couche de
particules est bornée à 48. Ce relevé n'est ni une mesure de FPS ni une
garantie de performance en présence de nombreux sorts simultanés.

La capture utilise une cible HDR `ARGBHalf` linéaire et une conversion sRGB
après postprocessing, pour éviter l'écrêtage des émissions avant bloom par
l'ancienne cible ARGB32. Le pas de capture est fixé à 1/60 s ; les temps
indiqués décrivent l'animation Unity, pas la durée réelle d'encodage des PNG.

## Relecture du vrai spectre conservé

Le test `CaptureExistingSpectreInLab` a rejoué **« Envol du spectre aux longs
voiles »** depuis son paquet joueur inchangé, SHA-256
`0ed08fee15298d8e61fc396eddc4f2a5ed2827d0b8efd51639e2771f2162e467`.
Les [six images et leurs métriques](stylized-spectre/capture.json) montrent
le vol et l'impact : **une touche, 12 000 unités internes de dégâts, une
impulsion**, santé de cible de 100 000 à 88 000. Le déplacement final est
d'environ **1,54 mm** ; cette preuve n'établit pas une forte projection de
la cible. Exemple : [vol à 0,70 s](stylized-spectre/spectre-frame-02.png).

## Build et limites

Le build Windows IL2CPP `1.2.1`, Unity `6000.3.24f1`, a terminé avec le
**code 0**, sans avertissement ni erreur shader trouvés dans le
[journal de build](stylized-vfx/build-windows.log). Le
[Player jouable](../../../game/Build/WindowsStylizedVfxPlayable/Palimpseste.exe)
contient **29 fichiers vérifiés, 122 476 445 octets**, hors les deux dossiers
`DoNotShip`. Le [manifeste de livraison](stylized-vfx-delivery.json) conserve
leurs tailles et empreintes. `GameAssembly.dll` :
`9ae73b92ce681f31e882564c17ca50a60ad66ff0d31f3a589d941b7b4030685c`.

Le [ZIP Player](../../../deliverables/Palimpseste-Windows-x64-IL2CPP.zip)
fait **44 217 522 octets**, SHA-256
`8c92040e5abdbc8d690216010cafd9522f824dca7c9cd7db6aa42c4c82a32c0c`.
Le nouveau Player a été lancé sous le PID **30968**, fenêtre réactive ;
le raccourci Bureau **Palimpseste Spell Lab** pointe sur cette livraison.
La santé API répond 200 ; le backend reste celui de D10.

L'acceptation artistique humaine du nouveau rendu et du son reste ouverte.
Aucun nouveau parcours complet dessin → modèles → Player `1.2.1`, essai
exhaustif de toutes les combinaisons de styles et recettes, essai sur un
autre poste, benchmark de charge ou déploiement public distant n'est
revendiqué par ces captures. Les preuves D10 restent historiques.
