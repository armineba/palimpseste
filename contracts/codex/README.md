# Schémas de sortie pour l'adaptateur Codex

Ces fichiers sont extraits de la propriété `schema` des enveloppes `model-a.response-format.json` et `model-b.response-format.json` du dossier SP-1.0. Ils ne contiennent pas l'enveloppe Responses.

Ils sont les schémas de transport de `codex exec --output-schema`. Une validation JSON Schema locale ne prouve pas leur acceptation par un modèle ou le transport installé. Adapter le sous-ensemble de transport si nécessaire, sans affaiblir la validation métier locale.

D21.1 : les valeurs constantes des deux transports B V2 sont explicitement typées et exprimées par un `enum` singleton. Le fournisseur avait rejeté `carrier: {const: ...}` sans `type`, avant toute génération. `ops/validate-codex-schemas.py` contrôle la structure des schémas V2 avant publication ; ce contrôle local ne remplace pas l'acceptation observée lors d'un vrai appel. Les contraintes métier des plans restent inchangées.

Pour la lecture globale du dessin (`sp.prompt.a/1.5`), Astra renseigne les observations avec `region: "full"` et rend `shape_requests: []`. Cette liste vide signifie que le résolveur construit sa banque de chemins, empreintes, silhouettes et distributions à partir de l'image entière. Elle ne dispense pas B de choisir pour chaque nœud un identifiant géométrique réel du rôle requis, ni le compilateur de contrôler cette référence. Les anciennes descriptions avec demandes régionales restent lisibles. Luna B reçoit la description figée, cette banque et le catalogue ; elle ne reçoit pas les images.
