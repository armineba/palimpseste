# Faisceau Luna réel : expiration logique et rendu — 20 septembre 2026

Le plan B réel lié à la description A SHA-256
`83c173816320c022a6c02a84c8ded3398f0b75987909d19c98f6e95eb6b36ccc`
demande un faisceau de feu sans effet, avec `lifetime_ticks=1`. Deux pas physiques
peuvent survenir avant la première image affichée. Le moteur retire désormais ce
porteur de sa liste active au tick prévu et conserve **uniquement** son objet
graphique pendant au moins une occasion de rendu et 0,16 seconde. La fin de cet
affichage supprime l'objet. `CancelAll()` demande sa suppression au moment de
l'annulation ; Unity applique `Object.Destroy` en fin de frame.

Le test PlayMode sur le paquet privé compilé depuis les vrais A/B a été relancé
avec Unity **6000.3.24f1** :

| Contrôle | Résultat XML | SHA-256 XML privé | SHA-256 journal privé |
|---|---|---|---|
| Logique, son, absence de collider et de source audio sur la rémanence, nettoyage | 1/1 passé, `-nographics` | `586682611458fed9e1281024401e2030c5d93f9d4beb46837532fdfa923eee3f` | `30d7bc47ce34d838f03192e71006fe48a4d1029243dd6651b1d5a26e669d9900` |
| Pixels hors écran après expiration logique, puis après nettoyage, paquet A vérifié dans ce test | 1/1 passé, Direct3D 12 | `ad485f7bd01f298ce3f7755c1454c9565524c8504b10ea93eb285b7e4fc1fc40` | `5dd4d2929b4995f8c6527ee3ca3530ffbcd37899228cc3fb6a0e54217ed1aa6b` |

Le contrôle graphique a utilisé une caméra orthographique isolée, un
`RenderTexture` de 256 × 256 pixels et le matériau URP du faisceau. Sa cible de
rendu ne montrait que ce porteur : **13 616 pixels rouges** après l'expiration
logique, puis **zéro** après le nettoyage. La [capture du rendu
isolé](real-luna-beam-afterimage.png), SHA-256
`c9ba58f02179a9c67683cb0e5ba50cc14398e78bc6fc340e07985f69acc8c4db`,
a été inspectée visuellement. Les deux journaux finissent par
`Test run completed. Exiting with code 0 (Ok).`

Le test vérifie que le porteur actif vaut zéro pendant la rémanence, que les
dégâts restent à zéro et que son objet ne porte aucun `Collider` ou `AudioSource`.
Le son procédural de naissance reste un événement one-shot distinct déjà émis
à l'apparition ; il n'a pas été écouté humainement. La caméra isolée prouve les
pixels URP du faisceau, mais pas leur visibilité depuis la caméra de jeu dans
le Player IL2CPP ni le parcours réseau joueur. Aucun modèle n'a été rappelé.
