"""Execute actual workflow PowerShell gates with synthetic inputs, never secrets."""
import os
import subprocess
import tempfile
from pathlib import Path

workflow = Path('.github/workflows/build-win64.yml').read_text()
assert 'outputs.skip' not in workflow
assert 'if-no-files-found: error' in workflow
assert '$buildExit = $LASTEXITCODE' in workflow
assert 'if ($buildExit -ne 0)' in workflow
assert 'if ($LASTEXITCODE -ne 0)' in workflow
license_block = workflow.split('        run: |\n', 1)[1].split('\n      - name:', 1)[0]
license_block = '\n'.join(line[10:] for line in license_block.splitlines())
package_block = workflow.split('          $zips = ', 1)[1].split('\n      - name:', 1)[0]
package_block = '$zips = ' + package_block
package_block = '\n'.join(line[10:] if line.startswith('          ') else line for line in package_block.splitlines())
with tempfile.TemporaryDirectory() as directory:
    root = Path(directory)
    summary = root / 'summary'
    for label, ulf, email, password, success in [
        ('missing', '', '', '', False),
        ('partial', '', 'synthetic', '', False),
        ('ulf', 'synthetic', '', '', True),
        ('account', '', 'synthetic', 'synthetic', True),
        ('whitespace', ' ', ' ', ' ', False),
    ]:
        env = dict(os.environ, UNITY_LICENSE=ulf, UNITY_EMAIL=email, UNITY_PASSWORD=password, GITHUB_STEP_SUMMARY=str(summary))
        result = subprocess.run(['pwsh', '-NoProfile', '-Command', license_block], env=env, capture_output=True, text=True, timeout=30)
        assert (result.returncode == 0) == success, (label, result.stdout, result.stderr)
        print(f'PASS license {label}')
    assert 'Win64 build blocked' in summary.read_text()
    for label, setup, success in [
        ('absent', '', False),
        ('empty', "New-Item -ItemType File (Join-Path $out 'build.zip') | Out-Null", False),
        ('missing checksum', "Set-Content (Join-Path $out 'build.zip') 'synthetic'", False),
        ('mismatch', "Set-Content (Join-Path $out 'build.zip') 'synthetic'; Set-Content (Join-Path $out 'build.zip.sha256') 'bad'", False),
        ('matching', "Set-Content (Join-Path $out 'build.zip') 'synthetic'; (Get-FileHash (Join-Path $out 'build.zip')).Hash | Set-Content (Join-Path $out 'build.zip.sha256')", True),
    ]:
        out = root / label
        out.mkdir()
        script = "$ErrorActionPreference = 'Stop'\n$out = $env:PACKAGE_TEST_DIR\n" + setup + '\n' + package_block
        result = subprocess.run(['pwsh', '-NoProfile', '-Command', script], env=dict(os.environ, PACKAGE_TEST_DIR=str(out)), capture_output=True, text=True, timeout=30)
        assert (result.returncode == 0) == success, (label, result.stdout, result.stderr)
        print(f'PASS package {label}')
print('10 PowerShell scenarios passed; actual Unity build not executed.')
