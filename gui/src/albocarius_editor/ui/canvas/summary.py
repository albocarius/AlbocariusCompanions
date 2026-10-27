"""Short texts for cards: what a condition / effect / trigger object says, in one line each. No Qt.

Order and names come from the schema catalogue; values are written compactly (``hasGold player ≥ 100``).
"""

from __future__ import annotations

import json
from typing import Any

from ...schema.catalog import SchemaCatalog


def _value(v: Any) -> str:
    if isinstance(v, bool):
        return "true" if v else "false"
    if isinstance(v, (list, tuple)):
        return ", ".join(_value(x) for x in v)
    if isinstance(v, dict):
        return json.dumps(v, ensure_ascii=False)
    return str(v)


def conditions(when: Any, catalog: SchemaCatalog) -> list[str]:
    if not isinstance(when, dict):
        return []
    out = []
    for entry in catalog.conditions_in(when):
        n = entry.name
        if n == "relation":
            lo, hi = when.get("minRelation"), when.get("maxRelation")
            if lo is not None and hi is not None:
                out.append(f"relation {lo}..{hi}")
            else:
                out.append(f"relation ≥ {lo}" if lo is not None else f"relation ≤ {hi}")
        elif n == "var":
            ops = [f"= {_value(when['eq'])}" if "eq" in when else "",
                   f"≥ {when['min']}" if "min" in when else "", f"≤ {when['max']}" if "max" in when else ""]
            out.append(f"{when.get('var')} " + " ".join(o for o in ops if o))
        elif n == "hasGold" and isinstance(when.get("hasGold"), dict):
            g = when["hasGold"]
            out.append(f"gold of {g.get('who')} ≥ {g.get('min')}")
        elif n == "hasItem" and isinstance(when.get("hasItem"), dict):
            i = when["hasItem"]
            out.append(f"{i.get('id')} × {i.get('min', 1)}")
        elif n in ("all", "any"):
            out.append(f"{n} of {len(when[n]) if isinstance(when[n], list) else '?'}")
        elif n == "not":
            inner = conditions(when.get("not"), catalog)
            out.append("not (" + "; ".join(inner) + ")")
        elif n == "minGold":
            out.append(f"player gold ≥ {when['minGold']}")
        elif n == "notFlags":
            out.append("not " + _value(when["notFlags"]))
        else:
            v = when[entry.keys[0]]
            out.append(n if v is True else f"{n} = {_value(v)}")
    return out


def effects(fx: Any, catalog: SchemaCatalog) -> list[str]:
    if not isinstance(fx, dict):
        return []
    out = []
    for entry in catalog.effects_in(fx):
        n = entry.name
        v = fx.get(n)
        if n in ("relation", "partyMorale"):
            out.append(f"{n} {v:+d}" if isinstance(v, int) else f"{n} {v}")
        elif n == "transfer" and isinstance(v, dict):
            what = f"{v['gold']} gold" if "gold" in v else f"{v.get('count', 1)} × {v.get('item')}"
            out.append(f"{what}: {v.get('from')} → {v.get('to')}")
        elif n == "join":
            out.append("joins the clan" if v else "join = false")
        elif n == "setFlags":
            out.append("set " + _value(v))
        elif n == "clearFlags":
            out.append("clear " + _value(v))
        elif n in ("set", "add") and isinstance(v, dict):
            sign = "=" if n == "set" else "+="
            out.extend(f"{k} {sign} {_value(x)}" for k, x in v.items())
        else:
            out.append(f"{n} {_value(v)}")
    return out


def trigger(spec: Any) -> str:
    if not isinstance(spec, dict):
        return ""
    parts = [str(spec.get("on", "?"))]
    if "subject" in spec:
        parts.append(f"subject {spec['subject']}")
    if "chance" in spec:
        parts.append(f"chance {spec['chance']}")
    if "delivery" in spec:
        parts.append(str(spec["delivery"]))
    return " · ".join(parts)
