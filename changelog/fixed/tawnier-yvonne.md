Core: Dev and test server startup now stops waiting at the configured deadline even if a readiness request stalls. Timeout messages include the last connection failure when available.
Core: Workers with large workflow caches avoid unnecessary CPU usage when no workflow tasks are waiting for a cache slot.
