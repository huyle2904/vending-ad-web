BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

WITH seeded_user AS (
    INSERT INTO "Users" (
        "Username",
        "Email",
        "PasswordHash",
        "FullName",
        "CreatedAt",
        "IsActive"
    )
    VALUES (
        'test@test',
        'test@test',
        encode(digest('test@test', 'sha256'), 'base64'),
        'Test User',
        NOW() AT TIME ZONE 'UTC',
        TRUE
    )
    ON CONFLICT ("Username") DO UPDATE
    SET
        "Email" = EXCLUDED."Email",
        "PasswordHash" = EXCLUDED."PasswordHash",
        "FullName" = EXCLUDED."FullName",
        "IsActive" = TRUE
    RETURNING "Id"
)
INSERT INTO "Devices" (
    "DeviceCode",
    "DeviceName",
    "Location",
    "ClaimCode",
    "ClaimedAt",
    "DeviceSecretHash",
    "DeviceSecretCreatedAt",
    "DeviceSecretRevokedAt",
    "LastSeen",
    "IsActive",
    "UserId"
)
SELECT
    format('DEVICE-%s', lpad(series_id::text, 3, '0')) AS "DeviceCode",
    format('Vending Device %s', lpad(series_id::text, 3, '0')) AS "DeviceName",
    format('Zone %s', ((series_id - 1) / 10) + 1) AS "Location",
    format('CLAIM-%s', lpad(series_id::text, 6, '0')) AS "ClaimCode",
    (NOW() AT TIME ZONE 'UTC') - make_interval(days => series_id % 5) AS "ClaimedAt",
    encode(digest(format('dev-secret-DEVICE-%s', lpad(series_id::text, 3, '0')), 'sha256'), 'base64') AS "DeviceSecretHash",
    (NOW() AT TIME ZONE 'UTC') - make_interval(days => series_id % 5) AS "DeviceSecretCreatedAt",
    NULL AS "DeviceSecretRevokedAt",
    (NOW() AT TIME ZONE 'UTC') - make_interval(mins => series_id % 30) AS "LastSeen",
    TRUE AS "IsActive",
    seeded_user."Id" AS "UserId"
FROM generate_series(1, 50) AS source(series_id)
CROSS JOIN seeded_user
ON CONFLICT ("DeviceCode") DO UPDATE
SET
    "DeviceName" = EXCLUDED."DeviceName",
    "Location" = EXCLUDED."Location",
    "ClaimCode" = EXCLUDED."ClaimCode",
    "ClaimedAt" = EXCLUDED."ClaimedAt",
    "DeviceSecretHash" = EXCLUDED."DeviceSecretHash",
    "DeviceSecretCreatedAt" = EXCLUDED."DeviceSecretCreatedAt",
    "DeviceSecretRevokedAt" = NULL,
    "LastSeen" = EXCLUDED."LastSeen",
    "IsActive" = TRUE,
    "UserId" = EXCLUDED."UserId";

COMMIT;

SELECT
    'test@test' AS username,
    'test@test' AS password;

SELECT
    format('DEVICE-%s', lpad(series_id::text, 3, '0')) AS device_code,
    format('dev-secret-DEVICE-%s', lpad(series_id::text, 3, '0')) AS device_secret
FROM generate_series(1, 50) AS source(series_id)
ORDER BY series_id;
