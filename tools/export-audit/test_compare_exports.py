import unittest
from compare_exports import compare, local_references, properties


class ExportAuditTests(unittest.TestCase):
    def test_blank_lines_are_presentation_only(self):
        report = compare({'dune_folder.txt': b'item.0.caption=A\r\n\r\n'},
                         {'dune_folder.txt': b'item.0.caption=A\n'})
        self.assertTrue(report['menus'][0]['semantic_equal'])

    def test_entry_order_and_numbering_are_preserved(self):
        report = compare({'dune_folder.txt': b'item.0.caption=B\nitem.2.caption=A\n'},
                         {'dune_folder.txt': b'item.0.caption=A\nitem.1.caption=B\n'})
        menu = report['menus'][0]
        self.assertFalse(menu['semantic_equal'])
        self.assertEqual([0, 2], [e['index'] for e in menu['before_entries']])

    def test_unicode_is_not_transliterated_or_normalized(self):
        self.assertNotEqual(properties('actor=Éric\n'.encode()), properties('actor=Eric\n'.encode()))
        self.assertNotEqual(properties('actor=Éric\n'.encode()), properties('actor=Éric\n'.encode()))

    def test_folder_icons_are_checked_without_confusing_them_with_navigation(self):
        files = {'00/01_Ghibli/dune_folder.txt': b'icon_path=icon.aai\n',
                 '05_03_Ghibli/dune_folder.txt': b'media_action=browse\nmedia_url=../00/01_Ghibli\nicon_path=icon.aai\n'}
        refs = local_references(files)
        self.assertEqual(2, sum(r['resolved'] is False and r['role'] == 'asset' for r in refs))
        self.assertFalse(any(r['resolved'] is False and r['role'] == 'navigation' for r in refs))

    def test_explicit_missing_assets_and_browse_targets_are_detected(self):
        files = {'00/03/A/dune_folder.txt': b'background_path=../../00/background.jpg\nitem.0.media_action=browse\nitem.0.media_url=../../99/Absent\nitem.0.icon_path=../../99/Absent/icon.aai\n',
                 '00/00/background.jpg': b'image'}
        refs = local_references(files)
        self.assertEqual([True, False, False], [r['resolved'] for r in refs])

    def test_absolute_and_smb_playback_paths_are_external(self):
        for path in ['/Séries/Été.mkv', 'smb://nas/Séries/Été.mkv', '../../movie.mkv']:
            refs = local_references({'01/Series/01/episode/dune_folder.txt': ('media_url='+path+'\n').encode()})
            self.assertEqual('external_media', refs[0]['kind'])


if __name__ == '__main__':
    unittest.main()
