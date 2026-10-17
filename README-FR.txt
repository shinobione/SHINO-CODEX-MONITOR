SHINO // CODEX MONITOR V5.0.0 — DATA TRUST (PREVERSION)
===================================================================
Version publique stable precedente : v4.7.2
Cette candidate n'est PAS encore une Release officielle.

OBJECTIF
Widget Windows local pour les quotas Codex ChatGPT, et non les quotas
du chat classique ou de l'API facturee. Aucune cle API demandee.

INSTALLATION / BUILD (sources V5.0)
- Sur Windows 10/11 : BUILD-RELEASE-V5.0.bat.
- Verifier les 98 autotests Windows avant toute installation.
- Quitter completement le widget actuellement installe.
- Installer depuis dist\SHINO_CODEX_MONITOR_V5_0_WINDOWS\INSTALLER-V5.0.bat
  ou utiliser PREPARER-ET-INSTALLER-V5.0.bat (build, tests et installation).
- Pas de droits administrateur.
- Les preferences et l'historique local sont conserves.
- Si Chrome/Windows bloque un executable non signe, ne desactivez pas
  les protections ; privilégiez une compilation locale des sources inspectees.

FONCTIONNALITES V5.0
- Widget epingle : poignée en bas a droite pour agrandir/reduire les roues.
  Largeur autorisee 470-720 px. Modes Vertical, Compact et Micro inchanges.
- Clic droit > Apparence > Taille des roues :
  Petit 480 px / Standard 540 px / Grand 620 px / Tres grand 700 px.
- Fenetre > Aimantation aux bords des ecrans : facultative, desactivee
  par defaut, active a proximite des bords lors d'un deplacement.
- Apparence > Opacite : 70, 75, 80, 85, 90, 95, 96, 98 ou 100 %.
- Notifications : activer independamment 5 h et 7 jours,
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
V5.0 est candidate ; tests CI, affichage DPI, plusieurs ecrans et
parcours de mise a jour depuis V4.7.2 non encore verifies en condition reelle.

MENU TRAY V5.0 - ORGANISATION
- Afficher/masquer et Actualiser maintenant : directement accessibles.
- Quotas & donnees : Tableau officiel, Reinitialisations, Frequence.
- Apparence : Disposition, Taille des roues, Opacite, Couleur du fond,
  Couleurs des jauges, Tray Icon (anneau + pourcentage ou anneau seul).
- Fenetre : Epingler, Aimantation multi-ecran, Toujours au-dessus,
  Afficher dans la barre des taches.
- Notifications : marche/arret, fenetres 5 h / 7 jours, seuils et resets.
- Historique : consulter, activer l'enregistrement, ouvrir le dossier.
- Application : Mise a jour, Releases GitHub, Demarrer avec Windows,
  Diagnostic et copier diagnostic.
- Quitter completement : directement accessible en bas du menu.
- L'aimantation et les options d'epingle sont disponibles seulement lorsque
  leur effet est pertinent. Aucun reglage n'est active automatiquement.

NOUVEAUTE V5.0
- Ancienne position sous un ecran debranche : retour a la zone visible du
  moniteur Windows le plus proche, sans reinitialiser les preferences.
- Version initiale Tray Icon synchronisee avec la version de l'executable.
- Quatre autotests C# nouveaux (92 attendus) et cinq tests Python nouveaux.
- Les fonctions V4.8 (roues, aimantation, menu trie) restent incluses.
- Release publique V5.0 non publiee tant que les tests Windows/CI ne sont pas valides.
V5 : prochaine etape majeure de fiabilite/ergonomie a cadrer sans nouvelle source non documentee.

V5.0 - ETAT DES DONNEES CODEX (EXPERIMENTAL)
- Menu : Quotas & donnees > Etat des donnees Codex.
- Rapport local : provenance, fraicheur, dernier releve, erreurs, prochain essai,
  5 heures, 7 jours, resets reels et statut d'usage bloque.
- Les fenetres manquantes ne sont pas simulees ; donnees anciennes indiquees.
- Interface pour le moment : boite de dialogue WinForms, non une page dediee.
- Sources Codex CLI et compte ChatGPT inchanges. Aucun secret lu.
- Tester le rendu Windows, l'horodatage, la veille et le parcours d'update.
- Candidat non publie. Jalons : GitHub issue #9.
