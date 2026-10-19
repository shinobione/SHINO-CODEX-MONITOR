# SHINO // CODEX MONITOR

**V5.1.0 — Windows (Release stable)**

Application native C# WinForms, sans Electron ni serveur, qui présente les **quotas Codex liés à l'abonnement ChatGPT** et une observation locale des tours Codex. Ce n'est ni le quota du chat classique ni celui de l'API payante.

[**Télécharger la dernière Release Windows**](https://github.com/shinobione/SHINO-CODEX-MONITOR/releases/latest) · [ROADMAP](ROADMAP.md) · [CHANGELOG](CHANGELOG.md) · [Notes V5.1](RELEASE-NOTES-V5.1.md)

## Fonctionnalités

- **Quota 5 h et 7 jours** : pourcentage **restant**, heure/délai de reset réellement exposé, source et horodatage du dernier relevé. Pas de quota, fenêtre ni reset inventé.
- **Widget tout-en-un** : activité Codex intégrée sous les jauges. Roue violette pendant un tour **local observé**, coche verte seulement après événement de fin confirmé, gris `NON VÉRIFIÉ` si les preuves ne suffisent pas. Durée, modèle et effort uniquement si communiqués par la source.
- **Affichage Windows** : 4 dispositions (Circulaire, Vertical, Compact, Micro), tray, option de barre des tâches, multi-écran, redimensionnement, palettes, opacité, historique local.
- **Notifications** : alertes de quotas et de fin de tour indépendantes ; test manuel dans le menu. L'envoi d'une bulle n'assure pas son affichage par Windows.
- **Sobriété/confidentialité** : aucun appel payant à un modèle IA, pas de mot de passe à saisir, pas de récupération des cookies ou jetons, stockage local des préférences et historique.

## Installation et mise à jour de V5.1.0

1. Télécharger **`SHINO_CODEX_MONITOR_V5_1_WINDOWS.zip`** depuis la [Release V5.1.0](https://github.com/shinobione/SHINO-CODEX-MONITOR/releases/tag/v5.1.0).
2. Quitter complètement une version déjà ouverte depuis le Tray.
3. Extraire le ZIP, puis lancer **`INSTALLER-V5.1.bat`**. Droits administrateur non nécessaires.
4. L'installateur vérifie l'intégrité SHA-256 du binaire, son numéro de version, lance **98 tests C# du programme** et **46 tests C# des tours Codex** avant installation, conserve les préférences/historique et sauvegarde l'ancien EXE pour retour arrière en cas d'échec.

Le widget est installé sous `%LOCALAPPDATA%\Programs\SHINO Codex Monitor`. Son icône Windows est incluse dans l'exécutable. Vous pouvez aussi **Vérifier les mises à jour** depuis le menu Application : seules les Releases GitHub stables sont proposées, avec vérification du digest `sha256:` exposé par GitHub, du ZIP et du binaire avant l'installation assistée. Aucune mise à jour automatique sans accord.

**Sécurité :** le binaire n'est **pas signé**. Le manifeste SHA-256 est une vérification d'intégrité, pas une preuve d'authenticité de l'éditeur. Ne désactivez pas Defender ou SmartScreen.

## Comprendre les sources

| Donnée | Provenance | Validité |
|---|---|---|
| Quotas Codex | CLI Codex déjà authentifié, `account/rateLimits/read` | Relevé direct, daté et marqué périmé quand nécessaire |
| Activité Codex | Fichiers de sessions JSONL locales | **Observation indicative, interface non documentée** |

L'activité essaie d'isoler le **tour utilisateur principal** et non les petits tours des sous-agents ; `task_started`, `task_complete` et `duration_ms` ne sont retenus que s'ils existent et sont cohérents. L'application **ne peut pas garantir** le suivi de l'onglet Codex Desktop affiché. Si les événements ne sont pas accessibles, elle doit annoncer `NON VÉRIFIÉ`, jamais déduire d'un journal silencieux que Codex a terminé.

Le CLI doit être installé et authentifié sur **le même compte ChatGPT** que le tableau officiel. Aucune clé API n'est nécessaire. Le moniteur est communautaire et non affilié à OpenAI.

## Menus

| Groupe | Commandes |
|---|---|
| Actions rapides | Afficher/masquer, actualiser les quotas |
| **Activité Codex (locale)** | Actualiser l'activité, diagnostic expurgé, résumé des sources |
| **Quotas & données** | Tableau officiel, état des données, resets, fréquence |
| **Apparence** | Disposition, taille, palettes, opacité et animation |
| **Fenêtre** | Épinglage, bords d'écrans, premier plan, barre des tâches |
| **Notifications** | Interrupteur général, alertes de tâche/quota et notification de test |
| **Historique** | Consulter et activer/désactiver l'historique |
| **Application** | Mise à jour stable, démarrage facultatif avec Windows, diagnostics, quitter |

## Compiler et tester depuis GitHub

Sur Windows, depuis la branche stable `main` :

```powershell
.\BUILD-RELEASE-V5.1.ps1
```

L'archive produite est `dist/SHINO_CODEX_MONITOR_V5_1_WINDOWS.zip`. Ce script compile le programme avec l'icône Windows, valide les **98 + 46 autotests**, construit le ZIP et exécute **l'installateur en mode `-ValidateOnly`**. Il ne modifie pas l'application installée.

Audits du code : `python -m unittest discover -s tests -p "test_*.py"`. La CI Windows est la preuve de compilation et de tests automatisés, pas une simulation d'accès à un compte Codex. Le workflow de publication est verrouillé sur la branche `main` et publie la Release `v5.1.0` une seule fois.

## Désinstallation et dépannage

Désinstaller via Applications installées ou le raccourci Démarrer. La désinstallation demande confirmation et préserve par défaut `settings.json` et `history.jsonl` sous `%LOCALAPPDATA%\SHINO-CODEX-MONITOR`.

- **Pas de quota :** vérifier le CLI Codex et `Quotas & données → État des données Codex`.
- **Activité grise/non vérifiée :** consulter `Activité Codex (locale) → Copier le diagnostic des tours` ; la source JSONL peut être insuffisante.
- **Notification absente :** vérifier `Notifications`, puis Paramètres Windows / Ne pas déranger.
- **Pas d'icône dans la barre des tâches :** `Fenêtre → Épingler` et `Afficher dans la barre des tâches`.
- **Installation refusée :** fermer toutes les instances du widget ; consulter le diagnostic de l'installateur, ne pas contourner la protection Windows.

Voir [ROADMAP.md](ROADMAP.md) pour les améliorations proposées et limites. Licence de réutilisation non attribuée : dépôt public ne signifie pas automatiquement logiciel open source.
