SHINO // CODEX MONITOR — V5.1.0 PREVIEW.1
=========================================================
BUILD DE TEST — NON SIGNE — AUCUN INSTALLATEUR INCLUS

UTILISATION
1. Quitter completement toute version deja ouverte (icone Tray > Quitter).
2. Decompresser le ZIP dans un dossier distinct.
3. Lancer SHINO-CODEX-MONITOR.exe directement (sans droits admin).
4. Clic droit icone Tray : Quotas & donnees / Activite Codex / Fenetre.

SUIVI D'ACTIVITE (INDICATIF)
Le module lit en lecture seule des sessions Codex JSONL locales non
documentees, et tente de suivre un tour principal utilisateur. Il
ne peut pas garantir le suivi de chaque commande de Codex Desktop.
Si le statut est indisponible, incertain ou ancien : ce n'est pas
une preuve que Codex ne travaille pas. Les 2 jauges de quotas viennent
toujours du CLI Codex / account/rateLimits/read (autre source).

ALERTES
Un test manuel est disponible dans Notifications. Son declenchement
ne garantit pas que Windows affiche une bulle. Verifier Windows >
Systeme > Notifications / Ne pas deranger.

TESTS DU BINAIRE
SHINO-CODEX-MONITOR.exe --self-test-silent
SHINO-CODEX-MONITOR.exe --live-task-self-test
Les rapports sont ecrits dans le meme dossier ; le mode normal ne
requiert ni console PowerShell ni abonnement supplementaire.

DESINSTALLATION DE CETTE PREVIEW PORTABLE
Clic droit Tray > Quitter completement, puis supprimer le dossier
decompresse. Aucune suppression de settings.json/history.jsonl.
Ne pas utiliser INSTALLER-V5.0 sur ce candidat.

RETOUR V5.0 STABLE
Quitter la Preview et relancer la V5.0 installee. La Release publique
reste V5.0.0 tant que V5.1 n'est pas approuvee et publiee.

SIGNATURE ET SECURITE
Pas de signature numerique ; SHA256SUMS.txt prouve l'integrite de
l'archive generee localement, pas l'identite de l'editeur.
Ne jamais desactiver Windows Defender/SmartScreen pour l'essai.
https://github.com/shinobione/SHINO-CODEX-MONITOR/pull/11
