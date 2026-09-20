# Corpus de conception pour le prompt A

`design/lava-laser-001.json` consigne l'intention donnée par l'auteur le 20 septembre 2026 pour le dessin Unity déjà utilisé dans la sonde active. Il s'agit d'un **cas de conception**, exclu des 30 cas inédits de recette. Les empreintes lient le cas à la référence, à la capture et à l'encre privées ; aucune image privée ni sortie du modèle n'est recopiée ici. La lecture précédente, « faisceau de feu visuel, sans cible ni dégâts », a été rejetée par l'auteur dans la conversation. Ce retour n'est pas une revue M7 signée.

Après avoir versionné une nouvelle instruction A, lancer sur les **mêmes images** une sonde autorisée du compte de service et archiver ses octets et son usage hors du dépôt. Le contrôle suivant examine sa description, sans appel fournisseur :

```powershell
python tests/calibration/check_prompt_a.py --self-test
python tests/calibration/check_prompt_a.py --description <nouvelle-sortie-A.json>
```

Le contrôle vérifie le schéma et des faits structurés du cas : faisceau, affinité feu, un sujet, un chemin issu du dessin et aucune relation ajoutée. Pour **D01 seulement**, l'évaluation de conception retient une trace unique sans indice distinct d'impact : le script rejette donc tout fait `effect`, tout fait `target` et `event=hit` dans n'importe quelle clause. Il ne cherche pas les mots « lave » ou « laser » pour obtenir un faux succès par simple titre. Une personne doit regarder l'image, les observations, l'émission unidirectionnelle et la matière de lave rendues par le texte, puis le cast Unity ; le tableau `human_review` du cas énumère les points à juger.

Ce contrôle est propre à **ce cas** et ne doit pas devenir une règle globale qui interdit les dégâts à tous les traits rouges. L'auteur a précisé la matière et la direction, sans signer une validation globale ni statuer ici sur tous les effets possibles. Une réussite du script ne remplace pas son verdict sur le dessin et le sort ; elle ne signifie ni lecture humaine acceptée, ni sort publié, ni pipeline joueur opérationnel. La description précédente et les parchemins déjà figés restent immuables. Ne pas utiliser les 30 dessins inédits de recette pour régler le prompt. Les diagnostics séquentiels de D01 ne constituent pas une campagne comparative complète ; pour une comparaison contrôlée, le cahier limite à deux configurations sur le même corpus de conception.
