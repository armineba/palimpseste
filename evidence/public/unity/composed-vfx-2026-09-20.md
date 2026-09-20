# D10 — relecture réelle du spectre et contrôles VFX Unity

## Ce qui a été exécuté

Unity **6000.3.24f1**, URP, GPU **NVIDIA GeForce RTX 3070**, a exécuté les
tests PlayMode ciblés le 20 septembre 2026, de 16:03:28 à 16:03:36 UTC.
Le [résultat XML](composed-spectre/composed-vfx-tests.xml) indique
**5/5 réussis, 0 échec**, en **7,9366746 s**. Le [journal Unity](composed-spectre/composed-vfx-tests.log)
et le [journal de préparation](composed-spectre/composed-vfx-prepare.log)
sont conservés après assainissement des données privées.

| Contrôle ciblé | Résultat |
|---|---|
| Relecture du vrai paquet spectre dans le laboratoire | Réussi |
| Étendue visible de la barrière cohérente avec son étendue physique | Réussi |
| Volumes bornés des formes contrôlées et libération de leurs ressources | Réussi |
| Projectile sémantique indépendant de la signature du dessin disponible | Réussi |
| Extinction de l'impact décoratif sans composant de gameplay supplémentaire | Réussi |

SHA-256 du XML public :
`E274A5BFAA14F0385650173C2F1E6B433ECAEE1956F43847DE7FF7FC5D2ADF2A`.

## Vrai sort conservé, sans régénération

La capture rejoue **« Envol du spectre aux longs voiles »**, depuis son paquet
joueur sauvegardé SHA-256
`0ED08FEE15298D8E61FC396EDDC4F2A5ED2827D0B8EFD51639E2771F2162E467`.
Elle ne crée aucun nouveau dessin, n'appelle aucun modèle et ne remplace pas
l'ancienne description. Le nouveau renderer compose les effets à partir de
la forme et de la palette de ce paquet existant.

La caméra emploie la pose et le post-traitement du laboratoire ; les images
capturent la zone 3D sans interface. Les [mesures de capture](composed-spectre/capture.json)
contiennent les temps réels, positions, compteurs et empreintes des six PNG :

| Temps après lancement | Image | État observé |
|---:|---|---|
| 0,406 s | [Image 0](composed-spectre/spectre-frame-00.png) | Porteur en vol |
| 0,457 s | [Image 1](composed-spectre/spectre-frame-01.png) | Porteur en vol |
| 0,704 s | [Image 2](composed-spectre/spectre-frame-02.png) | Porteur en vol |
| 1,052 s | [Image 3](composed-spectre/spectre-frame-03.png) | Porteur en vol |
| 1,402 s | [Image 4](composed-spectre/spectre-frame-04.png) | Impact comptabilisé, porteur terminé |
| 1,703 s | [Image 5](composed-spectre/spectre-frame-05.png) | Après impact |

Le moteur compte **1 impact, 12 000 unités internes de dégâts et 1 impulsion**.
La santé de la cible passe de 100 000 à 88 000. Le déplacement de cible mesuré
est de **0,00154 m** ; cette exécution ne démontre donc pas une forte projection
visuelle de la cible. SHA-256 du manifeste de capture :
`AACADAF815DE7D56CC7A490CC167A0506805964B399DAB4D6F88D40383F6783F`.

## Portée de la preuve et suite

Ces images sont issues du moteur Unity en PlayMode, pas du
[concept spectral généré](../../../docs/art-direction/spectral-veils-target.png).
Elles montrent un lancement animé d'un vrai sort conservé. Elles ne prouvent
ni un nouveau parcours joueur complet avec Sol → Astra, ni l'écoute du son,
ni une qualité artistique acceptée par le créateur. La comparaison avec les
références commerciales reste un jugement artistique humain.

## Build final et livraison

Le premier build Windows `1.2.0` a réussi avec le code 0. Après une correction
ciblée des bornes de `pow`, le build final IL2CPP/URP dans
`WindowsComposedVfxRelease` a lui aussi terminé avec le code 0.
Le [journal final assaini](composed-spectre/build-windows-final.log) contient
`PALIMPSESTE_BUILD_OK` ; aucun avertissement shader n'y a été trouvé.
Les shaders `SpellComposition` et `SpellSpectralCloth` sont inclus.

Le [Player jouable](../../../game/Build/WindowsComposedVfxPlayable/Palimpseste.exe)
contient **29 fichiers, 122 417 345 octets**, hors les deux dossiers de debug
`DoNotShip`. Le [manifeste de livraison](composed-vfx-delivery.json) donne leurs
empreintes. `GameAssembly.dll` a pour SHA-256
`463D73EEA6086E963DA3B73A5C7A04CE9281A303D3B63BEB1444B426B5F0F256`.

Le [ZIP distribué](../../../deliverables/Palimpseste-Windows-x64-IL2CPP.zip)
fait **44 189 567 octets**, SHA-256
`A8B137D587F6A4F2D3677C06C38D4BE7BAAA280183AE52CBEB3622E201A42B68`.
Le Player a été lancé en fenêtre normale sous le PID `32064` et le raccourci
Bureau « Palimpseste Spell Lab » a été mis à jour vers cette livraison.
Ce lancement ne vaut pas nouveau dessin complet ni validation artistique.
L'[état de réalisation](../../../docs/IMPLEMENTATION_STATUS.md) conserve le
point de reprise humain et les limites de déploiement restantes.
