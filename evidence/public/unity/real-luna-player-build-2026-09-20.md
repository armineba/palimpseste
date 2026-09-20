# Player Windows avec rémanence du faisceau — 20 septembre 2026

Unity **6000.3.24f1** a construit un Player Windows x64 **IL2CPP/URP** depuis
les sources incluant la rémanence purement visuelle des faisceaux d'un tick.
Le journal privé `unity-build-luna-afterimage.log`, SHA-256
`b10b777fbf6e3352ee047f65c9c272f6f0d1b941045d6ee590a139aab48ccdaf`,
contient `Build Finished, Result: Success` et `PALIMPSESTE_BUILD_OK`.

Le build brut `game/Build/WindowsPlayerLunaAfterimage/` contient les dossiers
de symboles et sources IL2CPP que Unity nomme `ButDontShip`. Une copie jouable
sans ces dossiers est dans `game/Build/WindowsPlayerLunaAfterimagePlayable/` :
**29 fichiers, 122 037 097 octets**. L'exécutable fait **667 136 octets**,
SHA-256 `049f79454586f2ac5445f26b55191cf6611be62f10c4a5e12f92f806050149c2` ;
`GameAssembly.dll` fait **50 678 784 octets**, SHA-256
`978329f2a0e85e2a6abfbfc28268a92fe4256c9b25e0840b73ab9914f487b1f3`.
`StreamingAssets/service.json` contient `{"service_url":""}`.

Après le build, les tests PlayMode standards ont été relancés sur le moteur
modifié : **2 passés, 0 échec** (`FocusInterruptionTests` et
`SpellLabFixtureTests`). Les **2 sondes Luna opt in** ont été sautées faute
de chemin de paquet privé dans cet essai général. Le journal termine par
`Test run completed. Exiting with code 0 (Ok).` Preuves originales privées :
XML SHA-256 `d3061bd3acbc397084bdd909feae742100166b731a2f0a589ac0a8071b8adabf`,
journal SHA-256 `b50089ac60562ced3c8f617f8c3028b4cba3b58136b059c68a547571e4122ba8`.

La copie jouable a démarré localement **hors ligne**, cachée pour ce contrôle,
pendant 12 secondes : processus encore actif et répondant, puis arrêté par le
test. Le journal du Player, SHA-256
`fe7d19a15842bec3e1268452e77aa90e33a3ad00126196d77f9840b0b5330e9f`,
montre Unity 6000.3.24f1, Direct3D 12 sur NVIDIA GeForce RTX 3070 et PhysX,
sans exception relevée. Il contient le message D3D12
`failed to query info queue interface (0x80004002)` sans arrêt du lecteur.

Ce lancement ne démontre ni l'affichage du faisceau réel dans la caméra du
Player, ni l'accès au service, ni le parcours dessin → Luna depuis l'interface.
Le [test graphique hors écran](real-luna-afterimage-2026-09-20.md) démontre
séparément les pixels du paquet réel avec Direct3D 12.
