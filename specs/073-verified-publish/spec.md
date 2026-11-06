# Spec: #73
Selected immutable chart versions and song metadata initialize Publish. Production uses live API only. Success requires persisted Catalog and exact chart readback. Failed verification preserves draft and warns a write may exist. No live write/deploy in implementation verification.
