# Calibration de lecture A · candidat `sp.prompt.a/1.3`

L'auteur a proposé « laser unidirectionnel de lave (car rouge / brun), par
exemple » pour la capture de conception D01. Cette précision porte sur la
matière et la direction. Elle ne demande ni dégâts, ni brûlure, ni cible
particulière. La sonde active de A `1.2` a trouvé « Faisceau de lave », mais
a ajouté `damage` et `event=hit` à partir de son unique observation `o0`
(une ligne rouge-brun pleine et diagonale). B `1.0` a ensuite ajouté un filtre
`hostile` absent de A ; voir [le diagnostic `1.2`](PROMPT_A_CALIBRATION_1_2.md).

## Règle proposée dans `1.3`

Un trait ouvert, long, continu et orienté dans la couronne peut suggérer un
faisceau émis dans un seul sens. Sa teinte rouge-brun peut suggérer une
matière de lave. Si ce trait est la seule marque et ne montre ni impact,
contact, cible ni autre interaction distincte, A décrit le porteur et son
apparence **sans effet, sans événement `hit` et sans fait de cible**.
La chaleur imaginaire de la lave et le mot « laser » ne sont pas des preuves
indépendantes de dégâts. Une autre trace ou un détail réellement distinct
d'un même geste peut justifier une interaction ; A doit le décrire et le
citer. La règle s'applique aux autres couleurs et matières de la même façon.

Pour D01, le contrôle attendu du prochain diagnostic est une description
qualitative de faisceau de lave dirigé, une demande géométrique `ring/path`,
aucun fait `effect`, `target` ou `event=hit` issu de la seule ligne. Ces
attentes ont été fixées comme critère de conception avant la sonde Luna décrite
plus bas.
Le prompt B `1.1` doit conserver cette absence d'effets et utiliser son
filtre technique `environment` pour un faisceau uniquement visuel ; ce filtre
ne devient pas une cible racontée par A. Un sort issu de cette description
peut être peu puissant, conformément au cahier.

## Provenance et état

Le prompt A `1.2` réellement envoyé est conservé octet pour octet dans
`prompts/history/01_MODEL_A_INTERPRETE_1_2.md`, SHA-256
`3BF549DF82EA54BF02ABC310D8614857D840A38A996F2D76D334D7D0BCE546C8`.
Le candidat `1.3` est dans `prompts/01_MODEL_A_INTERPRETE.md` (SHA-256 des
octets de travail `F55219FE29D7E67657B6A874FF21EB84A0B661517C9562D0C8E2ABDB6A62E0D7`)
et le provider vérifie que son en-tête correspond à `sp.prompt.a/1.3`. La base stocke la
version associée à chaque description ; une reprise conserve cette version
dans la provenance du paquet. Le hash de l'entrée A incorpore le texte exact
de prompt chargé.

Comparer `1.2` et `1.3` sur les **mêmes** dessins de conception avant une
nouvelle décision humaine. Les dessins inédits M7 restent hors réglage ; un
retour dans le labo ne relance pas la génération d'un parchemin déjà figé.

## Sonde active de `1.3`

Une sonde sous `PalRuntimeSvc` a exécuté A `1.3` et B `1.1` sur les deux mêmes
images réelles ; le rapport privé `doctor-active-ab13-b11-20260920.json` a
pour SHA-256 `256E564490E1A0A7E5BC50FBA216F4DC29F40EF496C867E03F3BE14291349610`.
Les deux étapes ont rapporté `gpt-5.6-luna` et `max` avec sortie transport
réussie. A, SHA-256 `E54DEC5810E00AABD0FFCB8F152DF06468DD8505593B6718C5B806C46B3254B4`,
décrit un faisceau rectiligne rouge-brun évoquant la lave, avec émission
unidirectionnelle dans l'observation et sans effet ni cible. Le contrôle
structurel du cas de conception D01 passe. B, SHA-256
`D7ED136F345CFFB0DA51C25AA6A7EE198F8B034073BA8EF997BCF39E65571E58`,
propose un faisceau purement visuel `environment`, sans effet, avec une seule
évaluation.

La première compilation contrôlée a rejeté la géométrie de signature de B :
le doctor avait fourni une géométrie provisoire contenant une silhouette que
la résolution réelle de A et de l'encre ne produit pas. Voir la
[preuve du premier rejet](../evidence/public/backend/doctor-ab13-b11-2026-09-20.md).

Le doctor corrigé a résolu la géométrie à partir de cette même sortie A figée
et de l'encre réelle. Il a rappelé uniquement B `1.1` : sa nouvelle sortie,
SHA-256 `E8164D8D654F470101BDCE77A4B0AF46476FF7AE48EA9DF1A92A5B9A84D9AEA1`,
a passé le compilateur contrôlé puis `RealProbe` (paquet privé SHA-256
`8DAC5FEB3293184564623C1F259567CB025078563F8BC788A9928806BC7672E2`).
Le faisceau est visuel, dirigé suivant `ring.path.0`, avec zéro effet et zéro
dégât. Voir la [preuve du plan résolu](../evidence/public/backend/doctor-plan-b11-resolved-2026-09-20.md).
Ce paquet a ensuite passé le test Unity PlayMode isolé avec rendu Direct3D 12,
puis les tests du rendu de lave préfabriqué, 2/2 réussis. Voir la
[preuve Unity](../evidence/public/unity/lava-beam-ab13-b11-2026-09-20.md).
Le paquet est un diagnostic technique à provenance synthétique : aucun sort
issu de ce dessin n'a encore été publié par le worker, lancé dans le Player ou
accepté humainement. Le worker joueur reste fermé.
