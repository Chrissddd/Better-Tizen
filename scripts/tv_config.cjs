'use strict';
// Same local configuration contract as tv_config.py; no network access.
const fs = require('node:fs');
const path = require('node:path');
const net = require('node:net');
const ROOT = path.resolve(__dirname, '..');
function localSettings() {
  const filename = path.resolve(ROOT, process.env.TV_CONFIG || '.local/device.json');
  if (!fs.existsSync(filename)) return {};
  const value = JSON.parse(fs.readFileSync(filename, 'utf8'));
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('Device configuration must be a JSON object');
  return value;
}
function tvAddress(explicit) {
  const value = explicit || process.env.TV_IP || localSettings().tv_ip;
  if (net.isIP(value || '') !== 4) throw new Error('Set TV_IP or copy scripts/device.example.json to .local/device.json with an IPv4 address');
  return value;
}
function sdbPath(fallback) {
  return path.resolve(ROOT, process.env.SDB || localSettings().sdb || fallback);
}
module.exports = {localSettings, tvAddress, sdbPath};
