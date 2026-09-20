# Bascule Codex et diagnostic Luna A/B réels

Date : 20 septembre 2026. Vérifications effectuées sous `PalRuntimeSvc` avec
son profil Codex ChatGPT dédié. Le worker joueur est resté arrêté. La preuve
complète, les images, les prompts, les sorties JSON et les journaux de processus
restent dans le runtime privé ; ce relevé public contient les résultats utiles
sans copier ces données.

## Binaires et accès

Le CLI reconstruit à partir du tag officiel `rust-v0.154.0-alpha.6.2`, commit
`b5bffd3ec4db487e7e3dec59663875b0ef7b72ca`, avec
`ops/codex-attestation.patch` a été installé dans `E:\PalimpsesteRuntime\bin`.
Son SHA-256 vivant est
`8AA8BF5CC27C55331C29C1050CD666174076E3C83D9AE84C6AE2BD54D9C7A72D`.
Le `ProviderDoctor.exe` vivant a pour SHA-256
`4135407E2366FACC20F5528AE4ED11733DF1F54D0486121CEDF4DD98F1550E6E`.
Le dossier de sauvegarde privé `cutover-2026-09-20-01` conserve les anciens
binaires et leurs SDDL : CLI SHA-256 `2271526227B06CA13AB2B975B88546460FC61B2A29225B6DDA0FDC803024CCC9`,
doctor SHA-256 `3BE177C2FE89C60C77C3241F8688870805669C5D3B56FDCF2A1E9D481CCEB822`.
Sous le compte de service, les huit vérifications d'accès sur les deux
exécutables ont autorisé lecture et exécution et refusé écriture et suppression.

Le doctor local a quitté avec le code 0 sans appel modèle. La preuve locale
revue a été copiée, octet pour octet, sous `approved-evidence` avec SHA-256
`DAD87BC114E80BEE70848C1C4FFDF83E042456554252AAE8ED53291278C1F3FE`.
Les trois clés de compatibilité des fonctionnalités du `runtime.env` privé
référencent cette copie. Une deuxième exécution locale a quitté avec le code 0,
sans appel modèle, avec pour seule réserve de production
`effort_not_verified` ; preuve SHA-256
`18DD794B55280743B818ED693CD3FDA54450454CD3687A2793D072928F0C2576`.

## Une sonde active A puis B

Le lanceur de service a effectué **une** sonde active avec les images d'un vrai
dessin Unity et une géométrie de diagnostic provisoire. Il a quitté avec le
code 0. La preuve privée `doctor-active-787d4c98e9ea443485755d8de2ee8999.json`
a pour SHA-256
`FD39E1220BF540C543184D56E3F3314FF0E2BF440BB65073EF219B031CD3E6F5`.
Elle rapporte `active_result=success`, `model_calls_executed=true`, aucun
`active_test_issues`, et le SHA-256 du CLI vivant ci-dessus. A et B ont chacun
quitté avec le code 0 et une sortie JSON conforme :

| Étape | Modèle et effort rapportés | SHA-256 de la sortie JSON | Jetons entrée / sortie, dont raisonnement |
| --- | --- | --- | --- |
| A multimodale | `gpt-5.6-luna` / `max` | `83C173816320C022A6C02A84C8DED3398F0B75987909D19C98F6E95EB6B36CCC` | 13 662 / 5 145, dont 4 751 |
| B traductrice | `gpt-5.6-luna` / `max` | `8EEA9FFCADCF3EC75DE8CFB66F6CA8712D3A1D6FE70A076293C38E1C5404A2FD` | 13 993 / 5 026, dont 4 788 |

A décrit un trait diagonal continu dans la couronne, lu comme un faisceau
rectiligne de teinte évoquant le feu. A ne relève ni effet, ni cible, ni
déclenchement additionnel. B conserve cette retenue : un faisceau visuel de
portée 1 000 cm, largeur 100 cm, durée 1 tick, avec affinité feu et
`effects: []`. Aucune action de dégâts n'est donc revendiquée pour ce dessin.

## Vérification du compilateur contrôlé

Les octets de A et B ci-dessus et l'encre réelle du dessin Unity (SHA-256
`18CDA98D1EF7E2A6F2148CFB81998D0B4494EA1E46BC288477459E33F1482AFC`)
ont été passés à `dotnet run --project tests/Palimpseste.Core.RealProbe --
<description.json> <plan.json> <ink.png>`, sans nouvel appel modèle. La
commande a quitté avec le code 0. `GeometryResolver` a produit
`full.silhouette.0` et `ring.path.0`, avec un masque. La validation de B et
`SpellCompiler.Compile` ont accepté le plan. Le paquet de vérification en
mémoire a le SHA-256
`856D1B37BBE41C6372B0319112053DFDCC9ADD6D8F1925AB2E5214336AE19C37B` ;
ses bornes rapportées sont une instance, un tick et zéro application d'effet.

La première validation de B avait rejeté `clause_trace` pour une clause
visuelle et `clause_event` pour l'événement de création du porteur sans effet.
Les règles génériques ont été corrigées pour autoriser ces faits tout en
contrôlant davantage les relations, les clauses visuelles et les filtres de
cible du porteur. Les régressions `Palimpseste.Core.Smoke` passent. Le paquet
de cette sonde porte volontairement des identifiants d'artefacts synthétiques
et une provenance `fixture` : il n'a été ni enregistré comme sort joueur, ni
publié, ni téléchargé et chargé dans Unity.
Le worker .NET contenant le compilateur corrigé a été republié en staging
opérateur (76 223 015 octets, SHA-256
`D04A81E54EF0FBFE8FFFCE6A121DABA1DCE0FB2C16F7B163CCF3EA1EA0E08CAA`) ;
il n'a été ni installé ni démarré. Sa preuve de publication est dans
`evidence/public/backend/worker-compiler-publish-2026-09-20.md`.

## Chargement Unity isolé

Le même paquet de vérification SHA-256
`856D1B37BBE41C6372B0319112053DFDCC9ADD6D8F1925AB2E5214336AE19C37B`
a été chargé dans `SpellLab` lors d'un test Unity 6000.3.24f1 PlayMode en batch
`-nographics` : **1 test sur 1 passé**, sortie du journal avec code 0. Le test
a observé `Ready` et `Cast=true`, un `LineRenderer` de deux points avec teinte
feu et longueur positive, la création d'un `AudioSource`, zéro dégât puis
l'expiration après un tick. La preuve technique détaillée figure dans
`evidence/public/unity/real-luna-playmode-2026-09-20.md`. Pour résoudre le
risque qu'un sort d'un tick expire avant sa première image, la présentation
conserve l'objet graphique seul pendant une occasion de rendu et 0,16 seconde,
sans porteur actif, collider ni audio persistant. Les tests corrigés passent
en PlayMode `-nographics` 1/1. Un contrôle Direct3D 12 séparé sur caméra et
`RenderTexture` isolées a mesuré 13 616 pixels rouges après expiration logique,
puis zéro après nettoyage ; sa capture et la preuve sont dans
`evidence/public/unity/real-luna-afterimage-2026-09-20.md`. Cette mesure ne
prouve pas l'affichage depuis la caméra du Player ou un son écouté.
Un Player Windows IL2CPP/URP contenant cette rémanence a ensuite été construit
et lancé hors ligne 12 s, répondant ; voir
`evidence/public/unity/real-luna-player-build-2026-09-20.md`. Son
`service.json` est vide et il n'a pas reçu ce paquet par le parcours joueur.

La sonde prouve l'appel A/B et l'attestation du modèle et de l'effort pour ce
diagnostic ; la vérification séparée prouve la compilation contrôlée de ces
octets en mémoire et leur exécution technique isolée dans Unity PlayMode.
Elle ne prouve pas un job de joueur traité, un lancement dans le Player IL2CPP,
une image visible depuis sa caméra, un son entendu, ni la fidélité de la lecture
approuvée par un humain. La géométrie
du diagnostic a été recalculée à partir de A dans la vérification du
compilateur ; le worker de production doit encore suivre ce chemin.
La preuve active demeure dans `pending` : elle n'est pas encore approuvée et
`PALIMPSESTE_EFFORT_VERIFIED` reste `false`. API, worker et HTTPS publics ne
sont pas déployés ; le lecteur Unity actuel n'a pas été testé connecté à ces
sorties. Aucun achat ni recharge n'a été déclenché par la sonde. Son usage est
compté par le compte Codex existant, sans preuve ici de son solde ou de l'état
de recharge automatique dans l'interface de facturation.
