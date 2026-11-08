#!/usr/bin/env node
// Finish the existing Samsung author flow. Default invocation is a local preflight.
// --execute issues the distributor certificate, registers DuskTV and builds the WGT.
// --permit-install additionally performs the narrowly scoped SDK device-profile push.
'use strict';

const fs = require('fs');
const path = require('path');
const os = require('os');
const crypto = require('crypto');
const cp = require('child_process');
const deviceConfig = require('./tv_config.cjs');

const ROOT = path.resolve(__dirname, '..');
const SDK = path.join(ROOT, 'toolchain/sdk');
const DATA = path.join(ROOT, 'toolchain/sdk-data');
const COMMON = path.join(ROOT, 'toolchain/samsung-agent/tizen-sdk-skills/common');
const PRIVATE = path.join(ROOT, 'toolchain/private');
const PROFILE = 'DuskTV';
const CERTS = path.join(DATA, 'keystore/samsung', PROFILE);
const PASSWORD_FILE = path.join(PRIVATE, 'samsung-certificate-password.env');
const DUID_FILE = path.join(PRIVATE, 'tv-duid.txt');
const REPORT = path.join(ROOT, 'reports/samsung-sign-finish.json');
const args = process.argv.slice(2);
const allowed = new Set(['--execute', '--skip-package', '--permit-install', '--serial', '--help']);
let serial = null;
for (let i = 0; i < args.length; i++) {
  if (!allowed.has(args[i])) throw new Error('Unknown argument: ' + args[i]);
  if (args[i] === '--serial') {
    serial = args[++i];
    if (!serial || !/^[A-Za-z0-9_.:-]+$/.test(serial)) throw new Error('Invalid --serial');
  }
}
if (args.includes('--help')) {
  console.log('node scripts/finish-samsung-sign.cjs [--execute] [--skip-package] [--permit-install --serial TV_IP:26101]');
  process.exit(0);
}
const execute = args.includes('--execute');
const permit = args.includes('--permit-install');
const packageApp = !args.includes('--skip-package');
if (permit && (!execute || !serial)) throw new Error('--permit-install requires --execute and --serial');
process.env.PATH = [path.join(SDK, 'tools'), path.join(SDK, 'jdk/Contents/Home/bin'), process.env.PATH || ''].join(path.delimiter);
const { resolveSdkDataPath } = require(path.join(COMMON, 'lib/core/sdb'));
const { readPasswordFile } = require(path.join(COMMON, 'lib/cli/password-file'));
const samsung = require(path.join(COMMON, 'lib/core/samsung-cert'));
const originalLog = console.log.bind(console);
let password = '';
let duid = '';
const hash = data => crypto.createHash('sha256').update(data).digest('hex');
const scrub = value => {
  let text = String(value);
  for (const secret of [password, duid]) if (secret) text = text.split(secret).join('<redacted>');
  return text.replace(/(access[_-]?token|refresh[_-]?token|id[_-]?token|authorization|Bearer)([\s"':=]+)[^\s,}"']+/gi, '$1$2<redacted>');
};
function run(file, argv, extra = {}) {
  const result = cp.spawnSync(file, argv, { cwd: ROOT, encoding: 'utf8', timeout: 120000, ...extra });
  if (result.error || result.status !== 0) {
    throw new Error(scrub((result.stderr || result.stdout || result.error?.message || 'Command failed').trim()));
  }
  return result.stdout;
}
async function sdkAction(label, action) {
  originalLog(label);
  // The SDK can print an OAuth URL. Keep auth details out of terminal logs.
  const log = console.log, error = console.error;
  console.log = () => {};
  console.error = () => {};
  let result;
  try { result = await action(); } finally { console.log = log; console.error = error; }
  if (result.status !== 'success') {
    const errors = (result.errors || []).map(e => ({ code: e.code, message: scrub(e.message || '') }));
    throw new Error(JSON.stringify({ status: result.status, errors }));
  }
  return result;
}
function inspectP12(filename, expectedDuid) {
  const pem = run('openssl', ['pkcs12', '-in', filename, '-clcerts', '-nokeys', '-passin', 'env:DUSK_CERT_PASS'], {
    env: { ...process.env, DUSK_CERT_PASS: password },
  });
  run('openssl', ['x509', '-checkend', '0', '-noout'], { input: pem });
  const text = run('openssl', ['x509', '-noout', '-text'], { input: pem });
  if (expectedDuid) {
    const ids = [...text.matchAll(/URN:tizen:deviceid=([^\s,]+)/gi)].map(m => m[1]);
    if (ids.length !== 1 || ids[0] !== expectedDuid) throw new Error('Distributor certificate does not contain exactly the requested secure TV DUID.');
  }
  const publicDetails = run('openssl', ['x509', '-noout', '-subject', '-issuer', '-dates', '-fingerprint', '-sha256'], { input: pem });
  return { sha256: hash(fs.readFileSync(filename)), current: true, exact_tv_duid_verified: Boolean(expectedDuid), public_details: scrub(publicDetails.trim()) };
}
function deviceProfileInfo(filename) {
  if (!fs.existsSync(filename)) return { exists: false, xml: false, exact_tv_duid_verified: false };
  const result = run('python3', ['-c',
    'import json,sys,xml.etree.ElementTree as E\ntry:\n r=E.parse(sys.argv[1]).getroot()\n vals=[(n.text or "").strip() for n in r.iter()]+[v for n in r.iter() for v in n.attrib.values()]\n print(json.dumps({"exists":True,"xml":True,"exact_tv_duid_verified":sys.stdin.read().strip() in vals}))\nexcept E.ParseError:\n print(json.dumps({"exists":True,"xml":False,"exact_tv_duid_verified":False}))', filename], { input: duid });
  return JSON.parse(result);
}
function saveReport(report) {
  fs.mkdirSync(path.dirname(REPORT), { recursive: true });
  fs.writeFileSync(REPORT, JSON.stringify(report, null, 2) + '\n', { mode: 0o600 });
}
function profileMatches(author, distributor) {
  const profiles = path.join(DATA, 'profile/profiles.xml');
  if (!fs.existsSync(profiles)) return false;
  return run('python3', ['-c',
    'import sys,xml.etree.ElementTree as E\nr=E.parse(sys.argv[1]).getroot()\np=r.find("./profile[@name=\\\"DuskTV\\\"]")\nitems={} if p is None else {n.get("distributor"):n.get("key") for n in p.findall("profileitem")}\nprint(str(items.get("0")==sys.argv[2] and items.get("1")==sys.argv[3]).lower())', profiles, author, distributor]).trim() === 'true';
}
async function main() {
  const resolved = resolveSdkDataPath();
  if (resolved.error || path.resolve(resolved.sdkRoot || '') !== SDK || path.resolve(resolved.dataPath || '') !== DATA) {
    throw new Error('SDK resolver does not select this project SDK. No global configuration was changed.');
  }
  password = readPasswordFile(PASSWORD_FILE, 'TIZEN_CERTIFICATE_PASSWORD');
  duid = fs.readFileSync(DUID_FILE, 'utf8').trim();
  if (!/^[A-Za-z0-9]{13}$/.test(duid)) throw new Error('Expected exactly the 13-character secure getduid from this TV.');
  const author = path.join(CERTS, 'author.p12');
  const distributor = path.join(CERTS, 'distributor.p12');
  const profileXml = path.join(CERTS, 'device-profile.xml');
  const report = { profile: PROFILE, preflight_only: !execute, author_exists: fs.existsSync(author), distributor_exists: fs.existsSync(distributor), tv_duid_sha256: hash(duid), tv_contacted: false, permit_install: 'not requested', tv_install_tested: false };
  if (!execute) {
    if (report.author_exists) report.author = inspectP12(author);
    if (report.distributor_exists) report.distributor = inspectP12(distributor, duid);
    report.profile_reused = profileMatches(author, distributor);
    report.device_profile = deviceProfileInfo(profileXml);
    originalLog(JSON.stringify(report, null, 2));
    return;
  }
  if (!report.author_exists) throw new Error('Samsung author certificate is not ready. Complete the existing author/OAuth flow first.');
  report.author = inspectP12(author);
  if (!fs.existsSync(distributor)) {
    await sdkAction('Issuing Samsung Public distributor for the saved TV DUID (browser login may be needed).', () => samsung.generateSamsungDistributorCertificate({ profileName: PROFILE, password, duidList: [duid], privilege: 'Public' }));
  }
  report.distributor = inspectP12(distributor, duid);
  report.profile_reused = profileMatches(author, distributor);
  if (!report.profile_reused) {
    await sdkAction('Registering the local Samsung signing profile.', () => samsung.createSamsungProfile({ profileName: PROFILE, privilege: 'Public', authorPassword: password, distributorPassword: password, active: true }));
  }
  const profiles = path.join(DATA, 'profile/profiles.xml');
  fs.chmodSync(profiles, 0o600);
  for (const filename of fs.readdirSync(CERTS)) {
    const full = path.join(CERTS, filename);
    if (fs.statSync(full).isFile()) fs.chmodSync(full, 0o600);
  }
  report.device_profile = deviceProfileInfo(profileXml);
  if (permit) {
    if (!report.device_profile.xml || !report.device_profile.exact_tv_duid_verified) throw new Error('Permit requested, but no valid device-profile XML contains the exact TV DUID.');
    const sdb = deviceConfig.sdbPath(path.join(SDK, 'tools/sdb'));
    const capability = run(sdb, ['-s', serial, 'capability']);
    const fields = Object.fromEntries(capability.split(/\r?\n/).filter(x => x.includes(':')).map(x => { const i=x.indexOf(':');return [x.slice(0,i).trim(),x.slice(i+1).trim()]; }));
    if (fields.profile_name !== 'tv' || fields.secure_protocol !== 'enabled' || !/^\/[A-Za-z0-9_./-]+$/.test(fields.sdk_toolpath || '') || fields.sdk_toolpath.includes('..')) throw new Error('Unexpected live TV capability or SDK tool path.');
    const liveDuid = run(sdb, ['-s', serial, 'shell', '0', 'getduid']).trim();
    if (liveDuid !== duid) throw new Error('Connected TV DUID differs from the certificate target.');
    report.tv_contacted = true;
    const destination = fields.sdk_toolpath.replace(/\/$/, '') + '/device-profile.xml';
    run(sdb, ['-s', serial, 'push', profileXml, destination]);
    report.permit_install = 'issued XML push succeeded; readback verification pending';
    saveReport(report);
    const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'dusk-permit-'));
    try {
      const pulled = path.join(temp, 'device-profile.xml');
      try {
        const output = run(sdb, ['-s', serial, 'pull', destination, pulled]);
        if (!fs.existsSync(pulled)) throw new Error('SDB pull produced no file: ' + output.trim());
        if (hash(fs.readFileSync(pulled)) !== hash(fs.readFileSync(profileXml))) throw new Error('Device-profile pullback differs from the issued XML.');
        report.device_profile_readback = 'sdb pull';
      } catch (error) {
        if (!/cannot pull|permission denied|access denied|not permitted|pull produced no file/i.test(error.message)) throw error;
        // The callback must target the same configured TV that passed the live DUID check.
        const callbackTv = deviceConfig.tvAddress();
        if (serial !== callbackTv + ':26101') throw new Error('SDB pull is restricted; callback readback TV_IP differs from the verified serial. Push succeeded; verification remains pending.');
        const archive = path.join(temp, 'device-profile-readback.tar.gz');
        run('python3', [path.join(ROOT, 'scripts/tv-fetch.py'), '--tv', callbackTv, '--sdb', sdb, archive, destination]);
        const readbackHash = run('python3', ['-c',
          'import hashlib,sys,tarfile\nwith tarfile.open(sys.argv[1],"r:gz") as t:\n members=[m for m in t.getmembers() if m.name.lstrip("/")==sys.argv[2].lstrip("/") and m.isfile()]\n if len(members)!=1: raise SystemExit("Expected one exact device-profile member")\n print(hashlib.sha256(t.extractfile(members[0]).read()).hexdigest())', archive, destination]).trim();
        if (readbackHash !== hash(fs.readFileSync(profileXml))) throw new Error('Authenticated device-profile readback differs from the issued XML.');
        report.device_profile_readback = 'authenticated tv-fetch callback and SHA256';
      }
    } finally { fs.rmSync(temp, { recursive: true, force: true }); }
    report.permit_install = 'issued XML pushed to live SDK tool path and verified by readback SHA256';
    saveReport(report);
  }
  const wgt = path.join(ROOT, 'toolchain/build/DuskLaunch.wgt');
  if (packageApp && fs.existsSync(wgt)) {
    const archive = path.join(ROOT, 'toolchain/build/archive');
    fs.mkdirSync(archive, { recursive: true });
    const oldHash = hash(fs.readFileSync(wgt));
    fs.copyFileSync(wgt, path.join(archive, 'DuskLaunch.' + oldHash + '.wgt'));
    const oldReport = wgt.replace(/\.wgt$/, '.build.json');
    if (fs.existsSync(oldReport)) fs.copyFileSync(oldReport, path.join(archive, 'DuskLaunch.' + oldHash + '.build.json'));
  }
  if (packageApp) {
    originalLog('Packaging the current launcher with DuskTV.');
    run('python3', [path.join(ROOT, 'scripts/package-launcher.py'), '--profile', PROFILE]);
    report.package = JSON.parse(fs.readFileSync(wgt.replace(/\.wgt$/, '.build.json'), 'utf8'));
    delete report.package.source_sha256;
  } else report.package = 'skipped';
  saveReport(report);
  originalLog(JSON.stringify(report, null, 2));
}
main().catch(error => { originalLog(scrub(error.message)); process.exitCode = 1; });
