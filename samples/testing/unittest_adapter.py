"""Standard-library unittest execution with structured failure/error/skip events."""
import argparse
import json
import time
import unittest
from pathlib import Path


class Result(unittest.TextTestResult):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.records = []
        self.active = {}

    def startTest(self, test):
        self.active[test.id()] = time.monotonic()
        super().startTest(test)

    def record(self, test, outcome, kind=None, reason=''):
        parent = getattr(test, 'test_case', test)
        name = parent.id()
        existing = next((r for r in self.records if r['name'] == name), None)
        part = {'name': test.id(), 'outcome': outcome, 'failureKind': kind, 'reason': reason}
        if existing is None:
            existing = {'name': name, 'outcome': outcome, 'failureKind': kind, 'reason': reason,
                        'durationSeconds': time.monotonic() - self.active[name], 'parts': []}
            self.records.append(existing)
        if {'passed': 0, 'not_executed': 1, 'failed': 2}[outcome] >= {'passed': 0, 'not_executed': 1, 'failed': 2}[existing['outcome']]:
            existing['outcome'] = outcome
            existing['failureKind'] = 'error' if existing['failureKind'] == 'error' or kind == 'error' else kind
            existing['reason'] = reason
        existing['durationSeconds'] = time.monotonic() - self.active[name]
        existing['parts'].append(part)

    def addSuccess(self, test):
        self.record(test, 'passed')
        super().addSuccess(test)

    def addFailure(self, test, err):
        self.record(test, 'failed', 'assertion', self._exc_info_to_string(err, test))
        super().addFailure(test, err)

    def addError(self, test, err):
        self.record(test, 'failed', 'error', self._exc_info_to_string(err, test))
        super().addError(test, err)

    def addSkip(self, test, reason):
        self.record(test, 'not_executed', reason=reason)
        super().addSkip(test, reason)

    def addExpectedFailure(self, test, err):
        self.record(test, 'not_executed', reason='expected failure: ' + self._exc_info_to_string(err, test))
        super().addExpectedFailure(test, err)

    def addUnexpectedSuccess(self, test):
        self.record(test, 'failed', 'assertion', 'unexpected success')
        super().addUnexpectedSuccess(test)

    def addSubTest(self, test, subtest, err):
        # Retain subtest failures, but do not claim a parent passed unless unittest reports success.
        if err is not None:
            self.record(test, 'failed', 'assertion' if issubclass(err[0], test.failureException) else 'error',
                        self._exc_info_to_string(err, test))
            self.records[-1]['parts'][-1]['name'] = subtest.id()
        super().addSubTest(test, subtest, err)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--start', required=True)
    parser.add_argument('--pattern', default='test_*.py')
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    suite = unittest.defaultTestLoader.discover(args.start, pattern=args.pattern)
    result = unittest.TextTestRunner(verbosity=2, resultclass=Result).run(suite)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_bytes((json.dumps({'schemaVersion': 1, 'cases': result.records}, sort_keys=True,
                                   ensure_ascii=False, allow_nan=False) + '\n').encode('utf-8'))
    return 0 if result.wasSuccessful() else 1


if __name__ == '__main__':
    raise SystemExit(main())
