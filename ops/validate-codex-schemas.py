#!/usr/bin/env python3
"""Bounded local structural preflight for the project's Codex V2 output schemas.

This checks the structural rules and published size limits used here, not model
availability, model output quality or acceptance by the live Codex endpoint.
No network, provider calls or schema rewriting occurs.
Reference: https://developers.openai.com/api/docs/guides/structured-outputs
"""

import argparse
import json
import math
from pathlib import Path
import sys


DEFAULT_SCHEMAS = (
    "model-b-v2.output-schema.json",
    "model-b-unity-god-v2.output-schema.json",
    "model-j-v2.output-schema.json",
)
TYPES = {"object", "array", "string", "number", "integer", "boolean", "null"}
UNSUPPORTED = {
    "allOf", "oneOf", "not", "dependentRequired", "dependentSchemas",
    "if", "then", "else", "prefixItems", "patternProperties",
}


def pointer_component(value):
    return str(value).replace("~", "~0").replace("/", "~1")


def matches_type(value, declared):
    """Use JSON Schema value types, not Python's bool-is-an-int inheritance."""
    if declared == "null":
        return value is None
    if declared == "boolean":
        return isinstance(value, bool)
    if declared == "integer":
        return (not isinstance(value, bool)
                and (isinstance(value, int)
                     or (isinstance(value, float) and math.isfinite(value)
                         and value.is_integer())))
    if declared == "number":
        return (not isinstance(value, bool)
                and (isinstance(value, int)
                     or (isinstance(value, float) and math.isfinite(value))))
    if declared == "string":
        return isinstance(value, str)
    if declared == "array":
        return isinstance(value, list)
    if declared == "object":
        return isinstance(value, dict)
    return False


def inspect_schema(root):
    issues = []
    metrics = {"properties": 0, "enum_values": 0, "identifier_characters": 0,
               "object_depth": 0, "container_depth": 0, "recursive_refs": False}

    def issue(path, text):
        issues.append(f"{path or '#'}: {text}")

    def resolve(reference):
        if reference == "#":
            return root
        if not isinstance(reference, str) or not reference.startswith("#/"):
            raise ValueError("only local JSON Pointer refs are permitted")
        value = root
        for component in reference[2:].split("/"):
            component = component.replace("~1", "/").replace("~0", "~")
            value = value[component]
        if not isinstance(value, dict):
            raise ValueError("ref must resolve to a schema object")
        return value

    def walk(schema, path):
        if not isinstance(schema, dict):
            issue(path, "schema must be an object, not boolean or tuple")
            return
        for keyword in sorted(UNSUPPORTED.intersection(schema)):
            issue(path, f"unsupported schema keyword {keyword}")
        if not any(key in schema for key in ("type", "$ref", "anyOf")):
            issue(path, "missing type (unless using $ref or anyOf)")
        declared = schema.get("type")
        types = declared if isinstance(declared, list) else [declared]
        valid_types = bool(types) and all(isinstance(t, str) and t in TYPES for t in types)
        if declared is not None and not valid_types:
            issue(path, "unknown or invalid type")
        if "$ref" in schema:
            try:
                resolve(schema["$ref"])
            except (KeyError, TypeError, ValueError) as error:
                issue(path + "/$ref", str(error))
        if "const" in schema and declared is None:
            issue(path, "const must have an explicit type")
        if "const" in schema and valid_types:
            if not any(matches_type(schema["const"], item) for item in types):
                issue(path + "/const", "constant value is incompatible with explicit type")
        if "enum" in schema:
            values = schema["enum"]
            if not isinstance(values, list) or not values:
                issue(path + "/enum", "enum must be a nonempty array")
            else:
                if valid_types:
                    for index, value in enumerate(values):
                        if not any(matches_type(value, item) for item in types):
                            issue(path + "/enum/" + str(index),
                                  "enum value is incompatible with explicit type")
                metrics["enum_values"] += len(values)
                chars = sum(len(value) for value in values if isinstance(value, str))
                metrics["identifier_characters"] += chars
                if len(values) > 250 and chars > 15000:
                    issue(path + "/enum", "string enum exceeds 15000 characters")
        if isinstance(schema.get("const"), str):
            metrics["identifier_characters"] += len(schema["const"])
        properties = schema.get("properties", {})
        if not isinstance(properties, dict):
            issue(path + "/properties", "properties must be an object")
            properties = {}
        if "object" in types:
            if schema.get("additionalProperties") is not False:
                issue(path, "object requires additionalProperties=false")
            required = schema.get("required")
            if (not isinstance(required, list)
                    or any(not isinstance(value, str) for value in required)
                    or len(required) != len(set(required))
                    or set(required) != set(properties)):
                issue(path, "required must contain every property exactly once")
        if properties and "object" not in types:
            issue(path, "properties require object type")
        metrics["properties"] += len(properties)
        metrics["identifier_characters"] += sum(map(len, properties))
        for key, value in properties.items():
            walk(value, path + "/properties/" + pointer_component(key))
        definitions = schema.get("$defs", {})
        if not isinstance(definitions, dict):
            issue(path + "/$defs", "definitions must be an object")
        else:
            metrics["identifier_characters"] += sum(map(len, definitions))
            for key, value in definitions.items():
                walk(value, path + "/$defs/" + pointer_component(key))
        if "array" in types and "items" not in schema:
            issue(path, "array requires items")
        if "items" in schema:
            walk(schema["items"], path + "/items")
        if "anyOf" in schema:
            variants = schema["anyOf"]
            if not isinstance(variants, list) or not variants:
                issue(path + "/anyOf", "anyOf must be a nonempty array")
            else:
                for index, variant in enumerate(variants):
                    walk(variant, path + "/anyOf/" + str(index))

    def measure_depth(schema, objects=0, containers=0, refs=()):
        if not isinstance(schema, dict):
            return
        if "$ref" in schema:
            reference = schema["$ref"]
            if reference in refs:
                metrics["recursive_refs"] = True
                return  # Recursion itself is supported; do not unfold forever.
            try:
                measure_depth(resolve(reference), objects, containers, refs + (reference,))
            except (KeyError, TypeError, ValueError):
                pass  # Reported during structural traversal.
            return
        declared = schema.get("type")
        types = declared if isinstance(declared, list) else [declared]
        objects += int("object" in types)
        containers += int("object" in types or "array" in types)
        metrics["object_depth"] = max(metrics["object_depth"], objects)
        metrics["container_depth"] = max(metrics["container_depth"], containers)
        properties = schema.get("properties", {})
        if isinstance(properties, dict):
            for value in properties.values():
                measure_depth(value, objects, containers, refs)
        measure_depth(schema.get("items"), objects, containers, refs)
        variants = schema.get("anyOf", [])
        if isinstance(variants, list):
            for value in variants:
                measure_depth(value, objects, containers, refs)

    if not isinstance(root, dict) or root.get("type") != "object" or "anyOf" in root:
        issue("#", "root must be an object without anyOf")
    walk(root, "#")
    measure_depth(root)
    for name, maximum in (("properties", 5000), ("enum_values", 1000),
                          ("identifier_characters", 120000), ("object_depth", 10)):
        if metrics[name] > maximum:
            issue("#", f"{name} exceeds documented limit {maximum}")
    return {"valid": not issues, "metrics": metrics, "issues": issues}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("paths", nargs="*", type=Path)
    args = parser.parse_args()
    schema_dir = Path(__file__).resolve().parent.parent / "contracts" / "codex"
    paths = args.paths or [schema_dir / name for name in DEFAULT_SCHEMAS]
    results = []
    for path in paths:
        try:
            result = inspect_schema(json.loads(path.read_text(encoding="utf-8-sig")))
        except (OSError, ValueError, RecursionError) as error:
            result = {"valid": False, "issues": [str(error)]}
        results.append({"file": path.name, **result})
    print(json.dumps({"check": "codex_v2_schema_structural_preflight",
                      "provider_acceptance_verified": False,
                      "depth_note": "Object nesting checked; array-inclusive depth reported separately.",
                      "results": results}, ensure_ascii=False, indent=2))
    return 0 if all(result["valid"] for result in results) else 1


if __name__ == "__main__":
    sys.exit(main())
