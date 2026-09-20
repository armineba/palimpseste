# Essai joueur réel : dessin → texte Luna → sort Unity → hors ligne

Essai effectué le 20 septembre 2026 sur le laboratoire **privé du propriétaire**.
Il ne vaut pas acceptation artistique du sort par le créateur ni ouverture du
service à plusieurs joueurs.

## Chaîne observée

Le dessin rouge-brun **courbe**, réalisé dans le Player puis reçu par l'API,
avait un job `production` en file. Le worker installé, SHA-256
`9243F9600973B1AAF5F97F07AD0983FF60A7EFA2EB0A7F7D42D142C05015847D`,
a tourné sous `PalRuntimeSvc` avec `codex exec`, `gpt-5.6-luna` et effort `max`.
Le contrôle local du Doctor installé, SHA-256
`AAAAB2686CD9A33ADB6130B205D10596A63AF8207CBAE580ACD723C862196811`,
a rapporté `dedicated_auth=authenticated` et **aucun** `production_issues` après
installation du manifeste composite v2 SHA-256
`56F349016A5B63D352DD37225AF9253889D496C2399F58F97F70D0DAAC353E3F`.
Son rapport privé local a pour SHA-256
`4FAF8EE53CA5DA823A53038D7DB3407FBA3349CAE9A164EA11C52B59ECA195AE`.

Le même job est passé par `interpreting`, `planning`, puis `ready`.
`ops/status-lab.ps1` a constaté **deux** `provider_attempts|success`, une
`description`, un `plan`, un `compiled_spell`, deux artefacts de géométrie et un
masque, sans bail expiré. Le Player a affiché la lecture A : **« Faisceau courbe
de lave »** ; son résumé décrit une émission unidirectionnelle rouge-brun en
faisceau incurvé, sans effet de contact identifiable.
[Capture du texte dans le jeu](owner-player-interpretation-2026-09-20.png),
SHA-256 `4B57F53380098F505F5750D11F38EDBA2A0E2382D4959A6383E47202E835FE99`.

Le Player a téléchargé et vérifié `spell.json`, SHA-256
`2BBD39846D64FA342F28B5D34CE0B597C1DD57307BF26DE9133A1426BA14FB72` ;
son `spell.json.sha256` local correspond. Le paquet lie la description SHA-256
`CF1C9CE9FFAD203AA9E76F2281DD15EB3CFAA3432BC52E1A176D2651ECA3B2DC`,
un nœud `beam`, zéro effet mécanique et une durée de **1 tick**. Il ne contient
pas de C# généré. La fiche « Faisceau courbe de lave » s'est ouverte, puis le
laboratoire. Un clic y a donné `Lancers : 1`, `Dégâts : 0`. La trace de lave
incurvée est [visible dans la caméra du Player](owner-player-lava-beam-2026-09-20.png),
SHA-256 `84B92C5FAA0DF7390D5D5A24EFC9FD99E51F901457D95FC99D7D111358011143` ;
elle [disparaît après l'écho visuel](owner-player-beam-cleanup-2026-09-20.png),
SHA-256 `31B635E9CFDD462BC41045A334B54C02EAF7D80DB6F89DE816D70F36CD99782A`.
L'écho dure au moins `0,6 s` pour rendre le sort perceptible ; le porteur
logique et ses effets expirent toujours après un tick.

## Tests et build

Le test PlayMode Direct3D12 a utilisé une **copie exacte du paquet téléchargé
par ce joueur** (SHA-256 ci-dessus), sa description et ses trois artefacts.
Unity a produit `playmode.xml` avec **2 tests passés, 0 échec, 0 ignoré** :
trajectoire issue de l'encre, pixels rouges après expiration logique, disparition
après `0,7 s`, absence de dégâts et création d'une source sonore procédurale.
Le rapport XML compte `128` points du chemin source, `119` positions sur la
ligne affichée et `1 377` pixels rouges sous Direct3D12, puis zéro après
nettoyage.
XML privé SHA-256
`B2F25BBFA35F9DC115ADDB4258FA186001572358F6F10AA2CDE5690352D1E5A2`,
log privé SHA-256
`29A1EAD872BE90342009D51FF23A03C639AE413EBB378B9E2B490406F26DD10C`.
Le son a été vérifié comme source et échantillons dans Unity ; aucune écoute
humaine n'est attestée.

Unity 6000.3.24f1 a construit le Player Windows x64 IL2CPP/URP avec code de
sortie **0** et `PALIMPSESTE_BUILD_OK` dans un log privé SHA-256
`B4CFDA4F7880B9C002F2CBC8A431F0867F9D30CFEBF2E42BFE326CCBBAE42C45`.
La copie jouable `game/Build/WindowsPlayerBeamVisibleReady/` contient **29**
fichiers, **122 093 149** octets ; ses 29 hashes correspondent au build brut.
`Palimpseste.exe` SHA-256
`049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2` ;
`GameAssembly.dll` SHA-256
`9FB651D09680CAF09B0A8668BEF2A914EFDE6B61F930EDBAE1CE90AF3B5C18C9`.
Le Player mis à jour a été lancé en fenêtre visible et a répondu.

## Reprise réellement hors ligne

Après fermeture du Player, l'API locale a été arrêtée ; aucun processus
`Palimpseste.Api.exe` ne tournait. Le même build a redémarré (PID `35804`) et
montré la [bibliothèque hors ligne](owner-player-offline-library-2026-09-20.png),
SHA-256 `D3BD7DBE50C99BCDAB5D461A8848BCE57DB708BBB65235CBCAF8ED446ACE3A16`.
Depuis « Sort disponible », la fiche, le laboratoire et un nouveau lancer ont
fonctionné **sans API** ; le [faisceau a été capturé hors ligne](owner-player-offline-lava-beam-2026-09-20.png),
SHA-256 `E92A45E0568DB9DF274C8B2A0E95D2E1B1FD12EE2FE1A666C0F370BB7429CC63`.
Le compteur indiquait `Lancers : 1`, `Dégâts : 0`. L'API a ensuite été relancée
sous `PalRuntimeSvc` (PID `16420`) et `/health/ready` a répondu **200** ; le
worker (PID `8180`) restait actif. Le service reste sur `127.0.0.1:18080` en
mode `private_lab`.
Une dernière commande de confort destinée à fermer puis relancer le Player
après restauration de l'API a été refusée **avant exécution** par la revue
automatique (`blocked by policy`, sans motif détaillé). Aucun nouvel essai
équivalent n'a été fait. Le Player hors ligne (PID `35804`) restait répondant,
l'API (PID `16420`) répondait 200 et le worker (PID `8180`) restait actif au
dernier contrôle.

## Verdicts réservés

Le créateur n'a pas encore accepté la fidélité de cette lecture A ni la qualité
du sort dans la scène. Aucune écoute humaine du son n'a été consignée. Aucun
déploiement public multi-joueur ni essai sur un second PC n'est attesté.
