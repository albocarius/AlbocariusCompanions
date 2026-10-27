from pathlib import Path

import pytest

from albocarius_editor.data import jsonfmt

from .conftest import SAMPLE, TEMPLATE


def all_json(root: Path) -> list[Path]:
    return sorted(root.rglob("*.json"))


@pytest.mark.parametrize("path", all_json(SAMPLE) + all_json(TEMPLATE), ids=lambda p: f"{p.parent.name}/{p.name}")
def test_unchanged_document_is_byte_identical(path: Path):
    text = path.read_bytes().decode("utf-8")
    value, layout = jsonfmt.loads(text)
    assert jsonfmt.dumps(value, layout) == text


def test_edit_changes_only_the_touched_line():
    text = '{\n  "a": { "x": 1, "y": 2 },\n  "rows": [\n    {"id": "r1", "v": 1},\n    {"id": "r2", "v": 2}\n  ],\n  "s": "caf\\u00e9"\n}\n'
    value, layout = jsonfmt.loads(text)
    value["rows"][1]["v"] = 3
    out = jsonfmt.dumps(value, layout)
    assert out == text.replace('{"id": "r2", "v": 2}', '{"id": "r2", "v": 3}')


def test_inline_container_keeps_its_padding_when_changed():
    value, layout = jsonfmt.loads('{\n  "a": { "x": 1 },\n  "b": {"y": 1}\n}\n')
    value["a"]["z"] = 2
    value["b"]["w"] = 2
    assert jsonfmt.dumps(value, layout) == '{\n  "a": { "x": 1, "z": 2 },\n  "b": {"y": 1, "w": 2}\n}\n'


def test_rows_are_keyed_by_id_so_inserting_keeps_the_others():
    text = '{\n  "rows": [\n    {"id": "a",   "v": 1},\n    {"id": "b",   "v": 2}\n  ]\n}\n'
    value, layout = jsonfmt.loads(text)
    value["rows"].insert(0, {"id": "new", "v": 0})
    out = jsonfmt.dumps(value, layout)
    # The odd spacing of the untouched rows survives; the new row is written expanded.
    assert '{"id": "a",   "v": 1}' in out and '{"id": "b",   "v": 2}' in out
    assert '"id": "new"' in out


def test_new_containers_are_expanded_with_the_file_indent():
    value, layout = jsonfmt.loads('{\n    "a": 1\n}\n')
    value["b"] = {"c": [1, 2], "d": []}
    assert jsonfmt.dumps(value, layout) == ('{\n    "a": 1,\n    "b": {\n        "c": [\n            1,\n            2\n        ],\n'
                                           '        "d": []\n    }\n}\n')


def test_crlf_bom_and_missing_final_newline_are_kept():
    text = '﻿{\r\n  "a": 1,\r\n  "b": [\r\n    1\r\n  ]\r\n}'
    value, layout = jsonfmt.loads(text)
    assert jsonfmt.dumps(value, layout) == text
    value["b"].append(2)
    assert jsonfmt.dumps(value, layout) == '﻿{\r\n  "a": 1,\r\n  "b": [\r\n    1,\r\n    2\r\n  ]\r\n}'


def test_bool_replacing_an_equal_int_is_a_change():
    value, layout = jsonfmt.loads('{"a": 1}')
    value["a"] = True
    assert jsonfmt.dumps(value, layout) == '{"a": true}'


def test_original_string_escapes_and_number_spelling_survive():
    text = '{"t": "\\u00e9\\/x", "n": 1.50}'
    value, layout = jsonfmt.loads(text)
    assert value == {"t": "é/x", "n": 1.5}
    assert jsonfmt.dumps(value, layout) == text


def test_no_layout_writes_two_space_json():
    assert jsonfmt.dumps({"a": [1, {"b": "ü"}], "c": {}}) == '{\n  "a": [\n    1,\n    {\n      "b": "ü"\n    }\n  ],\n  "c": {}\n}\n'


@pytest.mark.parametrize("bad", ['{"a": }', '{"a": 1', '[1, 2', '{"a": 1} x', '{a: 1}', '"abc'])
def test_syntax_errors_name_the_place(bad: str):
    with pytest.raises(jsonfmt.JsonSyntaxError, match="line 1"):
        jsonfmt.loads(bad)
