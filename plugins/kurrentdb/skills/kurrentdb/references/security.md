# Security Reference

## Table of Contents

- [Security Modes](#security-modes)
- [Authentication Methods](#authentication-methods)
- [Authorization](#authorization)
- [Access Control Lists (ACLs)](#access-control-lists-acls)
- [Stream Policy Authorization](#stream-policy-authorization)
- [Protocol Security (TLS)](#protocol-security-tls)
- [Encryption-at-Rest](#encryption-at-rest)

---

## Security Modes

### Insecure Mode

Setting `Insecure=true` disables **all** authentication and encryption. Only use for local development.

```yaml
---
Insecure: true
```

### Default Passwords

Default passwords are set via environment variables only:

```bash
KURRENTDB_DEFAULT_ADMIN_PASSWORD=changeit
KURRENTDB_DEFAULT_OPS_PASSWORD=changeit
```

Change these immediately in any non-development environment.

### Anonymous Access

| Setting                        | Default | Description                               |
|--------------------------------|---------|-------------------------------------------|
| `AllowAnonymousStreamAccess`   | `false` | Allow unauthenticated stream reads/writes |
| `AllowAnonymousEndpointAccess` | `false` | Allow unauthenticated endpoint access     |

### Always-Accessible Endpoints

The following are accessible regardless of security settings:

- `/ping`
- `/info`
- UI static content

### FIPS 140-2

FIPS 140-2 compliance is supported. Use FIPS-compatible certificates and algorithms.

---

## Authentication Methods

### 1. Basic Authentication (Default)

Uses internal user management. No license required.

**Built-in groups:**

| Group    | Access Level                             |
|----------|------------------------------------------|
| `$admin` | Full access — bypasses all authorization |
| `$ops`   | Operations access only                   |

### 2. X.509 Certificate Authentication (License Required)

Client certificates where CN (Common Name) maps to the username.

- The client certificate CA must share the root CA with the node certificates.
- Cannot forward authenticated identity between nodes.

### 3. LDAP Authentication (License Required)

```yaml
---
AuthenticationType: ldaps
LdapsAuth:
  Host: ldap.example.com
  Port: 636
  BaseDn: dc=example,dc=com
  Filter: (uid={0})
LdapGroupRoles:
  CN=Developers,OU=Groups,DC=example,DC=com: developer
```

### 4. OAuth Authentication (License Required)

```yaml
---
AuthenticationType: oauth
OAuth:
  Audience: kurrentdb
  Issuer: https://auth.example.com
  ClientId: kurrentdb-server
  ClientSecret: your-secret
```

JWT claims are mapped to roles for authorization.

### 5. Trusted Intermediary

Allows a reverse proxy or API gateway to forward authenticated user identity.

```yaml
---
EnableTrustedAuth: true
```

The intermediary sets the `Kurrent-TrustedAuth` HTTP header with the authenticated username.

---

## Authorization

### Built-in Roles

| Role         | Permissions                                          |
|--------------|------------------------------------------------------|
| `$admins`    | Bypasses **all** authorization checks                |
| `$ops`       | Operations endpoints only (scavenge, shutdown, etc.) |
| Custom roles | Defined per stream or via policies                   |

### Stream Actions

| Action         | Code  | Description               |
|----------------|-------|---------------------------|
| Read           | `$r`  | Read events from a stream |
| Write          | `$w`  | Append events to a stream |
| Delete         | `$d`  | Delete a stream           |
| Metadata Read  | `$mr` | Read stream metadata      |
| Metadata Write | `$mw` | Write stream metadata     |

---

## Access Control Lists (ACLs)

### Per-Stream ACL

Set ACLs in the stream's metadata under the `$acl` object:

```json
{
  "$acl": {
    "$r": ["reader-role"],
    "$w": ["writer-role"],
    "$d": ["$admins"],
    "$mr": ["reader-role"],
    "$mw": ["$admins"]
  }
}
```

### Default ACLs

Default ACLs are defined in the `$settings` system stream and apply to all streams that do not have explicit ACLs.

```json
{
  "$userStreamAcl": {
    "$r": "$all",
    "$w": "$all",
    "$d": "$all",
    "$mr": "$all",
    "$mw": "$all"
  },
  "$systemStreamAcl": {
    "$r": "$admins",
    "$w": "$admins",
    "$d": "$admins",
    "$mr": "$admins",
    "$mw": "$admins"
  }
}
```

---

## Stream Policy Authorization

**License Required — Available since v24.10**

Stream policies provide prefix-based authorization as an alternative to ACLs. When stream policies are enabled, **ACLs are NOT enforced**.

### Enabling Stream Policies

Write a `$authorization-policy-changed` event to the `$authorization-policy-settings` stream:

```json
{
  "eventType": "$authorization-policy-changed",
  "data": {
    "streamAccessPolicyType": "streampolicy"
  }
}
```

### Defining Policies

Write `$policy-updated` events to the `$policies` stream:

```json
{
  "eventType": "$policy-updated",
  "data": {
    "name": "orders-policy",
    "streamPrefix": "order-",
    "readers": ["order-reader"],
    "writers": ["order-writer"],
    "deleters": ["$admins"],
    "metadataReaders": ["$admins"],
    "metadataWriters": ["$admins"]
  }
}
```

### Matching Behavior

- Uses `startsWith` prefix matching against stream names.
- If no matching policy is found, access defaults to **admin-only**.
- If the policy configuration is invalid, access defaults to **admin-only**.

---

## Protocol Security (TLS)

TLS is required for production deployments.

### Certificate Requirements

Node certificates must have a CN (Common Name) or SAN (Subject Alternative Name) that matches the node's address.

### Configuration

```yaml
---
CertificateFile: /path/to/node.crt
CertificatePrivateKeyFile: /path/to/node.key
TrustedRootCertificatesPath: /path/to/ca/
```

### Certificate Generation

Use the `es-gencert-cli` tool to generate certificates for development and testing:

```bash
# Generate CA
es-gencert-cli create-ca

# Generate node certificate
es-gencert-cli create-node -ca-certificate ca.crt -ca-key ca.key \
  -out node1 \
  -dns-names node1.example.com \
  -ip-addresses 192.168.1.10
```

---

## Encryption-at-Rest

**License Required — Available since v24.10**

Encrypts chunk data files using AES-GCM. Index files are **NOT** encrypted.

### Algorithm

- AES-GCM with 128, 192, or 256-bit keys (default: **256-bit**)
- A master key derives per-chunk data keys using HKDF

### Configuration

```yaml
---
Transform: aes-gcm
EncryptionAtRest:
  MasterKeySource: File
  MasterKeyPath: /path/to/master.key
```

### Master Key Management

- **File** is the only built-in master key source (not recommended as the sole mechanism for production).
- Multiple master keys are supported: the latest active key encrypts new data, old keys are retained for decrypting existing data.

### Generating a Master Key

```bash
es-cli encryption generate-master-key --output /path/to/master.key
```

### Important Warnings

- Encryption is **IRREVERSIBLE** once new chunks are created or scavenged with encryption enabled.
- Index files are **not encrypted**.
- Back up master keys securely — losing them means permanent data loss.
