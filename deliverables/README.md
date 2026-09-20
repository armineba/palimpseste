# Livraison Windows — Palimpseste 1.2

Le [jeu Windows IL2CPP / URP](../game/Build/WindowsComposedVfxPlayable/Palimpseste.exe) est construit et lancé sur ce PC. Le raccourci **Palimpseste Spell Lab** du Bureau ouvre cette version. Conserver les 29 fichiers du dossier jouable ensemble.

## Player

Le [ZIP du jeu](Palimpseste-Windows-x64-IL2CPP.zip) contient **29 fichiers**, vérifiés par SHA-256 contre le build Unity `WindowsComposedVfxRelease`. Taille : **44 189 567 octets**. SHA-256 : `a8b137d587f6a4f2d3677c06c38d4be7baaa280183ae52cbeb3622e201a42b68`.

Unity 6000.3.24f1 a terminé le build Windows x64 IL2CPP avec retour 0. Le [journal final](../evidence/public/unity/composed-spectre/build-windows-final.log) et le [manifeste de livraison](../evidence/public/unity/composed-vfx-delivery.json) donnent les fichiers et empreintes exacts. Les dossiers de symboles `DoNotShip` sont exclus.

Le client ajoute des compositions VFX animées (sceaux, rubans, voiles, particules et impacts), les 21 formes 3D existantes et le temps réel écoulé pendant la génération. Les sorts déjà enregistrés bénéficient du nouveau renderer sans appel modèle. Le [vrai spectre sauvegardé](../evidence/public/unity/composed-vfx-2026-09-20.md) a été relancé dans Unity : six captures, contact, dégâts et impulsion. Les 5 contrôles ciblés ont réussi. Le rendu artistique et le son restent à accepter humainement.

## Génération et backend

Pour les nouveaux dessins : **Sol/high → Astra/high**, prompts A `2.2` et B `1.9`, puis compilation de données contrôlées. Le [diagnostic réel](../evidence/public/backend/sol-astra-active-2026-09-20.json) a pris **60,403 secondes d'appels**, sans réparation. Ce seul essai ne garantit pas le délai des prochains dessins. La génération ne lance aucun test ni build Unity.

L'[API et le worker](../evidence/public/backend/sol-astra-deployment-2026-09-20.json) sont déployés sous `PalRuntimeSvc`. Codex utilise le compte déjà connecté via `codex exec` isolé. Les dessins ne peuvent ni exécuter du code, ni modifier les sources, ni accéder aux secrets. Les 141 recettes reposent sur les 24 primitives contrôlées ; voir [la bibliothèque](../docs/EFFECT_LIBRARY.md).

Le [ZIP backend](Palimpseste-Backend-Windows-x64.zip) est réservé à l'opérateur. Il contient **114 fichiers vérifiés**, pour **111 461 849 octets**. SHA-256 : `3386c5b3f8ff9eebe0a672319ea82394ead743ecb661d7aedb7c8c3de38af007`. Voir le [contrôle de l'archive](../evidence/public/backend/sol-astra-package-2026-09-20.json). Aucun secret Codex, clé API, jeton joueur ou identifiant de base ne fait partie des archives.

Le déploiement actuel est privé sur **127.0.0.1**. L'accès HTTPS public, plusieurs joueurs distants, la recette de 30 dessins, un second créateur et un autre poste restent non validés. Voir [comment tester](../docs/TESTER_MAINTENANT.md) et [l'état de réalisation](../docs/IMPLEMENTATION_STATUS.md).
