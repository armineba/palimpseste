# Preuves Unity — 19 septembre 2026

## Environnement et portes exécutées

Unity 6000.3.24f1 Personal, Universal 3D/URP et Windows IL2CPP : `C:\ProgramData\6000.3.24f1-x86_64\Editor\Unity.exe`. Depuis la racine du dépôt, les dernières portes ont été lancées en batch ; le processus a été attendu et son code de sortie contrôlé.

```powershell
Unity.exe -batchmode -nographics -projectPath "$PWD\game" -runTests -testPlatform EditMode -testResults "$PWD\game\Logs\editmode-credentials-final.xml" -logFile "$PWD\game\Logs\editmode-credentials-final.log"
Unity.exe -batchmode -nographics -projectPath "$PWD\game" -runTests -testPlatform PlayMode -testResults "$PWD\game\Logs\playmode-credentials-final.xml" -logFile "$PWD\game\Logs\playmode-credentials-final.log"
Unity.exe -batchmode -nographics -quit -projectPath "$PWD\game" -executeMethod Palimpseste.Game.Editor.BuildPalimpseste.BuildWindows -logFile "$PWD\game\Logs\build-windows-credentials-final.log"
```

Les originaux Unity sont dans `game/Logs/` (ignoré par Git) ; les six originaux sélectionnés sont conservés hors dépôt dans `C:\ProgramData\Palimpseste\unity-proof-credentials`. Leurs copies publiques ont été expurgées par `ops/sanitize-unity-evidence.py`, qui conserve les lignes de résultats et vérifie les XML. La preuve d'import est issue de la porte précédente ; les tests et le build ci-dessous sont ceux des sources finales. Les captures publiques ne contiennent ni jeton ni journal de dessin privé.

| Porte | Résultat observé | Preuve publique |
|---|---|---|
| Import et compilation | sortie 0 | [import.log](../evidence/public/unity/import.log) |
| EditMode | 8/8 passés : dessin, reprise du journal, cache et coffre Windows | [XML](../evidence/public/unity/editmode.xml), [log](../evidence/public/unity/editmode.log) |
| PlayMode | 2/2 passés : six porteurs/effets et interruption au focus | [XML](../evidence/public/unity/playmode.xml), [log](../evidence/public/unity/playmode.log) |
| Build Windows x64 IL2CPP | sortie 0, `PALIMPSESTE_BUILD_OK` | [build-windows.log](../evidence/public/unity/build-windows.log) |
| ZIP final extrait | 28 fichiers, lecteur lancé, coffre Windows testé entre trois lancements | [preuve du coffre](../evidence/public/unity/credential-smoke.txt), [capture après effacement](../evidence/public/unity/credential-forget.png) |

Le PlayMode charge six plans manuels `projectile`, `beam`, `field`, `pulse`, `barrier`, `trap`. Il vérifie une touche et une impulsion physique du projectile, deux touches et 6 000 milli-dégâts du faisceau, l'empreinte et le trou d'un champ, le soin d'un allié par une onde sans toucher l'hostile, l'interception puis l'expiration d'une barrière et le ralentissement par un piège. `ResetTargets` annule les porteurs actifs. Une assertion B25 vérifie qu'un lancer crée un `AudioSource` avec un clip procédural de 3 969 échantillons à 22 050 Hz ; aucune écoute humaine n'a été faite. Les données détaillées figurent dans le XML. Les variantes sont des fixtures manuelles ; les combinaisons exhaustives de filtres, ressources et physique restent ouvertes.

## B07, B16 et B26

Le journal de dessin est une chaîne d'événements hachés. Trois essais EditMode couvrent la reprise d'événements durables plus récents que le checkpoint, la troncature d'un suffixe incomplet pendant un dessin ouvert et le rejet d'un événement complet altéré. Le test de focus PlayMode interrompt un trait actif, consigne `up`, verrouille la région touchée et conserve le parchemin ouvert ; retrouver le focus n'ajoute pas d'événement. Il s'agit d'une interruption simulée dans Unity, sans arrêt brutal du processus ni essai d'un vrai Alt+Tab pendant la capture.

Le vérificateur de cache refuse la corruption d'un octet du masque, l'absence d'un artefact, un marqueur SHA de paquet absent et une version de compilateur incompatible, puis relit le paquet restauré. Le refus d'un artefact corrompu a aussi été vu dans un lecteur IL2CPP direct issu du build immédiatement antérieur à la seule retouche du bandeau de bibliothèque : [capture du refus](../evidence/public/unity/cache-corrupt-direct-build.png). L'essai n'utilise qu'une fixture manuelle copiée localement et remise en état après le test. Le cache est protégé contre les dommages accidentels et les chargements partiels testés ; aucun mécanisme de signature contre un acteur local capable de réécrire le paquet et son marqueur n'est affirmé.

Le lecteur antérieur à l'ajout du coffre a été ouvert à **1024 × 768** et **1920 × 1080** ; les captures montrent la bibliothèque et ses contrôles lisibles : [1024](../evidence/public/unity/resolution-1024-library.png), [1920](../evidence/public/unity/resolution-1920-library.png). Le ZIP actuel a été lancé à **1920 × 1080** pendant le smoke du coffre. La liste de bibliothèque défile et le titre a un fond sombre. Ces tailles et le test de focus ne constituent pas une validation exhaustive du clavier, de l'accessibilité ou de toutes les résolutions.

## Rendu, réseau et archive

Le labo du lecteur comprend lumière avec ombres, sol quadrillé, silhouettes et barres de vie, VFX projectile/faisceau/champ/statut, HUD français et son procédural. Le [shader URP embarqué](../game/Assets/Palimpseste/Resources/LabUnlit.shader) échantillonne le masque de la fixture ; l'ancien gris est converti en alpha en mémoire après vérification du fichier haché. Les objets décoratifs n'ont pas de collision. [Capture du labo final](../evidence/public/unity/offline-fixture-curve-effect.png).

Un lecteur antérieur, connecté à l'API locale avec jeton privé, a reçu capacités et référence, alloué un parchemin, tracé à la souris, transmis `/begin`, fermé puis envoyé PNG et journal gzip. L'API a accepté la capture : `needs_capture=false`, job `queued` `df5b9540775d4ba489f2524c307a8a21` ([preuve](../evidence/public/unity/client-capture-queued.png)). Aucun appel Luna n'a été observé.

Archive finale : [Palimpseste-Windows-x64-IL2CPP.zip](../deliverables/Palimpseste-Windows-x64-IL2CPP.zip), **44 000 288 octets**, SHA-256 **`343A585873220C1511C53C30D37412208FDBC5AD29845F6060C817E4833791C2`** ([manifeste](../evidence/public/unity/archive.sha256)). Elle contient `Palimpseste.exe`, `Palimpseste_Data`, D3D12, `GameAssembly.dll`, `UnityPlayer.dll`, `baselib.dll` et `UnityCrashHandler64.exe` : **28 fichiers, 122 003 226 octets** extraits sous `.runtime/release-smoke-credentials-final/` sur ce PC. Le hash de l'exécutable extrait est identique à celui du build. Le lecteur extrait a été lancé trois fois à 1920 × 1080 pour vérifier le coffre Windows. Sur l'archive précédente, sans API ni jeton, la fixture manuelle « Braise en sillage » avait produit **1 lancer, 5 touches, 14 dégâts et 3 statuts** ([log](../evidence/public/unity/player-offline-curve.log), [capture](../evidence/public/unity/offline-fixture-curve-effect.png)). Cette fixture était déjà en cache sur ce PC ; elle ne provient pas de Luna. Une installation sur machine propre reste à essayer.

## Jeton Windows protégé

Le lecteur utilise un identifiant Credential Manager `CRED_TYPE_GENERIC` de l'utilisateur Windows, avec persistance locale. La cible inclut le SHA-256 de l'URL canonique du service : changer d'URL ne réutilise pas l'ancien jeton. Le jeton est écrit seulement après récupération réussie des capacités et de l'image de référence, relu au démarrage et effacé par « Oublier le jeton » ; `PlayerPrefs` ne garde que l'URL. En cas d'échec du coffre, la connexion reste limitée à la session et l'interface l'indique.

Un EditMode réel sous Windows a vérifié écriture/lecture, séparation entre deux URL et suppression. Puis le **ZIP final extrait** a été connecté à un petit serveur local de test avec jeton privé : le premier lancement a consigné `PALIMPSESTE_CREDENTIAL_WRITE_OK`, le second `PALIMPSESTE_CREDENTIAL_READ_OK`, puis le clic d'effacement `PALIMPSESTE_CREDENTIAL_DELETE_OK`. `cmdkey /list` ne trouvait plus la cible ; un troisième démarrage n'a pas consigné de lecture. Les [résultats et hashes des logs privés](../evidence/public/unity/credential-smoke.txt) et la [capture de l'état effacé](../evidence/public/unity/credential-forget.png) sont publiés sans secret. Ce smoke prouve le P/Invoke Windows dans le Player IL2CPP de ce PC ; il ne teste ni une autre session Windows, ni un autre compte ou poste, ni le comportement d'un coffre Windows indisponible.

## Endurance B28 sur le ZIP final et limites

Le [rapport de l'archive finale](../evidence/public/unity/b28-final-endurance.md) identifie son SHA-256 `343a585873220c1511c53c30d37412208fdbc5ad29845f6060c817e4833791c2`. Le lecteur extrait, avec la fixture manuelle locale « Braise en sillage » ouverte hors ligne, a été mesuré pendant **662,734 s** : 133 points Windows toutes les cinq secondes environ, tous `Responding=True`. L'ensemble résident est passé de **617 381 888 à 630 480 896 octets** (+13 099 008) et les octets privés de **868 331 520 à 895 758 336** (+27 426 816). Deux remises à zéro, 13 clics périodiques et deux bursts de 20 et 30 clics ont entretenu le labo. La [capture finale du HUD](../evidence/public/unity/offline-final-endurance.png) montre 19 lancers admis, 69 touches, 100,0 dégâts et 44 statuts depuis la dernière remise à zéro ; elle ne représente pas un sort généré par Luna.

Le [log filtré](../evidence/public/unity/player-b28-final-excerpt.log) contient 79 fenêtres de dix secondes de boucle Unity, entre 1 245,8 et 1 633,6 frames calculées par seconde, avec au plus deux instances actives. Ce compteur n'est pas une mesure des FPS présentés à l'écran. Aucune trace Unity Profiler ni mesure du coût CPU p95 de la logique des sorts n'a été produite ; la croissance mémoire constatée sur onze minutes ne suffit pas à établir une stabilité durable, et la charge n'a pas saturé de nombreuses instances. **B28 reste partiel.** L'essai initial de 77,68 s et ses [13 anciens échantillons](../evidence/public/unity/b28-samples.csv) concernent une archive précédente.

La boucle complète dessin → Luna A → Luna B → compilation → sort issu de ce dessin n'a pas été observée. Le job réel est resté en file d'attente. L'écoute du son, les scénarios physiques exhaustifs, l'installation sur une autre machine et l'acceptation humaine restent ouverts.
