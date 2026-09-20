# Livraison Windows — Palimpseste 1.2.1

Le [jeu Windows IL2CPP / URP](../game/Build/WindowsStylizedVfxPlayable/Palimpseste.exe) est construit et lancé sur ce PC. Le raccourci **Palimpseste Spell Lab** du Bureau ouvre cette version. Conserver les 29 fichiers du dossier jouable ensemble.

## Player

Le [ZIP du jeu](Palimpseste-Windows-x64-IL2CPP.zip) contient **29 fichiers**, vérifiés par SHA-256 contre le build Unity `WindowsStylizedVfxRelease`. Taille : **44 217 522 octets**. SHA-256 : `8c92040e5abdbc8d690216010cafd9522f824dca7c9cd7db6aa42c4c82a32c0c`.

Unity 6000.3.24f1 a terminé le build Windows x64 IL2CPP avec retour 0. Le [journal final](../evidence/public/unity/stylized-vfx/build-windows.log) et le [manifeste de livraison](../evidence/public/unity/stylized-vfx-delivery.json) donnent les fichiers et empreintes exacts. Les dossiers de symboles `DoNotShip` sont exclus.

Le client ajoute des spirales translucides ascendantes, des sceaux et couronnes animés, quatre familles de particules (lances, points en orbite, étoiles et nappes diffuses), des voiles plus doux et des impacts de 1,25 seconde. Le HDR et le bloom préservent la couleur des effets. Les 21 formes 3D existantes restent disponibles. Les sorts déjà enregistrés bénéficient du nouveau renderer sans appel modèle.

Le [rapport de vérification](../evidence/public/unity/stylized-vfx-2026-09-20.md) distingue neuf captures de compositions de présentation et six captures du vrai spectre sauvegardé, avec contact, dégâts et impulsion. Six contrôles distincts ont réussi au fil des corrections ; aucun passage unique « 6/6 » n'est revendiqué. Aucun nouvel appel fournisseur n'a été effectué pour cette finition. Le rendu artistique et le son restent à accepter humainement ; aucun benchmark de FPS n'a été réalisé.

## Génération et backend

Pour les nouveaux dessins : **Sol/high → Astra/high**, prompts A `2.2` et B `1.9`, puis compilation de données contrôlées. Le [diagnostic réel précédent](../evidence/public/backend/sol-astra-active-2026-09-20.json) a pris **60,403 secondes d'appels**, sans réparation. Ce délai n'a pas été remesuré pour la finition 1.2.1 et ne garantit pas celui des prochains dessins. La génération ne lance aucun test ni build Unity.

L'[API et le worker](../evidence/public/backend/sol-astra-deployment-2026-09-20.json) sont déployés sous `PalRuntimeSvc`. Codex utilise le compte déjà connecté via `codex exec` isolé. Les dessins ne peuvent ni exécuter du code, ni modifier les sources, ni accéder aux secrets. Les 141 recettes reposent sur les 24 primitives contrôlées ; voir [la bibliothèque](../docs/EFFECT_LIBRARY.md).

Le [ZIP backend](Palimpseste-Backend-Windows-x64.zip) est réservé à l'opérateur. Il reste inchangé et compatible avec cette évolution du renderer. Il contient **114 fichiers vérifiés**, pour **111 461 849 octets**. SHA-256 : `3386c5b3f8ff9eebe0a672319ea82394ead743ecb661d7aedb7c8c3de38af007`. Voir le [contrôle de l'archive](../evidence/public/backend/sol-astra-package-2026-09-20.json). Aucun secret Codex, clé API, jeton joueur ou identifiant de base ne fait partie des archives.

Le déploiement actuel est privé sur **127.0.0.1**. L'accès HTTPS public, plusieurs joueurs distants, la recette de 30 dessins, un second créateur et un autre poste restent non validés. Aucun nouveau parcours complet dessin → modèles → Player 1.2.1 n'a été effectué pour cette finition. Voir [comment tester](../docs/TESTER_MAINTENANT.md) et [l'état de réalisation](../docs/IMPLEMENTATION_STATUS.md).
