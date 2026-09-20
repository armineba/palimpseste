# Player Windows avec retour de lecture — 20 septembre 2026

Unity **6000.3.24f1** a construit un Player Windows x64 **IL2CPP/URP**
contenant le formulaire « Signaler une lecture incorrecte » dans les écrans
d'interprétation et de laboratoire. Le journal privé
`E:\Palimpseste\.runtime\operator-staging\unity-build-feedback.log`, SHA-256
`35F0AC6F9DC75E4B32D49AC9BBC68B40D983D05595AD70B48D8C09EEBC8995D0`,
contient `Build Finished, Result: Success` et `PALIMPSESTE_BUILD_OK`.

La copie jouable `game/Build/WindowsPlayerFeedbackPlayable/` contient
**29 fichiers, 122 074 957 octets**. `Palimpseste.exe` a pour SHA-256
`049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2` ;
`GameAssembly.dll` a pour SHA-256
`2EE47CF35B6EC20C6BD4B9D29E6C5220A0659E192D51511DC5A99F0736B3F1F2`.
`StreamingAssets/service.json` contient `{"service_url":""}`.

Les tests Unity **EditMode 11/11** ont réussi, XML privé SHA-256
`59050DA5A38C80AF97D45A454ECA55F301ADAFABF411BC3D557F2A739229B082`.
Le run **PlayMode** contient quatre cas : **2 passés, 0 échec, 2 sondes privées
facultatives ignorées** faute d'entrée ; XML privé SHA-256
`8325D5F816E64A523E87C542FBBB918AA1A9B4C261E4F59A1B557D11230DE14C`.
Ces tests ne prouvent pas un envoi HTTP du formulaire.

La copie jouable a été lancée hors ligne, fenêtre cachée, pendant 12 secondes.
Le processus est resté actif et répondant ; son journal privé SHA-256
`1A7CA791525780A6C274C5263626E5D30D740AE4C36AB7AF7F70813FD9495916`
montre Direct3D 12 et PhysX sans exception relevée. Aucun contrôle visuel de
la fenêtre, aucun envoi de retour à l'API, aucun job Luna joueur et aucun sort
du Player connecté n'ont été exécutés dans cette vérification.
