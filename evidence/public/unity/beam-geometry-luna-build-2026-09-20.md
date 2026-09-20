# Faisceau lié au chemin du dessin · Unity 20 septembre 2026

Le paquet A `1.3`/B `1.1` du diagnostic réel reste un **paquet de vérification**, pas un sort joueur publié. Le job issu du Player est une autre capture courbe, encore en file. Aucun verdict humain d'acceptation du diagnostic n'est enregistré.

## Changement exécuté

Le porteur `beam` utilise maintenant les points du `geometry_id` compilé pour sa trajectoire principale et les `SphereCast` correspondants. Il oriente les points dans le sens du lancer sans prétendre connaître le début du trait, conserve tous les points du chemin (dans la borne contractuelle de 128), respecte `scale_cm`, puis prolonge le faisceau dans l'axe du lancer jusqu'à `range_cm`. Les retours du trait sont dépliés vers l'avant en conservant leurs segments et leurs déviations latérales. Un obstacle physique coupe le centre du faisceau et son rendu au même avancement. Les relais utilisent la largeur physique ; l'impulsion `forward` suit le segment effectivement touchant. Un filtre explicite `self` ou `all_actors` peut toucher le lanceur une fois à l'origine. Le cœur et la croûte visibles du shader de lave sont ramenés à la largeur physique, avec un plancher visuel de 4 cm pour les faisceaux de 1 à 3 cm ; son halo plus large reste décoratif.

Les sources modifiées sont `game/Assets/Palimpseste/SpellRuntime/RuntimeEngine.cs`, `game/Assets/Palimpseste/Presentation/CarrierVisual.cs` et les tests PlayMode. Aucun code généré par un parchemin n'est exécuté.

## Tests réellement exécutés

- Test ciblé `BeamPathPlayModeTests` : Unity exit `0`, **2/2 passés**. Le chemin de fixture courbe donne trois points visibles, une déviation latérale de `-2 m`, `3 000` milli-dégâts à la cible placée sur la courbe et zéro à celle placée sur l'ancien rayon droit. Une impulsion physique `forward` possède une composante latérale vérifiée après pas physique. Un mur placé sur la courbe coupe la ligne au point `z=2,45 m` et bloque les dégâts. Le second test vérifie un unique contact explicite avec le lanceur. XML privé SHA-256 `7D2498FD8707937EBEDEAC18F3D1673163C63B3CC5AD43839D16505550ABB46A`, log `DF8C8B012EF7D1F9101F15939AE188BF797AC0BBA920DDD14BFEAF5A78C20DC1`.
- Paquet Luna A/B réel, test graphique Direct3D 12 : Unity exit `0`, **2/2 passés**. Son chemin `ring.path.0` comporte `111` points issus de l'encre ; la ligne avant le premier obstacle en expose `94`. Le plan conserve zéro effet et le test vérifie zéro dégât. La caméra isolée a mesuré `36 440` pixels rouges après l'expiration logique, puis zéro après nettoyage. XML privé SHA-256 `A940664D249CAF96B29AA112AEB1C70F641857B12041FCAAC4C838EFCF40B5FA`, log `C31CB512827EED4FFCBADB5D2B93B2C9AAE7D623B7801CA41A81EB767E8CDD0B`.
- Suite PlayMode complète après ces corrections : Unity exit `0`, **4 passés, 2 sondes réelles facultatives ignorées, 0 échec**. Les deux sondes réelles ont été exécutées séparément avec leur paquet et le périphérique Direct3D 12, ci-dessus. XML privé SHA-256 `E14BA787ED1404F928DDBF0CEA3EFD4F587078B78DEAC126EE8ED60F9FE5820C`, log `39C317989EA7E9576B8BB27C24F42FBD5EC9E516F4E042844149130BF58F6464`.

Une première exécution du test réel avait échoué seulement parce qu'une nouvelle ligne de diagnostic lisait le `LineRenderer` après destruction. Le test a été corrigé et la reprise ci-dessus a passé 2/2. Aucun échec moteur n'a été observé dans cet essai.

## Build Windows et lancement

Unity `6000.3.24f1` a construit le Player Windows x64 IL2CPP/URP dans `game/Build/WindowsPlayerBeamGeometry/` avec sortie `PALIMPSESTE_BUILD_OK`, code de sortie `0` ; log privé SHA-256 `A07718DEF3FDBB6A85AC10C510B155958547B880A5717D30F556D4066C22B52A`. Ce dossier brut contient aussi les dossiers temporaires `DoNotShip` de Unity. La copie jouable canonique `game/Build/WindowsPlayerBeamGeometryReady/` contient seulement les fichiers distribuables : **29 fichiers, 122 093 149 octets**. Ses 29 fichiers ont été comparés par SHA-256 avec le build brut : **0 différence**.

Empreintes de la copie jouable : `Palimpseste.exe` `049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`, `GameAssembly.dll` `200BB3A65EC799D316C769D54CDB4F03A90BB7627B58B22FECA8A2CFEEB81A9E`, `resources.assets` `62F6544B03BD5E70F556ACE80EDC282E684073B5DE6E8C7CF0998E2C2D072F5F`.

Cette copie a été lancée en fenêtre normale : PID `35552`, `Responding=True`, handle non nul `10815020`, titre `Palimpseste Spell Lab`. Ce constat ne démontre pas le lancement du paquet Luna depuis le Player ni la visibilité du faisceau dans sa caméra de jeu. Le worker joueur est encore arrêté et le job du Player n'a pas été transformé en sort.

Une deuxième copie propre nommée `WindowsPlayerBeamGeometryPlayable` a été créée localement avant la décision de garder `Ready` comme nom canonique. Sa suppression après vérification des chemins et de l'absence de processus utilisateur a été refusée avant exécution par la revue automatique de l'outil (`blocked by policy`, sans motif détaillé) ; elle reste un doublon local ignoré et ne fait pas partie de la livraison canonique.
