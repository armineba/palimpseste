# Backlog de réalisation · SP-1.0

**État initial de tous les travaux : à réaliser.** Les schémas du dossier sont des spécifications ; ils ne signifient pas qu’un module ci-dessous existe déjà. Les portes humaines sont des points de revue de l’équipe, pas des autorisations de considérer un sous-ensemble comme version finale.

| Ticket | Lot | Réalisation attendue | Preuve de sortie |
|---|---|---|---|
| B01 | M0 | Créer dépôt Unity 6.3 LTS, API et bibliothèques partagées ; verrouiller versions. | Build Windows IL2CPP vierge, versions et scripts de build. |
| B02 | M0 | Générer/écrire DTO C# compatibles Unity depuis les contrats. | Aller-retour JSON sans perte sur Editor et build. |
| B03 | M0 | Portes de qualité : corpus privé, dossiers de preuve et action de revue humaine. | Cas et reviewers enregistrés ; aucune approbation automatique. |
| B04 | M1 | Définir layout, transformations pointeur, zoom, palettes et trois traces. | Traces comparables à différentes fréquences d’images. |
| B05 | M1 | Raster canonique, masque d’encre et journal durable. | Empreintes reproductibles et rejeu du journal. |
| B06 | M1 | Encre, verrouillage par région et finalisation sans confirmation. | Tests limites de trait, régions traversées, fermeture partielle. |
| B07 | M1 | Perte de focus, arrêt brutal, reprise locale et budget d’encre. | Incident enregistré ; pas de retour à un support vierge. |
| B08 | M2 | API de capture, authentification privée, stockage et clés d’idempotence. | Double upload sans double tâche ni remplacement du dessin. |
| B09 | M2 | Worker durable, leases, heartbeats, fencing et reprise des tâches. | Redémarrage forcé sans double publication. |
| B10 | M2 | Connecter A à deux vraies images ; gérer toutes les issues fournisseur. | Réponses brutes, usages et descriptions réelles archivées. |
| B11 | M2 | Afficher interprétation intermédiaire et vrais états de travail. | Interface sans faux pourcentage ni choix de reroll. |
| B12 | M3 | Implémenter masques régionaux, chemins, empreintes et silhouettes. | Tests trous, composantes, traits ouverts/fermés et cas dégénérés. |
| B13 | M3 | Connecter B à la description figée et à la banque géométrique. | Descriptions indépendantes de A traduites en plans valides. |
| B14 | M3 | Implémenter traçabilité des clauses, sujets, relations et géométries. | Tests où B ajoute soin, change cible ou perd un enfant, tous bloqués. |
| B15 | M3 | Compilateur de budgets et graphe acyclique ; génération de la fiche factuelle. | Borne pessimiste incluant les statuts résiduels et tests négatifs. |
| B16 | M3 | Paquet immuable, manifestes JSON/PNG, cache et contrôle de versions. | Un octet modifié est détecté ; aucun chargement partiel. |
| B17 | M4 | Horloge, scheduler d’événements, admission des casts et pooling. | Arrêts, reprises, saturation et annulation sans objets orphelins. |
| B18 | M4 | Projectile droit/courbe/guidé, collisions balayées, rebond et pénétration. | Mur fin, cibles multiples, double collider et filtre de contact. |
| B19 | M4 | Faisceau entretenu, occlusion et chaîne sans revisite. | Ordre stable et cadence mesurée dans le build. |
| B20 | M4 | Champ et onde avec empreintes exactes. | Une cible dans un trou ne reçoit rien ; front rapide ne saute pas une cible. |
| B21 | M4 | Barrière solide et piège armé/réarmé. | Structure, blocages, expiration, cible déjà dans le piège. |
| B22 | M4 | Dégâts, soins, impulsion, brûlure, Mouillé et ralentissement. | Mesures, cumul non additif et réactions conformes. |
| B23 | M4 | Scène d’épreuve, récepteurs et contrôles de visée. | Tous les scénarios reproductibles sans système de quête. |
| B24 | M5 | Renderers des six porteurs, quatre matières et géométrie du dessin. | Aucun rendu générique universel ; superposition collision/rendu. |
| B25 | M5 | Audio, impacts, extinction et signature persistante du parchemin. | Rechargement identique et revue humaine de paires de dessins. |
| B26 | M5 | Interface française terminée, focus, clavier et adaptation des tailles. | Parcours complet sans l’éditeur, sur résolutions cibles. |
| B27 | M6 | Déploiement privé, secrets, sauvegarde, restauration et métriques. | Restauration à blanc et rapport de panne fournisseur. |
| B28 | M6 | Performance et endurance sur matériel nommé. | Captures profiler, saturation contrôlée et mémoire stable. |
| B29 | M7 | Corpus final inédit, comparaisons humaines et compilation autonome. | Preuves de dessin → vrai modèle → sort réel pour chaque famille. |
| B30 | M7 | Livraison, licence des assets, manuel et dossier de recette. | Installation sur machine propre et validation humaine signée. |

## Dépendances qui ne doivent pas être contournées

B10 n’attend pas le rendu final, mais n’autorise pas un résultat feint. B13 peut démarrer sur des descriptions manuelles de diagnostic, mais B29 exige de nouvelles images réelles. B18 à B22 peuvent utiliser des fixtures isolées pour tester la physique ; ces tests ne constituent pas la preuve que A comprend un dessin.

B24 et B25 ne peuvent pas se terminer avant que les données géométriques et les événements physiques soient stables. B29 ne peut pas être approuvé par le développeur automatique. B30 n’est pas un simple ZIP de sources : la livraison logicielle finale comprendra le build autonome et la procédure de déploiement décrits au chapitre 23.
