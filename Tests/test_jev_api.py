"""Mocked Flask endpoint contract checks for Jev classification and summaries.
Run in the server Python environment; missing server imports skip this suite.
These tests do not evaluate live models, network services, or headset interaction."""

# Offline Flask contract tests, using installed server dependencies and mocked models.
import importlib.util
import os
import pathlib
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1] / "Assets/Voice/PythonScripts"))
from jev_orchestration import JevService
from test_jev_orchestration import CONTEXT, example_plan


@unittest.skipUnless(importlib.util.find_spec("flask") and importlib.util.find_spec("langchain_openai"),
                     "Run with the project's server Python environment")
class ApiTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        with patch.dict(os.environ, {"OPENAI_API_KEY": "offline-test-placeholder"}):
            import langgraph_server
        cls.server = langgraph_server

    def setUp(self):
        self.answers = []

        async def model(role, prompt, payload):
            answer = self.answers.pop(0)
            if isinstance(answer, Exception):
                raise answer
            return answer

        async def direct(text, context):
            return {"input": text, "corrected_input": text, "actions": [["colorNode", "#FF0000"]],
                    "queries": [""], "stats": [], "clarify": ""}

        self.original = self.server.jev
        self.server.jev = JevService(model, direct)
        self.client = self.server.app.test_client()

    def tearDown(self):
        self.server.jev = self.original

    def route(self, name, **kwargs):
        return dict(route=name, clarity="unclear" if name == "clarify" else "clear", complexity="multi_step" if name == "plan" else "simple", **kwargs)

    def test_direct_contract_preserves_legacy_fields(self):
        self.answers = [self.route("direct")]
        response = self.client.post("/classify", json={"userText": "Color nodes red", "context": CONTEXT})
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.json["route"], "direct")
        self.assertEqual(response.json["actions"], [["colorNode", "#FF0000"]])

    def test_plan_and_clarification_round_trip(self):
        self.answers = [self.route("clarify", question="Incoming or outgoing?"), self.route("plan"), example_plan()]
        first = self.client.post("/classify", json={"userText": "Rank aggression and compare GPA", "context": CONTEXT, "conversationId": "test"})
        self.assertEqual(first.json["clarify"], "Incoming or outgoing?")
        second = self.client.post("/classify", json={"userText": "Outgoing", "context": CONTEXT,
            "conversationId": "test", "clarificationId": first.json["clarification_id"]})
        self.assertEqual(second.status_code, 200)
        self.assertEqual(len(second.json["plan"]["steps"]), 5)
        self.assertTrue(second.json["plan"]["plan_id"])
        self.assertEqual(second.json["actions"], [])

    def test_bad_plan_returns_no_actions(self):
        plan = example_plan()
        plan["steps"][-1]["args"]["attribute"] = "unknown"
        self.answers = [self.route("plan"), plan]
        response = self.client.post("/classify", json={"userText": "Rank and compare", "context": CONTEXT})
        self.assertEqual(response.status_code, 422)
        self.assertEqual(response.json["actions"], [])

    def test_model_failure_returns_explicit_unavailable_result(self):
        self.answers = [RuntimeError("fixture offline")]
        response = self.client.post("/classify", json={"userText": "Color red", "context": CONTEXT})
        self.assertEqual(response.status_code, 503)
        self.assertIn("no command was executed", response.json["error"])

    def test_summary_failure_does_not_change_results(self):
        self.answers = [RuntimeError("fixture summary failure")]
        response = self.client.post("/summarize", json={"command_id": "c", "steps": [
            {"StepId": "s1", "Status": "succeeded", "SummaryText": "Selected 5 nodes."}]})
        self.assertEqual(response.status_code, 503)
        self.assertIn("results remain valid", response.json["error"])

    def test_summary_correlates_execution_and_reuses_evidence(self):
        self.answers = [{"highlight_ids": ["s1"]}]
        response = self.client.post("/summarize", json={"command_id": "c", "steps": [
            {"StepId": "s1", "Status": "succeeded", "SummaryText": "Mean GPA: 3.2."}]})
        self.assertEqual(response.json, {"command_id": "c", "summary": "Mean GPA: 3.2."})


if __name__ == "__main__":
    unittest.main()
