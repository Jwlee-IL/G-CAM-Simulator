"""Test-owned scratch: remove successful fixtures, retain failures for diagnosis.

Each fixture is a fresh folder made by tempfile.mkdtemp in the system temporary folder. When its test passes, the folder
is removed (it holds only what the test wrote); when the test fails or errors, it is kept and its path printed. A folder
that cannot be removed is reported, never retried or widened. Imported by file location (see the tests' imports), so it
works both from the repository root (python -m unittest <file>) and under the unittest adapter's discovery.
"""
from contextvars import ContextVar
from pathlib import Path
import shutil
import tempfile
import unittest

_roots = ContextVar('gcam_fixture_roots', default=None)


def scratch(prefix='gcam-fixture-'):
    roots = _roots.get()
    if roots is None:
        raise RuntimeError('scratch requires ScratchTestCase (inside a running test)')
    root = Path(tempfile.mkdtemp(prefix=prefix))
    roots.append(root)
    return root


class ScratchTestCase(unittest.TestCase):
    def run(self, result=None):
        result = result or self.defaultTestResult()
        roots = []
        token = _roots.set(roots)
        before = len(result.failures) + len(result.errors) + len(getattr(result, 'unexpectedSuccesses', []))
        completed = False
        try:
            answer = super().run(result)
            completed = True
            return answer
        finally:
            _roots.reset(token)
            passed = completed and len(result.failures) + len(result.errors) + len(getattr(result, 'unexpectedSuccesses', [])) == before
            for root in roots:
                if not passed:
                    print('Retained failed fixture:', root)
                    continue
                try:
                    shutil.rmtree(root)
                except OSError as error:
                    print('Fixture not removed:', root, '(' + type(error).__name__ + ')')
