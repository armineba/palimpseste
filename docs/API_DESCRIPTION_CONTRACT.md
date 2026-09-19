# Contrat API de la description A

Ce contrat décrit la frontière entre la génération du worker et le client
joueur. Aucun appel Codex ou Luna n'est effectué par l'API.

## Job

`GET /v1/jobs/{id}` est authentifié par le bearer du propriétaire et renvoie
toujours la propriété `description_artifact_id`.

| Situation | Valeur |
|---|---|
| Avant la validation et la persistance de A | `null` |
| Après la transaction qui crée `interpretations` | `a` suivi de 32 chiffres hexadécimaux minuscules |
| États suivants | `resolving_geometry`, `planning`, `validating`, `waiting_retry`, `needs_operator`, `ready` |

L'identifiant est lu par une jointure entre `jobs`, `interpretations` et un
artefact `kind=description`, `owner_id` identique au job et
`content_type=application/json`. Il reste donc absent tant que la description
validée n'est pas liée durablement. Le texte, le prompt, les entrées d'image
et les sorties brutes de fournisseur ne sont pas inclus dans le Job DTO.

## Téléchargement

Le propriétaire demande `GET /v1/artifacts/{description_artifact_id}` avec son
bearer. L'API vérifie l'`owner_id`, lit les octets du stockage contrôlé,
recalcule SHA-256 et renvoie la valeur enregistrée dans
`X-Content-SHA256`. Toute divergence est une erreur `503 artifact_corrupted`.
Un autre principal reçoit `404`; une requête sans bearer reçoit `401`.

Le contenu attendu est un document JSON `sp.description/1.0`. Ce schéma ne
porte pas de `parchment_id` : le client associe le document au
`parchment_id` renvoyé dans le Job DTO et refuse une association incohérente.
Ce contrôle client s'ajoute au lien serveur et ne transforme pas une fixture
en appel Luna réel.

## Bootstrap joueur

`POST /v1/session/redeem` est la seule route sans bearer. Elle accepte
`{"invitation_code":"<64 caractères base64url>"}` et renvoie un jeton opaque
ainsi que `principal_id`. Le code est à usage unique, expire après 24 heures et
la réponse porte `Cache-Control: no-store`. Cette invitation ne contient ni
clé API, ni identifiant Codex, ni droit de lecture du serveur. Le compte
Codex partagé reste exclusivement dans l'environnement du worker.

`GET /v1/capabilities` renvoie aussi le `principal_id` du bearer authentifié
(GUID `N`, 32 caractères hexadécimaux minuscules). Le client s'en sert pour
associer les parchemins et descriptions en cache au bon joueur sur un profil
Windows partagé. Un cache sans propriétaire connu reste masqué.
