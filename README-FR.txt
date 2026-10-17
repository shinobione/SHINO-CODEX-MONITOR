SHINO // CODEX MONITOR V4.5 — Distribution Windows

IMPORTANT : cette archive BUILD KIT contient le code et les scripts de build.
Ce n'est PAS un logiciel precompile. Ne lancez pas INSTALLER-V4.5.bat dans le BUILD KIT.
Un paquet Windows precompile se cree sous dist/SHINO_CODEX_MONITOR_V4_5_WINDOWS.zip.

1. METHODE SIMPLE SANS BUILD MANUEL POUR L'UTILISATEUR FINAL
   - Placer le contenu de BUILD KIT a la racine d'un depot GitHub que vous gerez.
   - Lancer Actions > SHINO Codex Monitor - Build Windows V4.5 > Run workflow.
   - Attendre l'execution Windows, verifier qu'elle est verte puis recuperer l'artefact
     SHINO_CODEX_MONITOR_V4_5_WINDOWS (ZIP contenant un second ZIP).
   - Extraire SHINO_CODEX_MONITOR_V4_5_WINDOWS.zip, puis lancer INSTALLER-V4.5.bat.
   - Aucun compilateur necessaire sur le PC qui installe.
   - Ne pas rendre public de donnees de compte, credentials ou fichiers de configuration.

2. ALTERNATIVE SANS GITHUB, UNE SEULE COMPILATION LOCALE
   - Mode double-clic : quitter le widget, puis PREPARER-ET-INSTALLER-V4.5.bat.
     Le script construit le paquet, lance les tests, puis installe si tout reussit.
   - Vous n'avez aucune commande de compilation a saisir manuellement.
   - Double-clic BUILD-RELEASE-V4.5.bat sur un PC Windows 10/11 disposant de .NET Framework 4.x.
   - La compilation C# et 45 autotests tournent AVANT creation du paquet final.
   - Distribuer ou archiver dist/SHINO_CODEX_MONITOR_V4_5_WINDOWS.zip.
   - Pour installer depuis ce paquet, double-clic INSTALLER-V4.5.bat.

3. INSTALLATION DE LA RELEASE PRECOMPILÉE
   - Quitter la V4.2/V4.3 via l'icone de notification > Quitter completement.
   - Extraire le ZIP final dans un dossier utilisateur (ne pas le lancer depuis l'archive).
   - Double-cliquer INSTALLER-V4.5.bat.
   - L'installateur controle l'empreinte SHA-256 et lance 45 autotests.
   - Il sauvegarde l'ancien executable en *.previous.exe puis installe sans droits admin.
   - Il cree des raccourcis et une entree dans Applications installees (HKCU).
   - Aucun changement sur l'autostart existant, aucun autostart active par defaut.
   - Le widget se lance sans console apres l'installation.

4. DONNEES ET DESINSTALLATION
   - Executable : %LOCALAPPDATA%\Programs\SHINO Codex Monitor\SHINO-CODEX-MONITOR.exe
   - Preferences et historique : %LOCALAPPDATA%\SHINO-CODEX-MONITOR\settings.json / history.jsonl
   - Donnees conservees pendant mise a jour et desinstallation.
   - Desinstaller via Applications installees ou raccourci menu Demarrer ; saisir OUI.
   - Ne pas supprimer manuellement les JSON si vous souhaitez les garder.

5. RETOUR ARRIERE ET LIMITES
   - Si l'installation echoue AVANT remplacement, l'executable precedent reste intact.
   - Sur disque NTFS, le nouveau binaire remplace atomiquement l'ancien, qui est sauvegarde en *.previous.exe.
   - Pour restaurer : quitter l'app, copier previous.exe sur SHINO-CODEX-MONITOR.exe.
   - Codex CLI connecte a votre compte reste necessaire ; aucun mot de passe saisi dans l'app.
   - Aucun appel payant a un modele ; quota exact selon les donnees disponibles du CLI.
   - L'empreinte SHA-256 detecte un ZIP altere, mais ne constitue PAS une signature numerique.
   - Le binaire est non signe : Windows SmartScreen peut demander verification.
   - Ne pas executer un binaire obtenu d'une source non fiable.

6. TESTS DE RECETTE A FAIRE SUR WINDOWS
   - Verifier l'affichage avec le dashboard officiel Codex (meme compte/instant).
   - Veille/reveil : les donnees deviennent grises jusqu'a nouvelle lecture valide.
   - Coupure reseau/401/403 : donnees signalees indisponibles/perimees.
   - Systray, clic, pin, resets, historique 24h/7j/14j, DPI et plusieurs ecrans.
   - Verifier le rapport des autotests et mesurer RAM/CPU a 15 min d'intervalle.

7. ETAT DE VALIDATION
   - Build WINDOWS et 45 autotests : NON EXECUTES dans l'environnement de preparation Linux.
   - La V4.3 source n'a pas encore ete confirmee fonctionnelle sur le PC utilisateur.
   - La V4.2 validee reste la version de secours jusqu'a validation Windows.

8. VERIFICATION DES MISES A JOUR V4.5
   - Menu systray > Verifier les mises a jour.
   - Lit seulement la derniere Release GitHub publique en HTTPS.
   - Ne lit aucun identifiant Codex ou GitHub.
   - Ouvre la Release dans le navigateur apres ton accord.
   - Aucun telechargement / lancement / installation automatique.
   - Sans Internet, affiche une erreur et laisse la collecte Codex intacte.

9. AVERTISSEMENT AU DEMARRAGE - V4.7.0
   L'installateur tente de retirer le marquage Internet (MOTW) uniquement
   sur l'executable installe, apres verifications du SHA-256 et des 45
   autotests. Le logiciel reste non signe. SmartScreen n'est pas desactive.
   Si Windows affiche encore un avertissement, verifier le fichier et
   ses proprietes, ainsi que les politiques de votre installation.

10. MISE A JOUR V4.7
   Clic droit systray > Verifier les mises a jour.
   Confirmer un TELECHARGEMENT HTTPS officiel, puis une INSTALLATION distincte.
   Rejette un digest GitHub manquant, un zip corrompu, un chemin dangereux.
   Le programme se ferme uniquement apres confirmation de l'installation.
   Une fenetre PowerShell temporaire peut s'afficher pendant l'installation.
   Donnees locales settings.json / history.jsonl conservees. Binaire non signe.

V4.7 - CIRCULAR DASHBOARD
- Zone de notification : clic droit > Disposition > Circulaire · 2 roues.
- 76-100 % violet, 51-75 % turquoise, 26-50 % dore, 11-25 % orange, 0-10 % rouge.
- Pourcentages restants au centre ; 5h reset relatif, 7 jours date locale.
- La fraicheur, l'etat et la source restent visibles ; donnees anciennes en gris.
- Les anciens modes et reglages sont conserves. Aucun identifiant dans les sources.

V4.7.1 - APPARENCE
- Clic droit sur l'icone Windows : Apparence > Opacite (70 a 100 pour cent).
- Apparence > Couleur du fond : bleu nuit, noir, anthracite, bleu petrole, personnalisee.
- Une couleur trop claire est refusee pour garantir la lisibilite.
- Changement immediat et conserve dans settings.json ; aucun droit admin.
- Mode Micro : pourcentage de droite eloigne de la croix.
- Mode Circulaire : fenetre plus etroite, roues rapprochees.
