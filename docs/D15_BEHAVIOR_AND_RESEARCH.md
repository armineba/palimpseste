# D15 — intention, comportement et ressources visuelles

## Demande et diagnostic

Le créateur demande une correction pour les prochains parchemins, sans retoucher les sorts déjà générés. L'ancien `swirl` était une oscillation sinusoïdale de ±16° au maximum ; le choix `caster`/`aim_point` était laissé au plan B sans intention structurée dans A. Une belle image statique ne vérifiait donc ni rotation continue ni déploiement du porteur.

## Chaîne prévue pour les nouveaux sorts

1. A décrit chaque sujet du lancement à la disparition et déclare son origine, attachement, orientation, phénomène et voyage.
2. G produit l'image cible à partir de cette description.
3. Une action fixe du serveur recherche les références pertinentes dans un index de sources primaires vérifiées, consulte en parallèle quatre pages publiques et propose les textures CC0 importées. Aucun achat ni appel modèle supplémentaire pour cette recherche. Une panne réseau conserve explicitement son statut et utilise les notes revues et ressources embarquées.
4. B reçoit la description, l'image réelle et ce dossier de recherche. Il choisit les ressources disponibles, copie les intentions et règle des paramètres bornés en unités explicites.
5. Le compilateur vérifie l'égalité A/B, la compatibilité porteur/origine/trajectoire, les paramètres non applicables, les minima de rotation d'un vortex et le hash du dossier de recherche.
6. Unity applique placement, mouvement continu et trajectoire. Dream-loop Pro compare ensuite la cible à des captures réelles de plusieurs instants et phases, puis affine les données visuelles dans le budget existant.

Le déplacement d'un porteur et les forces de gameplay sont distincts de l'écoulement de ses couches visuelles. Une spirale lumineuse ne donne aucun dégât ni attraction non déclarés. Les contrôleurs sont des approximations stylisées bornées, pas un solveur général de mécanique des fluides.

## Recherche et réutilisation

Voir [les sources et licences](references-vfx-sources.md). L'index comprend la documentation Unity 6.3 relative aux vitesses orbitales/radiales, au bruit, aux traînées, aux sous-émetteurs, aux matières URP et au bloom. Les ressources Kenney offrent fumée, éclairs, couronnes, flammes, traces et étoiles.

La recherche par sort parcourt une bibliothèque primaire sélectionnée, puis lit les pages choisies ; ce n'est pas une exploration générale d'Internet. B fait la sélection finale en regardant l'image cible. L'ajout de nouvelles bibliothèques ou de plugins passe par le développement, une lecture de licence et une nouvelle livraison du moteur. Le worker joueur n'installe ni code ni plugin.

Le dossier `spell_reference_research` est figé par job, lié aux SHA de description et d'image et conservé lors des reprises et raffinements. Le plan reprend son SHA. Les statuts de consultation et les empreintes des pages lues sont enregistrés séparément des notes déjà revues ; une lecture en ligne n'est jamais inventée.

## Compatibilité et validation humaine

Migration 010 : version de pipeline 3 par défaut uniquement pour les futurs jobs ; aucune réécriture des documents ou des paquets existants. Les nouveaux profils nécessitent le Player 1.5.0. Les archives sans profil conservent leur comportement historique.

Le créateur demande à essayer lui-même : aucune campagne de tests, génération de diagnostic, capture indépendante ou partie jouée par l'agent. Les compilations et l'installation seront consignées avec leur résultat réel. Les captures automatiques de la génération joueur appartiennent au parcours Pro demandé ; elles ne prouvent pas les collisions ni l'acceptation visuelle humaine.

## Livraison et point de reprise

- Recherche, ressources, contrats et moteur intégrés. Deux compilations Unity ont réussi ; la dernière inclut les impacts monde déclenchant les enfants déclarés. Backend final compilé et empaqueté.
- Player 1.5.0 livré, raccourci actualisé, service installé avec migration 010 et renderer précompilé protégé. [Preuves réelles](../evidence/public/backend/behavior-d15.json).
- Aucun essai joueur ou appel modèle D15 lancé par le développement. Acceptation gameplay, adéquation de l'interprétation, fluidité, durée de génération et fidélité artistique : à faire par le créateur sur un nouveau parchemin.
- Le parcours de diagnostic manuel a été adapté à la recherche et à ses reprises, mais n'a pas été exécuté.
- Les travaux antérieurs sans description conservent A 2.3, son schéma et son catalogue depuis des snapshots du commit 3f57ee7. Aucune mise à niveau implicite d'un job ancien.
