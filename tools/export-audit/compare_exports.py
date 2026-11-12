#!/usr/bin/env python3
"""Compare deux exports Dune (ZIP ou dossiers), sans dépendance externe."""
import argparse
from collections import Counter
import csv
import difflib
import hashlib
import json
from pathlib import Path, PurePosixPath
import posixpath
import re
import zipfile


def inventory(source):
    source = Path(source)
    if source.is_dir():
        return {p.relative_to(source).as_posix(): p.read_bytes()
                for p in source.rglob('*') if p.is_file()}
    with zipfile.ZipFile(source) as archive:
        files = {i.filename: archive.read(i) for i in archive.infolist() if not i.is_dir()}
    # Les ZIP fournis enveloppent chacun l'export dans un dossier yaDISn.
    roots = {p.split('/')[0] for p in files}
    if len(roots) == 1 and all('/' in p for p in files):
        files = {p.split('/', 1)[1]: data for p, data in files.items()}
    return files


def properties(data):
    """Garde ordre, doublons et valeurs ; ignore seulement BOM, commentaires et lignes vides."""
    return [tuple(line.split('=', 1)) for line in data.decode('utf-8-sig').splitlines()
            if '=' in line and not line.lstrip().startswith('#')]


def entries(props):
    result = {}
    for key, value in props:
        match = re.fullmatch(r'item\.(\d+)\.(.+)', key)
        if match:
            result.setdefault(match[1], {})[match[2]] = value
    return [{'index': int(index), **values} for index, values in result.items()]


def local_references(files):
    """Résout les ressources et la navigation ; sépare les médias externes.

    icon_path non préfixé représente le dossier dans son parent ; les
    propriétés *_children ont une portée héritée distincte.
    """
    directories = {'.'}
    for name in files:
        directories.update(str(p) for p in PurePosixPath(name).parents)
    known = files.keys() | directories
    result = []
    for name, data in sorted(files.items()):
        if PurePosixPath(name).name != 'dune_folder.txt':
            continue
        props = properties(data)
        values = dict(props)
        for key, value in props:
            field = key.rsplit('.', 1)[-1]
            if field not in {'media_url', 'icon_path', 'icon_sel_path', 'background_path'} or not value:
                continue
            prefix = key[:-len(field)]
            if field == 'media_url':
                action = values.get(prefix + 'media_action', '')
                if re.match(r'^[\w+-]+://', value) or value.startswith(('/', '\\')) or action != 'browse':
                    kind, target = 'external_media', ''
                else:
                    kind, target = 'local', posixpath.normpath(posixpath.join(posixpath.dirname(name), value))
            elif key.startswith(('all_children.', 'direct_children.', 'level2_children.')):
                kind, target = 'inherited', ''
            elif re.match(r'^[\w+-]+://', value) or value.startswith('/'):
                kind, target = 'external_asset', ''
            else:
                kind, target = 'local', posixpath.normpath(posixpath.join(posixpath.dirname(name), value))
            result.append(dict(file=name, key=key, value=value, kind=kind,
                               role='playback' if kind == 'external_media' else
                               'navigation' if field == 'media_url' else 'asset', target=target,
                               resolved=target in known if kind == 'local' else None))
    return result


def compare(left, right):
    common = left.keys() & right.keys()
    changed = sorted(p for p in common if left[p] != right[p])
    menus = []
    for name in changed:
        if PurePosixPath(name).name != 'dune_folder.txt':
            continue
        a, b = properties(left[name]), properties(right[name])
        av, bv = dict(a), dict(b)
        keys = sorted(k for k in av.keys() | bv.keys() if av.get(k) != bv.get(k))
        ea, eb = entries(a), entries(b)
        menus.append(dict(file=name, semantic_equal=Counter(a) == Counter(b), property_order_equal=a == b,
                          changed_keys=keys, before_entries=ea, after_entries=eb,
                          before_properties=a, after_properties=b,
                          diff=''.join(difflib.unified_diff(
                              left[name].decode('utf-8-sig').splitlines(True),
                              right[name].decode('utf-8-sig').splitlines(True),
                              fromfile='yaDIS2/' + name, tofile='yaDIS3/' + name))))
    files = [dict(file=name, status=('only_right' if name not in left else
                                      'only_left' if name not in right else
                                      'identical' if left[name] == right[name] else 'different'),
                  left_bytes=len(left[name]) if name in left else None,
                  right_bytes=len(right[name]) if name in right else None,
                  left_sha256=hashlib.sha256(left[name]).hexdigest() if name in left else '',
                  right_sha256=hashlib.sha256(right[name]).hexdigest() if name in right else '')
             for name in sorted(left.keys() | right.keys())]
    return dict(inventory=files, summary=dict(left_files=len(left), right_files=len(right),
                             identical=len(common)-len(changed), different=len(changed),
                             changed_extensions=dict(Counter(PurePosixPath(p).suffix for p in changed))),
                only_left=sorted(left.keys()-right.keys()), only_right=sorted(right.keys()-left.keys()),
                changed=changed, menus=menus,
                left_references=local_references(left), right_references=local_references(right))


def write_reports(report, output):
    output.mkdir(parents=True, exist_ok=True)
    (output/'audit.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    (output/'txt-differences.diff').write_text('\n'.join(m['diff'] for m in report['menus']), encoding='utf-8')
    with (output/'menus.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.writer(stream)
        writer.writerow(['file', 'semantic_equal', 'property_order_equal', 'changed_keys', 'indices_before', 'indices_after'])
        for m in report['menus']:
            writer.writerow([m['file'], m['semantic_equal'], m['property_order_equal'],
                             ';'.join(m['changed_keys']),
                             ';'.join(str(e['index']) for e in m['before_entries']),
                             ';'.join(str(e['index']) for e in m['after_entries'])])
    with (output/'references.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.DictWriter(stream, fieldnames=['side', 'file', 'key', 'value', 'kind', 'role', 'target', 'resolved'])
        writer.writeheader()
        for side in ['left', 'right']:
            writer.writerows(dict(side=side, **r) for r in report[side+'_references'])
    with (output/'inventory.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.DictWriter(stream, fieldnames=['file', 'status', 'left_bytes', 'right_bytes', 'left_sha256', 'right_sha256'])
        writer.writeheader()
        writer.writerows(report['inventory'])
    lines = ['# Comparaison des exports', '', '```json', json.dumps(report['summary'], indent=2), '```', '',
             '| Fichier | Valeurs identiques | Ordre des propriétés identique |', '|---|---|---|']
    lines += [f"| {m['file']} | {m['semantic_equal']} | {m['property_order_equal']} |" for m in report['menus']]
    for side in ['left', 'right']:
        missing = [r for r in report[side+'_references'] if r['resolved'] is False]
        lines += ['', f'## Références locales absentes ({side}) : {len(missing)}', '']
        lines += [f"- `{r['file']}` : `{r['key']}={r['value']}` → `{r['target']}`" for r in missing]
    (output/'audit.md').write_text('\n'.join(lines)+'\n', encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('left')
    parser.add_argument('right')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--check-local-links', action='store_true')
    parser.add_argument('--check-navigation', action='store_true')
    args = parser.parse_args()
    report = compare(inventory(args.left), inventory(args.right))
    report['inputs'] = [dict(path=str(p), sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest())
                        for p in [args.left, args.right] if Path(p).is_file()]
    write_reports(report, args.output)
    print(json.dumps(report['summary'], ensure_ascii=False))
    missing = any(r['resolved'] is False for side in ['left', 'right'] for r in report[side+'_references'])
    broken_navigation = any(r['resolved'] is False and r['role'] == 'navigation'
                            for side in ['left', 'right'] for r in report[side+'_references'])
    return 1 if args.check_local_links and missing or args.check_navigation and broken_navigation else 0


if __name__ == '__main__':
    raise SystemExit(main())
