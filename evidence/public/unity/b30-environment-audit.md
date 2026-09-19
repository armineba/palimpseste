# Vérification de faisabilité B30 — 19 septembre 2026

Machine d'essai : Windows 11 Famille, build 26200. `HypervisorPresent=False`.
Les fonctionnalités `Containers-DisposableClientVM` et
`Microsoft-Hyper-V-All` ne sont pas présentes dans la liste des fonctionnalités
Windows locales. Aucun exécutable Windows Sandbox, Hyper-V Manager, VirtualBox,
VMware ou QEMU n'a été trouvé dans le `PATH`.

Une VM Windows propre utilisable immédiatement, sans installation ni
redémarrage, n'était donc pas disponible. **Aucun essai B30 sur machine propre
n'a été exécuté.** L'extraction et le lancement du ZIP sur le PC de
développement sont des essais locaux distincts et ne valent pas acceptation
B30.
