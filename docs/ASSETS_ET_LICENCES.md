# Provenance des assets

| Élément | Provenance | Usage dans le projet |
|---|---|---|
| 16 textures dans `Resources/SourcedVfx` | Kenney Particle Pack et Smoke Particles, CC0, téléchargements officiels et empreintes dans `assets/sourced-vfx/catalogue.json` | Masques de particules et détails de surface sélectionnés par le constructeur D15 ; intégrés au Player 1.5.0 |
| `Resources/SourcedNoise/SimplexNoise3D.hlsl` et `Common.hlsl` | NoiseShader, Ashima/stegu/Keijiro, MIT, commit épinglé et licence conservée ; include de Common adapté | Bruit spatial réutilisé par le shader de construction pour la turbulence organique |
| `game/Assets/Palimpseste/Art/LibraryBackdrop.png` et sa copie dans `Resources/` | Image générée pour ce projet le 19 septembre 2026 | Décor peint de la bibliothèque ; aucune donnée de sort n'en dépend |
| `docs/art-direction/storm-bird-target.png` | Image réellement générée par l'outil de développement le 20 septembre 2026 ; prompt et empreintes dans les fichiers voisins | Cible artistique de développement, distincte des images produites par le service joueur et des captures Unity |
| Références visuelles des nouveaux sorts D13 | PNG généré par l'outil image natif Codex depuis la description figée ; provenance, dimensions et empreinte enregistrées avec chaque sort | Référence observée par le constructeur et affichable dans la fiche ; le labo exécute une composition 3D animée, sans afficher ce PNG sur un plan à la place du sort |
| Meshes, shaders, matériaux, audio procédural et UI dans `game/Assets/Palimpseste/` | Sources créées dans ce dépôt | Client et scène d'épreuve |
| `reference/` et `examples/` | Dossier documentaire fourni par le commanditaire | Référence neutre et fixtures illustratives |
| Paquets Unity, dont URP et Newtonsoft JSON | Installés par Unity Package Manager ; versions dans `game/Packages/` | Dépendances de compilation et du lecteur |
| Codex CLI modifié pour le service | Source officielle `openai/codex`, tag `rust-v0.154.0-alpha.6.2`, avec les deux correctifs conservés dans `ops/` ; licence Apache 2.0 et notice amont dans `ops/codex-licenses/` | Exécutable du backend, si inclus dans l'archive avec son SHA ; aucun fichier d'authentification inclus |

Le bitmap de décor ne remplace pas la référence neutre envoyée à Luna A. D15 ajoute les ressources gratuites ci-dessus, avec notices dans le Player et sources dans le dépôt. Voir [le relevé des sources](references-vfx-sources.md). Les licences propres aux paquets Unity restent applicables à leurs composants ; ce document ne modifie pas leurs termes. Les droits de redistribution du dossier documentaire fourni restent à confirmer par le commanditaire avant publication publique.
# Compositeur de planche D16

Le backend utilise `System.Drawing.Common 10.0.12` sous licence MIT, via NuGet, pour découper l’atlas natif et ajouter la grille/typographie de la planche. [Paquet officiel](https://www.nuget.org/packages/System.Drawing.Common/10.0.12). Sa [notice MIT](../backend/Palimpseste.Provider/ThirdPartyNotices/System.Drawing.Common-MIT.txt) est incluse dans chaque publication backend. Les polices sont fournies par Windows et ne sont pas redistribuées. Aucun achat de ressource ni utilisation d’un service payant supplémentaire.
