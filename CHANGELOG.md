# SHINO // CODEX MONITOR — Change log

## V5.1.0-preview.1 — Live Task Monitor (PR #11, non publiée)

- **Version distincte :** assembly 5.1.0.0, libellé V5.1.0 / PREVIEW ; script `BUILD-PREVIEW-V5.1.ps1` et ZIP portable preview dédiés. Aucun installateur V5.0 utilisé pour cette candidate ; ancien builder bloqué si version inadéquate.
- **Un widget unique :** quotas 5 h / 7 j, resets, source et fraîcheur en haut ; bandeau « Activité Codex » sous les jauges, quatre dispositions et Tray conservés.
- **Tour utilisateur complet (dans la mesure observable) :** sources locales JSONL lues en lecture seule, filtrage des sous-agents selon métadonnées disponibles, prise en compte du début/de la fin et de `duration_ms` cohérent ; statut incertain explicite lorsque Codex Desktop ne publie pas les événements.
- **États graphiques :** roue violette en activité récente, coche verte à la fin enregistrée du même tour, statut non vérifié après péremption ; durée/modèle/effort affichés seulement avec provenance.
- **Notifications :** test manuel de bulle Windows, corrélation des événements de fin même sans turn_id, protection contre les replays.
- **Interface :** icône Windows assignée explicitement, menus organisés par action (Activité, Quotas, Apparence, Fenêtre, Notifications, Historique, Application), options explicites et rafraîchissements indépendants.
- **Vie privée :** diagnostic des tours expurgé ; pas de prompt, chemin complet, mot de passe, jeton, cookie ni API payante ; pas de publication automatique.
- **Validation :** build Windows et tests synthétiques sur PR ; suivi de l'onglet Codex Desktop, notification Windows, veille et multi-DPI **restent à vérifier en situation réelle**. Voir `ROADMAP.md`.

## V5.0.0 — Data Trust & Health (Release publique)

- Rapport d'état local accessible sous Quotas & données : source, statut, âge, horodatage local avec décalage horaire, échecs, dernier problème, prochain essai et statut des fenêtres.
- Quota présent/absent, reset futur/échu/non communiqué, blocage et relevés périmés explicitement différenciés, sans simulation.
- Nouveau module pur `SourceHealth.cs`, sans lecture de secrets ni nouveau réseau ; six autotests C# et six audits Python ajoutés (98/98 autotests C# PASS).
- Compatible données et préférences V4.9. Présentation dialoguée ; tests Windows fonctionnels confirmés par utilisateur, cas particuliers suivis dans issue #9.
- **Release V5.0.0** : première étape Data Trust, fonctionnalités restantes et validations spécifiques suivies dans issue #9.


## V4.9.0 — Reliability (intégré à V5)

- Réparation de la restauration de position après retrait ou déplacement d'écran : widget intégralement repositionné dans la zone visible, coordonnées Windows multi-écran conservées lorsque valides.
- Icône tray : version initiale calculée depuis l'assembly et non un ancien numéro V4.7.2.
- 4 nouveaux tests C# et 5 audits Python ; scripts V4.9 dédiés.
- Aucun changement au collecteur Codex, aux alertes ou aux préférences locales.
- V5 : milestone à définir, pas de Release annoncée.


## V4.8.0 — Desktop Experience (intégré à V5)

- Widget circulaire redimensionnable par poignée (470–720 px) et par tailles prédéfinies.
- Aimantation facultative aux bords utiles des écrans, calcul multi-moniteur en coordonnées Windows.
- Opacités affinées : 70, 75, 80, 85, 90, 95, 96, 98, 100 %.
- Notifications activables séparément pour quota 5 h et quota 7 j ; mêmes garde-fous de validité.
- Quatre nouveaux paramètres sauvegardés sans toucher à l'historique ni au collecteur Codex.
- Nouveau ZIP / installateur V4.8 ; huit autotests C# et sept audits Python initiaux ajoutés.
- Refonte du menu tray apres examen d'une vidéo du widget installé :
  six groupes cohérents (Quotas & données, Apparence, Fenêtre, Notifications, Historique, Application),
  **Tray Icon** et palette/disposition déplacés dans Apparence, démarrage Windows renommé.
- Sélection de fond/opacité/taille synchronisée à l'ouverture des sous-menus ;
  controles de fenetre desactives si le widget n'est pas epingle ;
  libelle de restauration limite explicitement au fond et à l'opacité.
- Huit tests Python de structure de menu ajoutés ; collecte Codex intacte.
- Validation du correctif de menu sur CI Windows et essai manuel restant à effectuer avant publication.


## V4.7.2 — Identification fiable de la version (candidate)

- Numéro d'assembly, affichage du widget et entrée « Applications installées » synchronisés en **4.7.2**.
- La future Release `v4.7.2` sera détectable par la V4.7.1, contrairement à deux builds portant le même numéro.
- Agent utilisateur GitHub Releases automatiquement synchronisé avec la version de l'exécutable.
- Quatre autotests C# dédiés au passage V4.7.1 → V4.7.2, à la non-régression et au nom du ZIP.
- Scripts d'installation et d'audit attendent **80/80** autotests C#.
- Code Codex CLI, fenêtres de quota, resets et données personnelles inchangés.
- **Candidate non publiée** : CI Windows 66/66 Python, 80/80 autotests C# PASS ; essai de mise à jour sur PC utilisateur encore nécessaire.

**Validation V4.7.1 :** CI Windows 60/60 tests Python, 76/76 autotests C#, compilation et test de téléchargement réel sans installation. Interface vérifiée sur PC utilisateur avant le dernier correctif ; reset en état bloqué encore à revalider en conditions réelles. Exécutable non signé.

## V4.7.1 — Appearance & Layout Polish

- Micro-widget : zone de fermeture réservée ; nombre hebdomadaire non recouvert.
- Tableau circulaire : 540 px de large au lieu de 768, roues de 130 px et colonnes rapprochées.
- Sous-menu Apparence : opacité 70/80/90/96/100 %, quatre fonds sombres, fond personnalisé avec validation de contraste.
- Préférences enregistrées dans settings.json, application immédiate, maintien des autres dispositions.
- Diagnostic : version exacte lue depuis l'assembly, sans numéro de version obsolète.
- Collecteur Codex inchangé, aucune nouvelle fonctionnalité réseau.

## V4.7.0 — Circular Dashboard

- Nouveau mode Circulaire (deux roues), pourcentages restants, reset et fraîcheur.
- Cinq seuils : 76–100 violet, 51–75 turquoise, 26–50 doré, 11–25 orange, 0–10 rouge.
- Barres et tray alignés sur la même palette ; gris pour les données périmées.
- Préférences existantes conservées : cinq nouvelles couleurs sans écraser les quatre anciennes.
- Collecte Codex inchangée ; interface intégrée à V4.7.1.

# Journal des changements

## V4.6.0 — One-Click Update

- Téléchargement opt-in depuis une Release GitHub stable plus récente.
- Empreinte GitHub `assets[].digest` obligatoire et vérification SHA-256 complète.
- Extraction sur liste blanche uniquement, limite de taille, prévention de traversée ZIP.
- Double confirmation de l'utilisateur, handoff PowerShell ponctuel après fermeture.
- Sauvegarde du précédent exécutable par l'installateur existant, préférences et historique conservés.
- Échec réseau et intégrité : aucune modification de l'installation.
- Pas de mise à jour silencieuse, pas de secret, pas de droits administrateur.
- Tests réels Windows / reprise après veille et rollback restent à valider.



## V4.5.1 — Correctif installateur

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

### V4.7.1 — Correctif du menu Réinitialisations en état bloqué

- Ne remplace plus les heures de reset réellement exposées par « Usage Codex bloqué » ; cet avertissement reste visible en bas du widget.
- Les quatre options du menu s'appliquent aussi à 0 % et en blocage global lorsque les mesures sont Direct.
- Reset absent : « Reset non communiqué » ; reset échu : « Reset attendu · vérification... » ; ancienne donnée : « Ancienne mesure · reset non vérifié ».
- 8 autotests C# et 6 contrôles Python ajoutés.
