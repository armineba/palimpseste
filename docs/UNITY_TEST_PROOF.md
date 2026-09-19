# Preuves Unity — 19 septembre 2026

## Environnement et portes exécutées

Unity 6000.3.24f1 Personal, Universal 3D/URP et Windows IL2CPP : `C:\ProgramData\6000.3.24f1-x86_64\Editor\Unity.exe`. Depuis la racine du dépôt, les dernières portes ont été lancées en batch ; le processus a été attendu et son code de sortie contrôlé.

```powershell
Unity.exe -batchmode -nographics -projectPath "$PWD\game" -runTests -testPlatform EditMode -testResults "$PWD\game\Logs\editmode-cache-final.xml" -logFile "$PWD\game\Logs\editmode-cache-final.log"
Unity.exe -batchmode -nographics -projectPath "$PWD\game" -runTests -testPlatform PlayMode -testResults "$PWD\game\Logs\playmode-recovery-final.xml" -logFile "$PWD\game\Logs\playmode-recovery-final.log"
Unity.exe -batchmode -nographics -quit -projectPath "$PWD\game" -executeMethod Palimpseste.Game.Editor.BuildPalimpseste.BuildWindows -logFile "$PWD\game\Logs\build-windows-b07-b16-b26-final2.log"
```

Les originaux Unity sont dans `game/Logs/` (ignoré par Git) ; les six originaux sélectionnés sont conservés hors dépôt dans `C:\ProgramData\Palimpseste\unity-proof-b07-b16-b26`. Leurs copies publiques ont été expurgées par `ops/sanitize-unity-evidence.py`, qui conserve les lignes de résultats et vérifie les XML. Les captures publiques ne contiennent ni jeton ni journal de dessin privé.

| Porte | Résultat observé | Preuve publique |
|---|---|---|
| Import et compilation | sortie 0 | [import.log](../evidence/public/unity/import.log) |
| EditMode | 7/7 passés : dessin, reprise du journal et cache | [XML](../evidence/public/unity/editmode.xml), [log](../evidence/public/unity/editmode.log) |
| PlayMode | 2/2 passés : six porteurs/effets et interruption au focus | [XML](../evidence/public/unity/playmode.xml), [log](../evidence/public/unity/playmode.log) |
| Build Windows x64 IL2CPP | sortie 0, `PALIMPSESTE_BUILD_OK` | [build-windows.log](../evidence/public/unity/build-windows.log) |
| Lecteur extrait hors ligne | processus lancé, fixture locale exécutée | [log lecteur](../evidence/public/unity/player-offline-curve.log), [capture après lancer](../evidence/public/unity/offline-fixture-curve-effect.png) |

Le PlayMode charge six plans manuels `projectile`, `beam`, `field`, `pulse`, `barrier`, `trap`. Il vérifie une touche et une impulsion physique du projectile, deux touches et 6 000 milli-dégâts du faisceau, l'empreinte et le trou d'un champ, le soin d'un allié par une onde sans toucher l'hostile, l'interception puis l'expiration d'une barrière et le ralentissement par un piège. `ResetTargets` annule les porteurs actifs. Une assertion B25 vérifie qu'un lancer crée un `AudioSource` avec un clip procédural de 3 969 échantillons à 22 050 Hz ; aucune écoute humaine n'a été faite. Les données détaillées figurent dans le XML. Les variantes sont des fixtures manuelles ; les combinaisons exhaustives de filtres, ressources et physique restent ouvertes.

## B07, B16 et B26

Le journal de dessin est une chaîne d'événements hachés. Trois essais EditMode couvrent la reprise d'événements durables plus récents que le checkpoint, la troncature d'un suffixe incomplet pendant un dessin ouvert et le rejet d'un événement complet altéré. Le test de focus PlayMode interrompt un trait actif, consigne `up`, verrouille la région touchée et conserve le parchemin ouvert ; retrouver le focus n'ajoute pas d'événement. Il s'agit d'une interruption simulée dans Unity, sans arrêt brutal du processus ni essai d'un vrai Alt+Tab pendant la capture.

Le vérificateur de cache refuse la corruption d'un octet du masque, l'absence d'un artefact, un marqueur SHA de paquet absent et une version de compilateur incompatible, puis relit le paquet restauré. Le refus d'un artefact corrompu a aussi été vu dans un lecteur IL2CPP direct issu du build immédiatement antérieur à la seule retouche du bandeau de bibliothèque : [capture du refus](../evidence/public/unity/cache-corrupt-direct-build.png). L'essai n'utilise qu'une fixture manuelle copiée localement et remise en état après le test. Le cache est protégé contre les dommages accidentels et les chargements partiels testés ; aucun mécanisme de signature contre un acteur local capable de réécrire le paquet et son marqueur n'est affirmé.

Le lecteur final extrait a été ouvert à **1024 × 768** et **1920 × 1080** ; les captures montrent la bibliothèque et ses contrôles lisibles : [1024](../evidence/public/unity/resolution-1024-library.png), [1920](../evidence/public/unity/resolution-1920-library.png). La liste de bibliothèque défile et le titre a un fond sombre. Ces deux tailles et le test de focus ne constituent pas une validation exhaustive du clavier, de l'accessibilité ou de toutes les résolutions.

## Rendu, réseau et archive

Le labo du lecteur comprend lumière avec ombres, sol quadrillé, silhouettes et barres de vie, VFX projectile/faisceau/champ/statut, HUD français et son procédural. Le [shader URP embarqué](../game/Assets/Palimpseste/Resources/LabUnlit.shader) échantillonne le masque de la fixture ; l'ancien gris est converti en alpha en mémoire après vérification du fichier haché. Les objets décoratifs n'ont pas de collision. [Capture du labo final](../evidence/public/unity/offline-fixture-curve-effect.png).

Un lecteur antérieur, connecté à l'API locale avec jeton privé, a reçu capacités et référence, alloué un parchemin, tracé à la souris, transmis `/begin`, fermé puis envoyé PNG et journal gzip. L'API a accepté la capture : `needs_capture=false`, job `queued` `df5b9540775d4ba489f2524c307a8a21` ([preuve](../evidence/public/unity/client-capture-queued.png)). Aucun appel Luna n'a été observé.

Archive finale : [Palimpseste-Windows-x64-IL2CPP.zip](../deliverables/Palimpseste-Windows-x64-IL2CPP.zip), **43 994 056 octets**, SHA-256 **`D75E4F24B6B5800E76B3C4AD3EC201F0C40A16EF947AA15447D8DC19E0991C51`** ([manifeste](../evidence/public/unity/archive.sha256)). Elle contient `Palimpseste.exe`, `Palimpseste_Data`, D3D12, `GameAssembly.dll`, `UnityPlayer.dll`, `baselib.dll` et `UnityCrashHandler64.exe` : **28 fichiers, 121 991 270 octets** extraits sous `.runtime/release-smoke-b07-b16-b26-final/` sur ce PC. Le lecteur extrait a été lancé sans API ni jeton ; la fixture locale « Braise en sillage » a produit **1 lancer, 5 touches, 14 dégâts et 3 statuts** dans la capture. Cette fixture manuelle était déjà en cache sur ce PC ; elle ne provient pas de Luna. Une installation sur machine propre reste à essayer.

## Mesure courte B28 et limites

Sur AMD Ryzen 7 5800X, NVIDIA RTX 3070 et 32 GiB de RAM, une **archive antérieure** a été mesurée pendant 77,68 s environ, avec cinq lancers manuels. Les [13 échantillons du processus Windows](../evidence/public/unity/b28-samples.csv) donnent 613 302 272 à 623 120 384 octets résidents (+9 818 112), 868 093 952 à 876 417 024 octets privés (+8 323 072), et `Responding=True` à chaque relevé. Le [log de cette archive antérieure](../evidence/public/unity/player-b28-prior-build.log) contient onze fenêtres de dix secondes de boucle Unity à 1 462,5–1 557,5 frames calculées par seconde. Ce compteur n'est ni une mesure des présentations visibles, ni une trace du Unity Profiler. **B28 n'a pas été remesuré sur le ZIP final** et l'endurance sous charge reste ouverte.

La boucle complète dessin → Luna A → Luna B → compilation → sort issu de ce dessin n'a pas été observée. Le job réel est resté en file d'attente. L'écoute du son, les scénarios physiques exhaustifs, l'installation sur une autre machine et l'acceptation humaine restent ouverts. Le jeton client n'est pas encore stocké dans un magasin Windows protégé ; il est saisi en mémoire pour la session.
