SHINO // CODEX MONITOR V5.1.0 — RELEASE STABLE WINDOWS
===========================================================
Dossier stable : https://github.com/shinobione/SHINO-CODEX-MONITOR/releases/tag/v5.1.0

INSTALLER LA V5.1
1. Telecharger SHINO_CODEX_MONITOR_V5_1_WINDOWS.zip depuis Releases.
2. Quitter completement SHINO // CODEX MONITOR depuis le Tray Windows.
3. Decompresser, puis lancer INSTALLER-V5.1.bat (sans admin).
4. Le script verifie SHA-256 et la version 5.1.0, execute 98 tests
   du programme et 46 tests du moniteur de taches avant installation.
5. Les preferences/history sont preservees, backup du binaire precedent.

FONCTIONS
Deux quotas Codex restants (5 h, 7 j) et reset lorsque exposes.
Une seule fenetre : roue d'activite locale violette si un tour principal
est observe, coche verte a sa fin confirmee, gris NON VERIFIE si le
journal ne permet pas de conclure. Durée, modèle et effort quand presents.
Apparence configurable ; Tray et barre des taches ; notifications opt-in.

LIMITES
Quotas : CLI Codex authentifie, account/rateLimits/read.
Taches : sessions JSONL locales non documentees. Une session silencieuse
n'est PAS necessairement inactive. Il n'existe pas de preuve de
correspondance systematique avec l'onglet Codex Desktop affiche.
Pas de jeton, mot de passe ou cookie recupere.

APRES INSTALLATION
Clic droit Tray > Activite Codex (locale) > Copier le diagnostic
des tours (sans donnees privees) pour investiguer toute incoherence.
Pour Windows Notifications : tester depuis Notifications dans le menu.

BUILD SOURCE
BUILD-RELEASE-V5.1.bat ou BUILD-RELEASE-V5.1.ps1.
ZIP attendu : dist\SHINO_CODEX_MONITOR_V5_1_WINDOWS.zip.
Le script peut valider l'installateur sans aucune installation.

DESINSTALLER
Applications installees > SHINO // CODEX MONITOR, ou raccourci
Desinstaller dans le menu Demarrer ; confirmer avec OUI.
Les donnees locales settings.json/history.jsonl restent par defaut.

NON SIGNE. Ne jamais desactiver Defender/SmartScreen.
Projet communautaire non officiel. Details dans README.md, ROADMAP.md.
