# Provenance des assets

| Élément | Provenance | Usage dans le projet |
|---|---|---|
| `game/Assets/Palimpseste/Art/LibraryBackdrop.png` et sa copie dans `Resources/` | Image générée pour ce projet le 19 septembre 2026 | Décor peint de la bibliothèque ; aucune donnée de sort n'en dépend |
| `docs/art-direction/storm-bird-target.png` | Image réellement générée par l'outil de développement le 20 septembre 2026 ; prompt et empreintes dans les fichiers voisins | Cible artistique de développement, distincte des images produites par le service joueur et des captures Unity |
| Références visuelles des nouveaux sorts D13 | PNG généré par l'outil image natif Codex depuis la description figée ; provenance, dimensions et empreinte enregistrées avec chaque sort | Référence observée par le constructeur et affichable dans la fiche ; le labo exécute une composition 3D animée, sans afficher ce PNG sur un plan à la place du sort |
| Meshes, shaders, matériaux, audio procédural et UI dans `game/Assets/Palimpseste/` | Sources créées dans ce dépôt | Client et scène d'épreuve |
| `reference/` et `examples/` | Dossier documentaire fourni par le commanditaire | Référence neutre et fixtures illustratives |
| Paquets Unity, dont URP et Newtonsoft JSON | Installés par Unity Package Manager ; versions dans `game/Packages/` | Dépendances de compilation et du lecteur |
| Codex CLI modifié pour le service | Source officielle `openai/codex`, tag `rust-v0.154.0-alpha.6.2`, avec les deux correctifs conservés dans `ops/` ; licence Apache 2.0 et notice amont dans `ops/codex-licenses/` | Exécutable du backend, si inclus dans l'archive avec son SHA ; aucun fichier d'authentification inclus |

Le bitmap de décor ne remplace pas la référence neutre envoyée à Luna A. Aucun asset tiers téléchargé séparément n'a été ajouté au jeu pendant cette réalisation. Les licences propres aux paquets Unity restent applicables à leurs composants ; ce document ne modifie pas leurs termes. Les droits de redistribution du dossier documentaire fourni restent à confirmer par le commanditaire avant publication publique.
