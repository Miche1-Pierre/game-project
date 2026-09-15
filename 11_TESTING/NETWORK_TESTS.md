# NETWORK TESTS

_Deferred until we have real netcode. Approach noted so we do not improvise later._

## To test (later)
- Simulated latency and packet loss (Unity / Steam tools).
- Disconnect and reconnect handling.
- Host authority and, if relevant, host migration.
- Worst-case object / player sync load.

## Rule
Only synchronize state that the gameplay actually needs. Untested sync is a liability.
