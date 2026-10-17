# SHINO // CODEX MONITOR — Change log

## V4.7.1 — Appearance & Layout Polish (candidate)

- Micro-widget : zone de fermeture réservée ; nombre hebdomadaire non recouvert.
- Tableau circulaire : 540 px de large au lieu de 768, roues de 130 px et colonnes rapprochées.
- Sous-menu Apparence : opacité 70/80/90/96/100 %, quatre fonds sombres, fond personnalisé avec validation de contraste.
- Préférences enregistrées dans settings.json, application immédiate, maintien des autres dispositions.
- Diagnostic : version exacte lue depuis l'assembly, sans numéro de version obsolète.
- Collecteur Codex inchangé, aucune nouvelle fonctionnalité réseau.

## V4.7.0 — Circular Dashboard (candidate)

- Nouveau mode Circulaire (deux roues), pourcentages restants, reset et fraîcheur.
- Cinq seuils : 76–100 violet, 51–75 turquoise, 26–50 doré, 11–25 orange, 0–10 rouge.
- Barres et tray alignés sur la même palette ; gris pour les données périmées.
- Préférences existantes conservées : cinq nouvelles couleurs sans écraser les quatre anciennes.
- Collecte Codex inchangée. V4.7 en PR draft, aucun binaire public publié.

# Journal des changements

## V4.6.0 — One-Click Update (candidate)

- Téléchargement opt-in depuis une Release GitHub stable plus récente.
- Empreinte GitHub `assets[].digest` obligatoire et vérification SHA-256 complète.
- Extraction sur liste blanche uniquement, limite de taille, prévention de traversée ZIP.
- Double confirmation de l'utilisateur, handoff PowerShell ponctuel après fermeture.
- Sauvegarde du précédent exécutable par l'installateur existant, préférences et historique conservés.
- Échec réseau et intégrité : aucune modification de l'installation.
- Pas de mise à jour silencieuse, pas de secret, pas de droits administrateur.
- Tests réels Windows / reprise après veille et rollback restent à valider.



## V4.6.0 — Correctif installateur (candidate)

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
