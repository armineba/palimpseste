# Reprise de A `1.3` et plan B `1.1` avec géométrie réelle

Le 20 septembre 2026, le `ProviderDoctor.exe` corrigé sous `PalRuntimeSvc` a exécuté le mode `plan` sur la sortie A `1.3` figée du [diagnostic précédent](doctor-ab13-b11-2026-09-20.md). Il a contrôlé son SHA-256 `E54DEC5810E00AABD0FFCB8F152DF06468DD8505593B6718C5B806C46B3254B4` et l'a **réutilisée sans nouvel appel A**. La géométrie envoyée à B a été recalculée depuis cette A et le PNG d'encre canonique : `full.silhouette.0` et `ring.path.0`.

Le rapport privé `E:\PalimpsesteRuntime\evidence\pending\doctor-plan-b11-from-a13-20260920.json` a pour SHA-256 vérifié `BFAFCA2DE085F60AF2A974C60D74CE02CAE58082E28E8A0A740B27EEB22E3787`. Il rapporte `active_result=success`, la réutilisation de A, un seul appel B et la validation du plan par le compilateur avec la géométrie recalculée. B a rapporté `gpt-5.6-luna` à l'effort `max` ; sa sortie finale a pour SHA-256 `E8164D8D654F470101BDCE77A4B0AF46476FF7AE48EA9DF1A92A5B9A84D9AEA1`.

`Palimpseste.Core.RealProbe` a ensuite quitté avec le code `0` sur A figée, ce nouveau B et l'encre réelle. La validation et la compilation contrôlée ont accepté le plan en mémoire. Le paquet de vérification privé `E:\Palimpseste\.runtime\operator-staging\realprobe-ab13-b11-resolved-20260920\packet.json` a pour SHA-256 vérifié `8DAC5FEB3293184564623C1F259567CB025078563F8BC788A9928806BC7672E2` ; la borne rapportée est `max_end_tick=1`, avec zéro effet. Ce paquet sert à vérifier le pipeline et porte une provenance de test ; ce n'est pas un sort joueur publié.

Le refus `signature_geometry` du [premier diagnostic A `1.3`/B `1.1`](doctor-ab13-b11-2026-09-20.md) reste un fait historique : son B avait reçu une géométrie provisoire. Ce nouvel essai B seul a utilisé la géométrie recalculée, sans réinterpréter le dessin par A. La [preuve Unity isolée](../unity/lava-beam-ab13-b11-2026-09-20.md) vérifie séparément l'exécution du paquet.

Le worker joueur reste arrêté et `PALIMPSESTE_EFFORT_VERIFIED=false`. Aucun job du Player n'a encore produit, téléchargé ou lancé ce sort ; aucun verdict humain d'acceptation n'est enregistré. M7 reste ouvert.
