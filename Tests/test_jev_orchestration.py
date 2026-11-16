"""Offline regression checks for plan validation, routing, clarification snapshots,
alternative acceptance, and evidence-constrained summaries. Model/compiler
callbacks are injected fixtures; no live model requests are made."""

import functools
import copy
import pathlib
import sys
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1] / "Assets/Voice/PythonScripts"))
from jev_orchestration import JevService, validate_plan


CONTEXT = {"graph_id": "school:1", "numeric_attributes": ["gpa", "grade"],
           "relationship_types": ["aggression", "friendship"], "selected_node_ids": [1], "max_steps": 8}


def example_plan():
    def step(sid, op, args, output, deps):
        return dict(id=sid, operation=op, description=op, args=args, output=output, depends_on=deps)
    return {"summary": "Rank five nodes, color their outgoing aggression links, and compare GPA.", "steps": [
        step("s1", "rank_nodes", dict(input="scope", relationship="aggression", direction="outgoing", limit=5), "top", []),
        step("s2", "select_nodes", dict(input="top"), "chosen", ["s1"]),
        step("s3", "find_links", dict(input="top", relationship="aggression", direction="outgoing"), "links", ["s1"]),
        step("s4", "color_links", dict(input="links", color="#FF0000"), "painted", ["s3"]),
        step("s5", "compare_numeric", dict(input="top", attribute="gpa"), "comparison", ["s1"]),
    ]}


class PlanTests(unittest.TestCase):
    def test_valid_example_and_copy(self):
        plan = example_plan()
        validated = validate_plan(plan, CONTEXT)
        plan["steps"][0]["args"]["limit"] = 99
        self.assertEqual(validated["steps"][0]["args"]["limit"], 5)

    def test_reject_invalid_plans_before_execution(self):
        changes = [
            lambda p: p["steps"][0].update(operation="execute_cypher"),
            lambda p: p["steps"][0]["args"].update(query="DELETE n"),
            lambda p: p["steps"][1].update(depends_on=[]),
            lambda p: p["steps"][0]["args"].update(input="future"),
            lambda p: p["steps"][3]["args"].update(input="top"),
            lambda p: p["steps"][4]["args"].update(attribute="unknown"),
            lambda p: p["steps"][0]["args"].update(relationship="unknown"),
            lambda p: p["steps"][0]["args"].update(direction="sideways"),
            lambda p: p["steps"][0]["args"].update(limit=True),
            lambda p: p["steps"][0]["args"].update(limit=0),
            lambda p: p["steps"][1].update(output="scope"),
            lambda p: p["steps"][1].update(id="s1"),
            lambda p: p["steps"][3]["args"].update(color="red"),
            lambda p: p["steps"][1].update(depends_on=["s5"]),
        ]
        for change in changes:
            plan = example_plan()
            change(plan)
            with self.subTest(plan=plan), self.assertRaises(ValueError):
                validate_plan(plan, CONTEXT)
        with self.assertRaises(ValueError):
            validate_plan(example_plan(), CONTEXT, max_steps=4)


def inline_async(test):
    # All I/O is mocked by immediately completing coroutines. Avoid opening an
    # OS event loop/socket pair just to exercise the orchestration state machine.
    @functools.wraps(test)
    def run(self):
        coroutine = test(self)
        try:
            coroutine.send(None)
        except StopIteration:
            return
        finally:
            coroutine.close()
        self.fail("Test unexpectedly attempted real asynchronous I/O")
    return run


class ServiceTests(unittest.TestCase):
    def setUp(self):
        self.answers, self.calls, self.direct_calls = [], [], []

        async def model(role, prompt, payload):
            self.calls.append((role, copy.deepcopy(payload)))
            return self.answers.pop(0)

        async def direct(text, context):
            self.direct_calls.append((text, copy.deepcopy(context)))
            return {"actions": [["colorNode", "#0000FF"]], "queries": [""]}

        self.service = JevService(model, direct)

    @staticmethod
    def route(route, **extra):
        return dict(route=route, clarity="unclear" if route == "clarify" else "clear",
                    complexity="multi_step" if route == "plan" else "simple", **extra)

    def request(self, text, **extra):
        return dict(userText=text, context=copy.deepcopy(CONTEXT), conversationId="a", **extra)

    @inline_async
    async def test_alternative_is_proposed_before_execution_and_can_be_accepted(self):
        self.answers = [self.route("plan"), {
            "unsupported": "Correlation is unavailable.",
            "alternative": {
                "request": "Compare GPA for the top five outgoing aggression nodes.",
                "question": "Instead compare group means: 1. Rank five nodes. 2. Compare GPA. Proceed?",
            },
        }, self.route("plan", resolved_request="Compare GPA for the top five outgoing aggression nodes."),
            example_plan()]
        proposal = await self.service.classify(self.request("Calculate correlation"))
        self.assertEqual(proposal["route"], "clarify")
        self.assertEqual(proposal["actions"], [])
        self.assertNotIn("plan", proposal)
        self.assertFalse(self.direct_calls)
        accepted = await self.service.classify(self.request(
            "yes", clarificationId=proposal["clarification_id"]))
        self.assertEqual(accepted["route"], "plan")
        self.assertIn("Compare GPA", self.calls[2][1]["pending"]["original"])
        self.assertFalse(self.service.pending)

    @inline_async
    async def test_planner_receives_registry_and_can_compose_supported_steps(self):
        plan = {
            "summary": "Compare GPA for students in grade nine or above.",
            "steps": [
                dict(id="filter", operation="filter_numeric", description="Filter grade",
                     args=dict(input="scope", attribute="grade", operator="gte", value=9),
                     depends_on=[], output="older_students"),
                dict(id="compare", operation="compare_numeric", description="Compare GPA",
                     args=dict(input="older_students", attribute="gpa"),
                     depends_on=["filter"], output="comparison"),
            ],
        }
        self.answers = [self.route("plan"), plan]
        result = await self.service.classify(self.request("Compare GPA by grade group"))
        self.assertEqual(result["plan"]["steps"], plan["steps"])
        registry = self.calls[1][1]["operations"]
        self.assertEqual(registry["filter_numeric"]["output_type"], "nodes")
        self.assertIn("operator", registry["filter_numeric"]["required_args"])
        self.assertFalse(self.direct_calls)

    @inline_async
    async def test_malformed_model_outputs_are_rejected(self):
        for answers in ([[]], [self.route("plan"), []],
                        [self.route("plan"), {"unsupported": "Missing tool", "steps": []}]):
            self.answers = list(answers)
            with self.assertRaises(ValueError):
                await self.service.classify(self.request("Analyze GPA"))
        plan = example_plan()
        plan["steps"][0]["operation"] = []
        with self.assertRaises(ValueError):
            validate_plan(plan, CONTEXT)

    @inline_async
    async def test_simple_request_never_calls_planner(self):
        self.answers = [self.route("direct", resolved_request="Color selected nodes blue")]
        result = await self.service.classify(self.request("Color them blue"))
        self.assertEqual(result["route"], "direct")
        self.assertEqual([c[0] for c in self.calls], ["router"])
        self.assertEqual(len(self.direct_calls), 1)

    @inline_async
    async def test_clarification_resumes_original_snapshot(self):
        self.answers = [self.route("clarify", question="Incoming or outgoing aggression links?"),
                        self.route("plan", resolved_request="Rank outgoing aggression and compare GPA"), example_plan()]
        first = await self.service.classify(self.request("Rank aggression and compare GPA"))
        followup = self.request("outgoing", clarificationId=first["clarification_id"])
        followup["context"]["selected_node_ids"] = [99]
        result = await self.service.classify(followup)
        self.assertEqual(result["route"], "plan")
        self.assertEqual(self.calls[1][1]["pending"]["original"], "Rank aggression and compare GPA")
        self.assertEqual(self.calls[2][1]["context"]["selected_node_ids"], [1])
        self.assertFalse(self.service.pending)
        self.assertFalse(self.direct_calls)

    @inline_async
    async def test_clarification_isolation_and_expiry(self):
        self.answers = [self.route("clarify", question="Which nodes?")]
        first = await self.service.classify(self.request("Color those"))
        other = self.request("selected", clarificationId=first["clarification_id"])
        other["conversationId"] = "b"
        self.assertEqual((await self.service.classify(other))["route"], "unsupported")
        self.service.pending[first["clarification_id"]]["expires"] = 0
        self.assertEqual((await self.service.classify(self.request("selected", clarificationId=first["clarification_id"])))["route"], "unsupported")
        self.assertFalse(self.direct_calls)

    @inline_async
    async def test_missing_rewrite_keeps_original_request_and_answer(self):
        self.answers = [self.route("clarify", question="Which direction?"), self.route("plan"), example_plan()]
        first = await self.service.classify(self.request("Rank aggression and compare GPA"))
        await self.service.classify(self.request("outgoing", clarificationId=first["clarification_id"]))
        self.assertIn("Rank aggression and compare GPA", self.calls[2][1]["request"])
        self.assertIn("outgoing", self.calls[2][1]["request"])

    @inline_async
    async def test_unsupported_does_not_execute(self):
        self.answers = [self.route("unsupported", message="Significance testing is unavailable.")]
        result = await self.service.classify(self.request("Test significance"))
        self.assertEqual(result["actions"], [])
        self.assertFalse(self.direct_calls)

    @inline_async
    async def test_ambiguous_cannot_execute(self):
        self.answers = [dict(route="plan", clarity="unclear", complexity="multi_step")]
        with self.assertRaises(ValueError):
            await self.service.classify(self.request("Find important nodes"))
        self.assertFalse(self.direct_calls)

    @inline_async
    async def test_invalid_late_step_rejects_whole_plan(self):
        plan = example_plan()
        plan["steps"][-1]["args"]["attribute"] = "unavailable"
        self.answers = [self.route("plan"), plan]
        with self.assertRaises(ValueError):
            await self.service.classify(self.request("Rank and compare"))
        self.assertFalse(self.direct_calls)

    @inline_async
    async def test_summary_only_uses_verified_sentences_and_keeps_failures(self):
        self.answers = [{"highlight_ids": ["s1"]}]
        data = {"command_id": "c", "steps": [
            {"StepId": "s1", "Status": "succeeded", "SummaryText": "Selected 5 nodes."},
            {"StepId": "s2", "Status": "failed", "SummaryText": "Comparison failed."}]}
        result = await self.service.summarize(data)
        self.assertEqual(result["summary"], "Comparison failed.\nSelected 5 nodes.")
        self.assertEqual(result["command_id"], "c")
        self.answers = [{"highlight_ids": ["invented"]}]
        with self.assertRaises(ValueError):
            await self.service.summarize(data)


if __name__ == "__main__":
    unittest.main()
