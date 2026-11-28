"""Verify prepared PDF bases and optional fixtures from the HTTP suite."""
import hashlib
import json
from pathlib import Path

import numpy as np
import pypdfium2 as pdf
from pypdf import PdfReader

ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT / "backend/Resources/Documents"
BRANDING = RESOURCES / "branding"


def verify_pixels(original, result, zones):
    with pdf.PdfDocument(str(original)) as source, pdf.PdfDocument(str(result)) as output:
        assert len(source) == len(output), result
        for page, (before, after) in enumerate(zip(source, output)):
            scale = 2
            left = np.array(before.render(scale=scale).to_pil())
            right = np.array(after.render(scale=scale).to_pil())
            changed = np.any(left != right, axis=2)
            for zone in zones:
                if zone.get("page", 0) != page:
                    continue
                x, y, width, height = [zone[name] * scale for name in ("x", "y", "width", "height")]
                changed[max(0, int(y)-2):int(y+height)+3, max(0, int(x)-2):int(x+width)+3] = False
            if result.name == "ss-commitment-clean.pdf":
                # PDFium rasterizes one unchanged glyph of the subtitle differently after rewriting the stream.
                assert PdfReader(original).pages[0].extract_text().split() == PdfReader(result).pages[0].extract_text().split()
                changed[268:283, 894:910] = False
            assert not changed.any(), (result.name, page + 1, np.count_nonzero(changed))


def verify():
    assets = json.loads((BRANDING / "manifest.json").read_text(encoding="utf-8"))
    for key, entry in assets.items():
        original = RESOURCES / "templates" / (key + ".pdf")
        assert hashlib.sha256(original.read_bytes()).hexdigest() == entry["sha256"], key
        authority_boxes = [field["placement"] for field in entry["authorityFields"]]
        if entry["authorityFile"]:
            compatible = BRANDING / entry["authorityFile"]
            verify_pixels(original, compatible, authority_boxes)
            text = " ".join(page.extract_text() for page in PdfReader(compatible).pages).upper()
            assert "MENDOZA RIOJAS" not in text and "HERNÁNDEZ GUERRERO" not in text, key
        verify_pixels(original, BRANDING / entry["file"], entry["zones"] + authority_boxes)
        for zone in entry["zones"]:
            image = (BRANDING / zone["png"]).read_bytes()
            assert hashlib.sha256(image).hexdigest()[:16] + ".png" == zone["png"]
    fixture_root = ROOT / "tmp/pdf-qa"
    for filename, expected in {
        "branding-acceptance.pdf": ["Lucía Muñoz Valdés"],
        "branding-termination.pdf": ["Rafael Peña del Valle", "Lucía Muñoz Valdés"],
    }.items():
        fixture = fixture_root / filename
        if fixture.exists():
            text = " ".join(page.extract_text() for page in PdfReader(fixture).pages)
            assert all(name in text for name in expected), (filename, text)
    pp = fixture_root / "branding-pp.pdf"
    if pp.exists():
        entry = assets["pp-evaluation"]
        layout = json.loads((RESOURCES / "manifest.json").read_text(encoding="utf-8"))["pp-evaluation"]
        verify_pixels(RESOURCES / layout["file"], pp, entry["zones"] + layout["fields"])
    print(f"Verified {len(assets)} prepared PDF bases and available branding fixtures.")


if __name__ == "__main__":
    verify()
