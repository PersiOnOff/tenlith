// Exercises the real HTTP/PostgreSQL/SMTP stack. Run only against a disposable
// local Development instance: node scripts/verify-stack.mjs
import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { fileURLToPath } from 'node:url';

const origin = process.env.TENLITH_TEST_URL || 'http://localhost:8080';
const inbox = process.env.TENLITH_MAILPIT_URL || 'http://localhost:8025';
assert.ok(['localhost', '127.0.0.1'].includes(new URL(origin).hostname),
  'This script creates real test records and is restricted to localhost.');

async function request(path, { token, cookie, body, method = 'GET', headers = {} } = {}) {
  const response = await fetch(`${origin}/api${path}`, {
    method,
    headers: {
      'X-Tenlith-Client': 'web',
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(cookie ? { Cookie: cookie } : {}),
      ...headers,
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  const raw = await response.text();
  let data;
  try { data = raw ? JSON.parse(raw) : undefined; } catch { data = undefined; }
  return {
    status: response.status,
    data,
    cookie: response.headers.getSetCookie().find(x => x.startsWith('tenlith.refresh='))?.split(';')[0]
      || response.headers.getSetCookie()[0]?.split(';')[0],
  };
}

function expect(result, status, label) {
  assert.equal(result.status, status, `${label}: expected ${status}, got ${result.status}${result.data?.detail ? ` (${result.data.detail})` : ''}`);
  return result;
}

async function emailLink(address, route) {
  for (let attempt = 0; attempt < 60; attempt++) {
    const list = await (await fetch(`${inbox}/api/v1/messages`)).json();
    for (const message of list.messages || []) {
      if (!message.To?.some(recipient => recipient.Address.toLowerCase() === address.toLowerCase())) continue;
      const full = await (await fetch(`${inbox}/api/v1/message/${message.ID}`)).json();
      const text = `${full.Text || ''} ${full.HTML || ''}`.replaceAll('&amp;', '&');
      const links = text.match(/https?:\/\/[^\s<>"']+/g) || [];
      const found = links.find(link => new URL(link).pathname === route);
      if (found) return new URL(found);
    }
    await new Promise(resolve => setTimeout(resolve, 500));
  }
  throw new Error(`Email for ${route} was not delivered to the local inbox within 30 seconds.`);
}

async function account(name, email) {
  const password = 'Local-acceptance-Password-123!';
  expect(await request('/auth/register', { method: 'POST', body: { name, email, password } }), 200, 'Register');
  expect(await request('/auth/login', { method: 'POST', body: { email, password } }), 401, 'Unverified login rejected');
  const link = await emailLink(email, '/verify-email');
  expect(await request('/auth/verify-email', { method: 'POST', body: { userId: link.searchParams.get('userId'), token: link.searchParams.get('token') } }), 200, 'Verify email');
  const session = expect(await request('/auth/login', { method: 'POST', body: { email, password } }), 200, 'Verified login');
  return { token: session.data.accessToken, cookie: session.cookie, email, password };
}

const configuration = expect(await request('/auth/config'), 200, 'Auth configuration').data;
assert.equal(configuration.demoEnabled, true, 'Local demo must be enabled for this acceptance script.');

const a = expect(await request('/auth/demo', { method: 'POST', body: {} }), 200, 'Demo A');
const b = expect(await request('/auth/demo', { method: 'POST', body: {} }), 200, 'Demo B');
const aToken = a.data.accessToken;
const bToken = b.data.accessToken;
const aOrganizations = expect(await request('/organizations', { token: aToken }), 200, 'Organizations A').data;
const bOrganizations = expect(await request('/organizations', { token: bToken }), 200, 'Organizations B').data;
const organization = aOrganizations[0].id;
assert.notEqual(organization, bOrganizations[0].id);
expect(await request(`/organizations/${organization}/projects`, { token: bToken }), 403, 'Foreign tenant read denied');
expect(await request(`/organizations/${organization}/projects`, { token: bToken, method: 'POST', body: { name: 'Foreign write' } }), 403, 'Foreign tenant write denied');
console.log('PASS isolated demo sessions and cross-tenant denial');

const before = expect(await request(`/organizations/${organization}/projects`, { token: aToken }), 200, 'Projects').data.length;
const creates = await Promise.all(Array.from({ length: 5 }, (_, index) => request(`/organizations/${organization}/projects`, {
  token: aToken, method: 'POST', body: { name: `Concurrent ${index}`, description: '', color: '#c8f56b' },
})));
assert.ok(creates.every(result => [200, 201, 409].includes(result.status)),
  `Concurrent writes must succeed or return a controlled quota/conflict response; got ${creates.map(x => x.status).join(', ')}.`);
const after = expect(await request(`/organizations/${organization}/projects`, { token: aToken }), 200, 'Projects after competing writes').data.length;
assert.ok(after <= 3 && after >= before, 'Free project limit must hold under concurrency.');
expect(await request(`/organizations/${organization}/billing/simulate`, { token: aToken, method: 'POST', body: { plan: 'Pro' } }), 200, 'Simulate Pro');
const upgraded = await request(`/organizations/${organization}/projects`, { token: aToken, method: 'POST', body: { name: 'Pro project', description: '', color: '#c8f56b' } });
assert.ok([200, 201].includes(upgraded.status), 'Pro entitlement allows another project.');
console.log('PASS concurrent quota enforcement and local subscription upgrade');

const rotated = expect(await request('/auth/refresh', { method: 'POST', cookie: a.cookie, body: {} }), 200, 'Rotate refresh');
assert.ok(rotated.cookie && rotated.cookie !== a.cookie, 'Rotation must issue a successor cookie.');
expect(await request('/auth/refresh', { method: 'POST', cookie: a.cookie, body: {} }), 401, 'Replay rejected');
expect(await request('/auth/refresh', { method: 'POST', cookie: rotated.cookie, body: {} }), 401, 'Replay revokes successor');
console.log('PASS refresh rotation and family replay protection');

const stamp = `${Date.now()}-${Math.floor(Math.random() * 10000)}`;
const owner = await account('Acceptance owner', `owner-${stamp}@example.test`);
const member = await account('Acceptance member', `member-${stamp}@example.test`);
const created = await request('/organizations', { token: owner.token, method: 'POST', body: { name: `Acceptance ${stamp}` } });
assert.ok([200, 201].includes(created.status), 'Organization create succeeds.');
const id = created.data.id;
expect(await request(`/organizations/${id}/invitations`, { token: owner.token, method: 'POST', body: { email: member.email, role: 'Member' } }), 200, 'Invite member');
const invitation = await emailLink(member.email, '/invite');
const invitationToken = invitation.searchParams.get('token');
expect(await request('/invitations/accept', { token: owner.token, method: 'POST', body: { token: invitationToken } }), 403, 'Wrong recipient denied');
expect(await request('/invitations/accept', { token: member.token, method: 'POST', body: { token: invitationToken } }), 200, 'Accept invitation');
const replay = await request('/invitations/accept', { token: member.token, method: 'POST', body: { token: invitationToken } });
assert.ok([400, 404, 409, 410].includes(replay.status), 'Invitation cannot be replayed.');
expect(await request(`/organizations/${id}/projects`, { token: member.token, method: 'POST', body: { name: 'Forbidden member write' } }), 403, 'Member cannot create project');
const members = expect(await request(`/organizations/${id}/members`, { token: owner.token }), 200, 'Team members').data;
const membership = members.find(item => item.email === member.email);
assert.ok(membership);
expect(await request(`/organizations/${id}/members/${membership.id}`, { token: owner.token, method: 'PATCH', body: { role: 'Admin' } }), 204, 'Promote member');
const adminWrite = await request(`/organizations/${id}/projects`, { token: member.token, method: 'POST', body: { name: 'Admin project', description: '', color: '#c8f56b' } });
assert.ok([200, 201].includes(adminWrite.status), 'Admin can create projects.');
console.log('PASS SMTP verification, invitation recipient/replay checks and role enforcement');

expect(await request('/auth/forgot-password', { method: 'POST', body: { email: member.email } }), 200, 'Request password reset');
const reset = await emailLink(member.email, '/reset-password');
const newPassword = 'Replaced-local-Password-456!';
if (process.argv.includes('--restart-api')) {
  await promisify(execFile)('docker', ['compose', 'restart', 'api'], {
    cwd: fileURLToPath(new URL('..', import.meta.url)), timeout: 60000,
  });
  let ready = false;
  for (let attempt = 0; attempt < 60; attempt++) {
    try { ready = (await request('/auth/config')).status === 200; } catch { /* starting */ }
    if (ready) break;
    await new Promise(resolve => setTimeout(resolve, 500));
  }
  assert.ok(ready, 'API becomes available after restart.');
  expect(await request('/auth/refresh', { cookie: owner.cookie, method: 'POST', body: {} }), 200, 'Refresh session survives restart');
  expect(await request(`/organizations/${id}/projects`, { token: owner.token }), 200, 'Organization data survives restart');
  console.log('PASS API restart preserves sessions, data and previously issued reset tokens');
}
expect(await request('/auth/reset-password', { method: 'POST', body: { email: member.email, token: reset.searchParams.get('token'), password: newPassword } }), 200, 'Reset password');
expect(await request('/auth/login', { method: 'POST', body: { email: member.email, password: member.password } }), 401, 'Old password rejected');
const newLogin = expect(await request('/auth/login', { method: 'POST', body: { email: member.email, password: newPassword } }), 200, 'New password works');
expect(await request('/auth/logout', { token: newLogin.data.accessToken, cookie: newLogin.cookie, method: 'POST', body: {} }), 204, 'Logout');
expect(await request('/auth/refresh', { cookie: newLogin.cookie, method: 'POST', body: {} }), 401, 'Logout revokes refresh');
console.log('PASS password recovery and logout revocation');
console.log('Stack acceptance checks passed. Real Stripe/GitHub provider credentials were not exercised.');
