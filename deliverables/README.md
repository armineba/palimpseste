# Livraison Windows — préparation Palimpseste 1.5.0 / D15

Les scripts de packaging sont adaptés à `WindowsBehaviorRelease` / `WindowsBehaviorPlayable`, version `1.5.0`, journal `behavior-build.log`. **Aucun build ou packaging D15 n'est attesté par cette mise à jour documentaire.** Les empreintes courantes ci-dessous désignent la livraison précédente jusqu'à remplacement effectif des archives et de leurs manifestes.

La distribution Player D15 ajoutera les notices CC0 des textures et la licence MIT du bruit HLSL réutilisé sous `ThirdPartyNotices/`. La spécification backend inclura les catalogues de références et de ressources, leurs licences et leurs PNG, avec les migrations jusqu'à `010`. Les sources HLSL de référence et scripts d'import ne font pas partie de la spécification runtime : le renderer Unity intègre le code relu lors du développement. Voir [les ressources](../docs/references-vfx-sources.md) et [l'exploitation D15](../ops/README.md).

Le déploiement sur le laboratoire D14 applique uniquement `010`, avec `009` déjà présent. Une nouvelle base exige toutes les migrations `001` à `010` dans l'ordre. Ne pas réappliquer `009` après D15 : l'ancienne contrainte exclut les jobs de version `3`.

## Livraison précédente attestée — 1.4.1 / D14

Les archives ont été construites et installées sur le poste d'origine. Pour transmettre les sources et reprendre ailleurs, lire [la passation](../docs/PASSATION.md).

## Player

Le [ZIP du jeu](Palimpseste-Windows-x64-IL2CPP.zip) contient 29 fichiers, 122 861 334 octets après extraction. Archive de 44 367 955 octets ; SHA-256 :

```text
15e056a3e21ae1549c3af857ca03cbdaf3a1394b4e0369511c9da77ce3489416
```

Build Unity 6000.3.24f1 / URP / Windows x64 IL2CPP, code 0. [Manifeste des fichiers](../evidence/public/unity/lifecycle-delivery.json), [résultat du build](../evidence/public/unity/lifecycle-capture-fix/build-result.log). Conserver tous les fichiers extraits ensemble et ouvrir `Palimpseste.exe`.

Le texte définit apparition, activité, contact et disparition. L'image générée est la cible visuelle. Un renderer précompilé produit quatre captures, puis la critique J guide les corrections bornées. La version 1.4.1 corrige les captures noires en mode batch ; le sort joueur bloqué a repris avec sa description, son image et son plan conservés.

## Backend opérateur

Le [ZIP backend](Palimpseste-Backend-Windows-x64.zip) fournit API, worker, outils diagnostiques, contrats, prompts A/G/B/J, migrations jusqu'à `009`, scripts opérateur et Codex natif durci. [Empreinte de l'archive courante](../evidence/public/backend/lifecycle-delivery.json).

Réglages conservés : A `gpt-5.6-sol` / `high`, G/B/J `gpt-6-astra` / `high`. Aucun outil pour A/B/J ; seulement l'image native pour G. Les sorts sont des données contrôlées. Aucune authentification Codex, clé API, chaîne DB ni identité joueur n'est distribuée.

L'extraire dans un dossier opérateur inaccessible au worker. Les scripts actuels utilisent les chemins et comptes du poste d'origine ; ils demandent une configuration adaptée sur un autre PC. Le renderer est une copie protégée du Player 1.4.1, avec manifeste de tous ses fichiers et empreinte configurée dans le service. Aucun accès HTTPS distant n'est préconfiguré.

## État observé et limites

[Correction et reprise réelles](../evidence/public/backend/lifecycle-capture-fix-2026-09-20.json) : captures distinctes et critique J réussie. L'affinage du sort joueur est encore en cours au point de reprise documenté ; le verdict artistique et les essais dans le laboratoire appartiennent au créateur. Aucun test indépendant supplémentaire n'a été lancé après sa consigne. Voir [l'état de réalisation](../docs/IMPLEMENTATION_STATUS.md).
