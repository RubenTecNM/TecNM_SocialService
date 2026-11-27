"""Prepare distributable static templates; never modify source documents."""
from pathlib import Path
import hashlib
import json
from pypdf import PdfReader, PdfWriter

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / 'backend/Resources/Documents'
SS = ROOT / 'docs/sources/servicio-social/formatos'
PP = ROOT / 'docs/sources/practicas-profesionales/formatos'


def prepare():
    entries = {}
    sources = {
        'ss-application': (SS, '1.*pdf'),
        'ss-commitment': (SS, '2.*pdf'),
        'ss-report': (SS, '4.*1.pdf'),
        'ss-supervisor-evaluation': (SS, '5.*pdf'),
        'ss-self-evaluation': (SS, '6.*pdf'),
        'ss-termination': (SS, '7.*pdf'),
        'ss-activity': (SS, '8.*pdf'),
        'pp-evaluation': (PP, 'ANEXO*.pdf'),
    }
    mapping = {'Nombre Completo': 'studentFullName', 'Teléfono': 'studentPhone', 'Domicilio': 'studentAddress',
               'No Control': 'studentControlNumber', 'Semestre': 'studentSemester', 'Titular de la Dependencia': 'dependencyHeadName',
               'Puesto de la Dependencia': 'dependencyHeadPosition', 'Programa de Actividades': 'activitiesOutline',
               'Dependencia': 'dependencyName', 'Carrera': 'careerName', 'Fecha': 'generatedAt', 'Genero': 'studentGender',
               'Periodo': 'periodCode', 'Programa': 'programName', 'Modalidad': 'modality',
               'Fecha_Inicio_af_date': 'startDate', 'Fecha_Fin_af_date': 'endDate'}
    for key, (directory, pattern) in sources.items():
        source = next(directory.glob(pattern))
        reader = PdfReader(source)
        fields = []
        if key == 'ss-application':
            for reference in reader.pages[0].get('/Annots', []):
                widget = reference.get_object()
                name = widget.get('/T')
                if name in mapping:
                    x0, y0, x1, y1 = map(float, widget['/Rect'])
                    fields.append(dict(key=mapping[name], x=x0, y=float(reader.pages[0].mediabox.height)-y1,
                                       width=x1-x0, height=y1-y0, fontSize=10, minFontSize=7))
        writer = PdfWriter()
        for page in reader.pages:
            # Exclude widgets before cloning: orphaned /V objects must never enter output.
            writer.add_page(page, excluded_keys=['/Annots', '/AA', '/Metadata', '/PieceInfo'])
        writer.add_metadata({})
        target = DEST / 'templates' / f'{key}.pdf'
        target.parent.mkdir(parents=True, exist_ok=True)
        writer.write(target)
        entries[key] = dict(file=f'templates/{key}.pdf', sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
                            version='1', font='Arial' if key == 'ss-application' else 'Century Gothic', fields=fields,
                              pages=len(reader.pages))
    layout = DEST / 'manifest.json'
    # Hand-calibrated layouts are maintained separately from template preparation.
    previous = json.loads(layout.read_text(encoding='utf-8')) if layout.exists() else {}
    for key, value in entries.items():
        if key in previous: value['fields'] = previous[key]['fields']
    for key, value in previous.items():
        if key not in entries:
            entries[key] = value
        else:
            for name in ['version', 'font', 'fontSize', 'minFontSize', 'institutional', 'stamps']:
                if name in value: entries[key][name] = value[name]
    for entry in entries.values():
        prepared = PdfReader(DEST / entry['file'])
        entry['pageSizes'] = [dict(width=float(p.mediabox.width), height=float(p.mediabox.height)) for p in prepared.pages]
    layout.write_text(json.dumps(entries, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'Prepared {len(entries)} static templates.')


if __name__ == '__main__':
    prepare()
