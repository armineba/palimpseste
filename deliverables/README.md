# Livraison Windows — Palimpseste 1.3.0 / D13

État du **20 septembre 2026**. Pour transmettre les sources et reprendre sur un autre poste, lire [la passation](../docs/PASSATION.md). Les archives ci-dessous ont été réellement produites ; leur installation sur un autre PC reste à vérifier.

## Player

Le [ZIP du jeu](Palimpseste-Windows-x64-IL2CPP.zip) contient **29 fichiers**, soit **122 734 785 octets** après extraction. Taille du ZIP : **44 320 872 octets**. SHA-256 :

```text
0ca7d134f733632f23483726b70fbbeb1de750b046d1478efa94f18d4891ad7b
```

Build **Unity 6000.3.24f1 / URP / Windows x64 IL2CPP**, terminé avec le code 0. Voir le [journal final](../evidence/public/unity/image-reference/build-final.log) et le [manifeste des fichiers](../evidence/public/unity/image-reference-delivery.json). Conserver tous les fichiers extraits ensemble, puis ouvrir `Palimpseste.exe`. Le Player correspondant a été lancé sur le poste d'origine depuis `game/Build/WindowsImageReferencePlayable/` ; ce dossier de build est ignoré par Git.

Le parcours D13 est **dessin → description → image générée → construction du sort depuis l'image → laboratoire**. Pour l'essayer : **Dessiner un parchemin → Dessin terminé → Lancer dans le laboratoire**, lorsque la construction est prête. La génération peut prendre plusieurs minutes. Un nouveau dessin est nécessaire pour essayer cette nouvelle chaîne ; les anciens sorts conservés ne sont pas régénérés. [Instructions joueur](../docs/TESTER_MAINTENANT.md).

## Backend opérateur

Le [ZIP backend](Palimpseste-Backend-Windows-x64.zip) contient **135 entrées**, pour **210 806 064 octets**. SHA-256 :

```text
6c29f85430cf1d7a9caeec02c8625343b811c754ac831a69bec60379e10f56a2
```

La [preuve du packaging](../evidence/public/image-reference-backend-package-2026-09-20.json) atteste la publication avec code 0 et les empreintes des entrées critiques. L'archive contient l'API, le worker et le doctor .NET autonomes, les contrats, prompts A/G/B, migrations `001` à `008`, scripts opérateur et le binaire natif durci `codex-image.exe`, avec ses correctifs et notices. Son SHA-256 est `0a38e51ceca23710d2ced5ed06c6822584aa8a8e418defe116a994ce384d3a4f`.

Configuration réellement déployée localement : **A `gpt-5.6-sol` / `high`, G et B `gpt-6-astra` / `high`**, prompts A `2.2` et B `2.1`. Les appels passent par `codex exec` avec la connexion ChatGPT du compte de service. A/B n'ont aucun outil exécutable ; G ne dispose que de l'image native. Les sorts produits restent des données bornées, sans code généré à exécuter. Voir [l'intégration native](../ops/codex-image-generation.md) et [la preuve du déploiement local](../evidence/public/backend/image-reference-deployment-2026-09-20.json).

Cette archive est réservée à l'opérateur. L'extraire dans un dossier inaccessible au worker, puis suivre [la passation](../docs/PASSATION.md) et [les instructions d'exploitation](../ops/README.md). Elle ne contient ni authentification Codex, ni jetons joueur, ni secrets DB. Les lanceurs actuels sont liés aux chemins, comptes et empreintes du laboratoire d'origine : ils nécessitent une configuration revue sur le nouveau poste. Aucun accès HTTPS distant n'est préconfiguré.

## Limites de validation

Les [sondes fournisseur réelles](../evidence/public/backend/image-reference-2026-09-20.json) et la [capture Unity du vrai paquet](../evidence/public/unity/image-reference-2026-09-20.md) sont documentées séparément. Le dernier réglage du verre est inclus au build, mais postérieur à la capture. Un nouveau parcours joueur D13 complet, la relecture hors ligne de ce nouveau parcours, la qualité artistique et sonore, la fidélité « 1 pour 1 », les 30 dessins de recette et l'accès de joueurs distants restent non acceptés ou non observés. Voir [l'état détaillé](../docs/IMPLEMENTATION_STATUS.md).
