# Preuves Unity — 20 septembre 2026

## Build actuel : sort joueur Luna dans le Player et hors ligne

Le [rapport de bout en bout du propriétaire](../evidence/public/unity/owner-player-end-to-end-2026-09-20.md)
lie une vraie capture rouge-brun courbe du Player à deux tentatives Luna A/B
réussies, un job `production` passé à `ready`, une description affichée, puis un
paquet téléchargé et vérifié. La fiche et le laboratoire du Player ont été
ouverts. Le faisceau incurvé est visible dans la caméra du jeu ; un clic a
donné **1 lancer et 0 dégât**, conformément au plan sans effet mécanique.

Une copie exacte du paquet de ce joueur et de ses artefacts a passé **2/2
tests PlayMode Direct3D12**, avec **1 377 pixels rouges** puis zéro après
nettoyage et aucune application de dégâts. Ces tests ont vérifié la création
d'une source sonore procédurale, sans écoute humaine. Unity 6000.3.24f1 a
construit `game/Build/WindowsPlayerBeamVisibleReady/` en Windows x64 IL2CPP/URP
avec sortie **0** et `PALIMPSESTE_BUILD_OK` ; le Player a été lancé en fenêtre
visible. Après arrêt de l'API et redémarrage de ce même build, la fiche, le
laboratoire et un lancer du même sort ont fonctionné **hors ligne**. Les
captures, hashes et journaux sont dans le rapport lié ci-dessus. Le verdict
artistique du créateur, l'écoute humaine du son, un second PC et l'ouverture
publique restent en attente ; M7 n'est pas accepté.

## Build précédent : trajectoire de faisceau issue de l'encre

Unity 6000.3.24f1 a construit `game/Build/WindowsPlayerBeamGeometryReady/` en
Windows x64 IL2CPP/URP : 29 fichiers, 122 093 149 octets. Le SHA-256 de
`Palimpseste.exe` est
`049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`,
celui de `GameAssembly.dll`
`200BB3A65EC799D316C769D54CDB4F03A90BB7627B58B22FECA8A2CFEEB81A9E`
et celui de `resources.assets`
`62F6544B03BD5E70F556ACE80EDC282E684073B5DE6E8C7CF0998E2C2D072F5F`.
Le shader URP `LavaBeam` préautorisé y est inclus. Les tests ciblés du chemin
de pixels de la silhouette principale ont passé **2/2**. Le paquet de
vérification compilé à partir de A `1.3`/B `1.1` et de l'encre réelle a passé
les tests Direct3D 12 **2/2** : 111 points source, 94 points visibles,
**36 440 pixels rouges** puis zéro après nettoyage, sans dégât. La suite
PlayMode complète a donné **4 tests passés, 2 sondes facultatives ignorées,
0 échec**. Les XML, journaux, hashes et limites sont détaillés dans la
[preuve du rendu géométrique](../evidence/public/unity/beam-geometry-luna-build-2026-09-20.md).
Ces tests lancent un paquet privé de vérification avec identifiants
synthétiques, pas un sort publié dans le Player.

Le Player BeamGeometryReady a été inspecté en fenêtre visible, PID 35552
répondant au contrôle. Le Player LavaReady précédent, avec session propriétaire
conservée dans le coffre Windows, affichait « Capture reçue / En file d'attente »
pour un job créé à 06:17 avant son build. Ce Player précédent a repris et
affiché la capture existante ; son envoi par cette version n'est pas établi.
La base contient un job `production`
`queued`, un parchemin `processing` et quatre artefacts : référence, dessin,
encre et journal. Le dessin est une courbe rouge-brun différente du trait
droit du corpus de calibration. Voir la
[preuve de la capture propriétaire](../evidence/public/unity/owner-player-capture-queued-2026-09-20.md).
À ce stade antérieur, le worker restait arrêté : aucun appel A/B, aucun texte
interprété, aucun plan et aucun sort n'existaient pour **ce job joueur**. La
caméra du Player n'avait pas affiché le paquet Luna de vérification ; aucun son
n'avait été écouté. Il n'existe pas de ZIP de ce build et M7 n'est pas accepté.

## Passe UI antérieure : parcours joueur et accès privé

Les sources de cette passe affichaient le dessin après échange automatique d'une invitation
privée contre une session joueur, puis les états du job et **le texte réel** de
l'artefact Luna A validé. L'accès au laboratoire attend le paquet compilé et,
pour un nouveau parchemin, cette description A vérifiée. La bibliothèque
ne charge que les parchemins dont `owner_id` correspond au `principal_id`
renvoyé par le service, ou au même principal mémorisé pour le jeton Windows
présent. Les anciens caches sans propriétaire restent masqués. L'interface n'affiche
ni URL de service, ni jeton, ni création locale de démonstration. Elle propose
« Ouvrir mon invitation » si l'accès manque. La configuration de service livrée
dans `StreamingAssets/service.json` contient seulement une URL vide à renseigner
par l'opérateur ; aucun secret n'est inclus.

Unity **6000.3.24f1**, Windows x64 **IL2CPP**, Universal 3D/URP : EditMode
**11/11**, PlayMode **2/2** et build IL2CPP réussis. Le journal
Unity de ce build indique `Build Finished, Result: Success` puis
`PALIMPSESTE_BUILD_OK` et une sortie Unity 0 ; le shell qui attendait le
processus est resté ouvert après l'arrêt de Unity et a été interrompu.
Les preuves expurgées sont [EditMode XML](../evidence/public/unity/player-flow/editmode.xml),
[PlayMode XML](../evidence/public/unity/player-flow/playmode.xml),
[import initial](../evidence/public/unity/player-flow/import.log) et
[build](../evidence/public/unity/player-flow/build-windows.log). Les originaux
restent hors dépôt dans la zone privée de preuves Unity. La compilation finale
a été relancée après le retour de `preloadedAssets` à la valeur du dépôt.

Le build de cette passe antérieure est dans `game/Build/WindowsPlayerFlowOwnerFinal/` : **30 fichiers,
122 203 713 octets** hors dossier de sauvegarde Unity. L'exécutable
`Palimpseste.exe` fait **667 136 octets** ; SHA-256
`049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2`.
Le code IL2CPP est dans `GameAssembly.dll`, SHA-256
`0A2235C36CE4FC2788F787301B66DEA93C92F74BED47EC8150D9808C4D51E4DD`.
Le lecteur brut a démarré sans API pendant **41,3 s**, fenêtre répondante,
aucune exception relevée dans son journal. La
[capture de démarrage](../evidence/public/unity/player-flow/offline-startup.png)
montre le besoin d'accès, le bouton d'invitation et une bibliothèque vide :
deux anciens parchemins sans propriétaire présents sur ce PC sont masqués,
aucune fixture n'est affichée et le bouton de dessin est désactivé avant accès.
Cette capture ne démontre ni l'échange d'invitation dans le Player,
ni l'affichage d'une description A réelle, ni le sort issu d'un dessin.
Un second lancement a ouvert le [sélecteur Windows d'invitation](../evidence/public/unity/player-flow/picker-smoke.md)
dans le Player IL2CPP ; Annuler a fermé le dialogue et le lecteur est resté
répondant. Aucun fichier d'accès n'a été utilisé.

**L'archive ZIP ci-dessous est historique et ne contient pas cette passe UI.**
La création/remplacement du nouveau ZIP a été rejetée par le contrôle
automatique avant exécution (`blocked by policy`, sans motif détaillé) ; aucune
archive actuelle n'est revendiquée. Le démarrage d'une API locale de test a
également été rejeté avant exécution. Après correction du schéma strict,
une passe doctor a obtenu un JSON A réel conforme au contrat, mais l'a refusé
faute de métadonnées prouvant le modèle et l'effort effectifs ; B n'a pas démarré.
À cette date de la passe UI, aucun parcours Luna A/B abouti dans Unity
n'avait eu lieu. Les tests isolés ultérieurs sont consignés ci-dessus.
Les sections suivantes consignent les essais de l'archive antérieure
SHA-256 `343A585873220C1511C53C30D37412208FDBC5AD29845F6060C817E4833791C2`.

## Environnement et portes exécutées

Unity 6000.3.24f1 Personal, Universal 3D/URP et Windows IL2CPP : `C:\ProgramData\6000.3.24f1-x86_64\Editor\Unity.exe`. Depuis la racine du dépôt, les dernières portes ont été lancées en batch ; le processus a été attendu et son code de sortie contrôlé.

```powershell
Unity.exe -batchmode -nographics -projectPath "$PWD\game" -runTests -testPlatform EditMode -testResults "$PWD\game\Logs\editmode-credentials-final.xml" -logFile "$PWD\game\Logs\editmode-credentials-final.log"
Unity.exe -batchmode -nographics -projectPath "$PWD\game" -runTests -testPlatform PlayMode -testResults "$PWD\game\Logs\playmode-credentials-final.xml" -logFile "$PWD\game\Logs\playmode-credentials-final.log"
Unity.exe -batchmode -nographics -quit -projectPath "$PWD\game" -executeMethod Palimpseste.Game.Editor.BuildPalimpseste.BuildWindows -logFile "$PWD\game\Logs\build-windows-credentials-final.log"
```

Les originaux Unity sont dans `game/Logs/` (ignoré par Git) ; les six originaux sélectionnés sont conservés hors dépôt dans `C:\ProgramData\Palimpseste\unity-proof-credentials`. Leurs copies publiques ont été expurgées par `ops/sanitize-unity-evidence.py`, qui conserve les lignes de résultats et vérifie les XML. La preuve d'import est issue de la porte précédente ; les tests et le build ci-dessous sont ceux des sources de cette archive historique. Les captures publiques ne contiennent ni jeton ni journal de dessin privé.

| Porte | Résultat observé | Preuve publique |
|---|---|---|
| Import et compilation | sortie 0 | [import.log](../evidence/public/unity/import.log) |
| EditMode | 8/8 passés : dessin, reprise du journal, cache et coffre Windows | [XML](../evidence/public/unity/editmode.xml), [log](../evidence/public/unity/editmode.log) |
| PlayMode | 2/2 passés : six porteurs/effets et interruption au focus | [XML](../evidence/public/unity/playmode.xml), [log](../evidence/public/unity/playmode.log) |
| Build Windows x64 IL2CPP | sortie 0, `PALIMPSESTE_BUILD_OK` | [build-windows.log](../evidence/public/unity/build-windows.log) |
| ZIP historique extrait | 28 fichiers, lecteur lancé, coffre Windows testé entre trois lancements | [preuve du coffre](../evidence/public/unity/credential-smoke.txt), [capture après effacement](../evidence/public/unity/credential-forget.png) |

Le PlayMode charge six plans manuels `projectile`, `beam`, `field`, `pulse`, `barrier`, `trap`. Il vérifie une touche et une impulsion physique du projectile, deux touches et 6 000 milli-dégâts du faisceau, l'empreinte et le trou d'un champ, le soin d'un allié par une onde sans toucher l'hostile, l'interception puis l'expiration d'une barrière et le ralentissement par un piège. `ResetTargets` annule les porteurs actifs. Une assertion B25 vérifie qu'un lancer crée un `AudioSource` avec un clip procédural de 3 969 échantillons à 22 050 Hz ; aucune écoute humaine n'a été faite. Les données détaillées figurent dans le XML. Les variantes sont des fixtures manuelles ; les combinaisons exhaustives de filtres, ressources et physique restent ouvertes.

## B07, B16 et B26

Le journal de dessin est une chaîne d'événements hachés. Trois essais EditMode couvrent la reprise d'événements durables plus récents que le checkpoint, la troncature d'un suffixe incomplet pendant un dessin ouvert et le rejet d'un événement complet altéré. Le test de focus PlayMode interrompt un trait actif, consigne `up`, verrouille la région touchée et conserve le parchemin ouvert ; retrouver le focus n'ajoute pas d'événement. Il s'agit d'une interruption simulée dans Unity, sans arrêt brutal du processus ni essai d'un vrai Alt+Tab pendant la capture.

Le vérificateur de cache refuse la corruption d'un octet du masque, l'absence d'un artefact, un marqueur SHA de paquet absent et une version de compilateur incompatible, puis relit le paquet restauré. Le refus d'un artefact corrompu a aussi été vu dans un lecteur IL2CPP direct issu du build immédiatement antérieur à la seule retouche du bandeau de bibliothèque : [capture du refus](../evidence/public/unity/cache-corrupt-direct-build.png). L'essai n'utilise qu'une fixture manuelle copiée localement et remise en état après le test. Le cache est protégé contre les dommages accidentels et les chargements partiels testés ; aucun mécanisme de signature contre un acteur local capable de réécrire le paquet et son marqueur n'est affirmé.

Le lecteur antérieur à l'ajout du coffre a été ouvert à **1024 × 768** et **1920 × 1080** ; les captures montrent la bibliothèque et ses contrôles lisibles : [1024](../evidence/public/unity/resolution-1024-library.png), [1920](../evidence/public/unity/resolution-1920-library.png). Ce ZIP historique a été lancé à **1920 × 1080** pendant le smoke du coffre. La liste de bibliothèque défile et le titre a un fond sombre. Ces tailles et le test de focus ne constituent pas une validation exhaustive du clavier, de l'accessibilité ou de toutes les résolutions.

## Rendu, réseau et archive

Le labo du lecteur comprend lumière avec ombres, sol quadrillé, silhouettes et barres de vie, VFX projectile/faisceau/champ/statut, HUD français et son procédural. Le [shader URP embarqué](../game/Assets/Palimpseste/Resources/LabUnlit.shader) échantillonne le masque de la fixture ; l'ancien gris est converti en alpha en mémoire après vérification du fichier haché. Les objets décoratifs n'ont pas de collision. [Capture du labo final](../evidence/public/unity/offline-fixture-curve-effect.png).

Un lecteur antérieur, connecté à l'API locale avec jeton privé, a reçu capacités et référence, alloué un parchemin, tracé à la souris, transmis `/begin`, fermé puis envoyé PNG et journal gzip. L'API a accepté la capture : `needs_capture=false`, job `queued` `df5b9540775d4ba489f2524c307a8a21` ([preuve](../evidence/public/unity/client-capture-queued.png)). Aucun appel Luna n'a été observé.

Archive historique : [Palimpseste-Windows-x64-IL2CPP.zip](../deliverables/Palimpseste-Windows-x64-IL2CPP.zip), **44 000 288 octets**, SHA-256 **`343A585873220C1511C53C30D37412208FDBC5AD29845F6060C817E4833791C2`** ([manifeste](../evidence/public/unity/archive.sha256)). Elle contient `Palimpseste.exe`, `Palimpseste_Data`, D3D12, `GameAssembly.dll`, `UnityPlayer.dll`, `baselib.dll` et `UnityCrashHandler64.exe` : **28 fichiers, 122 003 226 octets** extraits sous `.runtime/release-smoke-credentials-final/` sur ce PC. Le hash de l'exécutable extrait est identique à celui du build. Le lecteur extrait a été lancé trois fois à 1920 × 1080 pour vérifier le coffre Windows. Sur l'archive précédente, sans API ni jeton, la fixture manuelle « Braise en sillage » avait produit **1 lancer, 5 touches, 14 dégâts et 3 statuts** ([log](../evidence/public/unity/player-offline-curve.log), [capture](../evidence/public/unity/offline-fixture-curve-effect.png)). Cette fixture était déjà en cache sur ce PC ; elle ne provient pas de Luna. Une installation sur machine propre reste à essayer.

## Jeton Windows protégé

Cette archive historique utilise un identifiant Credential Manager `CRED_TYPE_GENERIC` de l'utilisateur Windows, avec persistance locale. La cible inclut le SHA-256 de l'URL canonique du service : changer d'URL ne réutilise pas l'ancien jeton. Dans cette version, le jeton est écrit seulement après récupération réussie des capacités et de l'image de référence, relu au démarrage et effacé par « Oublier le jeton » ; `PlayerPrefs` ne garde que l'URL. En cas d'échec du coffre, la connexion reste limitée à la session et l'interface l'indique. L'interface actuelle retire ces champs et échange une invitation privée au démarrage.

Un EditMode réel sous Windows a vérifié écriture/lecture, séparation entre deux URL et suppression. Puis le **ZIP historique extrait** a été connecté à un petit serveur local de test avec jeton privé : le premier lancement a consigné `PALIMPSESTE_CREDENTIAL_WRITE_OK`, le second `PALIMPSESTE_CREDENTIAL_READ_OK`, puis le clic d'effacement `PALIMPSESTE_CREDENTIAL_DELETE_OK`. `cmdkey /list` ne trouvait plus la cible ; un troisième démarrage n'a pas consigné de lecture. Les [résultats et hashes des logs privés](../evidence/public/unity/credential-smoke.txt) et la [capture de l'état effacé](../evidence/public/unity/credential-forget.png) sont publiés sans secret. Ce smoke prouve le P/Invoke Windows dans le Player IL2CPP de ce PC ; il ne teste ni une autre session Windows, ni un autre compte ou poste, ni le comportement d'un coffre Windows indisponible.

## Endurance B28 sur le ZIP historique et limites

Le [rapport de l'archive finale](../evidence/public/unity/b28-final-endurance.md) identifie son SHA-256 `343a585873220c1511c53c30d37412208fdbc5ad29845f6060c817e4833791c2`. Le lecteur extrait, avec la fixture manuelle locale « Braise en sillage » ouverte hors ligne, a été mesuré pendant **662,734 s** : 133 points Windows toutes les cinq secondes environ, tous `Responding=True`. L'ensemble résident est passé de **617 381 888 à 630 480 896 octets** (+13 099 008) et les octets privés de **868 331 520 à 895 758 336** (+27 426 816). Deux remises à zéro, 13 clics périodiques et deux bursts de 20 et 30 clics ont entretenu le labo. La [capture finale du HUD](../evidence/public/unity/offline-final-endurance.png) montre 19 lancers admis, 69 touches, 100,0 dégâts et 44 statuts depuis la dernière remise à zéro ; elle ne représente pas un sort généré par Luna.

Le [log filtré](../evidence/public/unity/player-b28-final-excerpt.log) contient 79 fenêtres de dix secondes de boucle Unity, entre 1 245,8 et 1 633,6 frames calculées par seconde, avec au plus deux instances actives. Ce compteur n'est pas une mesure des FPS présentés à l'écran. Aucune trace Unity Profiler ni mesure du coût CPU p95 de la logique des sorts n'a été produite ; la croissance mémoire constatée sur onze minutes ne suffit pas à établir une stabilité durable, et la charge n'a pas saturé de nombreuses instances. **B28 reste partiel.** L'essai initial de 77,68 s et ses [13 anciens échantillons](../evidence/public/unity/b28-samples.csv) concernent une archive précédente.

La boucle complète dessin → Luna A → Luna B → compilation → sort issu de ce
dessin a ensuite été observée sur le job propriétaire, y compris la reprise
hors ligne ; voir la
[preuve actuelle](../evidence/public/unity/owner-player-end-to-end-2026-09-20.md).
L'écoute humaine du son, les scénarios physiques exhaustifs, l'installation
sur une autre machine et l'acceptation artistique restent ouverts.
