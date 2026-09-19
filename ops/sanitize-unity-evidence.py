"""Publish Unity log/XML evidence after removing local identity fields.

Usage: python ops/sanitize-unity-evidence.py PRIVATE_RAW_DIR PUBLIC_DIR
The private originals are never changed. This script has a deliberately fixed
file list so an unrelated file cannot be published by accident.
"""

from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET


FILES = (
    "import.log",
    "build-windows.log",
    "editmode.log",
    "editmode.xml",
    "playmode.log",
    "playmode.xml",
)
PROFILE = re.compile(rb"(?i)[A-Z]:[\\/]+Users[\\/]+[^\\/\s\"'<>]+")
HOST = re.compile(rb"(?i)\b(?:DESKTOP|LAPTOP)-[A-Za-z0-9-]+\b")
PRIVATE_IPV4 = re.compile(
    rb"(?<![0-9])(?:10(?:\.[0-9]{1,3}){3}|192\.168(?:\.[0-9]{1,3}){2}|"
    rb"172\.(?:1[6-9]|2[0-9]|3[01])(?:\.[0-9]{1,3}){2})(?![0-9])"
)
LICENSE_CHANNEL = re.compile(rb"LicenseClient-[A-Za-z0-9_.-]+")
IDENTITY_LINE = re.compile(
    rb"(?m)^([ \t]*(?:Session Id|Correlation Id|External correlation Id|Machine Id):[ \t]*)[^\r\n]+"
)
LICENSE_GROUP_ID = re.compile(rb"(?m)^(  Id:[ \t]*)[^\r\n]+")
RESULT_LINE = re.compile(
    rb"PALIMPSESTE_BUILD_OK|<test-run\b|<test-suite\b|<test-case\b|Test Run Successful|Tests run:"
)


def result_lines(data: bytes) -> list[bytes]:
    return [line for line in data.splitlines() if RESULT_LINE.search(line)]


def sanitize(data: bytes) -> bytes:
    data = PROFILE.sub(b"USER_PROFILE", data)
    data = HOST.sub(b"HOST_REDACTED", data)
    data = PRIVATE_IPV4.sub(b"PRIVATE_IP", data)
    data = LICENSE_CHANNEL.sub(b"LicenseClient-REDACTED", data)
    data = IDENTITY_LINE.sub(rb"\1REDACTED", data)
    data = LICENSE_GROUP_ID.sub(rb"\1REDACTED", data)
    return data


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("usage: sanitize-unity-evidence.py PRIVATE_RAW_DIR PUBLIC_DIR")
    source, target = (Path(value).resolve(strict=True) for value in sys.argv[1:])
    if source == target:
        raise SystemExit("private source and public target must differ")
    for name in FILES:
        raw = (source / name).read_bytes()
        public = sanitize(raw)
        if raw == public:
            raise SystemExit(f"no identity fields found in {name}; review source")
        if result_lines(raw) != result_lines(public):
            raise SystemExit(f"result lines changed in {name}")
        if PROFILE.search(public) or HOST.search(public) or PRIVATE_IPV4.search(public) or any(
            not match.group(0).endswith(b"REDACTED")
            for match in IDENTITY_LINE.finditer(public)
        ) or any(
            not match.group(0).endswith(b"REDACTED")
            for match in LICENSE_GROUP_ID.finditer(public)
        ):
            raise SystemExit(f"redaction check failed in {name}")
        if name.endswith(".xml"):
            ET.fromstring(public)
        (target / name).write_bytes(public)
        print(f"sanitized={name} result_lines={len(result_lines(public))}")


if __name__ == "__main__":
    main()
