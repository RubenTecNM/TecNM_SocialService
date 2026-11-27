"""Validate prepared resources and optional generated PP evaluation, without editing sources."""
from pathlib import Path
import hashlib
import json
import math
import sys
import numpy as np
import pypdfium2 as pdfium
from pypdf import PdfReader
from pypdf.generic import ContentStream

ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT / 'backend/Resources/Documents'

CALIBRATED = {
    'ss-application': {'activities': (446.11, 460.5, 474.9), 'name': 184.1, 'mark': (404.1, 580.4)},
    'ss-report': {'activities': (359.93, 377.7, 395.6, 413.3), 'name': 210.38, 'mark': (242.75, 242.78)},
}


def verify_calibrated(key, sample, entry, long_sample):
    reader = PdfReader(sample)
    page = reader.pages[0]
    overlay = ContentStream(page['/Contents'][-1].get_object(), reader)
    placements = []
    x = y = size = 0
    for operands, operator in overlay.operations:
        if operator == b'BT':
            x = y = 0
        elif operator == b'Tf':
            size = float(operands[1])
        elif operator == b'Td':
            x += float(operands[0])
            y += float(operands[1])
        elif operator == b'Tj':
            placements.append((x, float(page.mediabox.height) - y, size))

    rows = [row for field in entry['fields'] for row in field['rows']]
    assert placements and all(any(r['x'] - .5 <= x <= r['x'] + r['width'] + .5
                                  and abs(r['baseline'] - y) <= .5 for r in rows)
                              for x, y, _ in placements), f'{sample.name}: text outside mapped baselines'
    expected = CALIBRATED[key]
    activity_start, activity_end = (430, 476) if key == 'ss-application' else (350, 430)
    actual = [y for _, y, _ in placements if activity_start < y < activity_end]
    target = expected['activities'] if long_sample else expected['activities'][:1]
    assert len(actual) == len(target) and np.allclose(actual, target, atol=.5), f'{sample.name}: activity rows'
    if key == 'ss-application':
        assert any(abs(x - 164.5) <= .5 and abs(y - expected['name']) <= .5
                   and size == (7 if long_sample else 10) for x, y, size in placements)
    else:
        supervisor = [y for _, y, _ in placements if y > 550]
        target = (580.9, 591.7, 602.5, 613.3) if long_sample else (591.7, 602.5, 613.3)
        assert len(supervisor) == len(target) and np.allclose(supervisor, target, atol=.5), f'{sample.name}: supervisor rows'
        if long_sample:
            assert not any(265 <= x < 395 and abs(y - expected['name']) <= .5 for x, y, _ in placements)
            assert 'septiembre' in page.extract_text()

    scale = 3
    with pdfium.PdfDocument(str(sample)) as result, pdfium.PdfDocument(str(RESOURCES / entry['file'])) as base:
        actual = np.array(result[0].render(scale=scale).to_pil())
        original = np.array(base[0].render(scale=scale).to_pil())
    changed = np.any(actual != original, axis=2)
    mask = np.zeros(changed.shape, dtype=bool)
    for field in entry['fields']:
        for row in field['rows']:
            x, w, y = [row[k] for k in ('x', 'width', 'baseline')]
            top = max(field['y'], y - field.get('fontSize', entry['fontSize']))
            mask[math.floor(top * scale):math.ceil((y + 2.3) * scale),
                 math.floor((x - .5) * scale):math.ceil((x + w + .5) * scale)] = True
    assert not np.any(changed & ~mask), f'{sample.name}: ink outside the calibrated rows'
    mark_x, mark_y = expected['mark']
    top, bottom = (mark_y - 6, mark_y + 6)
    left, right = (mark_x - 6, mark_x + 6)
    marks_y, marks_x = np.nonzero(changed[round(top * scale):round(bottom * scale),
                                          round(left * scale):round(right * scale)])
    assert len(marks_x) > 0, f'{sample.name}: missing mark'
    for indices, offset, center in [(marks_x, left, mark_x), (marks_y, top, mark_y)]:
        observed = (indices.min() + indices.max() + 1) / (2 * scale) + round(offset * scale) / scale
        assert abs(observed - center) <= 1, f'{sample.name}: mark off center'


def verify():
    manifest = json.loads((RESOURCES / 'manifest.json').read_text(encoding='utf-8'))
    for key, entry in manifest.items():
        path = RESOURCES / entry['file']
        assert hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256'], key
        reader = PdfReader(path)
        assert len(reader.pages) == entry['pages'], key
        assert len(entry['pageSizes']) == len(reader.pages), key
        for page, size in zip(reader.pages, entry['pageSizes']):
            assert math.isclose(float(page.mediabox.width), size['width'], abs_tol=0.01), key
            assert math.isclose(float(page.mediabox.height), size['height'], abs_tol=0.01), key
        for field in entry['fields']:
            assert 0 <= field.get('page', 0) < len(reader.pages), key
            size = entry['pageSizes'][field.get('page', 0)]
            assert all(math.isfinite(field[n]) for n in ['x', 'y', 'width', 'height']), key
            assert field['x'] >= 0 and field['y'] >= 0 and field['width'] > 0 and field['height'] > 0, key
            assert field['x'] + field['width'] <= size['width'] and field['y'] + field['height'] <= size['height'], key
            assert 0 < field.get('minFontSize', entry.get('minFontSize', 7)) <= field.get('fontSize', entry.get('fontSize', 9)), key
            if 'rows' in field:
                assert field['rows'], key
                baseline = field['y']
                for row in field['rows']:
                    assert all(math.isfinite(row[n]) for n in ['x', 'width', 'baseline']), key
                    assert field['x'] <= row['x'] < row['x'] + row['width'] <= field['x'] + field['width'] + .001, key
                    assert baseline < row['baseline'] < field['y'] + field['height'], key
                    baseline = row['baseline']
        assert not reader.get_fields(), key
        assert '/AcroForm' not in reader.trailer['/Root'], key
        assert all(not p.get('/Annots') for p in reader.pages), key
    application = (RESOURCES / manifest['ss-application']['file']).read_bytes()
    for secret in [b'Hector Gabriel', b'8662584004', b'I23050363', b'Heroes del 47']:
        assert secret not in application, 'Original field data leaked'
    for key in ['ss-application', 'ss-commitment', 'ss-report', 'ss-activity', 'ss-supervisor-evaluation', 'ss-self-evaluation', 'ss-termination', 'ss-individual-act']:
        sample = ROOT / 'tmp/pdf-qa' / f'{key}.pdf'
        if not sample.exists(): continue
        entry = manifest[key]
        with pdfium.PdfDocument(str(sample)) as result, pdfium.PdfDocument(str(RESOURCES / entry['file'])) as base:
            actual = np.array(result[0].render(scale=1).to_pil())
            expected = np.array(base[0].render(scale=1).to_pil())
            mask = np.zeros(actual.shape[:2], dtype=bool)
            for field in entry['fields']:
                x, y, w, h = [field[k] for k in ['x', 'y', 'width', 'height']]
                mask[max(0,int(y)-3):int(y+h)+4, max(0,int(x)-3):int(x+w)+4] = True
            assert np.array_equal(actual[~mask], expected[~mask]), f'{key}: changes outside authorized fields'
            text = PdfReader(sample).pages[0].extract_text()
            if key in ['ss-report', 'ss-supervisor-evaluation']:
                assert 'Ana Gómez Díaz' in text and 'Coordinadora' in text, key
            if key in ['ss-supervisor-evaluation', 'ss-self-evaluation', 'ss-termination']:
                assert 'María José Muñoz Peña' in text and 'Apoyo tecnológico' in text, key
            if key == 'ss-termination':
                assert '480' in text and 'noviembre' in text and 'Ana Gómez Díaz' in text, key
            result[0].render(scale=1.5).to_pil().save(sample.with_suffix('.png'))
        if key in CALIBRATED:
            verify_calibrated(key, sample, entry, long_sample=False)
            long_sample = sample.with_stem(f'{key}-long')
            assert long_sample.exists(), f'{key}: generate the long sample with backend.Tests'
            verify_calibrated(key, long_sample, entry, long_sample=True)
    if len(sys.argv) > 1:
        generated = Path(sys.argv[1])
        reference = RESOURCES / manifest['pp-evaluation']['file']
        with pdfium.PdfDocument(str(generated)) as output, pdfium.PdfDocument(str(reference)) as base:
            assert len(output) == len(base) == 2
            assert output[0].get_size() == base[0].get_size()
            assert np.array_equal(np.array(output[1].render(scale=1).to_pil()), np.array(base[1].render(scale=1).to_pil())), 'Instruction page changed'
            out = ROOT / 'tmp/pdf-qa'
            out.mkdir(parents=True, exist_ok=True)
            output[0].render(scale=1.5).to_pil().save(out / 'pp-evaluation.png')
        text = PdfReader(generated).pages[0].extract_text()
        assert 'Muñoz' in text and '12/01/2026' in text, 'Missing generated data'
        # The generation map must never touch grades or signature areas.
        assert all(f['y'] + f['height'] < 240 for f in manifest['pp-evaluation']['fields'])
    print(f'Verified {len(manifest)} templates, privacy, PDF structure and available samples.')


if __name__ == '__main__':
    verify()
