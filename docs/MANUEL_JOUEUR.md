# Manuel court du lecteur Palimpseste

Lancer l'exécutable extrait de `deliverables/Palimpseste-Windows-x64-IL2CPP.zip`. Le lecteur propose la bibliothèque, l'atelier de dessin et la scène d'épreuve. Les exemples embarqués servent aux tests hors ligne du moteur ; ils ne sont pas des résultats Luna.

Dans l'atelier, le premier contact de pinceau engage le parchemin. Les traits sont enregistrés localement avec le journal et le raster 1024 × 1024. La clôture soumet la capture à l'API privée si celle-ci est configurée. Le lecteur affiche l'état réel de la tâche ; une tâche en file n'est pas un sort publié. La création de sort réel attend actuellement l'activation du worker Luna dédié.

La bibliothèque relit les paquets sauvegardés et vérifie leurs versions et leurs empreintes. Les paquets de test peuvent être chargés hors ligne dans la scène d'épreuve pour lancer les porteurs et observer cibles, dégâts et statuts. Dans cette scène, viser puis cliquer pour lancer, maintenir le clic droit pour orbiter, utiliser la molette pour zoomer et « Remise à zéro » pour annuler les effets et remettre les cibles en état. « Bibliothèque » ramène aux parchemins. Le fichier `docs/UNITY_TEST_PROOF.md` distingue précisément ces essais de la recette du parcours Luna complet.

Les informations de connexion du laboratoire sont privées. Les jetons ne font pas partie du ZIP ; consulter l'opérateur du laboratoire pour configurer une session. Ne pas placer de jeton dans les assets Unity ou dans le dépôt.
