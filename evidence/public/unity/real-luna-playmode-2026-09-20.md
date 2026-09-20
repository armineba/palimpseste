# Sonde Unity PlayMode sur le paquet Luna réel — 20 septembre 2026

Le test opt in `RealLunaPacketPlayModeTests.CompiledRealLunaBeamCastsWithoutInventedDamage`
a été exécuté avec Unity **6000.3.24f1** en PlayMode batch, `-nographics`,
sur un paquet compilé **temporaire privé** issu des réponses réelles Luna A et B.
Le test ne crée aucun parchemin joueur et n'appelle ni modèle, ni API, ni worker.

| Entrée ou preuve privée | Octets | SHA-256 |
|---|---:|---|
| Description A validée | 1 542 | `83c173816320c022a6c02a84c8ded3398f0b75987909d19c98f6e95eb6b36ccc` |
| Plan B validé | 714 | `8eea9ffcadcf3ec75de8cfb66f6ca8712d3a1d6fe70a076293c38e1c5404a2fd` |
| Encre utilisée pour les géométries | 20 577 | `18cda98d1ef7e2a6f2148cfb81998d0b4494ea1e46bc288477459e33f1482afc` |
| Paquet de test `packet.json` | 2 317 | `856d1b37bbe41c6372b0319112053dfdcc9ad6d8f1925ab2e5214336ae19c37b` |
| Résultat Unity XML original, privé | 4 886 | `1bf968b3a8b651985adf755341581a93e5d8e3344874d50ed514239d95640f11` |
| Journal Unity original, privé | 60 691 | `ce30b7db8e5488daacb481a67dd93b0586ececf644393de3ed77a4c41cabd61b` |

Commande exécutée, avec `PALIMPSESTE_REAL_PROBE_PACKET_DIR` pointant sur le
dossier privé contenant `packet.json` et `artifacts/<artifact_id>` :

```powershell
Unity.exe -batchmode -nographics -projectPath game -runTests -testPlatform PlayMode -testFilter Palimpseste.Game.PlayModeTests.RealLunaPacketPlayModeTests -testResults <fichier-XML-prive> -logFile <journal-prive>
```

Le XML donne **1 test passé sur 1**, sans échec ni saut. Le journal se termine
par `Test run completed. Exiting with code 0 (Ok).` Le test a constaté :

- chargement du paquet et de ses artefacts hachés par `SpellLab`, puis `Cast=true` ;
- un porteur `beam`, deux positions de `LineRenderer`, longueur positive,
  matériau/shader présent et teinte feu ;
- création d'un `AudioSource` pour le son procédural de naissance ;
- zéro dégât, conformément au plan B sans effet, puis expiration après un tick.

La commande `-nographics` emploie le rendu nul Unity : ce contrôle ne prouve
aucun pixel affiché, aucune écoute humaine du son, aucun Player IL2CPP connecté,
ni l'arrivée du paquet par le parcours réseau joueur. Les identifiants
d'artefacts du paquet sont réservés à cette sonde privée et ne représentent
pas des artefacts publiés par le service.
