SHINO // CODEX MONITOR — CANDIDATE 4.7.2 (NON PUBLIEE)
========================================

Application Windows C# WinForms locale pour afficher les quotas Codex du compte
ChatGPT (et non ceux du chat classique ni de l'API OpenAI).
Projet communautaire non officiel. Aucun secret Codex ne doit etre partage.

1) INSTALLATION DEPUIS LA RELEASE GITHUB
   - Source officielle : https://github.com/shinobione/SHINO-CODEX-MONITOR/releases
   - Recuperer SHINO_CODEX_MONITOR_V4_7_WINDOWS.zip, puis le decompresser.
   - Fermer completement le widget existant (tray > Quitter completement).
   - Lancer INSTALLER-V4.7.bat sans droits administrateur.
   - Le paquet verifie SHA-256 + les autotests du binaire
     (Release V4.7.1 : 76 tests ; candidate V4.7.2 : 80 tests).
   - L'application est installee sous :
     %LOCALAPPDATA%\Programs\SHINO Codex Monitor
   - Les preferences et l'historique restent sous :
     %LOCALAPPDATA%\SHINO-CODEX-MONITOR
   - Le binaire n'est PAS signe ; un digest dans le meme ZIP prouve l'integrite
     par rapport au manifeste, pas l'identite de l'editeur. Une Release GitHub
     peut fournir une empreinte SHA-256 independante de l'archive.

2) INSTALLATION DEPUIS LES SOURCES
   - Compiler uniquement : BUILD-RELEASE-V4.7.bat
   - Puis ouvrir dist\SHINO_CODEX_MONITOR_V4_7_WINDOWS\INSTALLER-V4.7.bat
   - Ou, apres validation : PREPARER-ET-INSTALLER-V4.7.bat
   - Windows 10/11, .NET Framework 4.x avec compilateur csc.exe disponible.
   - Les fichiers sources ne sont pas une archive d'installation precompilee.

3) CONFIGURATION ET FONCTIONNEMENT
   - Installer Codex CLI et se connecter normalement a son compte ChatGPT.
   - Le widget ne demande aucun mot de passe, jeton, cookie ou cle API.
   - Clic droit sur l'icone tray : Disposition (Circulaire, Vertical, Compact, Micro).
   - Apparence : opacite 70/80/90/96/100 %, 4 fonds sombres ou personnalise.
   - Couleurs jauges : cinq niveaux de quota RESTANT :
       76-100 % violet ; 51-75 % turquoise ; 26-50 % jaune dore ;
       11-25 % orange ; 0-10 % rouge.
   - Reinitialisations : mixte, date/heure, decompte ou masquees.
   - Usage bloque : avertissement en pied. Si Codex donne une date de reset
     future dans un releve Direct, le reset reste visible selon le choix.
     Si reset absent : "Reset non communique".
     Ancienne donnee : reset non verifie. Aucun reset n'est invente.
   - Actualisation : manuelle ou automatique, intervalle configurable.
   - Les credits eventuels ne sont affiches que si Codex les fournit.

4) MAINTENANCE, SECURITE, PROTECTION
   - Windows Defender / Chrome peuvent signaler l'EXE non signe.
   - Ne pas desactiver Defender, SmartScreen ou Safe Browsing et ne pas
     forcer un telechargement bloque sans caracteriser le signalement.
   - Le script retire seulement le marquage Internet du binaire installe
     apres verification ; il ne desactive pas les protections systeme.
   - Test source : python -m unittest discover -s tests -p "test_*.py"
   - Test EXE : SHINO-CODEX-MONITOR.exe --self-test
   - Les mesures anciennes ou contradictoires ne doivent jamais etre
     affichees comme des mesures directes.

5) DESINSTALLATION
   - Menu Demarrer > Desinstaller SHINO Codex Monitor ou Applications
     installees ; une confirmation est demandee.
   - Les preferences et l'historique local restent conserves par defaut.

6) DEPANNAGE
   - Si Codex est indisponible, verifier le CLI et sa connexion.
   - Menu tray > Diagnostic : verifier "Etat", "Dernier releve valide"
     et "Erreur" ; ne jamais envoyer auth.json ni ses jetons.
   - En cas de dysfonctionnement, conserver la derniere version locale
     validee plutot que desactiver les protections de Windows.

Derniere Release publique : v4.7.1 (60 tests Python, 76 autotests C# PASS).
Candidate V4.7.2 : pas encore validee ni publiee au moment de la preparation.
Les heures de reset en etat bloque restent a verifier en situation reelle.

7) POURQUOI V4.7.2 ?
   - Une installation locale V4.7.1 existait avant le dernier correctif.
   - Deux executables V4.7.1 differents ont le meme numero de version.
   - V4.7.2 permet a la future Release d'etre detectee par le widget 4.7.1.
   - Pas de modification du collecteur, des quotas ou des donnees locales.
   - Les scripts restent V4.7 ; le build compile un executable 4.7.2.
