"""LLM translation (no Qt): request building, the HTTP call against a fake OpenAI-compatible server, reply checks."""

import json
import re
import threading
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path

import pytest

from albocarius_editor.data.companion import CompanionFolder
from albocarius_editor.translate import llm


class FakeChat:
    """A local OpenAI-compatible /v1/chat/completions; ``reply`` builds the answer from the request body."""

    def __init__(self, reply):
        self.reply = reply
        self.requests: list[dict] = []
        outer = self

        class Handler(BaseHTTPRequestHandler):
            def do_POST(self):
                body = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
                outer.requests.append({"path": self.path, "auth": self.headers.get("Authorization"), "body": body})
                status, payload = outer.reply(body)
                data = json.dumps(payload).encode()
                self.send_response(status)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(data)))
                self.end_headers()
                self.wfile.write(data)

            def log_message(self, *args):
                pass

        self.server = HTTPServer(("127.0.0.1", 0), Handler)
        threading.Thread(target=self.server.serve_forever, daemon=True).start()
        self.url = f"http://127.0.0.1:{self.server.server_port}/v1"

    def close(self):
        self.server.shutdown()


def completion(content: str) -> dict:
    return {"choices": [{"message": {"role": "assistant", "content": content}}]}


def translate_all(body: dict) -> tuple[int, dict]:
    """Answers with every TRANSLATE id, the text upper-cased (placeholders kept), inside a code fence."""
    user = body["messages"][-1]["content"]
    if "TRANSLATE" not in user:  # the connection test
        return 200, completion("ok")
    source = json.loads(user.split("TRANSLATE (id → English text):\n", 1)[1])
    # Upper-case the words, keep the placeholders: re.split with a group keeps them at odd positions.
    fixed = {k: "".join(part if i % 2 else part.upper() for i, part in enumerate(re.split(r"(\{[^{}]*\})", v)))
             for k, v in source.items()}
    return 200, completion("Here you go:\n```json\n" + json.dumps(fixed, ensure_ascii=False) + "\n```")


@pytest.fixture
def folder(sample: Path) -> CompanionFolder:
    return CompanionFolder.load(sample)


def test_request_contents(folder: CompanionFolder):
    source = llm.texts_to_translate(folder, "de")
    assert "scar_story" in source and len(source) == len([v for v in folder.langs["en"].value["lines"].values() if v])
    request = llm.build_request(folder, "de", source, custom_prompt="Gruff soldier voice.", include_dialogs=True)
    system, user = request.messages[0]["content"], request.messages[1]["content"]
    assert '"de"' in system and "exactly one JSON object" in system and "Gruff soldier voice." in system
    assert "dialogs.json" in user and '"conversations"' in user and "TRANSLATE" in user
    smaller = llm.build_request(folder, "de", source)
    assert "dialogs.json" not in smaller.messages[1]["content"]
    tokens_in, tokens_out = llm.estimate(request)
    assert tokens_in > llm.estimate(smaller)[0] > 0 and tokens_out > 0


def test_only_missing_and_existing_translations(folder: CompanionFolder):
    assert "scar_story" not in llm.texts_to_translate(folder, "tr")  # the sample's tr.json has it
    assert "scar_story" in llm.texts_to_translate(folder, "tr", only_missing=False)
    request = llm.build_request(folder, "tr", {"scar_story": folder.text("scar_story")})
    assert "Existing tr translations" in request.messages[1]["content"]


def test_call_and_check_through_a_fake_server(folder: CompanionFolder):
    server = FakeChat(translate_all)
    try:
        source = llm.texts_to_translate(folder, "de")
        request = llm.build_request(folder, "de", source)
        endpoint = llm.Endpoint(server.url, "local-model", api_key="sk-test")
        result = llm.check(source, llm.call(endpoint, request))
        assert all(line.ok for line in result.lines), [l.problem for l in result.lines if not l.ok]
        assert {l.local_id for l in result.lines} == set(source)
        sent = server.requests[0]
        assert sent["path"] == "/v1/chat/completions" and sent["auth"] == "Bearer sk-test"
        assert sent["body"]["model"] == "local-model"
        assert llm.test_connection(llm.Endpoint(server.url + "/chat/completions", "m")) != ""
        assert server.requests[-1]["auth"] is None  # no key: no header
    finally:
        server.close()


def test_errors_are_reported():
    server = FakeChat(lambda body: (401, {"error": {"message": "bad key"}}))
    try:
        with pytest.raises(llm.LlmError, match="HTTP 401"):
            llm.test_connection(llm.Endpoint(server.url, "m"))
    finally:
        server.close()
    with pytest.raises(llm.LlmError, match="cannot reach"):
        llm.test_connection(llm.Endpoint("http://127.0.0.1:9/v1", "m", timeout=5))


def test_reply_checks():
    source = {"a": "Hello {PLAYER.NAME}.", "b": "Bye.", "c": "Yes.", "d": "No."}
    reply = json.dumps({"a": "Hallo {SPIELER}.", "b": "", "c": 3, "x": "extra"})
    result = llm.check(source, reply)
    problems = {l.local_id: l.problem for l in result.lines}
    assert problems["a"].startswith("placeholders changed: lost {PLAYER.NAME}")
    assert problems == {"a": problems["a"], "b": "empty", "c": "not a text", "d": "missing from the reply"}
    assert result.extra_ids == ["x"]
    with pytest.raises(llm.LlmError, match="no JSON"):
        llm.check(source, "Sorry, I cannot help with that.")
