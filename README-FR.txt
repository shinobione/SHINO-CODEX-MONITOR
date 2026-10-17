SHINO // CODEX MONITOR V4.8.0 — DESKTOP EXPERIENCE (CANDIDATE)
===================================================================
Version publique stable precedente : v4.7.2
Cette candidate n'est PAS encore une Release officielle.

OBJECTIF
Widget Windows local pour les quotas Codex ChatGPT, et non les quotas
du chat classique ou de l'API facturee. Aucune cle API demandee.

INSTALLATION / BUILD (sources V4.8)
- Sur Windows 10/11 : BUILD-RELEASE-V4.8.bat.
- Verifier les 88 autotests Windows avant toute installation.
- Quitter completement le widget actuellement installe.
- Installer depuis dist\SHINO_CODEX_MONITOR_V4_8_WINDOWS\INSTALLER-V4.8.bat
  ou utiliser PREPARER-ET-INSTALLER-V4.8.bat (build, tests et installation).
- Pas de droits administrateur.
- Les preferences et l'historique local sont conserves.
- Si Chrome/Windows bloque un executable non signe, ne desactivez pas
  les protections ; privilégiez une compilation locale des sources inspectees.

FONCTIONNALITES V4.8
- Widget epingle : poignée en bas a droite pour agrandir/reduire les roues.
  Largeur autorisee 470-720 px. Modes Vertical, Compact et Micro inchanges.
- Clic droit > Apparence > Taille des roues :
  Petit 480 px / Standard 540 px / Grand 620 px / Tres grand 700 px.
- Apparence > Aimantation aux bords des ecrans : facultative, desactivee
  par defaut, active a proximite des bords lors d'un deplacement.
- Apparence > Opacite : 70, 75, 80, 85, 90, 95, 96, 98 ou 100 %.
- Notifications intelligentes : activer independamment 5 h et 7 jours,
  puis les seuils et alertes reset deja disponibles.
- Pourcentage central = quota RESTANT, jamais le pourcentage consomme.
- Couleurs de quota restant :
  76-100 violet, 51-75 turquoise, 26-50 jaune dore,
  11-25 orange, 0-10 rouge.
- Reset affiche seulement si Codex fournit un horodatage valide ;
  usage bloque, anciennete et erreurs de source explicitement signales.

CONFIDENTIALITE
- Codex CLI doit deja etre authentifie avec votre compte ChatGPT.
- Pas de lecture/copie de cookie, mot de passe ou fichier auth.json.
- Stockage : %LOCALAPPDATA%\SHINO-CODEX-MONITOR
- Aucun autostart cree sans selection explicite.

DESINSTALLATION
Menu Demarrer > Desinstaller SHINO Codex Monitor, confirmation OUI.
Historique et preferences locaux conserves par defaut.

DEPANNAGE
- Clic droit tray > Diagnostic : verifier source, relevé valide, erreur.
- Si la jauge est grise, regarder l'etat/fraicheur (pas de simulation).
- En cas d'absence du CLI, verifier son installation et sa connexion.
- Le binaire est non signe. Le SHA-256 verifie l'integrite, pas l'editeur.

STATUT
V4.8 est candidate ; tests CI, affichage DPI, plusieurs ecrans et
parcours de mise a jour depuis V4.7.2 non encore verifies en condition reelle.
