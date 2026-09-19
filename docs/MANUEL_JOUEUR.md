# Manuel court du lecteur Palimpseste

Extraire entièrement `deliverables/Palimpseste-Windows-x64-IL2CPP.zip`, puis lancer `Palimpseste.exe` dans le dossier extrait. Le lecteur propose la bibliothèque, l'atelier de dessin et la scène d'épreuve. Sur le profil Windows utilisé pour les essais de ce projet, des sorts de fixture manuelle sont déjà dans le cache local : ouvrir une ligne « Sort disponible », puis « Lancer dans le laboratoire ». Ces fixtures ne sont pas incluses dans le ZIP et ne sont pas des résultats Luna ; la bibliothèque d'un nouveau profil Windows sera vide.

Dans l'atelier, le premier contact de pinceau engage le parchemin. Les traits sont enregistrés localement avec le journal et le raster 1024 × 1024. La clôture soumet la capture à l'API privée si celle-ci est configurée. Le lecteur affiche l'état réel de la tâche ; une tâche en file n'est pas un sort publié. La création de sort réel attend actuellement l'activation du worker Luna dédié.

La bibliothèque relit les paquets sauvegardés et vérifie leurs versions et leurs empreintes. Les paquets de test peuvent être chargés hors ligne dans la scène d'épreuve pour lancer les porteurs et observer cibles, dégâts et statuts. Dans cette scène, viser puis cliquer pour lancer, maintenir le clic droit pour orbiter, utiliser la molette pour zoomer et « Remise à zéro » pour annuler les effets et remettre les cibles en état. « Bibliothèque » ramène aux parchemins. Le fichier `docs/UNITY_TEST_PROOF.md` distingue précisément ces essais de la recette du parcours Luna complet.

« Nouveau parchemin » exige une API de laboratoire en service, sa référence et un jeton joueur valide. Sans cette connexion, l'essai disponible immédiatement sur ce PC est le lancement hors ligne des sorts déjà en cache. La génération de nouveaux sorts par Luna reste bloquée tant que le compte Codex dédié du worker n'est pas authentifié et validé par le doctor actif.

Les informations de connexion du laboratoire sont privées. Les jetons ne font pas partie du ZIP ; consulter l'opérateur du laboratoire pour configurer une session. Ne pas placer de jeton dans les assets Unity ou dans le dépôt.
