# D16 — Planche d’animation des futurs sorts

## Décision du créateur

La référence visuelle des nouveaux parchemins remplace l’image unique par le format fourni le 21 septembre 2026 : **APPARITION / STABLE / DISPARITION**, trois lignes de sept cases numérotées. La description continue de définir les mécaniques, le placement et toute la chronologie ; la planche guide leur apparence. Les archives existantes conservent leur parcours.

## Production et reprise

1. A décrit les sujets, leur comportement physique et les quatre phases : apparition, activité, contact et expiration.
2. G 1.3 reçoit uniquement la description et produit un véritable atlas natif de 7 × 3 scènes avec `image_gen`, sans texte ni marges. Un appel G pour les 21 scènes ; aucun appel supplémentaire par case.
3. Le serveur enregistre cet atlas et son SHA dans un checkpoint immuable `visual_atlases`, puis termine la tentative fournisseur avec le SHA **natif**. Une reprise après cet enregistrement réutilise l’atlas, sans rappeler G.
4. Le compositeur fixe `sp.animation-sheet-composer/1.0` produit la planche **1536 × 1152** : fond sombre, titre du sort, sous-titre VFX ANIMATION SHEET, titres des trois lignes, cadres, accents bleu/vert/violet et numéros 1 à 7. Les scènes conservent leur palette et leur ratio. Cette mise en page possède son propre artefact/SHA ; elle n’est pas présentée comme la sortie native du modèle.
5. La recherche de références et de ressources gratuites D15 est liée à la description et au SHA de cette planche finale. B 2.4 voit la planche entière et construit un ensemble de sujets animé, avec les données bornées du moteur.
6. La critique indépendante J 1.2 compare la cible aux bandes temporelles réellement produites par le Player de rendu précompilé. La boucle Pro conserve son budget de reprises ; aucun modèle ne dispose d’une commande de capture, de téléchargement ou de build.

Les versions de pipeline 4 concernent seulement les nouveaux jobs. Les jobs plus anciens conservent G 1.2 et la référence unique ; les images déjà enregistrées ne sont pas recomposées. Les métadonnées facultatives sont omises des paquets historiques pour préserver leurs empreintes.

## Lecture temporelle

Les sept positions normalisées de chaque ligne sont **0, 130, 290, 470, 640, 820, 1000 millièmes**. STABLE signifie un régime actif continu : rotation, écoulement, battement ou voyage quand ils sont décrits. Les sept cases ne sont pas sept variantes.

`ending_basis` désigne la branche illustrée par DISPARITION : `contact` pour un premier sujet racine projectile/piège, `expiration` pour les autres porteurs. Le contact et l’expiration restent tous deux décrits et construits ; une seule de ces branches occupe la troisième ligne de référence. Le choix dérive des faits contrôlés de la description, sans correctif par nom de sort.

Le renderer D16 conserve **28 PNG réels de 1024 × 1024** avec leurs temps et hashes : sept pour apparition, activité, contact et expiration. Quatre bandes de sept cases sont transmises à J avec la cible, soit cinq images jointes. Les poses sont échantillonnées sur une horloge de présentation contrôlée ; la mesure de débit du rendu utilise séparément l’horloge normale. Voir [le protocole Unity](UNITY_ANIMATION_SHEET_D16.md).

## Limites et validation

La grille et les intitulés sont produits par du code fixe. La conformité artistique des scènes générées, la progression voulue et la fidélité du VFX ne sont pas garanties par un reçu JSON, un SHA ou un build réussi. Les captures de présentation ne prouvent pas les collisions, les dégâts ou la fluidité en jeu.

Le créateur effectue lui-même les essais : aucun test, diagnostic modèle, génération ou capture indépendante demandé pour D16. La boucle Pro s’exécute lors des futures créations joueur. Le meilleur candidat réellement évalué peut conserver des écarts ; le système ne doit pas présenter cela comme une acceptation visuelle parfaite.

## Livraison et dépendance

Player **1.6.0**, Unity **6000.3.24f1 / URP / IL2CPP Windows**. Backend .NET 10, migration `011_animation_sheet.sql`. Le compositeur utilise `System.Drawing.Common 10.0.12`, gratuit sous MIT, uniquement sur le serveur Windows ; sa notice accompagne les binaires. Les polices sont celles installées sur Windows, aucune police n’est redistribuée. Aucun achat ni changement d’authentification Codex.

Builds et installation réussis avec le code 0, migration 011 appliquée : [preuve réelle](../evidence/public/backend/animation-sheet-d16.json) et [point de reprise](NEXT_ACTIONS.md). Aucun parcours visuel D16 exécuté par le développement. Les anciens sorts ne sont ni modifiés ni régénérés pour cette livraison.
