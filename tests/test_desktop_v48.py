from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
FORM = (ROOT / 'src' / 'MonitorForm.cs').read_text(encoding='utf-8')
GEOMETRY = (ROOT / 'src' / 'DesktopGeometry.cs').read_text(encoding='utf-8')
PREF = (ROOT / 'src' / 'Models.cs').read_text(encoding='utf-8')
STORE = (ROOT / 'src' / 'LocalStore.cs').read_text(encoding='utf-8')
SELF = (ROOT / 'src' / 'SelfTests.cs').read_text(encoding='utf-8')


class DesktopV48Audit(unittest.TestCase):
    def test_resizable_circular_only_preserves_previous_layouts(self):
        self.assertIn('settings.Layout=="Circular"', FORM)
        self.assertIn('resizeGrip.Visible=circular && settings.Pinned;', FORM)
        self.assertIn('quotaHeight=DesktopGeometry.HeightForWidth(width)', FORM)
        for name in ('"Micro"', '"Compact"', '"Vertical"'):
            self.assertIn(name, FORM)

    def test_resize_grip_has_bounds_and_no_background_threads(self):
        self.assertIn('resizeGrip.Cursor = Cursors.SizeNWSE;', FORM)
        self.assertIn('private void AttachCircularResize()', FORM)
        self.assertIn('Math.Abs(desired - settings.CircularWidth) >= 5', FORM)
        self.assertIn('resizeGrip.MouseUp += delegate {', FORM)
        self.assertNotIn('Task.Run', GEOMETRY)

    def test_geometry_is_bounded_and_screen_aware(self):
        for fragment in ('MinimumWidth = 470', 'MaximumWidth = 720',
                         'ClampWidth(int proposed)', 'HeightForWidth(int width)',
                         'ScaleRect(int x, int y, int width, int height, int widgetWidth)',
                         'ClampToScreen(Rectangle proposed, Rectangle work)',
                         'Snap(Rectangle proposed, Rectangle work, int distance)'):
            self.assertIn(fragment, GEOMETRY)
        self.assertIn('Screen.FromRectangle(Bounds).WorkingArea', FORM)
        self.assertIn('settings.SnapToEdges', FORM)

    def test_persistence_and_migration_without_resetting_layout(self):
        for name in ('CircularWidth', 'SnapToEdges', 'AlertFive', 'AlertWeek'):
            self.assertIn(f'public {"int" if name == "CircularWidth" else "bool"} {name}', PREF)
            self.assertIn(f'preferences.{name} = ', STORE)
        self.assertIn('CircularWidth = 540, SnapToEdges = false', PREF)
        self.assertIn('AlertFive = true, AlertWeek = true', PREF)

    def test_precise_opacity_settings(self):
        self.assertIn('70, 75, 80, 85, 90, 95, 96, 98, 100', FORM)
        self.assertIn('settings.Opacity = percent / 100.0;', FORM)
        self.assertIn('if (Opacity < .70 || Opacity > 1.0', PREF)

    def test_notifications_are_per_window_and_still_observed_only(self):
        self.assertIn('if (settings.AlertFive)', FORM)
        self.assertIn('if (settings.AlertWeek)', FORM)
        self.assertIn('CollectNotification(messages, "5 h"', FORM)
        self.assertIn('CollectNotification(messages, "7 days"', FORM)
        self.assertIn('AlertRules.CrossedThreshold(', FORM)
        self.assertIn('SameWindow(previous, latest)', (ROOT / 'src' / 'AlertRules.cs').read_text(encoding='utf-8'))

    def test_self_tests_cover_two_monitors_and_snap(self):
        for label in ('V4.8 : largeur circulaire bornée',
                      'V4.8 : aimantation bord gauche et haut',
                      'V4.8 : aimantation bord droit et bas',
                      'V4.8 : écran secondaire à coordonnées négatives',
                      'V4.8 : aucune aimantation distante',
                      'V4.8 : préférences des fenêtres et taille migrées'):
            self.assertIn(label, SELF)


if __name__ == '__main__':
    unittest.main()
