# SHINO // CODEX MONITOR V5.1.0 — Live Task Monitor

Version Windows stable du widget de quotas **Codex liés au compte ChatGPT**, indépendante du chat classique et des quotas API.

## Nouveautés
- **Un seul widget** : quotas 5 heures / 7 jours, resets et fraîcheur, et zone **Activité Codex** intégrée sous les jauges.
- **Animation discrète** pendant une activité locale observée ; coche verte sur événement de fin du tour principal, états `NON VÉRIFIÉ` / `À VÉRIFIER` si les preuves sont insuffisantes.
- Durée, modèle et effort quand la session locale les expose ; évite de présenter le résultat d'un sous-agent comme celui d'une commande principale.
- Notifications Windows facultatives, action de test, menus simplifiés (Quotas, Activité, Apparence, Fenêtre, Notifications, Historique, Application).
- Fenêtre Windows avec icône SHINO explicite et paramètres conservés.

## Télécharger et installer
Télécharger **`SHINO_CODEX_MONITOR_V5_1_WINDOWS.zip`**, décompresser puis lancer `INSTALLER-V5.1.bat` sans droits administrateur, après avoir complètement fermé le monitor depuis le Tray. L'installateur vérifie SHA-256 du binaire et exécute **98 tests C# quotas + 46 tests C# tâches** avant toute modification de l'application installée. Il sauvegarde l'EXE précédent et conserve les préférences/historiques.

**EXE non signé.** Le SHA-256 vérifie l'intégrité, pas la signature de l'éditeur ; ne désactivez ni Defender ni SmartScreen.

## Limites connues
Les quotas utilisent `account/rateLimits/read` du Codex CLI déjà connecté. L'activité s'appuie sur des **journaux locaux JSONL non documentés**, elle **ne garantit pas** le statut temps réel de l'onglet Codex Desktop actif, ni les tâches Cloud. Ne pas déduire d'un journal silencieux que Codex a terminé. Les tests simulés et la compilation Windows ne garantissent pas l'équivalence avec toutes les versions Desktop.

## Sécurité et assistance
Aucun mot de passe, cookie ni jeton copié ; aucun appel payant de modèle. Pour contrôler le suivi : Tray → Activité Codex (locale) → Copier le diagnostic des tours (sans données privées). Les réglages sont dans `%LOCALAPPDATA%\SHINO-CODEX-MONITOR`. La désinstallation conserve par défaut les données et nécessite confirmation.

[README](https://github.com/shinobione/SHINO-CODEX-MONITOR/blob/main/README.md) · [ROADMAP](https://github.com/shinobione/SHINO-CODEX-MONITOR/blob/main/ROADMAP.md)
