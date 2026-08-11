#!/usr/bin/env python3
"""P7.4 — Assert SIM_SNAPSHOT_V2.md field matrix stays aligned with tip exports.

Compares docs/design/SIM_SNAPSHOT_V2.md §4 (E / W / U columns) against:
  - src/Forge.Engine/Simulation/SimSnapshot.cs          (E)
  - src/Forge.SimWasm/SimSnapshotDto.cs                 (W)
  - unity/.../Sim/CitySimState.cs (+ CitySimBridge.cs)  (U)

Fails CI when:
  1. A matrix cell claims presence (✅ / via / nested / partial) but the
     corresponding C# surface lacks the property (after alias expansion).
  2. A public scalar / array property on a tip export is missing from the
     matrix (and not allowlisted as nested-only / structural).

Usage (repo root):
  python3 scripts/verify-sim-snapshot-v2.py
  # or:  ./scripts/verify-sim-snapshot-v2.sh
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
DOC = REPO / "docs/design/SIM_SNAPSHOT_V2.md"
ENGINE = REPO / "src/Forge.Engine/Simulation/SimSnapshot.cs"
WASM = REPO / "src/Forge.SimWasm/SimSnapshotDto.cs"
UNITY_STATE = (
    REPO / "unity/CityMajor.Unity/Assets/Scripts/Sim/CitySimState.cs"
)
UNITY_BRIDGE = (
    REPO / "unity/CityMajor.Unity/Assets/Scripts/Sim/CitySimBridge.cs"
)

PROP_RE = re.compile(
    r"^\s*public\s+(?:static\s+)?(?:readonly\s+)?"
    r"(?:[\w.<>,\[\]\s]+?)\s+(\w+)\s*"
    r"(?:\{|[=;])",
    re.MULTILINE,
)
STRUCT_PROP_RE = re.compile(
    r"^\s*public\s+([\w.<>,\[\]]+)\s+(\w+)\s*;",
    re.MULTILINE,
)
BACKTICK_RE = re.compile(r"`([^`]+)`")

# Compound / display names → concrete C# identifiers per surface.
FIELD_EXPAND: dict[str, dict[str, list[str]]] = {
    "Tick / TickCount": {
        "E": ["TickCount"],
        "W": ["Tick"],
        "U": [],
    },
    "CityFunds": {
        "E": ["CityFunds"],
        "W": ["CityFunds"],
        "U": ["Funds"],
    },
    "MonthlyIncome / Expenses": {
        "E": ["MonthlyIncome", "MonthlyExpenses"],
        "W": ["MonthlyIncome", "MonthlyExpenses"],
        "U": ["MonthlyIncome", "MonthlyExpense"],
    },
    "Property/Commercial/IndustrialTaxRate": {
        "E": ["PropertyTaxRate", "CommercialTaxRate", "IndustrialTaxRate"],
        "W": [],
        "U": [],
    },
    "Weather / Season / Wind*": {
        "E": ["WeatherCondition", "Season", "WindSpeed", "WindDirection"],
        "W": [],
        "U": [],
    },
    "ApprovalRating": {
        "E": ["ApprovalRating"],
        "W": ["Approval"],
        "U": ["Approval"],
    },
    "TradeBalance / MonthlyExport/Import": {
        "E": ["TradeBalance", "MonthlyExportValue", "MonthlyImportCost"],
        "W": ["TradeBalance", "MonthlyExportValue", "MonthlyImportCost"],
        "U": ["TradeBalance", "MonthlyExportValue", "MonthlyImportCost"],
    },
    "ShortageGoods / SurplusGoods": {
        "E": ["ShortageGoods", "SurplusGoods"],
        "W": ["Economy"],
        "U": [],
    },
    "GoodsShortageIndex / SurplusIndex": {
        "E": ["GoodsShortageIndex", "GoodsSurplusIndex"],
        "W": ["GoodsShortageIndex", "GoodsSurplusIndex"],
        "U": ["GoodsShortageIndex", "GoodsSurplusIndex"],
    },
    "BuildingCount": {
        "E": ["BuildingCount"],
        "W": ["Buildings"],
        "U": ["BuildingCount"],
    },
    "Residential/Commercial/IndustrialDemand": {
        "E": [],
        "W": ["ResidentialDemand", "CommercialDemand", "IndustrialDemand"],
        "U": ["DemandResidential", "DemandCommercial", "DemandIndustrial"],
    },
    "Economy.Shortages/Surpluses/Flows/MarketZonePrices": {
        "E": [],
        "W": ["Economy"],
        "U": ["FoodAvgPrice", "WaterAvgPrice", "SteelAvgPrice", "HasGoodsPrices"],
    },
    "FrictionCorridors[]": {
        "E": [],
        "W": ["FrictionCorridors"],
        "U": ["LatestFrictionCorridors"],
    },
    "HasGoodsPrices / Food|Water|SteelAvgPrice": {
        "E": [],
        "W": [],
        "U": ["HasGoodsPrices", "FoodAvgPrice", "WaterAvgPrice", "SteelAvgPrice"],
    },
    "TileTraffic[]": {
        "E": ["TileTraffic"],
        "W": ["Traffic"],
        "U": [],
    },
    "RoadGraph (+ volumes/times)": {
        "E": [],
        "W": ["RoadGraph"],
        "U": [],
    },
    "CommuteOdSample[]": {
        "E": [],
        "W": ["CommuteOdSample"],
        "U": [],
    },
    "Car/Transit/WalkModeShare": {
        "E": [],
        "W": ["CarModeShare", "TransitModeShare", "WalkModeShare"],
        "U": ["CarModeShare", "TransitModeShare", "WalkModeShare"],
    },
    "TransitLineCount / BusCoverage": {
        "E": [],
        "W": ["TransitLineCount", "BusCoverage"],
        "U": [],
    },
    "MeanCommuteMinutes / MeanCommuteSatisfaction": {
        "E": [],
        "W": [],
        "U": ["MeanCommuteMinutes", "MeanCommuteSatisfaction"],
    },
    "ServiceCoverage[]": {
        "E": [],
        "W": ["ServiceCoverage"],
        "U": [],
    },
    "CouncilSeats": {
        "E": ["CouncilSeats"],
        "W": ["CouncilSeats"],
        "U": ["GetCouncilSeats"],
    },
    "LawResidential/Industrial/CommercialSpawnMult": {
        "E": [
            "LawResidentialSpawnMult",
            "LawIndustrialSpawnMult",
            "LawCommercialSpawnMult",
        ],
        "W": [
            "LawResidentialSpawnMult",
            "LawIndustrialSpawnMult",
            "LawCommercialSpawnMult",
        ],
        "U": [
            "LawResidentialSpawnMult",
            "LawIndustrialSpawnMult",
            "LawCommercialSpawnMult",
        ],
    },
    "ActiveEvents[]": {
        "E": [],
        "W": ["ActiveEvents"],
        "U": [],  # Herald path — not on CitySimState
    },
    "ActiveLawIds[]": {
        "E": [],
        "W": ["ActiveLawIds"],
        "U": [],
    },
    "ActiveOrdinances / NextElectionYear": {
        "E": ["ActiveOrdinances", "NextElectionYear"],
        "W": ["ActiveOrdinances", "NextElectionYear"],
        "U": [],
    },
    "CulturalDna[]": {
        "E": ["CulturalDna"],
        "W": ["CulturalDna"],
        "U": [],
    },
    "ResearchPoints / ResearchRate": {
        "E": ["ResearchPoints", "ResearchRate"],
        "W": ["ResearchPoints"],
        "U": [],
    },
    "CurrentResearchId / Progress": {
        "E": ["CurrentResearchId", "CurrentResearchProgress"],
        "W": ["CurrentResearchId", "CurrentResearchProgress"],
        "U": [],
    },
    "UnlockedTechIds / ResearchQueue / QueueProgress": {
        "E": [],
        "W": ["UnlockedTechIds", "ResearchQueue", "QueueProgress"],
        "U": [],  # Research panel — not CitySimState scalars
    },
    "EurekaBonuses / BranchingChoices": {
        "E": [],
        "W": ["EurekaBonuses", "BranchingChoices"],
        "U": [],
    },
}

# Properties that live on tip exports but are intentionally outside §4
# (arrays nested under §5, structural helpers, or bridge-only plumbing).
REVERSE_ALLOW: dict[str, set[str]] = {
    "E": {
        "WorldSize",
        "VehicleCount",
        "TileTerrainTypes",
        "TileZoneTypes",
        "TileRoadFlags",
        "Buildings",
        "Vehicles",
        "SurplusGoods",  # paired with ShortageGoods row
    },
    "W": {
        "Zones",
        "Roads",
        "PopulationL2",  # §5.2 nested
        "Buildings",
        "Traffic",
        "ServiceCoverage",
        "ActiveEvents",
        "ActiveLawIds",
        "Economy",
        "RoadGraph",
        "FrictionCorridors",
        "CommuteOdSample",
    },
    "U": {
        "ZonedTiles",
        "LawDefinitionCount",
        "SampleLawId",
        "SampleLawName",
        "SampleLawActive",
        "Households",
        "LatestFrictionCorridors",
        "GetCouncilSeats",
    },
}


def extract_class_props(path: Path, class_name: str) -> set[str]:
    text = path.read_text(encoding="utf-8")
    # Narrow to the named type body (first matching class/struct).
    m = re.search(
        rf"(?:public\s+)?(?:sealed\s+)?(?:class|struct)\s+{re.escape(class_name)}\b",
        text,
    )
    if not m:
        raise SystemExit(f"ERROR: type {class_name} not found in {path}")
    start = m.end()
    # Brace match from first '{' after type name.
    brace = text.find("{", start)
    if brace < 0:
        raise SystemExit(f"ERROR: no body for {class_name} in {path}")
    depth = 0
    end = brace
    for i, ch in enumerate(text[brace:], brace):
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                end = i
                break
    body = text[brace : end + 1]
    props: set[str] = set()
    for rx in (PROP_RE, STRUCT_PROP_RE):
        for match in rx.finditer(body):
            name = match.group(match.lastindex or 1)
            # STRUCT_PROP_RE has type + name
            if match.lastindex == 2:
                name = match.group(2)
            if name in {"get", "set", "init", "value"}:
                continue
            props.add(name)
    return props


def extract_bridge_extras(path: Path) -> set[str]:
    """Unity extras that satisfy U-column claims without living on CitySimState."""
    if not path.is_file():
        return set()
    text = path.read_text(encoding="utf-8")
    extras: set[str] = set()
    if "LatestFrictionCorridors" in text:
        extras.add("LatestFrictionCorridors")
    if "GetCouncilSeats" in text:
        extras.add("GetCouncilSeats")
    return extras


def cell_present(cell: str) -> bool:
    c = cell.strip()
    if not c or c == "—" or c.startswith("—"):
        return False
    if "✅" in c:
        return True
    # Soft / nested presence markers used in the matrix.
    markers = (
        "via ",
        "nested",
        "partial",
        "points only",
        "Research panel",
        "Herald path",
        "overlay",
    )
    return any(m in c for m in markers)


def parse_matrix(doc: Path) -> list[tuple[str, str, str, str]]:
    """Return rows of (field, E, W, U) from §4 tables."""
    text = doc.read_text(encoding="utf-8")
    rows: list[tuple[str, str, str, str]] = []
    in_scalar = False
    for line in text.splitlines():
        if line.startswith("## 4."):
            in_scalar = True
            continue
        if in_scalar and line.startswith("## 5."):
            break
        if not in_scalar or not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 4:
            continue
        field, e, w, u = cells[0], cells[1], cells[2], cells[3]
        if field in {"Field", ""} or field.startswith("-"):
            continue
        if e in {"E", "---"} or e.startswith("---"):
            continue
        rows.append((field, e, w, u))
    return rows


def resolve_names(field: str, surface: str, cell: str) -> list[str]:
    """Map a matrix field label to concrete identifiers for one surface."""
    if field in FIELD_EXPAND:
        return list(FIELD_EXPAND[field].get(surface, []))

    # Prefer explicit backtick aliases in the cell (e.g. `Funds`, `tick`).
    ticks = BACKTICK_RE.findall(cell)
    if ticks:
        names: list[str] = []
        for t in ticks:
            # Skip prose fragments
            if t in {"buildings[]", "economy.*", "LatestFrictionCorridors"}:
                if t == "LatestFrictionCorridors":
                    names.append(t)
                elif t == "buildings[]":
                    names.append("Buildings")
                elif t == "economy.*":
                    names.append("Economy")
                continue
            # Strip array markers / prose
            clean = t.split()[0].rstrip("[]")
            if clean and clean[0].isalpha():
                names.append(clean[0].upper() + clean[1:] if clean[0].islower() else clean)
                # Also keep camelCase as-is for WASM JSON-ish labels after Pascalize
                if clean[0].islower():
                    names.append(clean[0].upper() + clean[1:])
        # Dedupe preserving order
        seen: set[str] = set()
        out: list[str] = []
        for n in names:
            if n not in seen:
                seen.add(n)
                out.append(n)
        if out:
            return out

    # Default: strip array marker and use the field label as PascalCase property.
    base = field.rstrip("[]").strip()
    if "/" in base or "|" in base:
        # Unmapped compound — fail loudly so FIELD_EXPAND gets updated.
        return []
    return [base]


def check_doc_to_code(
    rows: list[tuple[str, str, str, str]],
    surfaces: dict[str, set[str]],
) -> list[str]:
    errors: list[str] = []
    for field, e_cell, w_cell, u_cell in rows:
        for surface, cell in (("E", e_cell), ("W", w_cell), ("U", u_cell)):
            if not cell_present(cell):
                continue
            # Documented soft targets that intentionally skip CitySimState.
            if surface == "U" and any(
                x in cell for x in ("Herald path", "Research panel", "overlay via")
            ):
                continue
            names = resolve_names(field, surface, cell)
            if not names:
                errors.append(
                    f"doc→code: cannot expand field '{field}' for {surface} "
                    f"(add FIELD_EXPAND entry)"
                )
                continue
            missing = [n for n in names if n not in surfaces[surface]]
            if missing:
                errors.append(
                    f"doc→code: {surface} claims '{field}' but missing {missing} "
                    f"(have aliases {names})"
                )
    return errors


def matrix_mentions(rows: list[tuple[str, str, str, str]], surface: str) -> set[str]:
    mentioned: set[str] = set()
    for field, e_cell, w_cell, u_cell in rows:
        cell = {"E": e_cell, "W": w_cell, "U": u_cell}[surface]
        if not cell_present(cell) and cell.strip() in {"—", ""}:
            # Still record expansions for reverse when other surfaces claim it
            pass
        names = resolve_names(field, surface, cell if cell_present(cell) else "")
        if not names and field in FIELD_EXPAND:
            names = list(FIELD_EXPAND[field].get(surface, []))
        for n in names:
            mentioned.add(n)
        # Also record bare field token for simple rows
        bare = field.rstrip("[]").strip()
        if "/" not in bare and "|" not in bare and " " not in bare:
            mentioned.add(bare)
    return mentioned


def check_code_to_doc(
    rows: list[tuple[str, str, str, str]],
    surfaces: dict[str, set[str]],
) -> list[str]:
    errors: list[str] = []
    for surface, props in surfaces.items():
        mentioned = matrix_mentions(rows, surface)
        allow = REVERSE_ALLOW.get(surface, set())
        for prop in sorted(props):
            if prop in allow or prop in mentioned:
                continue
            # Nested DTO types inside SimSnapshotDto.cs file are filtered by
            # class extract; remaining unknowns fail.
            errors.append(
                f"code→doc: {surface}.{prop} not listed in SIM_SNAPSHOT_V2 §4 "
                f"(update matrix or REVERSE_ALLOW)"
            )
    return errors


def main() -> int:
    for path in (DOC, ENGINE, WASM, UNITY_STATE):
        if not path.is_file():
            print(f"ERROR: missing {path}", file=sys.stderr)
            return 1

    rows = parse_matrix(DOC)
    if len(rows) < 40:
        print(
            f"ERROR: expected ≥40 scalar matrix rows, parsed {len(rows)}",
            file=sys.stderr,
        )
        return 1

    engine_props = extract_class_props(ENGINE, "SimSnapshot")
    wasm_props = extract_class_props(WASM, "SimSnapshotDto")
    unity_props = extract_class_props(UNITY_STATE, "CitySimState")
    unity_props |= extract_bridge_extras(UNITY_BRIDGE)

    surfaces = {"E": engine_props, "W": wasm_props, "U": unity_props}

    errors = check_doc_to_code(rows, surfaces) + check_code_to_doc(rows, surfaces)

    print(
        f"SIM_SNAPSHOT_V2 gap check: {len(rows)} matrix rows | "
        f"E={len(engine_props)} W={len(wasm_props)} U={len(unity_props)} props"
    )
    if errors:
        print(f"FAIL ({len(errors)} issue(s)):", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print("OK: SIM_SNAPSHOT_V2.md §4 aligned with SimSnapshot / DTO / CitySimState")
    return 0


if __name__ == "__main__":
    sys.exit(main())
