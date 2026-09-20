# Calibration de lecture A · candidat `sp.prompt.a/1.2`

Le 20 septembre 2026, l'auteur a rejeté la lecture de la sonde A `1.1` :
« faisceau de feu visuel, sans cible ni dégâts ». Il a précisé son intention
pour ce dessin : « laser unidirectionnel de lave (car rouge / brun), par
exemple ». Cette phrase est une intention humaine de conception, pas une
observation de Luna ni une consigne ajoutée au parchemin joueur. La sortie A
originale (SHA-256 `83C173816320C022A6C02A84C8DED3398F0B75987909D19C98F6E95EB6B36CCC`)
reste figée et rejetée artistiquement. Le PNG d'encre réel avait pour SHA-256
`18CDA98D1EF7E2A6F2148CFB81998D0B4494EA1E46BC288477459E33F1482AFC`.

## Correction proposée

Le candidat `1.2` demande à A d'examiner l'orientation et les extrémités
d'un trait ouvert, de considérer un porteur émis dans un seul sens et de
reconnaître qu'une couleur rouge-brun peut évoquer la lave en fusion. Il
sépare matière, mouvement, effets et récepteurs. Il ne garantit pas qu'un
trait rouge-brun soit toujours une attaque de lave : une couleur seule ne
justifie pas un dégât, une brûlure ou un filtre de cible. Une direction de
cast peut suivre l'axe visible sans inventer une origine marquée dans l'image.

Lors de la sonde `1.2`, B utilisait encore `sp.prompt.b/1.0`. Une correction
distincte de B est maintenant préparée en `1.1` ; elle ne change pas les
octets de A `1.2` déjà testés. Les schémas, le catalogue et les règles de
compilation restent inchangés pour cette calibration A. Chaque nouvelle
description conserve son identifiant de version ;
la reprise d'une ancienne description garde la version enregistrée en base
dans la provenance du paquet. Le hash d'entrée de A incorpore désormais le
hash exact du texte de prompt chargé. Le worker refuse au chargement un texte
de prompt A dont l'en-tête ne correspond pas à sa version attendue.

## Corpus de conception et contre-exemples à recueillir

Seul le premier cas ci-dessous possède aujourd'hui un vrai dessin et une
intention humaine consignée. Les autres lignes sont des **dessins à créer**
pour la mise au point, pas des résultats observés ni des sorties synthétiques
présentées comme réelles. Les 30 dessins inédits de M7 restent hors réglage.

| Cas de conception | Indice à faire varier | Risque à vérifier |
| --- | --- | --- |
| D01 · sonde Unity rejetée | Un trait diagonal continu rouge-brun dans la couronne | Lire une émission dirigée et une matière possiblement lave ; ne pas inventer une cible visible |
| D02 · à dessiner | Trait rouge-brun fermé ou statique | Ne pas forcer un laser pour toute couleur chaude |
| D03 · à dessiner | Trait droit ouvert d'une autre matière | Ne pas forcer la lave pour toute émission dirigée |
| D04 · à dessiner | Trait rouge-brun dans le noyau, sans indice de déploiement | Respecter le rôle de la région et la composition globale |
| D05 · à dessiner | Trait chaud dirigé avec indice distinct d'impact | Distinguer un effet de contact justifié du style seul |
| D06 · à dessiner | Trait avec pointe, rupture ou double émission | Distinguer sens, discontinuité et nombre de porteurs |

Conserver pour chaque cas l'image de référence, le PNG et le journal du
dessin, les hashes, l'intention enregistrée **avant** lecture des sorties,
les descriptions `1.1` et `1.2` obtenues sur les mêmes octets, leurs modèles
et efforts effectifs, les décisions de deux créateurs et les coûts de quota.
Comparer au plus ces deux configurations sur les mêmes dessins. Le formulaire
de retour du labo n'effectue ni réécriture de prompt, ni nouveau tirage d'un
parchemin déjà engagé.

## Diagnostic actif de `1.2`

Une sonde A/B supplémentaire a été exécutée sous le compte de service le
20 septembre 2026 avec les deux images de la même capture. Son rapport privé
`doctor-active-lava-calibration-20260920.json` a pour SHA-256
`0FE07D1A36C84E23239669ECD1B63EF6F517590C8EBE0F17D0FAC3B12E01EFB3`.
A et B ont tous deux terminé au niveau transport avec `gpt-5.6-luna` et
`max` rapportés. La sortie A, SHA-256
`D0C29D6FC344FDFA420ABCBCB0AF303BF0761B9D7C339D1FD033CCD25D004055`,
s'intitule « Faisceau de lave » et décrit le trait comme émission dans un
seul sens. C'est une amélioration de lecture apparente, **pas encore un
verdict humain d'acceptation**.

A n'a relevé qu'une observation `o0` : un unique trait rouge-brun plein,
allongé et diagonal, sans rupture ni embranchement. Sa clause `c1` ajoute
cependant `damage` au contact en citant seulement `o0`. La précision donnée
par l'auteur mentionne la matière et la direction, sans demander de dégâts
ni désigner de cible. Pour ce diagnostic, le travail de calibration retient
donc une lecture visuelle conservatrice et prépare `1.3` ; cela ne constitue
pas une approbation humaine formelle du sort. B a ensuite ajouté un filtre `hostile` absent de la
description A et `tick_interval=1` pour le faisceau ; le plan n'a pas reçu de
validation de compilation contrôlée. Sa sortie SHA-256 est
`E1D6B99385E1762F08E9621F7908A4364355E045F9E1E4B1E511B94F09A6B32C`.

Le texte de prompt `1.2` reste figé tel que testé, avec une copie octet pour
octet dans `prompts/history/01_MODEL_A_INTERPRETE_1_2.md` (SHA-256
`3BF549DF82EA54BF02ABC310D8614857D840A38A996F2D76D334D7D0BCE546C8`).
Le préfixe envoyé à A avant `LAYOUT_CONTEXT` correspond exactement à ce
fichier. La sonde est un diagnostic
de calibration, pas un sort joueur publié. Aucun corpus comparatif complet,
nouveau sort joueur ou acceptation M2/M7 n'est attesté ici. Le worker joueur
reste fermé jusqu'aux décisions humaines et à un plan B valide.
