# Lecteur IL2CPP : ouverture de l'invitation

Build testé : `game/Build/WindowsPlayerFlowOwnerFinal/Palimpseste.exe`, SHA-256
`049F79454586F2AC5445F26B55191CF6611BE62F10C4A5E12F92F806050149C2` ;
`GameAssembly.dll` SHA-256
`0A2235C36CE4FC2788F787301B66DEA93C92F74BED47EC8150D9808C4D51E4DD`.

Le 20 septembre 2026, sans API lancée et sans fichier d'invitation, le lecteur
a été ouvert à 1280 × 800. Un clic Windows sur le bouton visible « Ouvrir mon
invitation » a ouvert une fenêtre native visible de classe `#32770` et de titre
`Ouvrir mon invitation Palimpseste`, détenue par le processus Player. La
commande native Annuler a fermé cette fenêtre (`0` dialogue visible ensuite).
Le Player est resté répondant et son journal ne contient aucune exception.
Aucun fichier n'a été sélectionné, aucun code échangé, aucun secret affiché.

Cette observation vérifie le P/Invoke de sélection et l'annulation dans ce
Player Windows IL2CPP ; elle ne vérifie pas l'import d'une invitation valide.
