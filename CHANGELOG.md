# Journal des changements

## V4.5.1 — Correctif installateur (candidate)

- Vérification SHA-256 **du binaire installé**, après remplacement atomique.
- Suppression ciblée et documentée du marquage Internet éventuel du seul EXE installé avec `Unblock-File`.
- Aucune désactivation globale de SmartScreen ou des protections Windows.
- Échec de déblocage signalé explicitement ; pas de contournement silencieux.
- Version de l’exécutable et version de désinstallation synchronisées.
- Collecte Codex, icône, historiques et préférences inchangés.
- Non testé en situation réelle avant compilation et essai sur Windows.

## V4.5.0 — Release publiée

- Vérification manuelle de la dernière Release stable GitHub depuis le menu tray.
- Comparaison des versions et ouverture de la page officielle sur confirmation.
- Délai d'attente réseau de 8 secondes, lecture limitée à 128 KiB.
- Tests hors réseau pour les cas nouvelle version / identique / ancienne / brouillon / prerelease / tag invalide.
- Pas de mise à jour automatique, ni de collecte de secrets.
- V4.5.0 a passé 23 tests Python et 45 autotests Windows et a été publiée.
