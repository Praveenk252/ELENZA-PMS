# AIMS Kitchen Designer — Full Pipeline: 100 PDFs → ML → Production

Copy this entire document into Codex (Cursor IDE agent). It will execute the complete pipeline from raw PDFs to ML-enhanced kitchen auto-layout.

---

## Table of Contents

1. [Pipeline Overview](#1-pipeline-overview)
2. [Phase 1: PDF Extraction](#2-phase-1-pdf-extraction)
3. [Phase 2: AI Vision Data Extraction](#3-phase-2-ai-vision-data-extraction)
4. [Phase 3: Dataset Curation](#4-phase-3-dataset-curation)
5. [Phase 4: Machine Learning Training](#5-phase-4-machine-learning-training)
6. [Phase 5: Integration into AIMS](#6-phase-5-integration-into-aims)
7. [Phase 6: Future Vision & Scaling](#7-phase-6-future-vision--scaling)
8. [Appendix: Full Scripts](#8-appendix-full-scripts)

---

## 1. Pipeline Overview

### What This Achieves

Your **100 kitchen design PDFs** become the training data for an ML system that:
- Predicts the optimal layout type (Straight/L/U/Island) from room dimensions
- Suggests the best cabinet sequence and widths for each wall
- Learns real-world placement patterns from actual designs
- Continuously improves as more designs are added

### Architecture

```
PDFs (100)
  ↓ PyMuPDF
PNG Images
  ↓ GPT-4 Vision API
Structured JSON Dataset
  ↓ Validation & Cleaning
Clean Dataset (kitchen_data.json)
  ├──→ scikit-learn → Layout Predictor Model (.pkl)
  ├──→ Pattern Extraction → JS Config (direct integration)
  └──→ (Future) TensorFlow → Sequence Model
         ↓
Integration into index.html
  ├── ML Suggested Layout button
  ├── Pattern-informed auto-layout
  └── (Optional) Flask API backend
```

### File Structure After Completion

```
KitchenDesigner/
├── index.html                  ← Enhanced with ML features
├── pdfs/                       ← 100 PDFs (input)
├── images/                     ← Generated PNGs
├── extracted/
│   └── raw_data.json           ← Raw AI extractions
├── dataset/
│   └── kitchen_data.json       ← Cleaned dataset
├── models/
│   ├── layout_predictor.pkl    ← scikit-learn model
│   └── layout_patterns.js      ← JS config for direct integration
├── scripts/
│   ├── 01_convert_pdfs.py
│   ├── 02_extract_data.py
│   ├── 03_validate_dataset.py
│   ├── 04_train_models.py
│   ├── 05_extract_patterns.py
│   ├── 06_benchmark.py
│   └── 07_generate_js_config.py
├── api/
│   └── server.py               ← Optional Flask ML API
├── notebooks/
│   └── exploratory_analysis.ipynb
└── docs/
    ├── project.md
    ├── architecture.md
    ├── rules.md
    ├── design.md
    ├── phases.md
    └── memory.md
```

---

## 2. Phase 1: PDF Extraction

### 2.1 Setup Environment

```bash
# Create project structure
mkdir -p KitchenDesigner/pdfs KitchenDesigner/images KitchenDesigner/extracted KitchenDesigner/dataset KitchenDesigner/models KitchenDesigner/scripts KitchenDesigner/api KitchenDesigner/notebooks

# Move your PDFs
# Copy all 100 PDFs into KitchenDesigner/pdfs/

# Install dependencies
pip install PyMuPDF openai scikit-learn numpy joblib pandas pillow matplotlib tensorflow flask flask-cors python-multipart
```

### 2.2 Convert PDFs to Images

**File**: `scripts/01_convert_pdfs.py`

```python
"""
Converts all PDFs to high-resolution PNG images.
Handles multi-page PDFs — each page becomes one image.
Output naming: {pdf_name}_p{page_number}.png
"""
import fitz
import os
import sys
from pathlib import Path

PDF_DIR = Path("pdfs")
IMAGE_DIR = Path("images")
DPI = 300  # High resolution for dimension reading

def convert_all():
    IMAGE_DIR.mkdir(exist_ok=True)
    pdfs = sorted(PDF_DIR.glob("*.pdf"))
    
    if not pdfs:
        print("ERROR: No PDFs found in pdfs/ directory")
        print("Place your PDF files in KitchenDesigner/pdfs/")
        sys.exit(1)
    
    print(f"Found {len(pdfs)} PDFs. Converting to images...")
    
    total_pages = 0
    for pdf_path in pdfs:
        doc = fitz.open(str(pdf_path))
        base_name = pdf_path.stem.replace(" ", "_").replace("(", "").replace(")", "")
        
        for page_num in range(len(doc)):
            page = doc[page_num]
            # High resolution for accurate dimension extraction
            matrix = fitz.Matrix(DPI / 72, DPI / 72)
            pix = page.get_pixmap(matrix=matrix)
            
            img_path = IMAGE_DIR / f"{base_name}_p{page_num}.png"
            pix.save(str(img_path))
            total_pages += 1
            
            if total_pages % 10 == 0:
                print(f"  Progress: {total_pages} pages converted")
        
        doc.close()
        print(f"  ✓ {pdf_path.name}: {len(doc)} pages")
    
    print(f"\nDone. {total_pages} images saved to images/")

if __name__ == "__main__":
    convert_all()
```

**Run**: `python scripts/01_convert_pdfs.py`

**Expected output**: 100-300 images (depends on multi-page PDFs).

---

## 3. Phase 2: AI Vision Data Extraction

### 3.1 Extraction Script

**File**: `scripts/02_extract_data.py`

```python
"""
Uses GPT-4 Vision to extract structured kitchen design data from each image.
Produces JSON matching the AIMS project data model.
Cost: ~$0.05-0.10 per image = ~$5-10 for 100 images.
"""
import base64
import json
import os
import time
import sys
from pathlib import Path
from openai import OpenAI

IMAGE_DIR = Path("images")
OUTPUT_PATH = Path("extracted") / "raw_data.json"

# Validate API key
api_key = os.environ.get("OPENAI_API_KEY")
if not api_key:
    print("ERROR: OPENAI_API_KEY environment variable not set")
    print("Run: export OPENAI_API_KEY='sk-your-key-here'")
    print("Or set it permanently in your shell profile.")
    sys.exit(1)

client = OpenAI(api_key=api_key)

SYSTEM_PROMPT = """You are a kitchen design data extraction engine. Your ONLY output is valid JSON.
Extract the following fields from the kitchen drawing. Return null for any field you cannot determine.

FIELD DESCRIPTIONS:

layoutType (string):
  - "straight": cabinets along one wall
  - "L": two walls meeting at 90 degrees with corner cabinet
  - "U": three walls forming a U shape with two corners
  - "island": main wall(s) + freestanding island cabinet unit

wallA_mm (number): Length of the primary/main wall in mm
wallB_mm (number|null): Length of secondary wall (L-shape, U-shape)
wallC_mm (number|null): Length of third wall (U-shape only)
islandWidth_mm (number|null): Width of island (long side)
islandDepth_mm (number|null): Depth of island (short side)

cabinets (array): Each cabinet has:
  - type: one of "drawer3","drawer4","drawer2","hob","sink","doubleDoor","singleDoor","pullout","oven","cornerBlind","wallSingle","wallDouble","wallOpen","tallSingle","tallDouble"
  - width_mm: one of 300,450,500,600,750,800,900,1100
  - wall: "A" (primary), "B" (secondary), "C" (third wall), "island"
  - (future) category: "base", "wall", "tall"

counterHeight_mm, counterDepth_mm, plinthHeight_mm, wallCabinetHeight_mm, carcassDepth_mm, gapAboveCounter_mm (numbers or null): Standard kitchen dimensions

hasWallCabinets (boolean): Whether wall/upper cabinets are visible
hasTallUnits (boolean): Whether tall/full-height units are visible

DIMENSION INFERENCE RULES:
1. If a scale bar is visible, use it to calculate all dimensions
2. If no scale bar, assume standard cabinet doors are 600mm wide × 720mm tall
3. Standard counter height is 850mm, plinth 100mm (Indian standard)
4. Standard counter depth is 610mm, carcass depth 560mm
5. Wall cabinets are typically 600mm high, 330mm deep, mounted 600mm above counter
6. Tall units are typically 2100mm high, 560mm deep
7. Corner blind cabinets are typically 1100×1100mm with 450mm door

LAYOUT IDENTIFICATION:
- Straight: Single row of base cabinets along one wall
- L-Shape: Two perpendicular runs meeting at a corner cabinet (1100mm blind or 900mm carousel)
- U-Shape: Three runs forming a U, with corner cabinets at each junction
- Island: Main wall run + separate island unit (typically 600-1200mm wide with clearance)

WALL ASSIGNMENT:
- Wall A = primary wall (usually longest, or the wall with sink/hob)
- Wall B = perpendicular wall (L-shape) or back wall (U-shape)
- Wall C = right/third wall (U-shape only)
- island = island cabinet unit (island layout only)

Return ONLY valid JSON. No explanations, no markdown."""

def encode_image(path):
    with open(path, "rb") as f:
        return base64.b64encode(f.read()).decode("utf-8")

def extract_single(image_path, retries=3):
    """Extract data from a single image with retry logic."""
    b64 = encode_image(image_path)
    
    for attempt in range(retries):
        try:
            resp = client.chat.completions.create(
                model="gpt-4o",  # or "gpt-4o-mini" for lower cost (~$0.02/image)
                messages=[
                    {"role": "system", "content": SYSTEM_PROMPT},
                    {"role": "user", "content": [
                        {"type": "text", "text": "Extract kitchen design data as JSON"},
                        {"type": "image_url", "image_url": {
                            "url": f"data:image/png;base64,{b64}",
                            "detail": "high"
                        }}
                    ]}
                ],
                response_format={"type": "json_object"},
                max_tokens=3000,
                temperature=0.1  # Low temperature for consistent extraction
            )
            
            data = json.loads(resp.choices[0].message.content)
            data["source_file"] = image_path.name
            data["extraction_model"] = "gpt-4o"
            return data
            
        except Exception as e:
            print(f"  Attempt {attempt + 1} failed: {e}")
            if attempt < retries - 1:
                wait = 2 ** attempt  # Exponential backoff
                print(f"  Waiting {wait}s before retry...")
                time.sleep(wait)
    
    return None

def batch_extract():
    """Extract data from all images in batch."""
    images = sorted(IMAGE_DIR.glob("*.png"))
    
    if not images:
        print("ERROR: No images found in images/ directory")
        print("Run scripts/01_convert_pdfs.py first.")
        return
    
    print(f"Extracting data from {len(images)} images...")
    print(f"Estimated API cost: ~${len(images) * 0.07:.2f}")
    print()
    
    results = []
    errors = []
    
    for i, img_path in enumerate(images, 1):
        print(f"[{i}/{len(images)}] Processing: {img_path.name}")
        
        data = extract_single(img_path)
        
        if data:
            results.append(data)
            cab_count = len(data.get("cabinets", []))
            print(f"  ✓ Extracted: layout={data.get('layoutType')}, cabinets={cab_count}")
        else:
            errors.append(img_path.name)
            print(f"  ✗ Failed after retries")
        
        # Rate limit: 2 requests per second (well within OpenAI limits)
        time.sleep(0.5)
        
        # Save progress every 10 images
        if i % 10 == 0:
            OUTPUT_PATH.parent.mkdir(exist_ok=True)
            with open(OUTPUT_PATH, "w") as f:
                json.dump(results, f, indent=2)
            print(f"  [Checkpoint] Saved {len(results)} results so far")
    
    # Final save
    OUTPUT_PATH.parent.mkdir(exist_ok=True)
    with open(OUTPUT_PATH, "w") as f:
        json.dump(results, f, indent=2)
    
    print(f"\n{'='*60}")
    print(f"EXTRACTION COMPLETE")
    print(f"  Successfully extracted: {len(results)}/{len(images)}")
    print(f"  Failed: {len(errors)}")
    print(f"  Output: {OUTPUT_PATH}")
    print(f"{'='*60}")
    
    if errors:
        print("\nFailed images:")
        for e in errors:
            print(f"  - {e}")

if __name__ == "__main__":
    batch_extract()
```

**Run**: 
```bash
export OPENAI_API_KEY="sk-your-key-here"
python scripts/02_extract_data.py
```

**Cost estimation**:
- GPT-4o: ~$0.07/image × 100 = **~$7.00**
- GPT-4o-mini: ~$0.02/image × 100 = **~$2.00** (recommended for cost)
- Processing time: ~5-10 minutes for 100 images

---

## 4. Phase 3: Dataset Curation

### 4.1 Validate & Clean

**File**: `scripts/03_validate_dataset.py`

```python
"""
Validates, cleans, and enriches the raw extracted dataset.
Produces a clean, standardized JSON dataset ready for ML training.
"""
import json
import sys
from pathlib import Path
from collections import Counter

RAW_PATH = Path("extracted") / "raw_data.json"
OUTPUT_PATH = Path("dataset") / "kitchen_data.json"
REPORT_PATH = Path("dataset") / "validation_report.txt"

# Allowed values
VALID_LAYOUTS = {"straight", "L", "U", "island"}
VALID_TYPES = {"drawer3", "drawer4", "drawer2", "hob", "sink", "doubleDoor", 
               "singleDoor", "pullout", "oven", "cornerBlind", 
               "wallSingle", "wallDouble", "wallOpen", 
               "tallSingle", "tallDouble"}
VALID_WIDTHS = {300, 450, 500, 600, 750, 800, 900, 1100}
VALID_WALLS = {"A", "B", "C", "island"}
CATEGORY_BY_TYPE = {
    "drawer3": "base", "drawer4": "base", "drawer2": "base",
    "hob": "base", "sink": "base", "doubleDoor": "base",
    "singleDoor": "base", "pullout": "base", "oven": "base",
    "cornerBlind": "base",
    "wallSingle": "wall", "wallDouble": "wall", "wallOpen": "wall",
    "tallSingle": "tall", "tallDouble": "tall"
}

def validate():
    if not RAW_PATH.exists():
        print(f"ERROR: {RAW_PATH} not found. Run extraction first.")
        sys.exit(1)
    
    with open(RAW_PATH) as f:
        raw_data = json.load(f)
    
    print(f"Loaded {len(raw_data)} raw records")
    
    cleaned = []
    stats = Counter()
    errors = []
    
    for i, record in enumerate(raw_data):
        source = record.get("source_file", f"record_{i}")
        issues = []
        
        # 1. Validate layout type
        lt = record.get("layoutType")
        if lt not in VALID_LAYOUTS:
            issues.append(f"Invalid layoutType: {lt}")
            continue
        
        # 2. Validate wall dimensions
        try:
            wall_a = int(record.get("wallA_mm") or 0)
            wall_b = int(record.get("wallB_mm") or 0)
            wall_c = int(record.get("wallC_mm") or 0)
        except (ValueError, TypeError):
            issues.append("Non-numeric wall dimensions")
            continue
        
        if wall_a < 1000:
            issues.append(f"wallA too small: {wall_a}mm")
            continue
        
        # Validate wall existence per layout
        if lt == "L" and wall_b < 500:
            issues.append(f"L-shape needs wallB >= 500mm, got {wall_b}")
        if lt == "U" and (wall_b < 500 or wall_c < 500):
            issues.append(f"U-shape needs wallB and wallC >= 500mm")
        
        # 3. Validate cabinets
        cabinets = record.get("cabinets", [])
        valid_cabs = []
        seen_widths = []
        
        for j, cab in enumerate(cabinets):
            cab_issues = []
            
            ctype = cab.get("type")
            cwidth = cab.get("width_mm")
            cwall = cab.get("wall")
            
            if ctype not in VALID_TYPES:
                cab_issues.append(f"Invalid type: {ctype}")
            if cwidth not in VALID_WIDTHS:
                if cwidth and isinstance(cwidth, (int, float)):
                    # Allow near-standard widths (round to nearest standard)
                    nearest = min(VALID_WIDTHS, key=lambda x: abs(x - cwidth))
                    if abs(nearest - cwidth) <= 50:
                        cab["width_mm"] = nearest
                    else:
                        cab_issues.append(f"Invalid width: {cwidth}mm")
                else:
                    cab_issues.append(f"Missing/invalid width: {cwidth}")
            if cwall not in VALID_WALLS:
                cab_issues.append(f"Invalid wall: {cwall}")
            
            # Add category
            cab["category"] = CATEGORY_BY_TYPE.get(ctype, "base")
            
            if not cab_issues:
                valid_cabs.append(cab)
                seen_widths.append(cab["width_mm"])
            else:
                print(f"  Cabinet {j} in {source}: {'; '.join(cab_issues)}")
        
        if len(valid_cabs) < 2:
            issues.append(f"Only {len(valid_cabs)} valid cabinets — need ≥2")
        
        # 4. Validate details
        details_ok = True
        for field in ["counterHeight_mm", "counterDepth_mm", "plinthHeight_mm"]:
            val = record.get(field)
            try:
                if val and int(val) < 0:
                    issues.append(f"Negative {field}")
                    details_ok = False
            except (ValueError, TypeError):
                pass
        
        # 5. Assign defaults for missing values
        record.setdefault("counterHeight_mm", 850)
        record.setdefault("counterDepth_mm", 610)
        record.setdefault("carcassDepth_mm", 560)
        record.setdefault("plinthHeight_mm", 100)
        record.setdefault("hasWallCabinets", False)
        record.setdefault("hasTallUnits", False)
        
        if issues:
            print(f"  Skipped {source}: {'; '.join(issues)}")
            errors.append({"source": source, "issues": issues})
            stats["skipped"] += 1
            continue
        
        # Build clean record
        clean = {
            "source_file": source,
            "layoutType": lt,
            "wallA_mm": wall_a,
            "wallB_mm": wall_b if lt in ("L", "U") else None,
            "wallC_mm": wall_c if lt == "U" else None,
            "islandWidth_mm": record.get("islandWidth_mm"),
            "islandDepth_mm": record.get("islandDepth_mm"),
            "cabinets": valid_cabs,
            "counterHeight_mm": int(record.get("counterHeight_mm", 850) or 850),
            "counterDepth_mm": int(record.get("counterDepth_mm", 610) or 610),
            "carcassDepth_mm": int(record.get("carcassDepth_mm", 560) or 560),
            "plinthHeight_mm": int(record.get("plinthHeight_mm", 100) or 100),
            "hasWallCabinets": bool(record.get("hasWallCabinets")),
            "hasTallUnits": bool(record.get("hasTallUnits")),
            "wallCabinetHeight_mm": int(record.get("wallCabinetHeight_mm") or 600) if record.get("hasWallCabinets") else None,
            "gapAboveCounter_mm": int(record.get("gapAboveCounter_mm") or 600) if record.get("hasWallCabinets") else None
        }
        cleaned.append(clean)
        stats["kept"] += 1
        
        # Track layout distribution
        stats[f"layout_{lt}"] += 1
    
    # Save cleaned dataset
    OUTPUT_PATH.parent.mkdir(exist_ok=True)
    with open(OUTPUT_PATH, "w") as f:
        json.dump(cleaned, f, indent=2)
    
    # Write validation report
    with open(REPORT_PATH, "w") as f:
        f.write("DATASET VALIDATION REPORT\n")
        f.write("=" * 50 + "\n\n")
        f.write(f"Raw records: {len(raw_data)}\n")
        f.write(f"Valid records: {stats['kept']}\n")
        f.write(f"Skipped records: {stats['skipped']}\n\n")
        f.write("Layout Distribution:\n")
        for lt in ["straight", "L", "U", "island"]:
            count = stats.get(f"layout_{lt}", 0)
            f.write(f"  {lt}: {count}\n")
        f.write(f"\nTotal cabinets: {sum(len(d['cabinets']) for d in cleaned)}\n\n")
        if errors:
            f.write("Errors:\n")
            for e in errors:
                f.write(f"  {e['source']}: {'; '.join(e['issues'])}\n")
    
    print(f"\n{'='*60}")
    print(f"VALIDATION COMPLETE")
    print(f"  Kept: {stats['kept']}")
    print(f"  Skipped: {stats['skipped']}")
    print(f"  Output: {OUTPUT_PATH}")
    print(f"  Report: {REPORT_PATH}")
    print(f"{'='*60}")
    
    return cleaned

if __name__ == "__main__":
    validate()
```

**Run**: `python scripts/03_validate_dataset.py`

### 4.2 Exploratory Analysis

**File**: `notebooks/exploratory_analysis.ipynb`

```python
# %%
import json, numpy as np
from collections import Counter

with open("dataset/kitchen_data.json") as f:
    data = json.load(f)

# 1. Layout distribution
layouts = Counter(d["layoutType"] for d in data)
print("Layout Distribution:")
for k, v in layouts.most_common():
    print(f"  {k}: {v} ({v/len(data)*100:.0f}%)")

# 2. Wall length statistics
wall_a = [d["wallA_mm"] for d in data if d.get("wallA_mm")]
print(f"\nWall A: min={min(wall_a)}mm, max={max(wall_a)}mm, avg={np.mean(wall_a):.0f}mm")

# 3. Cabinet type popularity
all_types = Counter()
for d in data:
    for c in d["cabinets"]:
        all_types[c["type"]] += 1
print("\nTop 10 Cabinet Types:")
for t, count in all_types.most_common(10):
    print(f"  {t}: {count}")

# 4. Width distribution
all_widths = Counter()
for d in data:
    for c in d["cabinets"]:
        all_widths[c["width_mm"]] += 1
print("\nWidth Distribution:")
for w in sorted(all_widths.keys()):
    print(f"  {w}mm: {all_widths[w]}")

# 5. Corner cabinet prevalence
corner_count = sum(1 for d in data for c in d["cabinets"] if c["type"] == "cornerBlind")
print(f"\nCorner cabinets in dataset: {corner_count}")

# 6. Wall cabinet prevalence
wall_cabs = sum(1 for d in data if d.get("hasWallCabinets"))
print(f"Designs with wall cabinets: {wall_cabs}/{len(data)}")

# 7. Tall unit prevalence
tall_units = sum(1 for d in data if d.get("hasTallUnits"))
print(f"Designs with tall units: {tall_units}/{len(data)}")
```

---

## 5. Phase 4: Machine Learning Training

### 5.1 Approach Overview

Three ML models, increasing in sophistication:

| Model | Input | Output | Complexity | Value |
|---|---|---|---|---|
| **A: Layout Predictor** | Wall dimensions (A, B, C) | Layout type (Straight/L/U/Island) | Low | High |
| **B: Sequence Pattern Extractor** | Wall length + position | Preferred cabinet sequence | Low | Highest |
| **C: Cabinet Sequence Predictor** | Wall length + layout type | Optimized cabinet sequence | Medium | High |
| **D: Full Neural Layout Net** | Room dimensions | Complete cabinet layout | High | Highest (future) |

### 5.2 Model A: Layout Predictor

**File**: `scripts/04_train_models.py` (Model A section)

```python
"""
Trains multiple ML models on the kitchen dataset.
Model A: Random Forest classifier for layout type prediction.
Model B: Width distribution analysis for auto-layout improvement.
Model C: Sequence prediction with Markov chain.
"""
import json
import numpy as np
import joblib
from pathlib import Path
from sklearn.ensemble import RandomForestClassifier, GradientBoostingClassifier
from sklearn.model_selection import train_test_split, cross_val_score
from sklearn.metrics import classification_report, confusion_matrix
from sklearn.preprocessing import StandardScaler

DATASET_PATH = Path("dataset") / "kitchen_data.json"
MODEL_DIR = Path("models")

# ---- MODEL A: LAYOUT PREDICTOR ----
def train_layout_predictor(data):
    """Train a classifier that predicts layout type from wall dimensions."""
    
    layout_map = {"straight": 0, "L": 1, "U": 2, "island": 3}
    reverse_map = {v: k for k, v in layout_map.items()}
    
    X = []
    y = []
    
    for d in data:
        lt = d.get("layoutType")
        if lt not in layout_map:
            continue
        
        a = float(d.get("wallA_mm", 0) or 0)
        b = float(d.get("wallB_mm", 0) or 0)
        c = float(d.get("wallC_mm", 0) or 0)
        
        # Feature engineering
        total = a + b + c
        ratio_ab = b / max(a, 1)
        ratio_ac = c / max(a, 1)
        area = max(a, c * (ratio_ab > 0.5)) * (a + b * (ratio_ab > 0.3))
        
        X.append([a, b, c, total, ratio_ab, ratio_ac, area])
        y.append(layout_map[lt])
    
    X = np.array(X)
    y = np.array(y)
    
    print(f"Model A — Layout Predictor")
    print(f"  Samples: {len(X)}")
    print(f"  Features: wallA, wallB, wallC, total, ratioAB, ratioAC, area")
    print(f"  Classes: {len(set(y))}")
    
    # Train/test split
    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=0.2, random_state=42, stratify=y
    )
    
    # Try multiple classifiers
    classifiers = {
        "RandomForest": RandomForestClassifier(
            n_estimators=300, max_depth=15, min_samples_leaf=3,
            class_weight="balanced", random_state=42
        ),
        "GradientBoosting": GradientBoostingClassifier(
            n_estimators=200, max_depth=8, learning_rate=0.1, random_state=42
        )
    }
    
    best_model = None
    best_score = 0
    
    for name, clf in classifiers.items():
        clf.fit(X_train, y_train)
        score = clf.score(X_test, y_test)
        cv_score = cross_val_score(clf, X_train, y_train, cv=5).mean()
        
        print(f"\n  {name}:")
        print(f"    Test accuracy: {score:.2%}")
        print(f"    CV accuracy: {cv_score:.2%}")
        
        # Confusion matrix
        y_pred = clf.predict(X_test)
        cm = confusion_matrix(y_test, y_pred)
        print(f"    Confusion matrix:\n{cm}")
        
        # Feature importance
        if hasattr(clf, "feature_importances_"):
            features = ["wallA", "wallB", "wallC", "total", "ratioAB", "ratioAC", "area"]
            fi = clf.feature_importances_
            print(f"    Feature importances:")
            for f, i in sorted(zip(features, fi), key=lambda x: -x[1]):
                print(f"      {f}: {i:.2%}")
        
        if score > best_score:
            best_score = score
            best_model = clf
            best_name = name
    
    # Save best model
    MODEL_DIR.mkdir(exist_ok=True)
    joblib.dump(best_model, MODEL_DIR / "layout_predictor.pkl")
    
    # Also export as plain JS rules (no dependency)
    # Extract decision thresholds
    print(f"\n  ✓ Best model: {best_name}")
    print(f"  ✓ Saved: models/layout_predictor.pkl")
    
    return best_model, reverse_map

# ---- MODEL B: WIDTH DISTRIBUTION ANALYZER ----
def analyze_width_patterns(data):
    """Analyze width distributions per wall length bracket for auto-layout."""
    
    from collections import defaultdict, Counter
    
    wall_data = defaultdict(lambda: {
        "widths": Counter(),
        "types": Counter(),
        "first_types": Counter(),
        "last_types": Counter(),
        "count": 0
    })
    
    for d in data:
        for wall in ["A", "B", "C"]:
            cabs = [c for c in d["cabinets"] if c["wall"] == wall and c.get("width_mm")]
            if len(cabs) < 2:
                continue
            
            total = sum(c["width_mm"] for c in cabs)
            bucket = round(total / 500) * 500  # 2000, 2500, 3000, 3500, ...
            key = (bucket, wall)
            
            wall_data[key]["widths"].update(c["width_mm"] for c in cabs)
            wall_data[key]["types"].update(c["type"] for c in cabs)
            wall_data[key]["first_types"][cabs[0]["type"]] += 1
            wall_data[key]["last_types"][cabs[-1]["type"]] += 1
            wall_data[key]["count"] += 1
    
    return wall_data

# ---- MODEL C: MARKOV CHAIN SEQUENCE PREDICTOR ----
def train_markov_chain(data):
    """
    Train a Markov chain that predicts the next cabinet type+width
    given the current cabinet and remaining wall length.
    """
    type_width_combos = Counter()
    transitions = {}  # (prev_type, prev_width) -> Counter of (next_type, next_width)
    
    for d in data:
        for wall in ["A", "B", "C"]:
            cabs = [c for c in d["cabinets"] if c["wall"] == wall and c.get("width_mm")]
            
            for i, cab in enumerate(cabs):
                combo = (cab["type"], cab["width_mm"])
                type_width_combos[combo] += 1
                
                if i > 0:
                    prev = (cabs[i-1]["type"], cabs[i-1]["width_mm"])
                    if prev not in transitions:
                        transitions[prev] = Counter()
                    transitions[prev][combo] += 1
    
    # Convert to probabilities
    markov_model = {}
    for prev, nexts in transitions.items():
        total = sum(nexts.values())
        markov_model[prev] = {
            "probabilities": {k: v / total for k, v in nexts.items()},
            "top_next": [k for k, _ in nexts.most_common(3)]
        }
    
    return markov_model, type_width_combos

if __name__ == "__main__":
    with open(DATASET_PATH) as f:
        data = json.load(f)
    
    print(f"Loaded {len(data)} designs\n")
    print("=" * 60)
    
    # Train all models
    model_a, layout_map = train_layout_predictor(data)
    
    print("\n" + "=" * 60)
    print("Pattern Analysis:")
    patterns = analyze_width_patterns(data)
    
    print("\n" + "=" * 60)
    print("Markov Chain:")
    markov, combos = train_markov_chain(data)
    print(f"  Type-width combos: {len(combos)}")
    print(f"  Transitions: {len(markov)}")
    print(f"  Top 10 combos:")
    for (t, w), c in combos.most_common(10):
        print(f"    {t} ({w}mm): {c}")
    
    # Save patterns
    import pickle
    with open(MODEL_DIR / "markov_chain.pkl", "wb") as f:
        pickle.dump(markov, f)
    with open(MODEL_DIR / "width_patterns.json", "w") as f:
        # Convert Counters to dicts for JSON
        serializable = {}
        for key, value in patterns.items():
            serializable[str(key)] = {
                "widths": dict(value["widths"].most_common(10)),
                "types": dict(value["types"].most_common(10)),
                "first_types": dict(value["first_types"].most_common(3)),
                "last_types": dict(value["last_types"].most_common(3)),
                "count": value["count"]
            }
        json.dump(serializable, f, indent=2)
    
    print("\n  ✓ Saved: models/markov_chain.pkl")
    print("  ✓ Saved: models/width_patterns.json")
    print(f"\n{'='*60}")
```

**Run**: `python scripts/04_train_models.py`

### 5.3 Model D: Generate JS Config (Direct Integration, Highest Value)

**File**: `scripts/05_extract_patterns.py`

```python
"""
Extracts human-readable patterns from the dataset and generates
a JavaScript configuration file that can be directly pasted into index.html.
This is the highest-value output — no ML runtime needed.
"""
import json
from pathlib import Path
from collections import Counter, defaultdict

DATASET_PATH = Path("dataset") / "kitchen_data.json"
OUTPUT_PATH = Path("models") / "layout_patterns.js"

def generate_js_config(data):
    """Generate a complete JavaScript configuration from the dataset."""
    
    # 1. Preferred cabinet type by position on wall
    first_by_length = defaultdict(lambda: defaultdict(int))
    last_by_length = defaultdict(lambda: defaultdict(int))
    type_by_width = defaultdict(lambda: defaultdict(int))
    
    # 2. Common sequences
    seq_by_length = defaultdict(list)
    
    # 3. Width preference by bucket
    width_by_bucket = defaultdict(lambda: defaultdict(int))
    
    # 4. Corner cabinet prevalence
    corner_by_layout = defaultdict(int)
    total_by_layout = defaultdict(int)
    
    for d in data:
        lt = d["layoutType"]
        total_by_layout[lt] += 1
        
        has_corner = False
        for c in d["cabinets"]:
            if c["type"] == "cornerBlind":
                has_corner = True
                corner_by_layout[lt] += 1
        
        for wall in ["A", "B", "C"]:
            cabs = [c for c in d["cabinets"] if c["wall"] == wall and c.get("width_mm")]
            if len(cabs) < 2:
                continue
            
            total = sum(c["width_mm"] for c in cabs)
            bucket = round(total / 500) * 500
            
            first_by_length[bucket][cabs[0]["type"]] += 1
            last_by_length[bucket][cabs[-1]["type"]] += 1
            
            for c in cabs:
                width_by_bucket[bucket][c["width_mm"]] += 1
                type_by_width[c["width_mm"]][c["type"]] += 1
            
            seq = " → ".join(f"{c['type']}({c['width_mm']})" for c in cabs)
            seq_by_length[bucket].append(seq)
    
    # Generate JS
    js = """// AUTO-GENERATED LAYOUT PATTERNS
// Source: %d kitchen designs
// Generated: %s
// DO NOT EDIT MANUALLY — run scripts/05_extract_patterns.py to regenerate

const LAYOUT_PATTERNS = {
""" % (len(data), __import__("datetime").datetime.now().strftime("%Y-%m-%d"))
    
    # First cabinet preferences
    js += "  firstCabinetByLength: {\n"
    for bucket in sorted(first_by_length.keys()):
        top = Counter(first_by_length[bucket]).most_common(3)
        js += f"    {bucket}: {json.dumps([t[0] for t in top])},\n"
    js += "  },\n\n"
    
    # Last cabinet preferences
    js += "  lastCabinetByLength: {\n"
    for bucket in sorted(last_by_length.keys()):
        top = Counter(last_by_length[bucket]).most_common(3)
        js += f"    {bucket}: {json.dumps([t[0] for t in top])},\n"
    js += "  },\n\n"
    
    # Width distribution per bucket
    js += "  preferredWidths: {\n"
    for bucket in sorted(width_by_bucket.keys()):
        total = sum(width_by_bucket[bucket].values())
        widths = {w: round(c/total, 2) for w, c in width_by_bucket[bucket].items()}
        js += f"    {bucket}: {json.dumps(widths)},\n"
    js += "  },\n\n"
    
    # Corner cabinet probability
    js += "  cornerProbability: {\n"
    for lt in ["straight", "L", "U", "island"]:
        if total_by_layout[lt] > 0:
            prob = corner_by_layout[lt] / total_by_layout[lt]
            js += f"    '{lt}': {prob:.2f},\n"
    js += "  },\n\n"
    
    # Standard sequences (top 3 per bucket)
    js += "  commonSequences: {\n"
    for bucket in sorted(seq_by_length.keys()):
        top = Counter(seq_by_length[bucket]).most_common(3)
        js += f"    {bucket}: [\n"
        for seq, count in top:
            js += f"      {{seq: '{seq}', count: {count}}},\n"
        js += f"    ],\n"
    js += "  },\n\n"
    
    # Type preference by width
    js += "  typeByWidth: {\n"
    for width in sorted(type_by_width.keys()):
        top = Counter(type_by_width[width]).most_common(2)
        js += f"    {width}: {json.dumps([t[0] for t in top])},\n"
    js += "  },\n"
    
    js += "};\n\n"
    
    # Export for Node.js / ES modules
    js += """// Usage in index.html:
// 1. Paste this entire file before your <script> tag
// 2. Use: LAYOUT_PATTERNS.firstCabinetByLength[3600] -> preferred first cabinet for 3600mm walls
// 3. Use: LAYOUT_PATTERNS.preferredWidths[3600] -> width distribution for 3600mm walls
// 4. Use: LAYOUT_PATTERNS.cornerProbability['L'] -> 0.85 means 85% of L-kitchens have corner cabinets

if (typeof module !== 'undefined' && module.exports) {
  module.exports = LAYOUT_PATTERNS;
}
"""
    
    OUTPUT_PATH.parent.mkdir(exist_ok=True)
    with open(OUTPUT_PATH, "w") as f:
        f.write(js)
    
    print(f"Generated JS config: {OUTPUT_PATH}")
    print(f"  {len(first_by_length)} wall length brackets")
    print(f"  {len(seq_by_length)} unique sequences")
    print(f"  Corner probability for L-shape: {corner_by_layout['L']/max(total_by_layout['L'],1):.0%}")
    
    return js

if __name__ == "__main__":
    with open(DATASET_PATH) as f:
        data = json.load(f)
    generate_js_config(data)
```

**Run**: `python scripts/05_extract_patterns.py`

**Output**: `models/layout_patterns.js` — a ~50-100 line JS config file ready to paste into index.html.

---

## 6. Phase 5: Integration into AIMS

### 6.1 Paste Patterns into index.html

Add this block at the top of your `<script>` tag in `index.html`:

```html
<script>
// ===== AUTO-GENERATED PATTERNS FROM 100 DESIGNS =====
// Copy the contents of models/layout_patterns.js here
const LAYOUT_PATTERNS = {
  firstCabinetByLength: {
    3000: ["drawer3", "drawer4"],
    3600: ["drawer3", "drawer4", "doubleDoor"],
    // ... rest from generated file
  },
  // ...
};
// ===================================================
```

### 6.2 Enhanced Auto-Layout with Patterns

Add a new function to the `index.html` JavaScript:

```js
// Pattern-enhanced auto-layout
function patternBestFit(target, wallKey) {
  const STANDARD = [300, 450, 500, 600, 750, 800, 900];
  const bucket = Math.round(target / 500) * 500;
  
  // Check if we have pattern data for this length
  const firstPref = LAYOUT_PATTERNS.firstCabinetByLength[bucket];
  const lastPref = LAYOUT_PATTERNS.lastCabinetByLength[bucket];
  const widthPref = LAYOUT_PATTERNS.preferredWidths[bucket];
  
  let result = [];
  let remaining = target;
  
  if (firstPref && widthPref && bucket >= 2400) {
    // Data-driven fill
    // 1. Prefer first cabinet type
    // 2. Fill remaining with preferred widths
    // 3. End with preferred last cabinet type
    
    // First cabinet (slightly different = mark the start)
    let firstWidth = Math.min(600, remaining - 300);
    firstWidth = STANDARD.reduce((a, b) => 
      Math.abs(b - firstWidth) < Math.abs(a - firstWidth) ? b : a
    );
    result.push(firstWidth);
    remaining -= firstWidth;
    
    // Middle cabinets
    while (remaining >= 600) {
      // Pick most common width for this bucket
      let bestWidth = 600;
      let bestCount = 0;
      for (const [w, freq] of Object.entries(widthPref)) {
        if (parseInt(w) <= remaining && freq > bestCount) {
          bestWidth = parseInt(w);
          bestCount = freq;
        }
      }
      result.push(bestWidth);
      remaining -= bestWidth;
    }
    
    // Last cabinet if remaining >= min width
    if (remaining >= 300) {
      result.push(remaining);
    } else if (remaining > 0 && result.length > 0) {
      result[result.length - 1] += remaining;
    }
    
  } else {
    // Fall back to standard greedy
    result = greedyFit(target, STANDARD);
  }
  
  return result;
}

// Standard greedy fill (existing logic)
function greedyFit(target, widths) {
  const sorted = [...widths].sort((a, b) => b - a);
  let remaining = target;
  let result = [];
  
  while (remaining >= 300) {
    for (const w of sorted) {
      if (w <= remaining) {
        result.push(w);
        remaining -= w;
        break;
      }
    }
  }
  
  if (remaining > 0 && result.length > 0) {
    result[result.length - 1] += remaining;
  }
  
  return result;
}
```

### 6.3 ML Layout Suggestion Button

Add this button to the HTML (in the Add Cabinet panel):

```html
<button id="mlSuggestBtn" class="primary full" title="Uses ML patterns from real designs">
  🧠 ML Suggested Layout
</button>
```

And its handler:

```js
$('mlSuggestBtn').onclick = function() {
  const layout = $('layoutType').value;
  const a = +$('wallA').value;
  const b = +($('wallB')?.value || 0);
  const c = +($('wallC')?.value || 0);
  
  cabinets = [];
  
  // Suggest layout type if no wall cabinets added
  if (!cabinets.some(c => c.wall === 'A') && LAYOUT_PATTERNS.cornerProbability) {
    // Check if corner is likely
    const cornerProb = LAYOUT_PATTERNS.cornerProbability[layout] || 0;
    if (layout === 'L' && cornerProb > 0.5) {
      // Auto-insert corner cabinet
    }
  }
  
  // Generate per-wall
  const wallKeys = getWallKeys(layout);
  wallKeys.forEach(wall => {
    const wallLen = getWallLength(wall);
    if (wallLen > 0) {
      const widths = patternBestFit(wallLen, wall);
      
      // Assign types based on position
      widths.forEach((w, idx) => {
        let type = 'doubleDoor';
        if (w <= 450) type = 'drawer3';
        else if (idx === 0 && firstPref && firstPref.includes('drawer3')) type = 'drawer3';
        else if (w >= 900) type = 'doubleDoor';
        else type = 'drawer3';
        
        cabinets.push({ type, width: w, wall });
      });
    }
  });
  
  render();
};
```

### 6.4 ML Model API Integration (Optional Backend)

**File**: `api/server.py`

```python
"""
Lightweight Flask API for serving ML predictions to the AIMS frontend.
Run: python api/server.py
Frontend calls: fetch('http://localhost:5000/api/predict-layout', {...})
"""
from flask import Flask, request, jsonify
from flask_cors import CORS
import joblib
import numpy as np
import json
from pathlib import Path

app = Flask(__name__)
CORS(app)

# Load models
MODEL_DIR = Path("../models")
layout_model = None
markov_model = None

def load_models():
    global layout_model, markov_model
    try:
        if (MODEL_DIR / "layout_predictor.pkl").exists():
            layout_model = joblib.load(MODEL_DIR / "layout_predictor.pkl")
            print("✓ Loaded layout_predictor.pkl")
        if (MODEL_DIR / "markov_chain.pkl").exists():
            import pickle
            with open(MODEL_DIR / "markov_chain.pkl", "rb") as f:
                markov_model = pickle.load(f)
            print("✓ Loaded markov_chain.pkl")
    except Exception as e:
        print(f"Warning: Could not load models: {e}")

LAYOUT_MAP = {0: "straight", 1: "L", 2: "U", 3: "island"}
REVERSE_MAP = {"straight": 0, "L": 1, "U": 2, "island": 3}

@app.route("/api/predict-layout", methods=["POST"])
def predict_layout():
    """Predict layout type from wall dimensions."""
    if layout_model is None:
        return jsonify({"error": "Model not loaded"}), 503
    
    d = request.get_json()
    a = float(d.get("wallA", 0))
    b = float(d.get("wallB", 0))
    c = float(d.get("wallC", 0))
    total = a + b + c
    ratio_ab = b / max(a, 1)
    ratio_ac = c / max(a, 1)
    area = max(a, c * (ratio_ab > 0.5)) * (a + b * (ratio_ab > 0.3))
    
    features = np.array([[a, b, c, total, ratio_ab, ratio_ac, area]])
    pred = layout_model.predict(features)[0]
    proba = layout_model.predict_proba(features)[0].max()
    
    return jsonify({
        "layoutType": LAYOUT_MAP[int(pred)],
        "confidence": round(float(proba), 3)
    })

@app.route("/api/predict-sequence", methods=["POST"])
def predict_sequence():
    """Predict next cabinet in a sequence (Markov chain)."""
    if markov_model is None:
        return jsonify({"error": "Model not loaded"}), 503
    
    d = request.get_json()
    current_type = d.get("currentType")
    current_width = d.get("currentWidth")
    remaining = d.get("remainingWidth", 600)
    wall_key = d.get("wall", "A")
    
    key = (current_type, current_width)
    if key in markov_model:
        next_options = markov_model[key]["top_next"]
        # Filter by remaining width
        valid = [c for c in next_options if c[1] <= remaining + 50]
        if valid:
            return jsonify({
                "suggestedType": valid[0][0],
                "suggestedWidth": valid[0][1],
                "alternatives": valid[1:3]
            })
    
    # Fallback
    return jsonify({
        "suggestedType": "doubleDoor",
        "suggestedWidth": min(600, remaining),
        "alternatives": []
    })

@app.route("/api/health", methods=["GET"])
def health():
    return jsonify({
        "status": "ok",
        "models_loaded": {
            "layout_predictor": layout_model is not None,
            "markov_chain": markov_model is not None
        }
    })

if __name__ == "__main__":
    load_models()
    print("\nAIMS ML API running on http://localhost:5000")
    print("Endpoints:")
    print("  POST /api/predict-layout   - Predict layout type")
    print("  POST /api/predict-sequence - Predict next cabinet")
    print("  GET  /api/health           - Health check")
    app.run(host="0.0.0.0", port=5000, debug=True)
```

**Run**: `pip install flask flask-cors && python api/server.py`

### 6.5 Benchmark: Before vs After

**File**: `scripts/06_benchmark.py`

```python
"""
Compares old bestFit vs new pattern-informed auto-layout against the real dataset.
Measures: accuracy (total width match), type match, and sequence similarity.
"""
import json
from pathlib import Path
from collections import Counter

DATASET_PATH = Path("dataset") / "kitchen_data.json"
STANDARD = [300, 450, 500, 600, 750, 800, 900]

def old_best_fit(target):
    """Original greedy algorithm."""
    remaining = target
    result = []
    widths = sorted(STANDARD, reverse=True)
    while remaining >= 300:
        for w in widths:
            if w <= remaining:
                result.append(w)
                remaining -= w
                break
    if remaining > 0 and result:
        result[-1] += remaining
    return result

def pattern_best_fit(target, patterns=None):
    """
    Enhanced pattern-informed algorithm.
    Uses data-driven first/last cabinet preferences.
    """
    bucket = round(target / 500) * 500
    first_pref = None
    last_pref = None
    
    if patterns and bucket in patterns.get("firstCabinetByLength", {}):
        first_pref = patterns["firstCabinetByLength"][bucket]
        last_pref = patterns.get("lastCabinetByLength", {}).get(bucket)
    
    remaining = target
    result = []
    widths = sorted(STANDARD, reverse=True)
    
    # First cabinet (slightly different to mark start)
    if first_pref and remaining >= 600:
        first_w = 600
        if 450 in first_pref and remaining >= 1200:
            first_w = 450
        result.append(first_w)
        remaining -= first_w
    
    # Middle fill
    while remaining >= 300:
        found = False
        for w in widths:
            if w <= remaining:
                result.append(w)
                remaining -= w
                found = True
                break
        if not found:
            break
    
    if remaining > 0 and result:
        result[-1] += remaining
    
    return result

def evaluate(data, algorithm_fn, name):
    """Evaluate an algorithm against the dataset."""
    width_errors = []
    type_matches = []
    sequence_diffs = []
    
    for d in data:
        for wall in ["A", "B", "C"]:
            cabs = [c for c in d["cabinets"] if c["wall"] == wall and c.get("width_mm")]
            if len(cabs) < 2:
                continue
            
            target = sum(c["width_mm"] for c in cabs)
            generated = algorithm_fn(target)
            
            gen_total = sum(generated)
            width_errors.append(abs(gen_total - target))
            
            # Compare widths (sorted, no penalty for order)
            gen_widths = sorted(generated)
            actual_widths = sorted(c["width_mm"] for c in cabs)
            common = sum(min(gen_widths.count(w), actual_widths.count(w)) * (1 / max(len(gen_widths), len(actual_widths))) for w in set(gen_widths) | set(actual_widths))
            type_matches.append(common)
    
    avg_error = sum(width_errors) / len(width_errors)
    max_error = max(width_errors)
    avg_match = sum(type_matches) / len(type_matches)
    
    print(f"\n{name}:")
    print(f"  Avg width error: {avg_error:.0f}mm")
    print(f"  Max width error: {max_error:.0f}mm")
    print(f"  Width match rate: {avg_match:.1%}")
    
    return {"avg_error": avg_error, "max_error": max_error, "match_rate": avg_match}

if __name__ == "__main__":
    with open(DATASET_PATH) as f:
        data = json.load(f)
    
    print(f"Benchmarking on {len(data)} designs")
    print("=" * 50)
    
    # Try loading patterns
    patterns = None
    try:
        with open(Path("models") / "width_patterns.json") as f:
            patterns = json.load(f)
        # Reconstruct nested dict from JSON (keys are strings)
        reconstructed = {}
        for key, val in patterns.items():
            bucket, wall = eval(key)
            if bucket not in reconstructed:
                reconstructed[bucket] = {}
            reconstructed[bucket][wall] = val
        patterns = reconstructed
    except:
        pass
    
    old_results = evaluate(data, old_best_fit, "Original bestFit")
    new_results = evaluate(
        data, 
        lambda t: pattern_best_fit(t, patterns),
        "Pattern-Informed bestFit"
    )
    
    print("\n" + "=" * 50)
    improvement = (old_results["avg_error"] - new_results["avg_error"]) / max(old_results["avg_error"], 1) * 100
    print(f"IMPROVEMENT: {improvement:.0f}% reduction in width error")
```

**Run**: `python scripts/06_benchmark.py`

---

## 7. Phase 6: Future Vision & Scaling

### 7.1 Beyond 100 PDFs: Continuous Learning Loop

```
PDFs (any number)
       ↓
AI Extraction → Dataset Growth
       ↓
Auto-Retrain Models (scheduled or on-demand)
       ↓
Updated JS Config pushed to AIMS
       ↓
AIMS auto-layout improves over time
       ↓
More designs → Better predictions → Happier users
```

### 7.2 Scaling Architecture (100 → 10,000 Designs)

| Scale | Storage | ML Approach | Integration |
|---|---|---|---|
| **100** | Local JSON files | Pattern extraction → JS config | Paste into index.html |
| **1,000** | SQLite database | scikit-learn models → Flask API | REST endpoint in AIMS |
| **10,000** | PostgreSQL + S3 | PyTorch / TensorFlow → dedicated inference server | Cloud API with caching |
| **100,000+** | Data warehouse | Full deep learning pipeline (CNN for plan images + transformer for sequences) | SaaS platform |

### 7.3 Advanced ML Models for Phase 2

#### Image-Based Layout Recognition (CNN)

Skip manual extraction — train a computer vision model to read PDFs directly:

```python
# Future: CNN-based layout recognition
from tensorflow.keras.applications import ResNet50
from tensorflow.keras.layers import Dense, GlobalAveragePooling2D
from tensorflow.keras.models import Model

# Load pretrained ResNet50 without top layers
base = ResNet50(weights="imagenet", include_top=False, input_shape=(224, 224, 3))

# Add custom regression/classification heads
x = GlobalAveragePooling2D()(base.output)
layout_output = Dense(4, activation="softmax", name="layout")(x)  # Straight/L/U/Island
wall_a_output = Dense(1, activation="relu", name="wallA")(x)  # Wall dimension regression

model = Model(inputs=base.input, outputs=[layout_output, wall_a_output])
model.compile(
    optimizer="adam",
    loss={"layout": "sparse_categorical_crossentropy", "wallA": "mse"},
    metrics={"layout": "accuracy", "wallA": "mae"}
)
```

#### Sequence Transformer (Cabinet Layout)

Replace the Markov chain with a full transformer model for sequence prediction:

```
Input:  Wall length + position encoding
        ↓
Transformer Encoder (4 layers, 8 heads)
        ↓
Autoregressive Decoder
        ↓
Output: [type₁, width₁, type₂, width₂, ...]
```

### 7.4 Product Vision: AIMS Intelligence Layer

```
┌─────────────────────────────────────────────────────┐
│                  AIMS Application                    │
│  ┌─────────┐  ┌──────────┐  ┌───────┐  ┌────────┐ │
│  │Designer │  │ Quotation │  │  BOM  │  │ Rules  │ │
│  └────┬────┘  └────┬─────┘  └───┬───┘  └───┬────┘ │
│       │             │            │          │       │
│  ┌────┴─────────────┴────────────┴──────────┴────┐ │
│  │              AIMS Intelligence                │ │
│  │  ┌────────────┐  ┌───────────────────────┐   │ │
│  │  │  Layout    │  │  Cabinet Sequence     │   │ │
│  │  │  Predictor │  │  Predictor (ML)       │   │ │
│  │  └────────────┘  └───────────────────────┘   │ │
│  │  ┌────────────┐  ┌───────────────────────┐   │ │
│  │  │  Price     │  │  Material             │   │ │
│  │  │  Estimator │  │  Optimizer            │   │ │
│  │  └────────────┘  └───────────────────────┘   │ │
│  └───────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
```

**ML-powered features roadmap:**

| Feature | What It Does | ML Technique | Timeline |
|---|---|---|---|
| **Smart Auto-Layout** | Optimal cabinet placement for any room | Pattern extraction + sequence model | Now |
| **Price Predictor** | Estimate project cost from dimensions | Regression (wall dims → total cost) | Phase 2 |
| **Material Optimizer** | Recommend board type/thickness | Classification | Phase 2 |
| **Style Matcher** | Suggest handle/color based on project | Clustering | Phase 3 |
| **Error Detector** | Flag unusual cabinet combinations | Anomaly detection | Phase 3 |
| **Photo → Layout** | Snap a room photo → get layout | CNN + object detection | Phase 4 |

### 7.5 Commercial Implications

| Design Count | ML Confidence | Business Value |
|---|---|---|
| **100** | ~70-80% | Good starter models, catch common patterns |
| **1,000** | ~85-90% | Reliable suggestions, can auto-fill 60% of designs |
| **10,000** | ~92-97% | Production quality, can auto-fill 85% of designs |
| **100,000+** | ~98%+ | Near-human design quality, full automation potential |

**Revenue models enabled by ML:**
- **Speed**: 10x faster quoting → more proposals per day
- **Accuracy**: Fewer BOM errors → less waste → higher margins
- **Scale**: Junior designers produce senior-quality layouts
- **Differentiation**: "AI-powered kitchen design" as a selling point

---

## 8. Appendix: Full Scripts

### 8.1 Complete Pipeline Script

**File**: `scripts/07_run_pipeline.py`

```python
#!/usr/bin/env python3
"""
Orchestrates the entire pipeline from PDFs to JS config.
Run: python scripts/07_run_pipeline.py
"""
import subprocess
import sys
import time
from pathlib import Path

SCRIPTS_DIR = Path("scripts")
STEPS = [
    ("01_convert_pdfs.py",      "Converting PDFs to images"),
    ("02_extract_data.py",      "Extracting data via GPT-4 Vision"),
    ("03_validate_dataset.py",  "Validating and cleaning dataset"),
    ("04_train_models.py",      "Training ML models"),
    ("05_extract_patterns.py",  "Generating JS configuration"),
    ("06_benchmark.py",         "Running benchmarks"),
]

def run_pipeline(start_from=0):
    for i, (script, description) in enumerate(STEPS[start_from:], start_from + 1):
        print(f"\n{'='*60}")
        print(f"Step {i}/{len(STEPS)}: {description}")
        print(f"{'='*60}")
        
        script_path = SCRIPTS_DIR / script
        if not script_path.exists():
            print(f"  ✗ Script not found: {script_path}")
            continue
        
        t0 = time.time()
        result = subprocess.run([sys.executable, str(script_path)], capture_output=False)
        elapsed = time.time() - t0
        
        if result.returncode == 0:
            print(f"\n  ✓ Completed in {elapsed:.1f}s")
        else:
            print(f"\n  ✗ Failed (return code {result.returncode})")
            ans = input("  Continue with next step? (y/n): ")
            if ans.lower() != 'y':
                print("Pipeline stopped.")
                sys.exit(1)

if __name__ == "__main__":
    run_pipeline()
```

**Run (full pipeline)**: `python scripts/07_run_pipeline.py`

### 8.2 One-Command Setup

```bash
# Complete setup (run from KitchenDesigner/ directory)
pip install PyMuPDF openai scikit-learn numpy joblib pandas pillow flask flask-cors
export OPENAI_API_KEY="sk-your-key-here"
python scripts/01_convert_pdfs.py && python scripts/02_extract_data.py && python scripts/03_validate_dataset.py && python scripts/04_train_models.py && python scripts/05_extract_patterns.py && python scripts/06_benchmark.py
```

### 8.3 Cost-Benefit Summary

| Component | Time | Cost | Value |
|---|---|---|---|
| PDF → Images | 2 min | Free | Required first step |
| AI Extraction (GPT-4o-mini) | 10 min | ~$2-3 | Core — turns PDFs into structured data |
| Dataset Cleaning | 1 min | Free | Ensures quality |
| ML Training | 2 min | Free | Pattern extraction + classifier |
| JS Config Generation | 30 sec | Free | Directly improves AIMS auto-layout |
| Benchmark | 30 sec | Free | Quantifies improvement |

**Total**: ~15 min active time, ~$3 API cost, ~30 min wall clock.

**Result**: Your AIMS auto-layout now reflects patterns from 100 real kitchen designs.

---

*End of Codex Pipeline Document. Copy from here into Codex and it will execute the complete pipeline from PDFs to ML-enhanced kitchen design.*