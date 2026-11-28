"""Prepare logo-free and authority-compatible bases from the official static PDFs."""
import hashlib
import io
import json
from pathlib import Path

import pymupdf as pdf
from PIL import Image

RESOURCES = Path(__file__).resolve().parents[2] / "backend/Resources/Documents"
OUT = RESOURCES / "branding"
OUT.mkdir(exist_ok=True)

HEADERS = {
    "ss-application": [(30, 184), (194, 272)],
    "ss-commitment": [(46, 221), (233, 323)],
    "ss-report": [(45, 220), (232, 322)],
    "ss-supervisor-evaluation": [(59, 234), (245, 336)],
    "ss-self-evaluation": [(59, 234), (246, 336)],
    "ss-activity": [(57, 232), (244, 333)],
    "ss-termination": [(30, 205), (217, 307), (339, 383), (429, 548)],
    "pp-evaluation": [(12, 186), (201, 291), (295, 382), (399, 442), (456, 575)],
}
FOOTER = [(66, 116), (118, 156), (157, 193), (194, 230), (231, 265), (266, 305), (307, 343), (344, 400), (429, 577)]
PP_LOGO_IMAGES = {"Image5", "Image12", "Image14", "Image16", "Image18", "Image20", "Image21",
                  "Image22", "Image23", "Image25", "Image26", "Image27"}

AUTHORITIES = {
    "ss-termination": [
        ("institute_director", "name", "C. ", 63.86, 545.8, 177.26, "left"),
        ("institute_director", "position", None, 63.86, 545.8, 192.74, "left"),
        ("outreach_division_head", "name", "Atención: ", 63.86, 545.8, 239.3, "right"),
        ("outreach_division_head", "position", None, 63.86, 545.8, 254.81, "right"),
    ],
    "ss-acceptance-example": [
        ("outreach_division_head", "name", None, 63.86, 545.8, 273.41, "left"),
        ("outreach_division_head", "position", None, 63.86, 545.8, 288.89, "left"),
    ],
}


def remove_names(document, key):
    page = document[0]
    fields = []
    for role, part, prefix, x, right, baseline, align in AUTHORITIES.get(key, []):
        page.add_redact_annot(pdf.Rect(62.5, baseline - 11.3, 551, baseline + 2.5), fill=False)
        fields.append(dict(role=role, part=part, prefix=prefix, placement=dict(
            key=f"{role}.{part}", x=x, y=baseline-11.5, width=right-x, height=14,
            fontSize=11, minFontSize=8, alignment=align,
            rows=[dict(x=x, width=right-x, baseline=baseline)],
        )))
    if fields:
        page.apply_redactions(images=0, graphics=0, text=0)
        text = page.get_text().upper()
        assert all(value not in text for value in ("MENDOZA RIOJAS", "HERNÁNDEZ GUERRERO")), key
    return fields


def save(document, name):
    document.save(OUT / name, garbage=4, deflate=True)
    return name


def prepare():
    entries = {}
    for key in sorted(HEADERS.keys() | AUTHORITIES.keys()):
        base = RESOURCES / "templates" / (key + ".pdf")
        zones = []
        with pdf.open(base) as document:
            fields = remove_names(document, key)
            compatibility = save(document, key + "-authority.pdf") if fields else None
            for page in document:
                marks = [(a, 7, b, 57 if key == "ss-report" else 61) for a, b in HEADERS.get(key, [])]
                if key == "pp-evaluation":
                    marks += [(a, 728, b, 792) for a, b in FOOTER]
                for index, mark in enumerate(marks):
                    rect = pdf.Rect(mark)
                    rendered = page.get_pixmap(clip=rect, matrix=pdf.Matrix(4, 4), alpha=True)
                    image = Image.open(io.BytesIO(rendered.tobytes("png"))).convert("RGBA")
                    output = io.BytesIO()
                    image.save(output, format="PNG", optimize=True)
                    png = output.getvalue()
                    filename = hashlib.sha256(png).hexdigest()[:16] + ".png"
                    (OUT / filename).write_bytes(png)
                    head_labels = {
                        "ss-termination": ["Educación", "TecNM", "Coahuila", "SEDU"],
                        "pp-evaluation": ["Educación", "TecNM", "ITSM", "Coahuila", "SEDU"],
                    }.get(key, ["Educación", "TecNM"])
                    label = head_labels[index] if index < len(head_labels) else f"Marca de pie {index - len(head_labels) + 1}"
                    zones.append(dict(name=label, page=page.number, x=rect.x0, y=rect.y0,
                                      width=rect.width, height=rect.height, png=filename))
            if key == "pp-evaluation":
                # Other large image resources contain instructions and must stay untouched.
                logos = {image[0] for page in document for image in page.get_images() if image[7] in PP_LOGO_IMAGES}
                assert len(logos) == len(PP_LOGO_IMAGES)
                for xref in logos:
                    document[0].delete_image(xref)
            for page in document:
                marks = [(z["x"], z["y"], z["x"]+z["width"], z["y"]+z["height"]) for z in zones if z["page"] == page.number]
                if marks:
                    for mark in marks:
                        page.add_redact_annot(pdf.Rect(mark), fill=False)
                    page.apply_redactions(images=0 if key == "pp-evaluation" else 2, graphics=1, text=0)
                    for mark in marks:
                        cleaned = page.get_pixmap(clip=pdf.Rect(mark), alpha=False)
                        assert min(cleaned.samples) >= 248, (key, page.number, mark)
            branded = save(document, key + "-clean.pdf") if zones else compatibility
        entries[key] = dict(sha256=hashlib.sha256(base.read_bytes()).hexdigest(), file=branded,
                            authorityFile=compatibility, authorityFields=fields, zones=zones)
    (OUT / "manifest.json").write_text(json.dumps(entries, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    prepare()
