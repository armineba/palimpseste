# Intégration du fournisseur · A et B

Ce document précise le branchement à réaliser dans le backend. Aucun appel réel n’est exécuté par le dossier livré. Les exemples de corps de requête sont des instructions de construction, pas des clés ou configurations déjà actives.

## 1 · Une clé côté serveur, aucun secret dans Unity

Le backend lit `OPENAI_API_KEY` depuis son gestionnaire de secrets ou son environnement. Unity appelle uniquement l’API du laboratoire avec son jeton utilisateur distinct. Le fournisseur de référence est l’API Responses d’OpenAI ; son endpoint documenté est `https://api.openai.com/v1/responses`. La documentation officielle est référencée aux sources W8, W9, W10 et W14 du cahier.

L’intégration implémente `IMultimodalInterpreter` pour A et `IDescriptionPlanner` pour B. Leur contrat de résultat distingue réponse complète, refus, contenu incomplet, erreur transitoire et erreur permanente. Le SDK éventuel et sa version sont verrouillés dans le dépôt après le test de branchement. Ne pas assimiler un HTTP 200 à un document métier valide.

## 2 · Corps de requête A

Construire le corps JSON suivant avec un sérialiseur ; ne pas effectuer de remplacement de chaînes à partir du dessin. Les mentions entre chevrons ci-dessous désignent des valeurs construites par le serveur.

```json
{
  "model": "<MODEL_A>",
  "store": false,
  "input": [
    {
      "role": "system",
      "content": [{"type": "input_text", "text": "<01_MODEL_A_INTERPRETE.md + layout + catalogue>"}]
    },
    {
      "role": "user",
      "content": [
        {"type": "input_text", "text": "Image 1 : référence du layout. Image 2 : dessin réel à interpréter."},
        {"type": "input_image", "image_url": "data:image/png;base64,<REFERENCE_PNG>"},
        {"type": "input_image", "image_url": "data:image/png;base64,<DRAWING_PNG>"}
      ]
    }
  ],
  "text": {"format": "<OBJET JSON model-a.response-format.json>"}
}
```

Dans le corps réel, `text.format` est **l’objet JSON chargé du fichier**, pas la chaîne montrée pour la lisibilité de cet exemple. Les images sont de vraies données PNG validées. Aucun identifiant de pinceau ne devient une consigne de dégâts ou de soin. Les guides régionaux restent dans le prompt et dans l’image de référence.

Le serveur calcule les hashes des images et du prompt avant l’appel. Il conserve les octets exacts de la première description acceptée techniquement. Les réparations structurelles de A restent limitées aux erreurs ; elles ne proposent pas plusieurs sorts au joueur.

## 3 · Corps de requête B

Employer la même structure avec `MODEL_B` et `model-b.response-format.json`, mais sans `input_image`. Le message système contient `02_MODEL_B_TRADUCTEUR.md`. Le message utilisateur contient un objet sérialisé composé de :

```text
source_description_sha256 : hash exact du document de A archivé
source_description        : document SpellDescription complet
authorized_catalog        : catalogue réellement compilable dans ce build
geometry_bank             : IDs, régions, rôles, mesures, propriétés disponibles
rules_profile             : bornes lab_v1 et unités
```

La banque contient les résultats du résolveur, pas un chemin de fichier libre à ouvrir par le modèle. Les masques eux-mêmes sont gérés par le compilateur et le runtime ; B peut choisir des références autorisées. B recopie l’empreinte fournie ; **il ne calcule pas un SHA-256 mentalement**.

L’étape B n’emploie pas de fonction permettant d’exécuter du code ou d’installer une mécanique. La sortie est validée selon le schéma, puis selon les clauses, relations, géométries et budgets. Une réponse non représentable est un incident technique à résoudre, jamais une publication avec des champs ignorés.

## 4 · Préflight obligatoire avant une série de générations

Vérifier successivement l’accès aux identifiants de modèles, un appel à deux images, une réponse A conforme, une réponse B conforme avec références locales de schéma, et la gestion d’un refus ou résultat incomplet. L’utilisation d’un schéma JSON localement correct ne prouve pas que le fournisseur accepte tous ses mots-clés dans ce mode de réponse. Corriger l’enveloppe de transport en conservant le contrat métier, puis enregistrer ce préflight dans le dépôt.

Ne pas injecter automatiquement des paramètres `temperature`, `seed`, `reasoning` ou des niveaux supposés compatibles. Leur disponibilité dépend du modèle et du mode choisis. Enregistrer uniquement ceux réellement pris en charge et testés. Le modèle recommandé est un candidat initial, pas une assurance d’accès avec le compte de l’équipe.

## 5 · Lecture et gestion du résultat

Parcourir les éléments de sortie typés plutôt que supposer que le premier élément est toujours du texte. Une sortie de refus ou de contenu incomplet ne passe pas dans le parseur de `SpellDescription`. Une sortie textuelle JSON est lue avec taille maximale, rejet des clés dupliquées, profondeur bornée et contrôle des valeurs finies.

Conserver le modèle retourné, l’identifiant de requête quand disponible, les usages facturables, les dates, la version de prompt et le hash de la tentative. N’inscrire aucune clé d’API dans les logs. Le client reçoit l’étape de travail et un identifiant de diagnostic ; les détails du fournisseur ne sont pas affichés comme une propriété du sort.

Le timeout de 120 secondes est une limite de travail proposée, non une promesse de latence. Une limite de sortie doit être dimensionnée après mesure des schémas ; une troncature est un incident distinct de l’indéchiffrabilité d’un dessin. Les compteurs de tentatives sont persistants et respectent le budget global du cahier.

## 6 · Mode concepteur isolé

`POST /v1/authoring/plan` accepte une `SpellDescription` structurée rédigée ou modifiée par un humain, accompagnée de références de géométries accessibles. Il n’appelle pas A. Il produit une tâche de diagnostic qui parcourt B, le compilateur et Unity.

Les géométries utilisées sont sélectionnées dans un dossier de diagnostic importé par l’outil d’administration ou dans une capture existante autorisée. Ne pas accepter de chemin absolu saisi dans un formulaire. Ce chemin ne crée pas un bouton « modifier » sur un parchemin joueur et ne convertit pas automatiquement une phrase libre en tous les champs structurés.
