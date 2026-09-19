# Nature des exemples

Tous les exemples de ce dossier sont rédigés ou fabriqués pour illustrer les contrats. Aucun n’est une sortie observée d’un appel multimodal ou d’un moteur Unity.

`01_description_illustrative.json`, `02_plan_illustratif.json` et `03_paquet_illustratif.json` forment un dossier cohérent de contrat. La description est manuelle, et la géométrie de `geometry/` est une fixture manuelle : elle n’a pas été extraite du PNG par un résolveur livré. Les images servent d’illustration de l’entrée, pas de preuve de fidélité.

Les `fixture_*.json` exercent les six types de porteurs et effets. Leur empreinte de description nulle signale une fixture isolée du traducteur. Ne jamais les utiliser pour prétendre qu’une génération réelle a fonctionné.

Les budgets du paquet principal sont des bornes illustratives conservatrices. Le développement doit les recalculer avec le compilateur livré et documenter tout changement de règle. Les masques et chemins sont des assets de tests ; ils ne constituent pas une bibliothèque de sorts joueur.
