## Summary

- Replaces the VS-CommandAutoComplete project stub with a new standalone Vintage Story mod: Efficient Ambient Heating
- Adds BehaviorEfficientHeating — a generic BlockEntityBehavior that slows fuel consumption on any heater when idle inside a fully enclosed room, using reflection so it works on vanilla and modded block entities without hardcoding types
- Adds JSON patches targeting vanilla firepit, clay stove, and iron stove
- Adds /heating mult [value] and /heating status admin commands for live tuning and room diagnostics

## Test plan

- [ ] Test Case A (Greenhouse): Light a firepit inside a glass-enclosed room — confirm fuel lasts ~2× longer
- [ ] Test Case B (Exploit check): Place a cooking pot on the lit firepit — confirm burn rate returns to 1× (normal)
- [ ] Test Case C (Breach): Remove one glass block — confirm efficiency bonus drops to 0×
- [ ] Commands: /heating status inside/outside a room; /heating mult 3 persists after reload
- [ ] Verify JSON patch paths match VS 1.22 asset paths
