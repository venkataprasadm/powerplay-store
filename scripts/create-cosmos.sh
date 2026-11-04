#!/usr/bin/env bash
# Creates the Cosmos DB database and containers in Azure with the exact partition keys and indexing used by the app.
# The API also creates these itself at startup (Cosmos:CreateIfNotExists, see CosmosSchema.EnsureAsync). Use this script for
# environments where the app identity only has data-plane rights, which is the recommended production setup.
#
# Usage: ./scripts/create-cosmos.sh <resource-group> <account-name> [database]
set -euo pipefail
RG="${1:?resource group}"; ACCOUNT="${2:?cosmos account name}"; DB="${3:-iplstore}"

az cosmosdb sql database create -g "$RG" -a "$ACCOUNT" -n "$DB"

# store: products, price/stock audit trail and orders share ONE logical partition per tenant,
# which is what makes checkout a single transactional batch. Partition keys cannot be changed later.
az cosmosdb sql container create -g "$RG" -a "$ACCOUNT" -d "$DB" -n store \
  --partition-key-path "/tenantId" \
  --idx @"$(dirname "$0")/store-indexing-policy.json" \
  --max-throughput 4000

# carts: one document per user, point reads only, so the index is switched off to save write RU. Idle carts expire after 30 days.
az cosmosdb sql container create -g "$RG" -a "$ACCOUNT" -d "$DB" -n carts \
  --partition-key-path "/userId" \
  --idx @"$(dirname "$0")/carts-indexing-policy.json" \
  --ttl 2592000 \
  --max-throughput 4000
