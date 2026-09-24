# Fixture V2 isolée

Météore canonique déterministe créé par `tests/Palimpseste.BlueprintV2.Tests` pour vérifier schéma, compilation et runner Unity V2. Ce n'est ni un sort joueur, ni le résultat d'un appel Astra/Luna.

Le test écrit une copie uniquement dans `.runtime/v2-fixture-cache`. Il ne touche pas les parchemins existants et ne simule aucun verdict de fidélité visuelle.

`drawing.png` est une entrée synthétique dessinée par le développement pour le diagnostic opérateur ; ce n'est ni un dessin joueur, ni une image générée par un modèle. Une variante numérique de boucle est écrite dans `.runtime/v2-loop-fixture-cache` pour mesurer la fermeture des cycles. Aucun de ces caches n'entre dans la bibliothèque du jeu.
