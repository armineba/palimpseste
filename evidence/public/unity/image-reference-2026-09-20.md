# D13 — Relecture Unity du vrai plan guidé par l'image

## Capture 04 exécutée

Le 20 septembre 2026, Unity a rejoué le paquet de **« Pique du Grand Oiseau d’Orage »** issu de la reprise B réussie. Cette capture a terminé avec **exit 0, 1/1 test Passed**, après correction des incidents des premières tentatives. Voir [le résultat NUnit](image-reference/bird-final/capture-results.xml), [le journal Unity](../../../game/Logs/image-reference-bird-capture-04.log) et [les mesures de capture](image-reference/bird-final/capture.json).

| Mesure réelle | Résultat |
| --- | --- |
| Appels fournisseur pendant cette relecture | 0 |
| Parties déclarées / maximum visible | 36 / 72 |
| Impacts / dégâts en milli-unités / impulsions | 1 / 12000 / 1 |
| Porteurs actifs en fin de contrôle | 0 |
| Renderers de construction restants | 0 |
| Cache sauvegardé | Inchangé |
| Captures | 7 vues du labo et 7 vues latérales supplémentaires |

La vue du labo conserve la pose et le post-traitement de la caméra livrée ; les captures montrent l'arène sans l'interface. La caméra latérale est une vue de comparaison distincte, à 40°, qui suit les bornes du renderer. Les frames viennent d'une cible HDR linéaire puis sont converties en sRGB. La simulation à 60 Hz sert à la capture et **ne constitue pas un benchmark de performances**.

- [Vraie référence G](image-reference/bird-final/visual-reference.png)
- [Vue du labo](image-reference/bird-final/image-spell-frame-01.png)
- [Vue latérale supplémentaire](image-reference/bird-final/image-spell-side-frame-01.png)

## Provenance

Le paquet rejoué porte le SHA-256 `efe24fa464c061b93bdbe7e44195dc513c99b548bce78c0ba325bec9abff7cd5`. Sa référence G est le PNG réel 1536 × 1024, SHA-256 `04f621d5cc1b75cba6c7c564c0ffc4f77e944b82826000ad484643e0071f6bee`. La provenance des appels A, G, B1 rejeté puis B2 réussi est conservée dans [la preuve fournisseur](../backend/image-reference-2026-09-20.json).

Ce paquet est le cache de diagnostic exporté par le Doctor depuis le plan réel ; son champ fournisseur `mode=fixture` / `doctor.offline` décrit cet emballage de relecture. Cette capture **ne démontre pas un nouveau job joueur de production** ni le téléchargement dans le Player final.

Empreintes des preuves :

- Résultat NUnit : `cb9d6ab1504f811fd27a882de7eb4f79f78b3ce4537f668f3da5c7112cfb4e10`.
- Mesures `capture.json` : `cacee435d0ca25f3b5baee695f1fa25cdda2d371e5d1a0806bc3216dc43fc8fb`.

## Limites et dernier changement

Les premières tentatives ne sont pas masquées : la première n'a exécuté aucun test à cause d'un GUID de fixture invalide ; la suivante a révélé une initialisation de `MaterialPropertyBlock` dans un constructeur. La capture 04 réussie est postérieure aux corrections.

**Un dernier réglage du matériau verre a été effectué après la capture 04 et n'a pas été recapturé.** Le build final a ensuite réussi, le backend local a été déployé et le Player `1.3.0` lancé sous le PID `39012` ; ces faits distincts figurent dans [la livraison](image-reference-delivery.json) et [le déploiement](../backend/image-reference-deployment-2026-09-20.json). Aucune acceptation humaine, fidélité exacte à l'image, validation du son ou validation de tous les sorts n'est déduite de cette capture.
