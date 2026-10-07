"""Small fixtures exercise provenance boundaries without NumPy, SciPy or a .NET build."""
import argparse
import copy
import json
import os
import pathlib
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

HERE = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(HERE))
import provenance as pv
import calibration_record as cal
import run_seeds


class ProvenanceTests(unittest.TestCase):
    def setUp(self):
        scratch = pathlib.Path(os.environ.get('GCAM_PROVENANCE_TEST_ROOT',
                                             str(pathlib.Path(tempfile.gettempdir()) / 'gcam-todo37')))
        scratch.mkdir(parents=True, exist_ok=True)
        # Retain fixtures for audit; no cleanup/deletion or repository Git mutation.
        self.root = pathlib.Path(tempfile.mkdtemp(prefix='provenance-test-', dir=scratch))
        source = {'Commit': 'a' * 40, 'Dirty': True, 'Scope': ['src'],
                  'DiffSha256': pv.sha(b'patch'), 'Untracked': {'src/new.cs': pv.sha(b'code')}}
        source['SnapshotSha256'] = pv.digest(source)
        exe = {'Kind': 'cli', 'Files': {'Gcam.Cli.dll': pv.sha(b'assembly')},
               'Tools': {'DotnetRuntime': '9.0.13'}}
        self.prepared = {'Source': {'Source': source, 'Executor': exe, 'ExecutionId': pv.digest(exe)},
                         'Recipe': pv.digest({'request': 1}), 'Files': {}}

    def seed(self, seed=12345, family='fixture', prepared=None, raw=None, root=None):
        root = root or self.root
        prepared = prepared or self.prepared
        run = root / 'runs' / family / str(seed)
        run.mkdir(parents=True, exist_ok=True)
        data = raw or {'Family': 'antimask', 'ComputeSeconds': 1.0, 'Rates': {}, 'Conditions': {}}
        pv.write_json(run / 'ambient-evidence.json', data)
        prov = pv.seed_provenance(prepared, family, seed, {'ambient-evidence.json': pv.file_sha(run / 'ambient-evidence.json')})
        pv.write_json(run / 'done.json', {'exit': 0, 'family': family, 'seed': seed, 'Provenance': prov})
        sources = pv.read_json(root / 'run-info.json')['Sources'] if (root / 'run-info.json').exists() else {}
        sources.update(prov['Sources'])
        pv.write_json(root / 'run-info.json', {'SchemaVersion': 1, 'Sources': sources})
        return run, prov

    def changed(self):
        p = copy.deepcopy(self.prepared)
        p['Source']['Executor']['Files']['Gcam.Cli.dll'] = pv.sha(b'other assembly')
        p['Source']['ExecutionId'] = pv.digest(p['Source']['Executor'])
        return p

    def test_canonical_order(self):
        self.assertEqual(pv.digest({'b': 2, 'a': 1}), pv.digest({'a': 1, 'b': 2}))

    def test_dirty_is_accepted(self):
        run, _ = self.seed()
        self.assertTrue(next(iter(pv.validate_run(run)['Sources'].values()))['Source']['Dirty'])

    def test_missing_root(self):
        run, _ = self.seed()
        pv.write_json(self.root / 'run-info.json', {})
        with self.assertRaisesRegex(SystemExit, 'root provenance'):
            pv.validate_run(run)

    def test_missing_seed_provenance(self):
        run, _ = self.seed()
        pv.write_json(run / 'done.json', {'exit': 0})
        with self.assertRaisesRegex(SystemExit, 'schema'):
            pv.validate_run(run)

    def test_malformed_source_binding(self):
        _, p = self.seed()
        next(iter(p['Sources'].values()))['Source']['Dirty'] = False
        with self.assertRaisesRegex(SystemExit, 'snapshot'):
            pv.validate(p)

    def test_unsupported_schema(self):
        _, p = self.seed()
        p['SchemaVersion'] = 99
        with self.assertRaisesRegex(SystemExit, 'schema'):
            pv.validate(p)

    def test_output_tampering(self):
        run, _ = self.seed()
        (run / 'ambient-evidence.json').write_bytes(b'{}')
        with self.assertRaisesRegex(SystemExit, 'output hash mismatch'):
            pv.validate_run(run)

    def test_seed_binding(self):
        run, _ = self.seed()
        with self.assertRaisesRegex(SystemExit, 'seed/family'):
            pv.validate_run(run, 'wrong', 12345)

    def test_mixed_execution(self):
        _, a = self.seed(1)
        _, b = self.seed(2, prepared=self.changed())
        with self.assertRaisesRegex(SystemExit, 'mixed execution'):
            pv.merge([a, b])

    def test_typed_executors(self):
        _, a = self.seed(1)
        prepared = self.changed()
        prepared['Source']['Executor']['Kind'] = 'probe'
        prepared['Source']['ExecutionId'] = pv.digest(prepared['Source']['Executor'])
        _, b = self.seed(2, family='probe_fixture', prepared=prepared)
        self.assertEqual(len(pv.merge([a, b])['Sources']), 2)

    def test_conflicting_duplicates(self):
        a, _ = self.seed()
        b, _ = self.seed(root=self.root / 'other', prepared=self.changed())
        with self.assertRaisesRegex(SystemExit, 'conflicting duplicate'):
            pv.duplicate(a, b)

    def test_identical_duplicates(self):
        a, _ = self.seed()
        b, _ = self.seed(root=self.root / 'other')
        pv.duplicate(a, b)

    def test_recipe_conflict(self):
        _, a = self.seed(1)
        changed = copy.deepcopy(self.prepared)
        changed['Recipe'] = pv.digest({'request': 2})
        _, b = self.seed(2, prepared=changed)
        with self.assertRaisesRegex(SystemExit, 'mixed recipe'):
            pv.merge([a, b])

    def test_reuse_preserves_original_source(self):
        run, original = self.seed()
        changed = copy.deepcopy(self.prepared)
        src = changed['Source']['Source']
        src['Commit'] = 'b' * 40
        src['SnapshotSha256'] = pv.digest({k: v for k, v in src.items() if k != 'SnapshotSha256'})
        self.assertEqual(pv.check_reuse(run, changed, 'fixture', 12345), original)

    def test_reuse_changed_executor(self):
        run, _ = self.seed()
        with self.assertRaisesRegex(SystemExit, 'reuse identity changed'):
            pv.check_reuse(run, self.changed(), 'fixture', 12345)

    def test_sidecar_binds_every_member(self):
        _, p = self.seed()
        csv, js = self.root / 'out.csv', self.root / 'out.json'
        pv.publish({csv: b'a\n1\n', js: pv.json_bytes({'Provenance': p, 'Value': 2})}, p)
        self.assertEqual(pv.validate_sidecar(csv), p)
        js.write_bytes(b'{}')
        with self.assertRaisesRegex(SystemExit, 'output-set hash mismatch'):
            pv.validate_sidecar(csv)

    def test_preflight_writes_nothing_on_mixed_inputs(self):
        _, p = self.seed()
        p['SchemaVersion'] = 2
        target = self.root / 'never.csv'
        with self.assertRaises(SystemExit):
            pv.publish({target: b'a\n'}, p)
        self.assertFalse(target.exists())

    def test_unbound_numerical_file_is_refused(self):
        run, _ = self.seed()
        (run / 'stale.csv').write_bytes(b'old data\n')
        with self.assertRaisesRegex(SystemExit, 'unbound output'):
            pv.validate_run(run)

    def test_envelope_and_sidecar_must_agree(self):
        _, p = self.seed()
        target = self.root / 'summary.json'
        with self.assertRaisesRegex(SystemExit, 'envelope disagrees'):
            pv.publish({target: pv.json_bytes({'Value': 2})}, p)
        self.assertFalse(target.exists())

    def test_sidecar_binding_across_output_directories(self):
        _, p = self.seed()
        a, b = self.root / 'a/output.csv', self.root / 'b/output.csv'
        pv.publish({a: b'a\n', b: b'b\n'}, p)
        self.assertEqual(pv.validate_sidecar(a), p)
        self.assertEqual(pv.validate_sidecar(b), p)

    def test_staged_python_import_and_rtl_survive_original_edits(self):
        live = self.root / 'live'
        live.mkdir()
        (live / 'helper.py').write_bytes(b'value = 7\n')
        prepared = {'Files': {'rtl/main.py': b'import helper; print(helper.value)\n',
                              'rtl/helper.py': (live / 'helper.py').read_bytes(),
                              'rtl/testbench.sv': b'module testbench; endmodule\n'}}
        stage = pv.stage(self.root / 'run', prepared)
        (live / 'helper.py').write_bytes(b'value = 999\n')
        result = subprocess.run([sys.executable, '-B', str(stage / 'rtl/main.py')], capture_output=True, check=True)
        self.assertEqual(result.stdout.strip(), b'7')
        self.assertEqual((stage / 'rtl/testbench.sv').read_bytes(), prepared['Files']['rtl/testbench.sv'])

    def test_staged_managed_bytes_survive_original_rebuild(self):
        live = self.root / 'engine.dll'
        live.write_bytes(b'old assembly')
        prepared = {'Files': {'managed/cli/engine.dll': live.read_bytes()}}
        stage = pv.stage(self.root / 'run', prepared)
        live.write_bytes(b'new assembly')
        self.assertEqual((stage / 'managed/cli/engine.dll').read_bytes(), b'old assembly')

    def test_source_hash_uses_untracked_contents(self):
        (self.root / 'new.py').write_bytes(b'a')
        def query(args, **kw):
            if 'rev-parse' in args: return b'a' * 40 + b'\n'
            if 'ls-files' in args: return b'new.py\0'
            return b''
        with mock.patch.object(pv, 'command', side_effect=query):
            a = pv.source_record(self.root, ['new.py'])
            (self.root / 'new.py').write_bytes(b'b')
            b = pv.source_record(self.root, ['new.py'])
        self.assertNotEqual(a['SnapshotSha256'], b['SnapshotSha256'])
        self.assertTrue(a['Dirty'])

    def test_failed_git_query_is_not_clean(self):
        with mock.patch.object(subprocess, 'run', return_value=subprocess.CompletedProcess([], 1, b'', b'failed')):
            with self.assertRaisesRegex(SystemExit, 'query failed'):
                pv.source_record(self.root)

    def test_calibration_legacy_and_roles(self):
        legacy = cal.data_section({'CodeLandedIn': 'old'}, {'measured': 'date'}, [], [], 'fixed', {})
        self.assertIn('| Engine commit of the runs | not recorded; the code landed in `old` |', legacy)
        _, p = self.seed()
        rows = pv.data_rows({'Provenance': p}, 'Selection') + pv.data_rows({'Provenance': p}, 'Validation')
        self.assertEqual(rows, pv.data_rows({'Provenance': copy.deepcopy(p)}, 'Selection')
                         + pv.data_rows({'Provenance': copy.deepcopy(p)}, 'Validation'))
        text = '\n'.join(cal.data_section({}, {'measured': 'date'}, [], [], 'fixed', {}, rows))
        self.assertIn('Selection provenance', text)
        self.assertIn('Validation provenance', text)
        self.assertIn('dirty tree — diff', text)
        with self.assertRaises(SystemExit):
            pv.data_rows({'Provenance': {}}, 'Selection')

    def test_real_ambient_aggregator_and_refusal_before_write(self):
        seeds = pv.read_json(HERE / 'seeds.json')['O128'][:2]
        for seed in seeds:
            self.seed(seed)
        manifest = self.root / 'manifest.json'
        pv.write_json(manifest, {'families': [{'id': 'fixture', 'seeds': 'O128', 'n': 2, 'config': 'fixture.json'}]})
        target = self.root / 'summary.json'
        args = [sys.executable, '-B', str(HERE / 'ambient/aggregate_ev.py'), '--runs', str(self.root),
                '--manifest', str(manifest), '--family', 'fixture', '--out', str(target)]
        result = subprocess.run(args, capture_output=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(pv.read_json(target)['Summary'], {'Rates': {}, 'Conditions': {}})
        pv.validate_sidecar(target)
        self.seed(seeds[1], prepared=self.changed())
        args[-1] = str(self.root / 'refused.json')
        result = subprocess.run(args, capture_output=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(b'mixed execution identity', result.stderr)
        self.assertFalse((self.root / 'refused.json').exists())

    def test_seed_offset(self):
        family = {'id': 'f', 'seeds': 's', 'n': 2, 'seed_offset': 1}
        self.assertEqual([s for _, s in run_seeds.jobs_for({'families': [family]}, {'s': [1, 2, 3]}, set(), 0)], [2, 3])

    def test_shared_calibration_file_keeps_role_families_separate(self):
        _, a = self.seed(1, family='selection')
        _, b = self.seed(2, family='validation')
        value = {'Provenance': pv.merge([a, b])}
        selection = str(pv.data_rows(value, 'Selection', ['selection']))
        validation = str(pv.data_rows(value, 'Validation', ['validation']))
        self.assertIn('(selection)', selection)
        self.assertNotIn('(validation)', selection)
        self.assertIn('(validation)', validation)

    @unittest.skipUnless(os.environ.get('GCAM_PROVENANCE_GIT_TESTS') == '1',
                         'isolated Git mutations require explicit local approval; enabled in CI')
    def test_dirty_diff_on_temporary_repository(self):
        root = self.root / 'git-fixture'
        root.mkdir()
        def git(*args):
            return subprocess.run(['git', *args], cwd=root, capture_output=True, check=True).stdout
        git('init', '-q', '--template=')
        (root / '.gitattributes').write_bytes(b'* text=auto eol=lf\n')
        (root / 'source.py').write_bytes(b'value = 1\n')
        git('add', '.gitattributes', 'source.py')
        git('-c', 'commit.gpgsign=false', '-c', 'user.name=Provenance test', '-c', 'user.email=provenance@example.invalid',
            'commit', '-q', '-m', 'fixture')
        (root / 'source.py').write_bytes(b'value = 2\n')
        lf = pv.source_record(root, ['source.py'])
        (root / 'source.py').write_bytes(b'value = 2\r\n')
        crlf = pv.source_record(root, ['source.py'])
        self.assertTrue(lf['Dirty'])
        self.assertEqual(lf['DiffSha256'], crlf['DiffSha256'])


if __name__ == '__main__':
    unittest.main()
