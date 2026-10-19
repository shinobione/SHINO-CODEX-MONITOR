# SHINO // CODEX MONITOR

**Widget Windows natif (C# WinForms) pour les quotas Codex liés au compte ChatGPT.** Il ne représente ni les limites du chat ChatGPT classique ni les quotas de l'API facturée. Sans Electron, serveur local, hébergement ni appel payant à un modèle IA.

| Canal | Version | Statut |
|---|---|---|
| **Stable** | **V5.0.0** | [Release publique](https://github.com/shinobione/SHINO-CODEX-MONITOR/releases) |
| **Preview** | **V5.1.0-preview.1** | [PR #11 — Draft](https://github.com/shinobione/SHINO-CODEX-MONITOR/pull/11) ; tests réels en attente |

**Attention :** la Preview est une candidate de développement **non signée, non installée et non publiée**. Le programme indique `V5.1.0 / PREVIEW`, avec un assembly `5.1.0.0`. La V5.0 demeure la seule Release stable tant que la validation terrain et la publication ne sont pas approuvées.

## Ce que montre le widget

- **Quotas Codex ChatGPT :** pourcentage **restant**, fenêtres 5 heures et 7 jours uniquement si réellement exposées, reset, fraîcheur, état de source et crédits/réinitialisations éventuels réellement communiqués.
- **Activité Codex locale (V5.1 Preview) :** dans **la même fenêtre**, roue violette si un tour principal est **observé** dans des journaux récents, coche verte seulement pour la **fin enregistrée de ce tour**, gris `NON VÉRIFIÉ` si la présence réelle ne peut pas être confirmée. Durée, modèle et effort quand disponibles.
- **Tray :** jauge de quota et petit point de statut **statique**, menu au clic droit. Notifications facultatives et test manuel ; ne pas considérer une notification envoyée comme nécessairement affichée par Windows.
- **Affichage :** Circulaire (2 roues), Vertical, Compact ou Micro ; épinglage, placement multi-écran, opacité, couleurs et historique local limité.

### Deux sources, deux niveaux de confiance

| Quoi | Source | Signification |
|---|---|---|
| Quotas | **Codex CLI** déjà authentifié, méthode `account/rateLimits/read` | Relevé direct si disponible, daté, jamais inventé |
| Tâches | Sessions Codex locales `%USERPROFILE%\.codex\sessions` ou `%CODEX_HOME%\sessions` | **Observation indicative non documentée**, pas une API live Codex Desktop |

Pour l'activité, le monitor essaie d'identifier les **tours utilisateurs principaux** (`thread_source=user`), d'exclure les sous-agents et de reconnaître `task_started` / `task_complete`. Il n'affirme **jamais** que l'onglet actuellement ouvert dans Codex Desktop est forcément celui du journal. Le silence du journal, les sous-agents et certaines tâches Cloud empêchent un suivi garanti. Une durée complète peut provenir de `duration_ms` uniquement lorsqu'il est présent et cohérent.

## Installation stable V5.0.0

1. Télécharger `SHINO_CODEX_MONITOR_V5_0_WINDOWS.zip` depuis la [Release officielle](https://github.com/shinobione/SHINO-CODEX-MONITOR/releases).
2. Vérifier l'origine du paquet, décompresser et lancer `INSTALLER-V5.0.bat` sans droits administrateur.
3. L'installateur valide le SHA-256 et les 98 autotests C# avant installation sous `%LOCALAPPDATA%\Programs\SHINO Codex Monitor`.
4. Utiliser le Codex CLI déjà authentifié sur le **même compte ChatGPT**. Ne jamais saisir son mot de passe dans le widget.

L'EXE n'est **pas signé** : n'affaiblissez pas Windows Defender ou SmartScreen. Le SHA-256 d'un ZIP prouve l'intégrité du contenu téléchargé, pas l'identité de son éditeur.

## Essayer V5.1.0-preview.1 sans installation

Sur la [PR #11](https://github.com/shinobione/SHINO-CODEX-MONITOR/pull/11), ouvrir le dernier **workflow Windows réussi** et télécharger l'artefact **portable Preview** du commit correspondant.

1. Quitter complètement V5.0 depuis le Tray (sinon le mutex empêche une seconde instance).
2. Décompresser l'artefact V5.1 dans un dossier distinct puis lancer `SHINO-CODEX-MONITOR.exe` ; aucune console PowerShell ne doit rester ouverte.
3. Vérifier que le widget indique **V5.1.0 / PREVIEW**, puis exécuter une commande Codex Desktop. Comparer l'heure et la durée totale du **tour utilisateur**, et non celles d'un sous-agent.
4. Clic droit Tray → **Activité Codex (locale) → Copier le diagnostic des tours (sans données privées)** si le journal et l'interface divergent. Le diagnostic ne copie aucun message, chemin complet ni identifiant de session.

**Compiler depuis les sources (Windows) :** `BUILD-PREVIEW-V5.1.bat` ou `BUILD-PREVIEW-V5.1.ps1`. Le résultat est `dist/SHINO_CODEX_MONITOR_V5_1_0_PREVIEW_WINDOWS.zip`, **portable**, sans installateur et sans publication automatique.

**Tests hors ligne intégrés :** `SHINO-CODEX-MONITOR.exe --self-test-silent` et `SHINO-CODEX-MONITOR.exe --live-task-self-test`. Tests de code : `python -m unittest discover -s tests -p "test_*.py"`. Les scénarios C# d'activité sont **simulés** ; ils ne prouvent pas la synchronisation avec Codex Desktop sur un vrai PC.

**Retour à la version stable / suppression de la Preview :** quitter complètement V5.1 depuis le Tray, supprimer uniquement son dossier décompressé, puis relancer V5.0. Ni `settings.json` ni `history.jsonl` ne doivent être supprimés.

### Menus V5.1 Preview

| Section | Actions |
|---|---|
| **Afficher / masquer** et **Actualiser les quotas** | Actions directes du widget ; quotas et activité restent séparés |
| **Activité Codex (locale)** | Rafraîchissement de l'activité, diagnostic expurgé, nombre de sources observées |
| **Quotas & données** | Tableau officiel, santé des données, options de reset et fréquence de collecte |
| **Apparence** | Disposition, dimension, fond, opacité, couleurs, icône Tray et animation d'activité |
| **Fenêtre** | Épinglage, aimantation, premier plan, présence dans la barre des tâches |
| **Notifications** | Interrupteur principal, alerte de tâche, fenêtres/seuils de quota, test Windows |
| **Historique** | Vue historique et réglage de collecte |
| **Application** | Mises à jour **stables** uniquement, autostart opt-in, diagnostic général, sortie |

Le réglage global **Notifications → Activer les notifications** doit être coché en plus de l'alerte de tâche pour les notifications réelles. Le test manuel n'implique pas que Windows affiche une bulle (vérifier Paramètres → Notifications / Ne pas déranger).

## Confidentialité et état dégradé

- Aucune lecture de `auth.json`, cookies ou secrets ; les journaux sont ouverts **en lecture seule** avec traitement borné et métadonnées expurgées.
- Paramètres et historique restent localement sous `%LOCALAPPDATA%\SHINO-CODEX-MONITOR`. Aucune donnée personnelle n'est envoyée au dépôt.
- Absence, erreur, contradiction ou fraîcheur insuffisante → **Indisponible / À vérifier / Ancienne donnée**, avec date du dernier relevé quand disponible. Une simulation ne s'affiche jamais comme donnée réelle.
- La fenêtre peut indiquer **NON VÉRIFIÉ** alors que Codex Desktop est réellement en train de travailler ; cela évite une **fausse coche verte**.
- Un nouveau `codex app-server` n'est pas nécessairement connecté à l'instance Desktop. L'observation directe de l'onglet courant reste à étudier avec une interface documentée.

## Dépannage rapide

| Symptôme | À vérifier |
|---|---|
| Pas de quotas | Codex CLI installé et déjà connecté ; clic droit → Quotas & données → État des données |
| Quotas anciens | Heure du dernier relevé, réseau, source et rafraîchissement ; ne pas supposer que 0 % est réel |
| Aucune activité / état non vérifié | Journaux locaux Codex, version Desktop/CLI, diagnostic expurgé ; aucune extraction de secrets |
| Pas de notification Windows | Notifications globales et tâche, mode Ne pas déranger, test manuel |
| Pas d'icône barre des tâches | Tray → Fenêtre → Épingler + Afficher dans la barre des tâches ; réépingler le raccourci si cache Windows |
| EXE ne démarre pas | Quitter l'autre instance, contrôler la provenance, exécuter les autotests, consulter la CI |
| Désinstallation stable | Menu Démarrer / Applications installées ; confirmation requise et conservation des données locales par défaut |

Pour les décisions de validation et tâches à venir : **[ROADMAP.md](ROADMAP.md)**. Pour l'historique des versions : **[CHANGELOG.md](CHANGELOG.md)**. Notice en français : [README-FR.txt](README-FR.txt) et [README-PREVIEW-V5.1.txt](README-PREVIEW-V5.1.txt).

**Projet communautaire non officiel, sans affiliation avec OpenAI.** Licence de redistribution non attribuée ; dépôt public ne signifie pas licence open source.
