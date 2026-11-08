#!/usr/bin/env python3
"""Add/update a source string and Simplified Chinese translation in both catalogs."""
import argparse
import hashlib
import json
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('english')
parser.add_argument('--zh', required=True, help='Simplified Chinese translation')
args = parser.parse_args()
key = 'S' + hashlib.sha256(args.english.encode()).hexdigest()[:12]
root = Path(__file__).resolve().parents[1] / 'src/InfoPanel.App/Localization'
for locale, value in [('en', args.english), ('zh-CN', args.zh)]:
    path = root / (locale + '.json')
    catalog = json.loads(path.read_text(encoding='utf-8'))
    catalog[key] = value
    path.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'XAML: {{loc:Tr {key}}}')
print('C#: Localizer.Text(' + json.dumps(args.english, ensure_ascii=False) + ')')
