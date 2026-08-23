import json
import os
import uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlparse

MODE = os.getenv("FAKE_MODE", "notify")
PORT = int(os.getenv("PORT", "4010"))
STORE = Path(os.getenv("REQUEST_STORE", "/data/requests.jsonl"))
FIXTURE_ROOT = Path("/fixtures")

FIELD_TYPES = {
    "static": {"name": "Symbol", "heading": "Symbol", "body": "Text", "footer": "Boolean"},
    "resource": {"name": "Symbol", "body": "Text"},
    "course": {"service_name": "Symbol", "internal_mailbox": "Symbol", "privacy_policy_url": "Symbol", "feedback": "Array"},
    "trainingModule": {"title": "Symbol", "name": "Symbol", "upcoming": "Text", "description": "Text", "outcomes": "Text", "criteria": "Text", "about": "Text", "duration": "Number", "position": "Integer", "pages": "Array"},
    "page": {"name": "Symbol", "page_type": "Symbol", "heading": "Text", "body": "Text", "notes": "Boolean"},
    "video": {"name": "Symbol", "page_type": "Symbol", "heading": "Text", "body": "Text", "title": "Symbol", "video_id": "Symbol", "video_provider": "Symbol", "transcript": "Text"},
    "question": {"name": "Symbol", "page_type": "Symbol", "body": "Text", "success_message": "Text", "failure_message": "Text", "answers": "Object", "other": "Text", "or": "Text", "more": "Boolean", "multi_select": "Boolean", "skippable": "Boolean"},
    "userSetting": {"name": "Symbol", "title": "Symbol", "local_authority": "Boolean", "role_type": "Symbol", "active": "Boolean"},
}

def sys_data(identifier, kind="Entry", content_type=None):
    value = {
        "id": identifier,
        "type": kind,
        "createdAt": "2026-01-01T00:00:00Z",
        "updatedAt": "2026-01-01T00:00:00Z",
        "revision": 1,
        "space": {"sys": {"type": "Link", "linkType": "Space", "id": "synthetic"}},
        "environment": {"sys": {"type": "Link", "linkType": "Environment", "id": "master"}},
    }
    if content_type:
        value["contentType"] = {"sys": {"type": "Link", "linkType": "ContentType", "id": content_type}}
    return value

def link(identifier):
    return {"sys": {"type": "Link", "linkType": "Entry", "id": identifier}}

def entry(identifier, content_type, fields):
    return {"sys": sys_data(identifier, content_type=content_type), "fields": fields}

def content_types():
    items = []
    for name, fields in FIELD_TYPES.items():
        definitions = []
        for field_name, field_type in fields.items():
            definition = {"id": field_name, "name": field_name, "type": field_type, "required": False, "localized": False, "validations": [], "disabled": False, "omitted": False}
            if field_type == "Array":
                definition["items"] = {"type": "Link", "linkType": "Entry", "validations": []}
            definitions.append(definition)
        items.append({"sys": sys_data(name, "ContentType"), "name": name, "displayField": next(iter(fields)), "description": "Synthetic parity content", "fields": definitions})
    return collection(items)

def content_type(identifier):
    return next((item for item in content_types()["items"] if item["sys"]["id"] == identifier), None)

def collection(items, includes=None):
    value = {"sys": {"type": "Array"}, "total": len(items), "skip": 0, "limit": 1000, "items": items}
    if includes:
        value["includes"] = {"Entry": includes}
    return value

def load_json(name):
    path = FIXTURE_ROOT / name
    if not path.exists():
        path = FIXTURE_ROOT / "data" / name
    return json.loads(path.read_text(encoding="utf-8-sig"))

def content_entries(content_type, query):
    if content_type == "resource":
        # The Rails backend falls back to its committed locale when Contentful has
        # no override. Returning a made-up resource here would silently replace
        # every locale value and invalidate the parity result.
        return collection([])
    if content_type == "course":
        feedback = load_json("demo-feedback-content.json").get("questions", [])
        questions = [question_entry(x, "feedback-" + str(i)) for i, x in enumerate(feedback)]
        course = entry("course", "course", {"service_name": "Early years child development training", "internal_mailbox": "child-development.training@education.gov.uk", "privacy_policy_url": "/privacy-policy", "feedback": [link(x["sys"]["id"]) for x in questions]})
        return collection([course], questions)
    if content_type == "static":
        pages = [entry("static-" + x["name"], "static", {"name": x["name"], "heading": x.get("heading", x.get("title", x["name"])), "body": x.get("body", ""), "footer": x.get("footer", False)}) for x in load_json("demo-static-pages.json").get("pages", [])]
        name = query.get("fields.name", [None])[0]
        return collection([x for x in pages if name is None or x["fields"]["name"] == name])
    if content_type == "trainingModule":
        modules, children = [], []
        for module in load_json("demo-training-content.json").get("modules", []):
            pages = []
            for index, page in enumerate(module.get("pages", [])):
                identifier = module["name"] + "-" + str(index)
                if page.get("answers"):
                    child = question_entry(page, identifier)
                elif page.get("pageType") == "video_page":
                    child = video_entry(page, identifier)
                else:
                    child = page_entry(page, identifier)
                children.append(child)
                pages.append(link(child["sys"]["id"]))
            fields = {key: module.get(key) for key in ("title", "name", "upcoming", "description", "outcomes", "criteria", "duration", "position") if module.get(key) is not None}
            fields["about"] = module.get("description", "")
            fields["pages"] = pages
            modules.append(entry("module-" + module["name"], "trainingModule", fields))
        requested = query.get("fields.name", [None])[0]
        if requested:
            modules = [item for item in modules if item["fields"].get("name") == requested]
            child_ids = {link["sys"]["id"] for item in modules for link in item["fields"].get("pages", [])}
            children = [child for child in children if child["sys"]["id"] in child_ids]
        return collection(modules, children)
    if content_type == "userSetting":
        settings = []
        for item in load_json("reference-data.json").get("settingTypes", []):
            if item["id"] == "other":
                continue
            settings.append(entry("setting-" + item["id"], "userSetting", {
                "name": item["id"],
                "title": item["label"],
                "local_authority": item.get("requiresLocalAuthority", False),
                "role_type": item.get("roleGroup", "none"),
                "active": True,
            }))
        requested = query.get("fields.name", [None])[0]
        return collection([item for item in settings if requested is None or item["fields"]["name"] == requested])
    return collection([])

def page_entry(page, identifier):
    return entry(identifier, "page", {"name": page["name"], "page_type": page.get("pageType", "text_page"), "heading": page.get("heading", page["name"]), "body": page.get("body", ""), "notes": page.get("notes", False)})

def video_entry(page, identifier):
    return entry(identifier, "video", {
        "name": page["name"],
        "page_type": "video_page",
        "heading": page.get("heading", page["name"]),
        "body": page.get("body", ""),
        "title": page.get("videoTitle", page.get("heading", page["name"])),
        "video_id": page.get("videoId"),
        "video_provider": page.get("videoProvider"),
        "transcript": page.get("transcript", ""),
    })

def question_entry(question, identifier):
    answers = [[x.get("text", ""), x.get("correct", False)] for x in question.get("answers", [])]
    if not answers:
        answers = [[x, False] for x in question.get("options", [])]
    fields = {"name": question["name"], "page_type": question.get("pageType", "feedback"), "body": question.get("body") or question.get("legend") or question.get("heading", ""), "success_message": question.get("successMessage", "Thank you"), "failure_message": question.get("failureMessage", "Thank you"), "answers": answers, "more": question.get("hasMore", False), "multi_select": question.get("inputType") == "checkbox", "skippable": question.get("skippable", False)}
    if question.get("otherLabel"): fields["other"] = question["otherLabel"]
    if question.get("orLabel"): fields["or"] = question["orLabel"]
    return entry(identifier, "question", fields)

class Handler(BaseHTTPRequestHandler):
    def _json(self, status, payload):
        body = json.dumps(payload).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _record(self, body):
        STORE.parent.mkdir(parents=True, exist_ok=True)
        with STORE.open("a", encoding="utf-8") as output:
            output.write(json.dumps({"method": self.command, "path": self.path, "headers": dict(self.headers), "body": body}) + "\n")

    def do_GET(self):
        if self.path == "/health":
            return self._json(200, {"status": "ok", "mode": MODE})
        if self.path == "/_requests":
            rows = [json.loads(line) for line in STORE.read_text().splitlines()] if STORE.exists() else []
            return self._json(200, rows)
        self._record("")
        if MODE == "contentful":
            parsed = urlparse(self.path)
            if parsed.path.endswith("/content_types"):
                return self._json(200, content_types())
            if "/content_types/" in parsed.path:
                identifier = parsed.path.rsplit("/", 1)[-1]
                item = content_type(identifier)
                return self._json(200, item) if item else self._json(404, {"error": "not found"})
            if parsed.path.endswith("/entries"):
                query = parse_qs(parsed.query)
                return self._json(200, content_entries(query.get("content_type", [""])[0], query))
        self._json(404, {"error": "not found"})

    def do_POST(self):
        length = int(self.headers.get("Content-Length", "0"))
        body = self.rfile.read(length).decode()
        self._record(body)
        if MODE == "notify" and self.path == "/v2/notifications/email":
            return self._json(201, {"id": str(uuid.uuid4()), "content": {"body": "synthetic"}})
        self._json(200, {"status": "recorded"})

    def log_message(self, format, *args):
        return

ThreadingHTTPServer(("0.0.0.0", PORT), Handler).serve_forever()
