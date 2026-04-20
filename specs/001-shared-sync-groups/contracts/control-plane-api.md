# Control Plane API Contract

**Feature**: 001-shared-sync-groups
**Date**: 2026-04-18
**Runtime**: Azure Functions (HTTP-triggered, .NET 10 isolated worker)
**Auth**: All endpoints require a valid Entra External ID bearer token unless noted.

## Base URL

`https://{functions-app-name}.azurewebsites.net/api`

---

## Endpoints

### 1. Register Device

**POST** `/devices/register`

Register a new device to the authenticated user's account. Called on first sign-in from a new device.

**Request**:
```json
{
  "deviceId": "string (app-generated UUID)",
  "deviceName": "string (human-readable, max 80 chars)",
  "publicKey": "string (base64-encoded RSA public key, min 2048 bits)"
}
```

**Response 201**:
```json
{
  "deviceId": "string",
  "userId": "string",
  "registeredAt": "ISO 8601 datetime"
}
```

**Errors**: 400 (invalid input), 401 (unauthenticated), 409 (device ID already registered to this user)

---

### 2. List Devices

**GET** `/devices`

List all registered devices for the authenticated user.

**Response 200**:
```json
{
  "devices": [
    {
      "deviceId": "string",
      "deviceName": "string",
      "registeredAt": "ISO 8601 datetime",
      "lastSeenAt": "ISO 8601 datetime",
      "isRevoked": false
    }
  ]
}
```

---

### 3. Revoke Device

**POST** `/devices/{deviceId}/revoke`

Revoke a device belonging to the authenticated user.

**Response 200**:
```json
{
  "deviceId": "string",
  "revokedAt": "ISO 8601 datetime"
}
```

**Errors**: 401, 403 (device does not belong to user), 404

---

### 4. Create Shared Group

**POST** `/groups`

Register a new shared group. The authenticated user becomes the owner.

**Request**:
```json
{
  "groupId": "string (existing local group ID)",
  "groupDisplayName": "string",
  "initialWrappedKey": {
    "keyVersion": 1,
    "wrappedKeys": [
      {
        "deviceId": "string",
        "wrappedKeyBlob": "string (base64)"
      }
    ]
  }
}
```

**Response 201**:
```json
{
  "groupId": "string",
  "containerName": "string (group-{groupId})",
  "ownerId": "string",
  "sharedAt": "ISO 8601 datetime"
}
```

**Errors**: 400, 401, 409 (group already shared)

**Side effects**: Creates the Blob Storage container for the group.

---

### 5. Get Group Info

**GET** `/groups/{groupId}`

Get shared group metadata including membership. Requires active membership.

**Response 200**:
```json
{
  "groupId": "string",
  "groupDisplayName": "string",
  "containerName": "string",
  "ownerId": "string",
  "isReadOnly": false,
  "currentKeyVersion": 1,
  "members": [
    {
      "userId": "string",
      "displayName": "string",
      "role": "Owner | Member",
      "joinedAt": "ISO 8601 datetime"
    }
  ],
  "pendingInvitations": [
    {
      "invitationId": "string",
      "createdAt": "ISO 8601 datetime",
      "expiresAt": "ISO 8601 datetime",
      "status": "Pending"
    }
  ]
}
```

**Note**: `pendingInvitations` is only included when the requester is the owner.

**Errors**: 401, 403, 404

---

### 6. Request Sync Token

**POST** `/groups/{groupId}/sync-token`

Request a short-lived User Delegation SAS token scoped to the group's Blob Storage container. The control plane verifies the user is an active member before issuing the token.

**Request**:
```json
{
  "deviceId": "string",
  "permissions": "read | readwrite"
}
```

**Response 200**:
```json
{
  "sasUri": "string (full URI with SAS query string)",
  "containerName": "string",
  "expiresAt": "ISO 8601 datetime",
  "permissions": "read | readwrite"
}
```

**Token lifetime**: 15 minutes (configurable).

**Errors**: 401, 403 (not a member, or member revoked), 404

---

### 7. Create Invitation

**POST** `/groups/{groupId}/invitations`

Generate an invitation link for the group. Only the owner can create invitations.

**Request**:
```json
{
  "expiresInHours": 72
}
```

**Response 201**:
```json
{
  "invitationId": "string",
  "token": "string (URL-safe base64, 32+ bytes)",
  "invitationUrl": "string (deep link with token)",
  "expiresAt": "ISO 8601 datetime"
}
```

**Errors**: 400, 401, 403 (not owner)

---

### 8. Accept Invitation

**POST** `/invitations/{token}/accept`

Accept an invitation by its token. The authenticated user joins the group and receives the wrapped group key.

**Request**:
```json
{
  "deviceId": "string"
}
```

**Response 200**:
```json
{
  "groupId": "string",
  "groupDisplayName": "string",
  "containerName": "string",
  "role": "Member",
  "wrappedKey": {
    "keyVersion": 1,
    "wrappedKeyBlob": "string (base64, wrapped to requesting device's public key)"
  }
}
```

**Side effects**: 
- Invitation status → Accepted.
- User added to group membership.
- Group key wrapped to the accepting device's public key and returned.
- The owner is notified to wrap the key for any additional devices the new member owns (via next sync metadata exchange).

**Errors**: 400 (already a member), 401, 404 (invalid/expired/consumed token)

---

### 9. Decline Invitation

**POST** `/invitations/{token}/decline`

Decline an invitation.

**Response 200**:
```json
{
  "status": "Declined"
}
```

**Errors**: 401, 404

---

### 10. Cancel Invitation

**DELETE** `/groups/{groupId}/invitations/{invitationId}`

Cancel a pending invitation. Only the owner.

**Response 200**:
```json
{
  "status": "Cancelled"
}
```

**Errors**: 401, 403, 404

---

### 11. Revoke Member

**POST** `/groups/{groupId}/members/{userId}/revoke`

Revoke a member from the group. Only the owner. Triggers key rotation by the owner's device.

**Response 200**:
```json
{
  "userId": "string",
  "revokedAt": "ISO 8601 datetime",
  "keyRotationRequired": true
}
```

**Side effects**: 
- Membership status → Revoked.
- The response signals the owner's device to generate a new group key and upload new wrapped keys.

**Errors**: 401, 403, 404, 422 (cannot revoke owner)

---

### 12. Upload Rotated Key

**POST** `/groups/{groupId}/keys`

Upload a new group key version with wrapped keys for all remaining authorized devices. Called by the owner's device after key rotation.

**Request**:
```json
{
  "keyVersion": 2,
  "wrappedKeys": [
    {
      "deviceId": "string",
      "wrappedKeyBlob": "string (base64)"
    }
  ]
}
```

**Response 201**:
```json
{
  "groupId": "string",
  "keyVersion": 2,
  "distributedToDevices": 3
}
```

**Errors**: 400, 401, 403 (not owner), 409 (key version already exists)

---

### 13. Get Wrapped Keys for Device

**GET** `/groups/{groupId}/keys?deviceId={deviceId}`

Retrieve all wrapped group key versions for a specific device. Used during initial sync and after key rotation.

**Response 200**:
```json
{
  "groupId": "string",
  "keys": [
    {
      "keyVersion": 1,
      "wrappedKeyBlob": "string (base64)"
    },
    {
      "keyVersion": 2,
      "wrappedKeyBlob": "string (base64)"
    }
  ]
}
```

**Errors**: 401, 403, 404

---

### 14. Transfer Ownership

**POST** `/groups/{groupId}/transfer-ownership`

Transfer group ownership to another current member. Only the current owner.

**Request**:
```json
{
  "newOwnerId": "string (userId)"
}
```

**Response 200**:
```json
{
  "groupId": "string",
  "previousOwnerId": "string",
  "newOwnerId": "string",
  "transferredAt": "ISO 8601 datetime"
}
```

**Errors**: 401, 403, 404, 422 (target user is not a member)

---

### 15. Get Invitation Info (unauthenticated preview)

**GET** `/invitations/{token}/info`

**Auth**: No bearer token required. Returns minimal, non-sensitive metadata about an invitation so the app can show a preview before sign-in.

**Response 200**:
```json
{
  "groupDisplayName": "string",
  "ownerDisplayName": "string",
  "status": "Pending | Expired | Accepted | Declined | Cancelled",
  "expiresAt": "ISO 8601 datetime"
}
```

**Note**: Does NOT expose group content, member list, or any sensitive data.

**Errors**: 404 (invalid token)

---

## Error Response Format

All error responses follow a consistent shape:

```json
{
  "error": {
    "code": "string (machine-readable, e.g., 'invitation_expired')",
    "message": "string (human-readable, English)"
  }
}
```

## Rate Limiting

- Invitation creation: 10 per group per hour.
- Sync token requests: 60 per device per hour.
- Device registration: 5 per user per hour.

Exceeded limits return 429 with `Retry-After` header.
