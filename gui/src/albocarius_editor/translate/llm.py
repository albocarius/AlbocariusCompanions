"""Translating a companion's texts with an LLM the user points at (a local model or any provider). No Qt.

The endpoint speaks the OpenAI-compatible chat API (``POST <base>/chat/completions``): Ollama, LM Studio, llama.cpp,
vLLM and most hosted providers do. One request carries the app's fixed instructions (reply with one JSON object of
id → translation, keep placeholders), the user's own (artistic) instructions, the context (``en.json``, optionally
``dialogs.json``, existing translations) and the ids to translate. The answer is checked line by line before anything
is written: the window applies accepted lines with ``data.edit.set_text`` as one undoable step.
"""

from __future__ import annotations

import json
import re
import urllib.error
import urllib.request
from dataclasses import dataclass, field

from ..data.companion import ENGLISH, CompanionFolder

PLACEHOLDER = re.compile(r"\{[^{}]*\}")  # {PLAYER.NAME}, {SALUTATION}, gender switches {?PLAYER.GENDER}…{?}…{\?}

APP_INSTRUCTIONS = """\
You translate the dialogue of a Mount & Blade II: Bannerlord companion mod from English (language code "en") into the \
language with code "{target}".
Reply with exactly one JSON object and nothing else: no explanation, no code fences. Its keys are exactly the ids listed \
under TRANSLATE, its values the translated texts.
Keep every placeholder in curly braces exactly as written, untranslated: {{PLAYER.NAME}}, {{SALUTATION}}, \
{{?PLAYER.GENDER}}…{{?}}…{{\\?}} and the like; translate only the words between them.
Keep line breaks. Do not translate or change the ids."""


class LlmError(Exception):
    """The endpoint could not be reached or did not answer with a chat completion."""


@dataclass
class Endpoint:
    url: str              # base URL ("http://localhost:11434/v1") or the full .../chat/completions URL
    model: str
    api_key: str = ""     # empty for a local server that needs none
    timeout: float = 600  # local models can be slow on a whole companion

    @property
    def chat_url(self) -> str:
        url = self.url.strip().rstrip("/")
        return url if url.endswith("/chat/completions") else url + "/chat/completions"


@dataclass
class Request:
    """What is sent: the chat messages and the ids (with their English text) it asks for."""

    messages: list[dict[str, str]]
    source: dict[str, str]  # id → English text to translate

    @property
    def prompt_chars(self) -> int:
        return sum(len(m["content"]) for m in self.messages)


def estimate_tokens(text_chars: int) -> int:
    """About 4 characters per token for English and JSON: a rough figure (every model counts a bit differently)."""
    return max(1, round(text_chars / 4))


def estimate(request: Request) -> tuple[int, int]:
    """(≈ input tokens, ≈ output tokens). The output is the translated texts plus their JSON keys; translations run
    a bit longer than English, so ×1.3."""
    out_chars = sum(len(k) + len(v) + 8 for k, v in request.source.items())
    return estimate_tokens(request.prompt_chars), estimate_tokens(int(out_chars * 1.3))


def texts_to_translate(folder: CompanionFolder, target: str, only_missing: bool = True,
                       ids: list[str] | None = None) -> dict[str, str]:
    """English texts (non-empty) to translate into ``target``: all, or those the target language lacks."""
    english = folder.langs[ENGLISH].value.get("lines", {}) if ENGLISH in folder.langs else {}
    chosen = {}
    for local_id, text in english.items():
        if not isinstance(text, str) or not text.strip() or (ids is not None and local_id not in ids):
            continue
        if only_missing and folder.text(local_id, target):
            continue
        chosen[local_id] = text
    return chosen


def build_request(folder: CompanionFolder, target: str, source: dict[str, str], custom_prompt: str = "",
                  include_dialogs: bool = False) -> Request:
    """The chat messages: system = app instructions (+ the user's instructions); user = context and the ids."""
    system = APP_INSTRUCTIONS.format(target=target)
    if custom_prompt.strip():
        system += "\n\nInstructions from the author (style, tone, terms):\n" + custom_prompt.strip()
    parts = []
    if include_dialogs:
        parts.append("dialogs.json (how the lines are used: speakers, conversations, order; for context only):\n"
                     + folder.dialogs.text())
    english = folder.langs[ENGLISH].value.get("lines", {}) if ENGLISH in folder.langs else {}
    parts.append("en.json lines (all English texts of this companion, for context):\n"
                 + json.dumps(english, ensure_ascii=False, indent=1))
    existing = {k: v for k, v in (folder.langs[target].value.get("lines") or {}).items() if v} \
        if target in folder.langs else {}
    if existing:
        parts.append(f"Existing {target} translations (keep their style and terms consistent):\n"
                     + json.dumps(existing, ensure_ascii=False, indent=1))
    parts.append("TRANSLATE (id → English text):\n" + json.dumps(source, ensure_ascii=False, indent=1))
    return Request([{"role": "system", "content": system}, {"role": "user", "content": "\n\n".join(parts)}], source)


def call(endpoint: Endpoint, request: Request) -> str:
    """Sends the request; returns the model's reply text."""
    body = {"model": endpoint.model, "messages": request.messages, "temperature": 0.3, "stream": False}
    data = json.dumps(body).encode("utf-8")
    headers = {"Content-Type": "application/json"}
    if endpoint.api_key.strip():
        headers["Authorization"] = f"Bearer {endpoint.api_key.strip()}"
    req = urllib.request.Request(endpoint.chat_url, data=data, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=endpoint.timeout) as response:
            answer = json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        detail = e.read().decode("utf-8", "replace")[:300]
        raise LlmError(f"HTTP {e.code} from {endpoint.chat_url}: {detail}") from None
    except (urllib.error.URLError, TimeoutError, OSError) as e:
        raise LlmError(f"cannot reach {endpoint.chat_url}: {getattr(e, 'reason', e)}") from None
    except json.JSONDecodeError:
        raise LlmError(f"{endpoint.chat_url} did not answer with JSON") from None
    try:
        return answer["choices"][0]["message"]["content"]
    except (KeyError, IndexError, TypeError):
        raise LlmError(f"unexpected answer: {json.dumps(answer)[:300]}") from None


def test_connection(endpoint: Endpoint) -> str:
    """A one-line request; returns the reply (raises LlmError)."""
    request = Request([{"role": "user", "content": "Reply with the single word: ok"}], {})
    return call(endpoint, request).strip()[:80]


@dataclass
class LineResult:
    local_id: str
    english: str
    translation: str = ""
    problem: str = ""     # "" when the line looks right

    @property
    def ok(self) -> bool:
        return not self.problem


@dataclass
class Result:
    lines: list[LineResult] = field(default_factory=list)
    extra_ids: list[str] = field(default_factory=list)  # ids in the reply that were not asked for (ignored)


def extract_json(reply: str) -> dict:
    """The JSON object in a reply, also when the model wrapped it in a code fence or a sentence."""
    text = reply.strip()
    fence = re.search(r"```(?:json)?\s*(.*?)```", text, re.DOTALL)
    if fence:
        text = fence.group(1).strip()
    start, end = text.find("{"), text.rfind("}")
    if start < 0 or end < start:
        raise LlmError("the reply holds no JSON object")
    try:
        value = json.loads(text[start:end + 1])
    except json.JSONDecodeError as e:
        raise LlmError(f"the reply is not valid JSON ({e.msg} at character {e.pos})") from None
    if not isinstance(value, dict):
        raise LlmError("the reply's JSON is not an object of id → text")
    return value


def check(source: dict[str, str], reply: str) -> Result:
    """Each asked id with its translation and what is wrong with it: missing, empty, not text, or placeholders that
    differ from the English (``{…}`` tokens must stay exactly as they are)."""
    value = extract_json(reply)
    result = Result(extra_ids=[k for k in value if k not in source])
    for local_id, english in source.items():
        line = LineResult(local_id, english)
        got = value.get(local_id)
        if local_id not in value:
            line.problem = "missing from the reply"
        elif not isinstance(got, str):
            line.problem = "not a text"
            line.translation = json.dumps(got, ensure_ascii=False)
        elif not got.strip():
            line.problem = "empty"
        else:
            line.translation = got
            want, have = sorted(PLACEHOLDER.findall(english)), sorted(PLACEHOLDER.findall(got))
            if want != have:
                lost = [p for p in want if p not in have]
                added = [p for p in have if p not in want]
                line.problem = "placeholders changed" + (f": lost {' '.join(lost)}" if lost else "") \
                               + (f"; new {' '.join(added)}" if added else "")
        result.lines.append(line)
    return result
