#!/usr/bin/env python3
"""Compare les XML des archives originales et d'un workspace régénéré."""
import argparse
import csv
import hashlib
import io
from pathlib import Path
import xml.etree.ElementTree as ET
from zipfile import ZipFile


def files(path):
    if path.is_dir():
        return {str(p.relative_to(path)).replace('\\', '/'): p.read_bytes()
                for p in path.rglob('*.xml')}
    with ZipFile(path) as archive:
        return {name: archive.read(name) for name in archive.namelist()
                if name.endswith('.xml')}


def logical(data):
    # XmlDocument.PreserveWhitespace=false dans yaDIS2. Respecter xml:space.
    def node(element, preserve=False):
        space = element.attrib.get('{http://www.w3.org/XML/1998/namespace}space')
        preserve = space == 'preserve' if space else preserve
        def text(value):
            return value or '' if preserve or (value or '').strip() else ''
        return (element.tag, sorted(element.attrib.items()), text(element.text),
                [(node(child, preserve), text(child.tail)) for child in element])
    return node(ET.fromstring(data))


def offset(a, b):
    return next((i for i, pair in enumerate(zip(a, b)) if pair[0] != pair[1]),
                min(len(a), len(b))) if a != b else ''


def cause(path, a, b):
    if a == b:
        return ''
    reasons = []
    if a.startswith(b'\xef\xbb\xbf') != b.startswith(b'\xef\xbb\xbf'):
        reasons.append('BOM UTF-8')
    if b'\r\n' in a and b'\r\n' not in b:
        reasons.append('fins de ligne')
    if path.endswith('/data.xml'):
        reasons.append('paire vide date / indentation XmlDocument')
    if path in ('groups.xml', 'shortcuts.xml'):
        reasons.append('éléments vides explicites / autofermés')
    if logical(a) != logical(b):
        reasons.append('contenu XML')
    return '; '.join(reasons) or 'formatage à examiner'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference', type=Path)
    parser.add_argument('before', type=Path)
    parser.add_argument('--after', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    reference, before = files(args.reference), files(args.before)
    after = files(args.after) if args.after else {}
    rows = []
    for path, expected in sorted(reference.items()):
        original, generated = before.get(path), after.get(path)
        rows.append({
            'fichier': path,
            'identique_avant': original == expected,
            'logique_avant': logical(original) == logical(expected) if original else False,
            'premier_ecart_avant_octet': offset(expected, original) if original else 'absent',
            'cause_avant': cause(path, expected, original) if original else 'absent',
            'identique_apres': generated == expected if args.after else 'non mesuré',
            'logique_apres': logical(generated) == logical(expected) if generated else 'non mesuré',
            'premier_ecart_apres_octet': offset(expected, generated) if generated else 'non mesuré',
            'cause_residuelle': cause(path, expected, generated) if generated else 'non mesuré',
            'sha256_reference': hashlib.sha256(expected).hexdigest(),
            'sha256_avant': hashlib.sha256(original).hexdigest() if original else '',
            'sha256_apres': hashlib.sha256(generated).hexdigest() if generated else '',
        })
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open('w', encoding='utf-8', newline='') as out:
        writer = csv.DictWriter(out, fieldnames=list(rows[0]), lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
    print(f"Référence : {len(reference)} XML ; avant : {sum(r['identique_avant'] for r in rows)} identiques ; "
          f"après : {sum(r['identique_apres'] is True for r in rows)} identiques.")
    print('Logique avant :', sum(r['logique_avant'] for r in rows), '/', len(rows))
    if args.after:
        print('Logique après :', sum(r['logique_apres'] is True for r in rows), '/', len(rows))
        print('Chemins supplémentaires après :', sorted(after.keys() - reference.keys()))


if __name__ == '__main__':
    main()
