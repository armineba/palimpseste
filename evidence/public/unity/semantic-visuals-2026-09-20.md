# Rendu sémantique 3D et build Windows final — 20 septembre 2026

## Build final après correction d'émission

Le matériau `Assets/Palimpseste/Resources/LabEmissive.mat` a été ajouté pour conserver l'émission dans le Player IL2CPP ; le [journal final expurgé](semantic-visuals/build-windows-release.log) montre cet asset dans la liste de build (ligne 5631 de l'original), `Build Finished, Result: Success` (ligne 7733), `PALIMPSESTE_BUILD_OK` (ligne 7746) et `Application will terminate with return code 0` (ligne 7772). Unity 6000.3.24f1 a construit `game/Build/WindowsSemanticVfxRelease/`. Le journal original `game/Logs/semantic-build-windows-release.log` a pour SHA-256 `57C28DBCA2D7C69F9087571CDD662D7833DAEED289C15B877055AEA52567FDB4` ; sa copie publique expurgée, `3339A10BE1130D0101BAC514905060C5B15002AD6E7CAD3ABE1579580A91F8FF`.

Le [dossier jouable final](../../../game/Build/WindowsSemanticVfxPlayable/Palimpseste.exe) contient **29 fichiers, 122 350 769 octets** ; chaque fichier correspond par SHA-256 au build brut, avec **0 écart**, après exclusion des deux dossiers `DoNotShip`. `Palimpseste.exe` a pour SHA-256 `049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2` ; `GameAssembly.dll`, `59124E2AE6463AA29499EE1043AAD816E0D8ACAFDBB2D85F13634F7A1E73B6C1`.

Le [ZIP Player final](../../../deliverables/Palimpseste-Windows-x64-IL2CPP.zip) fait **44 157 438 octets**, SHA-256 `4904A3F42D52D14683D7577C3A05829E49317B1C12572093DACE6339326464E6`. Ses 29 entrées de fichiers totalisent 122 350 769 octets non compressés ; leurs empreintes ont été comparées au dossier jouable avec **0 écart** et aucun chemin `DoNotShip`. Ce contrôle porte sur la structure et les octets livrés ; le lancement du nouveau Player et l'acceptation visuelle sont des preuves séparées.

Le Player final a été lancé sur ce PC à **16:53:38 (Paris)**, PID `26864` : processus `Palimpseste`, fenêtre `Palimpseste Spell Lab`, `Responding=True`, handle `1772348`, observés par `Get-Process`. Son journal privé contient `PALIMPSESTE_CREDENTIAL_READ_OK` et aucun match `Exception`/`Error` au contrôle effectué. Aucun clic, nouveau dessin, téléchargement de paquet D09 ou lancement de sort dans ce Player n'a été réalisé pour cette preuve ; son démarrage ne vaut pas acceptation visuelle.

## Portée

La décision [D09](../../../docs/DECISIONS.md) demande que l'objet interprété par Astra soit construit en 3D dans le laboratoire, sans recopier automatiquement le contour de l'encre. Le Player embarque 21 formes `visual_form` contrôlées et conserve la lecture des anciens paquets. Cette preuve sépare les tests Unity sur fixtures, le build livrable et le diagnostic fournisseur réel.

## Tests Unity

Le [rapport PlayMode expurgé](semantic-visuals/playmode.xml) indique **5 passés, 0 échec, 0 ignoré** : étendue physique d'une barrière, volumes bornés et libération des 21 formes, projectile sémantique indépendant de l'ancienne signature d'encre, extinction de l'impact, puis capture des objets pour revue. Le XML original `game/Logs/semantic-playmode-final.xml` a pour SHA-256 `4AC0BCF958F306551E50292FB57F840E625FD432BC2804AC6DE151321CE63F5A` ; le [XML public](semantic-visuals/playmode.xml), `2E0DC198AA3EE6A5F277159A4240A20C31EB487E5407472C163B2FB117C78826`. Le [journal PlayMode public](semantic-visuals/playmode.log) a pour SHA-256 `161CCE216D7144ACF22D9BDDFC15DD92EF2D191297C33AAC2CBB0B7CA94C3308`.

Le [rapport EditMode de cache expurgé](semantic-visuals/cache-editmode.xml) indique **1 passé, 0 échec, 0 ignoré** : un paquet complet s'ouvre hors ligne, un paquet partiel ou altéré est rejeté. Le [journal public](semantic-visuals/cache-editmode.log) et le XML public ont pour SHA-256 respectifs `5B353FAFFA2113BD68879DA5C6CF1B9440D2781E9BCE5960AAEB03BDFB5CA04C` et `25A2D560C308D8C17A0C005894CFE34A46D6298133F789E093980425DAFC12F4`.

La [capture de démonstration](semantic-forms-preview-20260920.png) a pour SHA-256 `E288BA50DE3AFBA7A6FAB6FD73A25551F08C0620E21BD390265056CE515EF56D`. Elle provient d'une fixture de test Unity et a été inspectée par les agents ; elle n'est pas un sort généré par le dessin d'un joueur et ne remplace pas un verdict visuel du créateur.

Les copies publiques des journaux et XML ont été produites avec la fonction `sanitize` de [`ops/sanitize-unity-evidence.py`](../../../ops/sanitize-unity-evidence.py). Pour chaque fichier, les lignes de résultats ont été comparées avant/après, les motifs d'identité connus ont été contrôlés et les XML expurgés ont été reparsés. Les originaux restent dans `game/Logs/`, ignoré par Git.

## Historique du premier build provisoire

Après le premier build réussi, une revue avait identifié un risque de retrait du shader d'émission par IL2CPP car aucun matériau d'émission n'était explicitement retenu dans les assets. Sa copie jouable est conservée sous `game/Build/WindowsSemanticVfxPlayableProvisional/` et son ZIP hors dépôt. Les empreintes qui suivent décrivent exclusivement cette première version.

Unity 6000.3.24f1 a terminé le build Windows IL2CPP/URP `game/Build/WindowsSemanticVfxReady/` avec `Build Finished, Result: Success`, `PALIMPSESTE_BUILD_OK` et `Application will terminate with return code 0`. Le journal original `game/Logs/semantic-build-windows.log` a pour SHA-256 `C5E38C5137D30FB1444C391D52E40D1DC453FE8CC57D0F87328E80FAA5C8B515` ; le [journal public expurgé](semantic-visuals/build-windows.log), `F4C4C6EFDABAB3FAFA699479366934D3456010A5B3A9B3EF0B3CD38F984791DD`.

La copie jouable provisoire `game/Build/WindowsSemanticVfxPlayableProvisional/` a été créée sans les deux dossiers `DoNotShip`. Elle contient **29 fichiers, 122 255 113 octets** ; tous correspondaient à leur original par SHA-256. L'EXE a pour SHA-256 `049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2` ; `GameAssembly.dll`, `37BBEE31C6617D9CDEA10BFE2129E4B6A9C14392978D07BACEFE52F5EE96CD96`.

Le ZIP de cette copie provisoire faisait **44 104 244 octets**, SHA-256 `F6CA514233DC697646959A7EE1AF50E23D36F39075419AB61F30EFFE2BBAE1FC`. Ses 29 entrées de fichiers totalisaient 122 255 113 octets non compressés ; chaque entrée a été comparée par SHA-256 au dossier jouable, avec **0 écart** et aucun chemin `DoNotShip`. Il est conservé hors dépôt dans `E:\PalimpsesteBackups\semantic-playerzip-provisional-20260920.zip`. L'ancien ZIP (44 054 317 octets, SHA-256 `8F8ADC123388801065234D0E1179D137C03B0B574D80F5D2B8A2690BBF768587`) avait été restauré pendant la correction. Les anciens dossiers de build n'ont pas été supprimés.

## Diagnostic réel et validation humaine

Un diagnostic A/B réel sur un dessin existant a produit, côté Astra, **« Ruée du golem au poing-bélier »**, palette `stone`, forme `golem`. Un premier plan Luna a été compilé, mais son rayon `1` était trop petit pour rendre la forme comme attendu malgré l'échelle demandée. Une borne minimale par forme (45 pour `golem`) a été ajoutée côté compilateur. Luna B `1.8` a ensuite été rappelée **sur la même description Astra figée, sans second appel A** ; son plan a été validé et compilé avec `radius=55`, `scale=180`, `palette=stone` et `signature_geometry_id=null`. Le rapport de diagnostic privé `E:\PalimpsesteRuntime\evidence\pending\doctor-semantic-size-plan-20260920.json` a pour SHA-256 `905D844E6EEBB43313DAF28E80491C7853D8D8563FD6C0495ECEBEEA4880A273`. Ce diagnostic technique ne constitue ni un nouveau job joueur ni un lancement dans le Player. **Aucun verdict humain sur ce golem en jeu n'est encore déclaré.**
