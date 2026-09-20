# Livraison Windows — Palimpseste 1.2.2

Le [jeu Windows IL2CPP / URP](../game/Build/WindowsSpectralEnergyPlayable/Palimpseste.exe) est construit et lancé sur ce PC. Le raccourci **Palimpseste Spell Lab** du Bureau ouvre cette version. Conserver les 29 fichiers du dossier jouable ensemble.

## Player

Le [ZIP du jeu](Palimpseste-Windows-x64-IL2CPP.zip) contient **29 fichiers**, vérifiés par SHA-256 contre le build Unity `WindowsSpectralEnergyRelease`. Taille : **44 089 295 octets**. SHA-256 : `6836f2cac5941a58d28b5a07c1b7f5e0b88cc5c51430320a6c9d9d199c1c542e`.

Unity 6000.3.24f1 a terminé le build Windows x64 IL2CPP avec retour 0. Le [journal final](../evidence/public/unity/spectral-fidelity/build-windows.log) et le [manifeste de livraison](../evidence/public/unity/spectral-energy-delivery.json) donnent les fichiers et empreintes exacts. Les dossiers de symboles `DoNotShip` sont exclus.

Après le refus artistique du spectre 1.2.1, quatre itérations ont repris son rendu. La tête devient une brume lumineuse sans visage ni capuche opaques. Six nappes d'énergie suivent la trajectoire déjà parcourue, ondulent avec des phases distinctes, puis se dissipent pendant 0,62 seconde après le contact. Un éclair court précède les fragments d'impact. La caméra du labo reste inchangée. Le sort sauvegardé bénéficie de ces changements sans nouvelle génération.

Le [rapport de vérification](../evidence/public/unity/spectral-fidelity-2026-09-20.md) conserve les quatre itérations du vrai spectre : 1/1, 1/1, 5/5, puis 1/1 contrôles réussis. La [vidéo du labo](../evidence/public/unity/spectral-fidelity/iteration-04/spectre-lab.mp4) montre les 2,433 secondes capturées, sans son. Aucun appel fournisseur n'a été effectué pour cette reprise. La correspondance « 1 pour 1 » à la référence n'est pas démontrée ; le verdict artistique et le son restent ouverts. La cadence de capture ne constitue pas un benchmark de FPS. Les autres formes et recettes n'ont pas toutes fait l'objet de cette revue.

## Génération et backend

Pour les nouveaux dessins : **Sol/high → Astra/high**, prompts A `2.2` et B `1.9`, puis compilation de données contrôlées. Le [diagnostic réel précédent](../evidence/public/backend/sol-astra-active-2026-09-20.json) a pris **60,403 secondes d'appels**, sans réparation. Ce délai n'a pas été remesuré pour la finition 1.2.2 et ne garantit pas celui des prochains dessins. La génération ne lance aucun test ni build Unity.

L'[API et le worker](../evidence/public/backend/sol-astra-deployment-2026-09-20.json) sont déployés sous `PalRuntimeSvc`. Codex utilise le compte déjà connecté via `codex exec` isolé. Les dessins ne peuvent ni exécuter du code, ni modifier les sources, ni accéder aux secrets. Les 141 recettes reposent sur les 24 primitives contrôlées ; voir [la bibliothèque](../docs/EFFECT_LIBRARY.md).

Le [ZIP backend](Palimpseste-Backend-Windows-x64.zip) est réservé à l'opérateur. Il reste inchangé et compatible avec cette évolution du renderer. Il contient **114 fichiers vérifiés**, pour **111 461 849 octets**. SHA-256 : `3386c5b3f8ff9eebe0a672319ea82394ead743ecb661d7aedb7c8c3de38af007`. Voir le [contrôle de l'archive](../evidence/public/backend/sol-astra-package-2026-09-20.json). Aucun secret Codex, clé API, jeton joueur ou identifiant de base ne fait partie des archives.

Le déploiement actuel est privé sur **127.0.0.1**. L'accès HTTPS public, plusieurs joueurs distants, la recette de 30 dessins, un second créateur et un autre poste restent non validés. Aucun nouveau parcours complet dessin → modèles → Player 1.2.2 n'a été effectué pour cette finition. Voir [comment tester](../docs/TESTER_MAINTENANT.md) et [l'état de réalisation](../docs/IMPLEMENTATION_STATUS.md).
