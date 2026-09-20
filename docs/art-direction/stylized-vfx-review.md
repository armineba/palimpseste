# Effets stylisés — direction et revue dans Unity

## Cible visuelle

Les deux références fournies par le joueur donnent une direction précise :
un soin vert composé de spirales ascendantes et d'étoiles, et une explosion
violette composée d'un cercle au sol, d'une couronne lumineuse et de particules
verticales. Leur richesse vient de formes et de mouvements complémentaires.

La composition recherchée comporte une silhouette principale lisible, un socle
lumineux, des rubans de largeurs différentes, des particules fines et quelques
accents très brillants. Les phases doivent se distinguer : apparition courte,
mouvement ou maintien, impact, disparition. Le vert doit suggérer un mouvement
doux ascendant ; le violet une expansion énergique ; le feu une course avec
traînée et débris chauds. Le dessin fournit l'idée, pas un contour plaqué sur
chaque effet.

L'éclat doit conserver ses couleurs et ses détails. Augmenter seulement la
luminosité produit une masse blanche et ne satisfait pas cette direction.
Ces références sont des orientations artistiques ; aucun asset commercial
issu des captures n'est importé par ce travail.

## Capture de revue bornée

`StylizedCompositionPresentationTests.CaptureLayeredHealingPulseAndFire`
rend trois compositions écrites dans le test, séparément, dans le décor réel
du laboratoire : soin vert, impulsion violette, projectile de feu. Trois
images par composition montrent des moments différents. La caméra se
rapproche pour permettre la revue des détails ; son cadrage est déclaré dans
`capture.json`.

Ces compositions sont des fixtures de rendu. Elles ne proviennent ni d'un
dessin joueur ni d'un appel modèle, et ne prouvent pas une interprétation
correcte ou une validation artistique humaine. Les nœuds exacts, les temps
simulés, le périphérique graphique, les empreintes des images et les nombres
de renderers, de particules et de lumières sont enregistrés. Le test vérifie
que les couches décoratives n'ajoutent aucun collider.

La capture conserve un tampon `ARGBHalf` linéaire jusqu'après les effets URP,
puis convertit le résultat en sRGB pour le PNG. Le code URP installé
(`UniversalRenderPipelineCore.CreateRenderTextureDescriptor`) reprend le
format de la RenderTexture externe pour son tampon intermédiaire : une
cible ARGB32 écrêtait donc l'émission avant le bloom. Le tampon HDR évite
cette différence entre la capture et le jeu. L'animation utilise un pas
de capture de 1/60 s ; les images ne constituent pas une mesure de performance.

Le test existant `CachedSpectrePresentationTests.CaptureExistingSpectreInLab`
emploie la même conversion HDR pour rejouer un véritable paquet joueur
inchangé. Il conserve ses contrôles de dégâts et de poussée.

## Exécution

Définir `PALIMPSESTE_STYLIZED_CAPTURE_DIR` vers le dossier de preuves souhaité,
puis exécuter le filtre Unity PlayMode
`Palimpseste.Game.PlayModeTests.StylizedCompositionPresentationTests` avec un
périphérique graphique réel. Préparer les matériaux et le profil URP avant
la capture si leurs réglages ont été modifiés.

La présence du test ne constitue pas une preuve de son exécution. Les
résultats XML, le journal Unity et les fichiers `capture.json` du lot exécuté
font foi. L'appréciation du style final reste celle du joueur.
