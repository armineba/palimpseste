# Livraison Windows — préparation Palimpseste 1.6.0 / D16

Les scripts de packaging ciblent `WindowsAnimationSheetRelease` / `WindowsAnimationSheetPlayable`, version `1.6.0`, journal `animation-sheet-build.log`. Cette préparation documentaire ne prouve aucun build, packaging ou déploiement D16. Les versions, dates et empreintes effectivement livrées sont consignées dans les manifestes ci-dessous.

## Archives et preuves courantes

- [ZIP du jeu](Palimpseste-Windows-x64-IL2CPP.zip) : Player Unity 6000.3.24f1 / URP / Windows x64 IL2CPP. Consulter son [manifeste réel](../evidence/public/unity/lifecycle-delivery.json) pour la version, le build et le SHA de l'archive. Extraire tous les fichiers ensemble puis ouvrir `Palimpseste.exe`.
- [ZIP backend](Palimpseste-Backend-Windows-x64.zip) : API, worker, programmes diagnostiques, spécification et scripts opérateur. Son [index de livraison](../evidence/public/backend/lifecycle-delivery.json) identifie la publication et la preuve correspondante.
- [État de réalisation](../docs/IMPLEMENTATION_STATUS.md), [point de reprise](../docs/NEXT_ACTIONS.md) et [passation](../docs/PASSATION.md).

Les chemins historiques `lifecycle-delivery.json` restent les index courants de toutes les versions ; leur contenu fait autorité sur les anciens hashes cités ailleurs. Une compilation réussie ne vaut pas acceptation visuelle ou validation du gameplay.

## Cible D16

La référence devient une planche **3 lignes × 7 cases** : APPARITION, STABLE, DISPARITION. Chaque ligne est numérotée de 1 à 7 ; le nom du sort et VFX ANIMATION SHEET sont composés par le serveur, avec repères bleu/vert/violet sur fond sombre. G produit l'atlas natif sans marges ; un compositeur fixe crée la planche finale **1536 × 1152**. Les deux PNG conservent des artefacts et empreintes distincts : la planche mise en page n'est pas présentée comme une sortie native du fournisseur.

Le texte définit le cycle et les mécaniques. La planche, les références primaires sélectionnées et les ressources gratuites guident la construction. Le renderer Unity précompilé et la critique J assurent les corrections bornées pendant les générations demandées par le joueur. La validation humaine reste à consigner.

## Contenu et installation

Le Player conserve les notices CC0 des textures et MIT du bruit HLSL dans `ThirdPartyNotices/`. Le backend inclut les migrations `001` à `011`, les PNG et catalogues de ressources, les prompts courants et les historiques A 2.3 / G 1.2. Les programmes publiés conservent la notice MIT de System.Drawing.Common ; aucun fichier de police, code HLSL de référence ou script d'import automatique n'est distribué dans la spécification runtime.

Sur le laboratoire D15 existant, `deploy-lifecycle.ps1` exige le schéma de `010`, dont `spell_reference_research`, et applique **uniquement `011_animation_sheet.sql`**. Une base neuve exige toutes les migrations `001` à `011` dans l'ordre. Ne pas rejouer les anciennes contraintes `009` / `010` sur des jobs plus récents. Voir [les instructions d'exploitation](../ops/README.md).

L'archive backend doit être extraite dans un dossier opérateur inaccessible au worker. Les chemins et comptes du poste d'origine demandent une configuration propre sur un autre PC. Le renderer est une copie protégée du Player attendu par le déploiement, avec manifeste SHA de tous ses fichiers. Aucune authentification Codex, clé API, chaîne DB ou identité joueur n'est distribuée ; aucun endpoint HTTPS distant n'est préconfiguré.

A reste `gpt-5.6-sol` / `high` ; G/B/J restent `gpt-6-astra` / `high`. A/B/J n'ont aucun outil ; G dispose uniquement de l'image native. Les sorts sont des données contrôlées.
